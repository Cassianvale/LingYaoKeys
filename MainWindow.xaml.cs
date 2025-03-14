using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WpfApp.ViewModels;
using WpfApp.Services.Config;
using WpfApp.Services.Utils;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace WpfApp;

/// <summary>
/// MainWindow.xaml 的交互逻辑
/// </summary>
public partial class MainWindow
{
    private readonly SerilogManager _logger = SerilogManager.Instance;
    private readonly MainViewModel _viewModel;
    private bool _isClosing;
    private bool _hasShownMinimizeNotification;
    private NotifyIcon _trayIcon;
    internal ContextMenu _trayContextMenu;
    private bool _isNavExpanded = false;
    private readonly SemaphoreSlim _cleanupSemaphore = new(1, 1);
    private CancellationTokenSource _cleanupCts;

    // 窗口调整大小相关
    private bool _isResizing;
    private ResizeDirection _resizeDirection;
    private System.Windows.Point _startPoint;
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
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetCursorPos(out POINT pt);

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
            _viewModel = new MainViewModel(App.LyKeysDriver, this);

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

            // 设置 MainWindow 引用到 KeyMappingViewModel
            if (_viewModel.KeyMappingViewModel != null) _viewModel.KeyMappingViewModel.SetMainWindow(this);

            _logger.Debug($"窗口初始化完成 - 尺寸: {Width}x{Height}");
        }
        catch (Exception ex)
        {
            _logger.Error("窗口初始化失败", ex);
            System.Windows.MessageBox.Show($"窗口初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            System.Windows.Application.Current.Shutdown();
        }
    }

    private void InitializeTrayIcon()
    {
        try
        {
            // 创建WPF样式的上下文菜单
            _trayContextMenu = new ContextMenu
            {
                Style = System.Windows.Application.Current.FindResource("TrayContextMenuStyle") as Style,
                Placement = PlacementMode.MousePoint,
                StaysOpen = false // 默认不保持打开
            };

            // 添加菜单打开和关闭事件处理
            _trayContextMenu.Opened += (s, e) =>
            {
                SetMouseHook(); // 设置鼠标钩子
                if (_trayContextMenu.Items.Count > 0 && _trayContextMenu.Items[0] is MenuItem firstItem)
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => { firstItem.Focus(); }),
                        System.Windows.Threading.DispatcherPriority.Input);
            };

            _trayContextMenu.Closed += (s, e) =>
            {
                RemoveMouseHook(); // 移除鼠标钩子
                Keyboard.ClearFocus();
                _trayContextMenu.StaysOpen = false; // 确保重置 StaysOpen
            };

            var showMenuItem = new MenuItem
            {
                Header = "显示主窗口",
                Style = System.Windows.Application.Current.FindResource("TrayMenuItemStyle") as Style,
                Icon = new System.Windows.Controls.Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri("pack://application:,,,/Resource/icon/app.ico")),
                    Width = 16,
                    Height = 16
                }
            };
            showMenuItem.Click += (s, e) =>
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _trayContextMenu.IsOpen = false;
                    ShowMainWindow();
                }), System.Windows.Threading.DispatcherPriority.Normal);
            };

            var exitMenuItem = new MenuItem
            {
                Header = "退出程序",
                Style = System.Windows.Application.Current.FindResource("TrayMenuItemStyle") as Style,
                Icon = new TextBlock
                {
                    Text = "\uE8BB",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = System.Windows.Media.Brushes.DarkRed
                }
            };
            exitMenuItem.Click += (s, e) => System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                _trayContextMenu.IsOpen = false;
                Close();
            }), System.Windows.Threading.DispatcherPriority.Normal);

            var separator = new Separator
            {
                Style = System.Windows.Application.Current.FindResource("TrayMenuSeparatorStyle") as Style
            };

            _trayContextMenu.Items.Add(showMenuItem);
            _trayContextMenu.Items.Add(separator);
            _trayContextMenu.Items.Add(exitMenuItem);

            // 初始化托盘图标
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resource", "icon", "app.ico");
            _trayIcon = new NotifyIcon
            {
                Icon = File.Exists(iconPath)
                    ? new Icon(iconPath)
                    : Drawing.Icon.ExtractAssociatedIcon(Forms.Application.ExecutablePath),
                Visible = true,
                Text = "灵曜按键"
            };

            // 添加托盘图标的点击事件处理
            _trayIcon.MouseClick += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(ShowMainWindow),
                        System.Windows.Threading.DispatcherPriority.Normal);
                else if (e.Button == MouseButtons.Right)
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 确保菜单在显示前是关闭状态
                        _trayContextMenu.IsOpen = false;

                        // 获取鼠标位置
                        GetCursorPos(out var pt);
                        var mousePosition = new System.Windows.Point(pt.X, pt.Y);

                        // 确保菜单在显示时不会自动关闭
                        _trayContextMenu.StaysOpen = true;

                        // 延迟一帧后显示菜单
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            _trayContextMenu.IsOpen = true;

                            // 订阅菜单关闭事件，以便在关闭时重置 StaysOpen
                            _trayContextMenu.Closed += (s, args) => { _trayContextMenu.StaysOpen = false; };
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }), System.Windows.Threading.DispatcherPriority.Normal);
            };

            // 初始化鼠标钩子回调
            _mouseHookProc = new Win32.HookProc(MouseHookCallback);
        }
        catch (Exception ex)
        {
            _logger.Error("初始化托盘图标失败", ex);
        }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN))
        {
            var hookStruct = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);

            // 检查点击是否在菜单区域外
            if (_trayContextMenu.IsOpen)
            {
                var menuPosition = _trayContextMenu.PointToScreen(new System.Windows.Point(0, 0));
                var menuRect = new Rect(
                    menuPosition.X,
                    menuPosition.Y,
                    _trayContextMenu.ActualWidth,
                    _trayContextMenu.ActualHeight);

                if (!menuRect.Contains(new System.Windows.Point(hookStruct.pt.x, hookStruct.pt.y)))
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(
                        new Action(() => { _trayContextMenu.IsOpen = false; }),
                        System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        return Win32.CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private void SetMouseHook()
    {
        if (_hookID == IntPtr.Zero)
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
        _logger.Debug($"窗口源初始化 - 实际尺寸: {Width}x{Height}");
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
                    _trayIcon.ShowBalloonTip(
                        1000, // 显示时间（毫秒）
                        _viewModel.Config.AppInfo.Title, // 从ViewModel获取标题
                        "程序已最小化到系统托盘\n双击托盘图标或浮窗可重新打开窗口！", // 提示内容
                        ToolTipIcon.Info // 提示图标
                    );
            }

            _logger.Debug("窗口已最小化到托盘");
        }
        else
        {
            if (WindowState == WindowState.Maximized)
            {
                // 最大化时移除圆角
                if (FindName("MainBorder") is Border mainBorder) mainBorder.CornerRadius = new CornerRadius(0);
                // 调整边距以防止窗口内容溢出屏幕
                Padding = new Thickness(7);
            }
            else
            {
                // 还原时恢复圆角
                if (FindName("MainBorder") is Border mainBorder) mainBorder.CornerRadius = new CornerRadius(8);
                Padding = new Thickness(0);
            }
        }
    }

    private void TrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
    {
    }

    private void TrayIcon_TrayRightMouseDown(object sender, RoutedEventArgs e)
    {
    }

    private void ShowWindow_Click(object sender, RoutedEventArgs e)
    {
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
    }

    private void ShowMainWindow()
    {
        _logger.Debug("正在从托盘还原窗口...");
        RestoreFromMinimized();
    }

    public void RestoreFromMinimized()
    {
        try
        {
            // 确保窗口可见
            Show();

            // 如果窗口被最小化，先恢复到普通状态
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;

            // 取消置顶状态
            Topmost = false;
            if (FindName("TopMostButton") is System.Windows.Controls.Button topMostButton)
                topMostButton.Content = "\uE840"; // 使用未置顶图标

            // 激活窗口并设置焦点
            Activate();
            Focus();

            _logger.Debug("窗口已成功还原并激活");
        }
        catch (Exception ex)
        {
            _logger.Error("还原窗口时发生错误", ex);
        }
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosing) return;

        try
        {
            // 防止重入
            if (!await _cleanupSemaphore.WaitAsync(0))
            {
                e.Cancel = true;
                return;
            }

            _isClosing = true;
            _logger.Debug("正在关闭应用程序");

            // 创建取消令牌，设置3秒超时
            _cleanupCts = new CancellationTokenSource();
            _cleanupCts.CancelAfter(TimeSpan.FromSeconds(3));

            try
            {
                // 1. 立即禁用UI交互
                IsEnabled = false;

                // 2. 异步执行清理操作
                await Task.Run(async () =>
                {
                    try
                    {
                        _logger.Debug("开始清理窗口资源...");

                        // 保存窗口大小到配置
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (WindowState == WindowState.Normal)
                            {
                                _logger.Debug($"保存窗口大小: {ActualWidth}x{ActualHeight}");
                                AppConfigService.UpdateConfig(config =>
                                {
                                    config.UI.MainWindow.Width = ActualWidth;
                                    config.UI.MainWindow.Height = ActualHeight;
                                });
                            }
                        });

                        // 停止ViewModel操作
                        if (_viewModel is IDisposable disposableViewModel)
                            await Task.Run(() => disposableViewModel.Dispose(), _cleanupCts.Token);

                        // 清理托盘图标
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (_trayIcon != null)
                            {
                                _trayIcon.Visible = false;
                                _trayIcon.Dispose();
                                _trayIcon = null;
                            }
                        });

                        // 清理托盘菜单
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (_trayContextMenu != null)
                            {
                                _trayContextMenu.Items.Clear();
                                _trayContextMenu = null;
                            }
                        });

                        _logger.Debug("窗口资源清理完成");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warning("清理操作已超时取消");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("清理过程中发生错误", ex);
                    }
                }, _cleanupCts.Token);
            }
            finally
            {
                // 设置关闭模式并关闭应用程序
                await Dispatcher.InvokeAsync(() =>
                {
                    System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    System.Windows.Application.Current.Shutdown();
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Error("窗口关闭异常", ex);
        }
        finally
        {
            _cleanupSemaphore.Release();
            _cleanupCts?.Dispose();
        }
    }

    protected override async void OnClosed(EventArgs e)
    {
        try
        {
            // 确保钩子被移除
            await Dispatcher.InvokeAsync(() => RemoveMouseHook());

            // 清理页面缓存
            await Task.Run(() => Services.PageCacheService.ClearCache());

            if (_isClosing) return;
            _isClosing = true;
        }
        catch (Exception ex)
        {
            _logger.Error("OnClosed 事件处理异常", ex);
        }
        finally
        {
            base.OnClosed(e);
        }
    }

    // 在类的末尾添加析构函数
    ~MainWindow()
    {
        _cleanupSemaphore?.Dispose();
        _cleanupCts?.Dispose();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleMaximizeRestore();
        else
            DragMove();
    }

    private void TopMostButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        if (sender is System.Windows.Controls.Button button) button.Content = Topmost ? "\uE77A" : "\uE840"; // 切换图标
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
            WindowState = WindowState.Normal;
        else
            WindowState = WindowState.Maximized;
        UpdateMaximizeButtonState();
    }

    private void UpdateMaximizeButtonState()
    {
        var maximizeButton = FindName("MaximizeButton") as System.Windows.Controls.Button;
        if (maximizeButton != null)
        {
            if (WindowState == WindowState.Maximized)
            {
                maximizeButton.Content = "\uE923"; // 还原图标
                maximizeButton.ToolTip = "向下还原";
            }
            else
            {
                maximizeButton.Content = "\uE739"; // 最大化图标
                maximizeButton.ToolTip = "最大化";
            }
        }
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
        _startPoint = PointToScreen(e.GetPosition(this)); // 使用屏幕坐标
        _startWidth = ActualWidth;
        _startHeight = ActualHeight;
        _startLeft = Left;
        _startTop = Top;

        // 捕获鼠标
        Mouse.Capture(e.Source as IInputElement);
        e.Handled = true;

        // 开始调整大小时禁用动画
        if (FindName("MainBorder") is Border mainBorder) mainBorder.BeginAnimation(MarginProperty, null);
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        if (!_isResizing)
        {
            base.OnMouseMove(e);
            return;
        }

        // 控制更新频率
        var now = DateTime.Now;
        if ((now - _lastResizeTime).TotalMilliseconds < RESIZE_INTERVAL) return;
        _lastResizeTime = now;

        var currentPoint = PointToScreen(e.GetPosition(this));
        var deltaX = (currentPoint.X - _startPoint.X) * RESIZE_ACCELERATION;
        var deltaY = (currentPoint.Y - _startPoint.Y) * RESIZE_ACCELERATION;

        // 应用调整阈值
        if (Math.Abs(deltaX) < RESIZE_THRESHOLD && Math.Abs(deltaY) < RESIZE_THRESHOLD) return;

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
            _logger.Error("调整窗口大小时发生错误", ex);
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
            if (FindName("MainBorder") is Border mainBorder) mainBorder.BeginAnimation(MarginProperty, null);

            e.Handled = true;
        }

        base.OnMouseUp(e);
    }

    private void HandleLeftResize(double deltaX)
    {
        var newWidth = Math.Max(MinWidth, _startWidth - deltaX);
        var maxWidth = SystemParameters.WorkArea.Width;

        if (newWidth > MinWidth && newWidth < maxWidth)
        {
            var newLeft = _startLeft + (_startWidth - newWidth);
            if (newLeft >= 0 && newLeft + newWidth <= maxWidth)
            {
                Left = newLeft;
                Width = newWidth;
            }
        }
    }

    private void HandleRightResize(double deltaX)
    {
        var newWidth = Math.Max(MinWidth, _startWidth + deltaX);
        var maxWidth = SystemParameters.WorkArea.Width - Left;

        if (newWidth > MinWidth && newWidth < maxWidth) Width = newWidth;
    }

    private void HandleTopResize(double deltaY)
    {
        var newHeight = Math.Max(MinHeight, _startHeight - deltaY);
        var maxHeight = SystemParameters.WorkArea.Height;

        if (newHeight > MinHeight && newHeight < maxHeight)
        {
            var newTop = _startTop + (_startHeight - newHeight);
            if (newTop >= 0 && newTop + newHeight <= maxHeight)
            {
                Top = newTop;
                Height = newHeight;
            }
        }
    }

    private void HandleBottomResize(double deltaY)
    {
        var newHeight = Math.Max(MinHeight, _startHeight + deltaY);
        var maxHeight = SystemParameters.WorkArea.Height - Top;

        if (newHeight > MinHeight && newHeight < maxHeight) Height = newHeight;
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

    public void NavigateToPage<T>() where T : Page, new()
    {
        try
        {
            var page = Services.PageCacheService.GetPage<T>();
            if (FindName("MainFrame") is Frame mainFrame) mainFrame.Navigate(page);
        }
        catch (Exception ex)
        {
            _logger.Error($"导航到页面 {typeof(T).Name} 失败", ex);
        }
    }

    private void NavToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // 获取ViewModel
        if (DataContext is MainViewModel viewModel)
        {
            // 切换导航栏状态
            viewModel.IsNavExpanded = !viewModel.IsNavExpanded;

            // 创建列宽动画
            var animation = new GridLengthAnimation
            {
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                To = viewModel.NavColumnWidth
            };

            // 获取Grid并应用动画
            if (MainBorder.Parent is Grid mainGrid && mainGrid.ColumnDefinitions.Count > 0)
                mainGrid.ColumnDefinitions[0].BeginAnimation(ColumnDefinition.WidthProperty, animation);
        }
    }

    /// <summary>
    /// GridLength 动画类
    /// </summary>
    public class GridLengthAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore()
        {
            return new GridLengthAnimation();
        }

        public GridLength? From
        {
            get => (GridLength?)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public GridLength? To
        {
            get => (GridLength?)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public IEasingFunction EasingFunction
        {
            get => (IEasingFunction)GetValue(EasingFunctionProperty);
            set => SetValue(EasingFunctionProperty, value);
        }

        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register("From", typeof(GridLength?), typeof(GridLengthAnimation));

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register("To", typeof(GridLength?), typeof(GridLengthAnimation));

        public static readonly DependencyProperty EasingFunctionProperty =
            DependencyProperty.Register("EasingFunction", typeof(IEasingFunction), typeof(GridLengthAnimation));

        public override object GetCurrentValue(object defaultOriginValue,
            object defaultDestinationValue,
            AnimationClock animationClock)
        {
            var from = From ?? (GridLength)defaultOriginValue;
            var to = To ?? (GridLength)defaultDestinationValue;

            if (animationClock.CurrentProgress == null)
                return from;

            var progress = animationClock.CurrentProgress.Value;
            if (EasingFunction != null)
                progress = EasingFunction.Ease(progress);

            return new GridLength((1 - progress) * from.Value + progress * to.Value,
                to.IsStar ? GridUnitType.Star : GridUnitType.Pixel);
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        // 如果导航栏是展开状态，点击后自动收起
        if (_isNavExpanded) NavToggleButton_Click(FindName("NavToggleButton"), e);
    }
}