using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WpfApp.ViewModels;
using WpfApp.Services;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using System.IO;
using System.Windows.Media;

namespace WpfApp
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private readonly LogManager _logger = LogManager.Instance;
        private readonly MainViewModel _viewModel;
        private bool _isClosing;
        private bool _isShuttingDown;
        private bool _hasShownMinimizeNotification;
        private Forms.NotifyIcon _trayIcon;
        internal ContextMenu _trayContextMenu;

        // 窗口调整大小相关
        private bool _isResizing;
        private ResizeDirection _resizeDirection;
        private Point _startPoint;
        private double _startWidth;
        private double _startHeight;
        private double _startLeft;
        private double _startTop;

        // Windows Hook API
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private IntPtr _hookID = IntPtr.Zero;
        private Win32.HookProc _mouseHookProc;

        private static class Win32
        {
            public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int x;
                public int y;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MSLLHOOKSTRUCT
            {
                public POINT pt;
                public uint mouseData;
                public uint flags;
                public uint time;
                public IntPtr dwExtraInfo;
            }
        }

        private enum ResizeDirection
        {
            Left, Right, Top, Bottom,
            TopLeft, TopRight, BottomLeft, BottomRight
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool GetCursorPos(out POINT pt);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        public MainWindow()
        {
            try
            {
                // 先初始化ViewModel
                _viewModel = new MainViewModel(App.DDDriver, this);
                
                // 设置初始窗口大小
                Width = _viewModel.Config.UI.MainWindow.DefaultWidth;
                Height = _viewModel.Config.UI.MainWindow.DefaultHeight;
                
                // 初始化组件
                InitializeComponent();
                
                // 设置DataContext
                DataContext = _viewModel;
                
                // 初始化托盘图标
                InitializeTrayIcon();
                
                // 更新最大化按钮状态
                UpdateMaximizeButtonState();
                
                // 注册窗口状态改变事件
                StateChanged += MainWindow_StateChanged;
                
                _logger.LogDebug("MainWindow", $"窗口初始化完成 - 尺寸: {Width}x{Height}");
            }
            catch (Exception ex)
            {
                _logger.LogError("MainWindow", "窗口初始化失败", ex);
                MessageBox.Show($"窗口初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private void InitializeTrayIcon()
        {
            try
            {
                // 创建WPF样式的上下文菜单
                _trayContextMenu = new ContextMenu
                {
                    Style = Application.Current.FindResource("TrayContextMenuStyle") as Style,
                    Placement = PlacementMode.Custom,
                    CustomPopupPlacementCallback = new CustomPopupPlacementCallback(MenuCustomPlacementCallback),
                    StaysOpen = true  // 改为true，由我们自己控制关闭
                };

                // 添加菜单打开和关闭事件处理
                _trayContextMenu.Opened += (s, e) =>
                {
                    SetMouseHook();  // 设置鼠标钩子
                    if (_trayContextMenu.Items.Count > 0 && _trayContextMenu.Items[0] is MenuItem firstItem)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            firstItem.Focus();
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    }
                };

                _trayContextMenu.Closed += (s, e) =>
                {
                    RemoveMouseHook();  // 移除鼠标钩子
                    Keyboard.ClearFocus();
                };

                var showMenuItem = new MenuItem
                {
                    Header = "显示主窗口",
                    Style = Application.Current.FindResource("TrayMenuItemStyle") as Style,
                    Icon = new Image
                    {
                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resource/icon/app.ico")),
                        Width = 16,
                        Height = 16
                    }
                };
                showMenuItem.Click += (s, e) => 
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _trayContextMenu.IsOpen = false;
                        ShowMainWindow();
                    }), System.Windows.Threading.DispatcherPriority.Normal);
                };

                var exitMenuItem = new MenuItem
                {
                    Header = "退出程序",
                    Style = Application.Current.FindResource("TrayMenuItemStyle") as Style,
                    Icon = new TextBlock
                    {
                        Text = "\uE8BB",
                        FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = System.Windows.Media.Brushes.DarkRed
                    }
                };
                exitMenuItem.Click += (s, e) => Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _trayContextMenu.IsOpen = false;
                    _isShuttingDown = true;
                    Close();
                }), System.Windows.Threading.DispatcherPriority.Normal);

                var separator = new Separator
                {
                    Style = Application.Current.FindResource("TrayMenuSeparatorStyle") as Style
                };

                _trayContextMenu.Items.Add(showMenuItem);
                _trayContextMenu.Items.Add(separator);
                _trayContextMenu.Items.Add(exitMenuItem);

                // 初始化托盘图标
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resource", "icon", "app.ico");
                _trayIcon = new Forms.NotifyIcon
                {
                    Icon = File.Exists(iconPath) 
                        ? new Drawing.Icon(iconPath) 
                        : Drawing.Icon.ExtractAssociatedIcon(Forms.Application.ExecutablePath),
                    Visible = true,
                    Text = "灵曜按键"
                };

                // 添加托盘图标的点击事件处理
                _trayIcon.MouseClick += (sender, e) =>
                {
                    if (e.Button == Forms.MouseButtons.Left)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(ShowMainWindow), 
                            System.Windows.Threading.DispatcherPriority.Normal);
                    }
                    else if (e.Button == Forms.MouseButtons.Right)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // 确保菜单在显示前是关闭状态
                            _trayContextMenu.IsOpen = false;

                            // 延迟一帧后显示菜单
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                _trayContextMenu.IsOpen = true;
                            }), System.Windows.Threading.DispatcherPriority.Loaded);
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }
                };

                // 初始化鼠标钩子回调
                _mouseHookProc = new Win32.HookProc(MouseHookCallback);
            }
            catch (Exception ex)
            {
                _logger.LogError("MainWindow", "初始化托盘图标失败", ex);
            }
        }

        private CustomPopupPlacement[] MenuCustomPlacementCallback(
            Size popupSize, Size targetSize, Point offset)
        {
            // 获取鼠标位置（托盘图标位置）
            GetCursorPos(out POINT pt);

            // 获取工作区
            var workArea = SystemParameters.WorkArea;

            // 计算菜单位置
            double x = pt.X;
            double y = pt.Y;

            // 确保菜单不会超出屏幕
            if (x + popupSize.Width > workArea.Right)
            {
                x = workArea.Right - popupSize.Width;
            }

            // 默认显示在托盘图标上方
            y -= popupSize.Height;

            // 如果上方空间不够，则显示在下方
            if (y < workArea.Top)
            {
                y = pt.Y;
            }

            return new[] { new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Horizontal) };
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN))
            {
                var hookStruct = (Win32.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32.MSLLHOOKSTRUCT));
                
                // 检查点击是否在菜单区域外
                if (_trayContextMenu.IsOpen)
                {
                    var menuPosition = _trayContextMenu.PointToScreen(new Point(0, 0));
                    var menuRect = new Rect(
                        menuPosition.X, 
                        menuPosition.Y, 
                        _trayContextMenu.ActualWidth, 
                        _trayContextMenu.ActualHeight);

                    if (!menuRect.Contains(new Point(hookStruct.pt.x, hookStruct.pt.y)))
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            _trayContextMenu.IsOpen = false;
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    }
                }
            }
            return Win32.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void SetMouseHook()
        {
            if (_hookID == IntPtr.Zero)
            {
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    _hookID = Win32.SetWindowsHookEx(
                        WH_MOUSE_LL,
                        _mouseHookProc,
                        Win32.GetModuleHandle(curModule.ModuleName),
                        0);
                }
            }
        }

        private void RemoveMouseHook()
        {
            if (_hookID != IntPtr.Zero)
            {
                Win32.UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _logger.LogInitialization("App", 
                $"窗口源初始化 - 实际尺寸: {Width}x{Height}");
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                // 最小化到托盘
                Hide();
                
                
                // 首次最小化时显示通知
                if (!_hasShownMinimizeNotification)
                {
                    _hasShownMinimizeNotification = true;
                    if (_trayIcon != null)
                    {
                        _trayIcon.ShowBalloonTip(
                            1000,  // 显示时间（毫秒）
                            _viewModel.Config.AppInfo.Title,  // 从ViewModel获取标题
                            "程序已最小化到系统托盘\n双击托盘图标或浮窗可重新打开窗口！",  // 提示内容
                            Forms.ToolTipIcon.Info  // 提示图标
                        );
                    }
                }
                _logger.LogDebug("MainWindow", "窗口已最小化到托盘");
            }
            else
            {
                if (WindowState == WindowState.Maximized)
                {
                    // 最大化时移除圆角
                    if (FindName("MainBorder") is Border mainBorder)
                    {
                        mainBorder.CornerRadius = new CornerRadius(0);
                    }
                    // 调整边距以防止窗口内容溢出屏幕
                    Padding = new Thickness(7);
                }
                else
                {
                    // 还原时恢复圆角
                    if (FindName("MainBorder") is Border mainBorder)
                    {
                        mainBorder.CornerRadius = new CornerRadius(8);
                    }
                    Padding = new Thickness(0);
                }


            }
        }

        private void TrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e) { }
        private void TrayIcon_TrayRightMouseDown(object sender, RoutedEventArgs e) { }
        private void ShowWindow_Click(object sender, RoutedEventArgs e) { }
        private void Exit_Click(object sender, RoutedEventArgs e) { }

        private void ShowMainWindow()
        {
            _logger.LogDebug("MainWindow", "正在从托盘还原窗口...");
            RestoreFromMinimized();
        }

        public void RestoreFromMinimized()
        {
            try
            {
                // 确保窗口可见
                Show();
                
                // 如果窗口被最小化，先恢复到普通状态
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }
                
                // 取消置顶状态
                Topmost = false;
                if (FindName("TopMostButton") is Button topMostButton)
                {
                    topMostButton.Content = "\uE840";  // 使用未置顶图标
                }
                
                // 激活窗口并设置焦点
                Activate();
                Focus();
                
                _logger.LogDebug("MainWindow", "窗口已成功还原并激活");
            }
            catch (Exception ex)
            {
                _logger.LogError("MainWindow", "还原窗口时发生错误", ex);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _isShuttingDown = true;
            _logger.LogDebug("MainWindow", "正在关闭应用程序");

            // 设置关闭模式为显式关闭，这样浮窗才会真正关闭
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 清理资源
            try 
            {
                _logger.LogDebug("MainWindow", "开始清理窗口资源...");
                if (_trayIcon != null)
                {
                    _trayIcon.Dispose();
                    _trayIcon = null;
                }
                _viewModel.Cleanup();
                _logger.LogDebug("MainWindow", "窗口资源清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError("MainWindow", "窗口关闭异常", ex);
            }

            // 确保应用程序退出
            Application.Current.Shutdown();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
            }
            else
            {
                DragMove();
            }
        }

        private void TopMostButton_Click(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            if (sender is System.Windows.Controls.Button button)
            {
                button.Content = Topmost ? "\uE77A" : "\uE840";  // 切换图标
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleMaximizeRestore()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
            UpdateMaximizeButtonState();
        }

        private void UpdateMaximizeButtonState()
        {
            var maximizeButton = FindName("MaximizeButton") as System.Windows.Controls.Button;
            if (maximizeButton != null)
            {
                if (WindowState == WindowState.Maximized)
                {
                    maximizeButton.Content = "\uE923";  // 还原图标
                    maximizeButton.ToolTip = "向下还原";
                }
                else
                {
                    maximizeButton.Content = "\uE739";  // 最大化图标
                    maximizeButton.ToolTip = "最大化";
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            RemoveMouseHook();  // 确保钩子被移除
            if (_isClosing) return;
            _isClosing = true;
            base.OnClosed(e);
        }

        #region 窗口大小调整

        private const double RESIZE_THRESHOLD = 1.0; // 调整阈值，避免微小变化
        private const double RESIZE_ACCELERATION = 1.0; // 调整加速度，使移动更平滑
        private const int RESIZE_INTERVAL = 16; // 约60fps的更新间隔
        private DateTime _lastResizeTime = DateTime.MinValue;

        private void StartResize(ResizeDirection direction, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Maximized) return;

            _isResizing = true;
            _resizeDirection = direction;
            _startPoint = PointToScreen(e.GetPosition(this));  // 使用屏幕坐标
            _startWidth = ActualWidth;
            _startHeight = ActualHeight;
            _startLeft = Left;
            _startTop = Top;

            // 捕获鼠标
            Mouse.Capture(e.Source as IInputElement);
            e.Handled = true;

            // 开始调整大小时禁用动画
            if (FindName("MainBorder") is Border mainBorder)
            {
                mainBorder.BeginAnimation(Border.MarginProperty, null);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_isResizing)
            {
                base.OnMouseMove(e);
                return;
            }

            // 控制更新频率
            var now = DateTime.Now;
            if ((now - _lastResizeTime).TotalMilliseconds < RESIZE_INTERVAL)
            {
                return;
            }
            _lastResizeTime = now;

            Point currentPoint = PointToScreen(e.GetPosition(this));
            double deltaX = (currentPoint.X - _startPoint.X) * RESIZE_ACCELERATION;
            double deltaY = (currentPoint.Y - _startPoint.Y) * RESIZE_ACCELERATION;

            // 应用调整阈值
            if (Math.Abs(deltaX) < RESIZE_THRESHOLD && Math.Abs(deltaY) < RESIZE_THRESHOLD)
            {
                return;
            }

            try
            {
                switch (_resizeDirection)
                {
                    case ResizeDirection.Left:
                        HandleLeftResize(deltaX);
                        break;
                    case ResizeDirection.Right:
                        HandleRightResize(deltaX);
                        break;
                    case ResizeDirection.Top:
                        HandleTopResize(deltaY);
                        break;
                    case ResizeDirection.Bottom:
                        HandleBottomResize(deltaY);
                        break;
                    case ResizeDirection.TopLeft:
                        HandleLeftResize(deltaX);
                        HandleTopResize(deltaY);
                        break;
                    case ResizeDirection.TopRight:
                        HandleRightResize(deltaX);
                        HandleTopResize(deltaY);
                        break;
                    case ResizeDirection.BottomLeft:
                        HandleLeftResize(deltaX);
                        HandleBottomResize(deltaY);
                        break;
                    case ResizeDirection.BottomRight:
                        HandleRightResize(deltaX);
                        HandleBottomResize(deltaY);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("MainWindow", "调整窗口大小时发生错误", ex);
            }

            e.Handled = true;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                Mouse.Capture(null);

                // 恢复动画
                if (FindName("MainBorder") is Border mainBorder)
                {
                    mainBorder.BeginAnimation(Border.MarginProperty, null);
                }

                e.Handled = true;
            }
            base.OnMouseUp(e);
        }

        private void HandleLeftResize(double deltaX)
        {
            double newWidth = Math.Max(MinWidth, _startWidth - deltaX);
            double maxWidth = SystemParameters.WorkArea.Width;
            
            if (newWidth > MinWidth && newWidth < maxWidth)
            {
                double newLeft = _startLeft + (_startWidth - newWidth);
                if (newLeft >= 0 && newLeft + newWidth <= maxWidth)
                {
                    Left = newLeft;
                    Width = newWidth;
                }
            }
        }

        private void HandleRightResize(double deltaX)
        {
            double newWidth = Math.Max(MinWidth, _startWidth + deltaX);
            double maxWidth = SystemParameters.WorkArea.Width - Left;
            
            if (newWidth > MinWidth && newWidth < maxWidth)
            {
                Width = newWidth;
            }
        }

        private void HandleTopResize(double deltaY)
        {
            double newHeight = Math.Max(MinHeight, _startHeight - deltaY);
            double maxHeight = SystemParameters.WorkArea.Height;
            
            if (newHeight > MinHeight && newHeight < maxHeight)
            {
                double newTop = _startTop + (_startHeight - newHeight);
                if (newTop >= 0 && newTop + newHeight <= maxHeight)
                {
                    Top = newTop;
                    Height = newHeight;
                }
            }
        }

        private void HandleBottomResize(double deltaY)
        {
            double newHeight = Math.Max(MinHeight, _startHeight + deltaY);
            double maxHeight = SystemParameters.WorkArea.Height - Top;
            
            if (newHeight > MinHeight && newHeight < maxHeight)
            {
                Height = newHeight;
            }
        }

        private void ResizeLeft_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                StartResize(ResizeDirection.Left, e);
        }

        private void ResizeRight_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                StartResize(ResizeDirection.Right, e);
        }

        private void ResizeTop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                StartResize(ResizeDirection.Top, e);
        }

        private void ResizeBottom_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                StartResize(ResizeDirection.Bottom, e);
        }

        private void ResizeTopLeft_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                StartResize(ResizeDirection.TopLeft, e);
        }

        private void ResizeTopRight_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                StartResize(ResizeDirection.TopRight, e);
        }

        private void ResizeBottomLeft_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                StartResize(ResizeDirection.BottomLeft, e);
        }

        private void ResizeBottomRight_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                StartResize(ResizeDirection.BottomRight, e);
        }

        #endregion
    }
}