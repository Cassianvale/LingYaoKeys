using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using WpfApp.ViewModels;
using WpfApp.Services;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using System.IO;

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

        public MainWindow()
        {
            _viewModel = new MainViewModel(App.DDDriver, this);
            DataContext = _viewModel;
            Width = _viewModel.Config.UI.MainWindow.DefaultWidth;
            Height = _viewModel.Config.UI.MainWindow.DefaultHeight;
            InitializeComponent();
            InitializeTrayIcon();
            
            UpdateMaximizeButtonState();
            
            // 注册窗口状态改变事件
            StateChanged += MainWindow_StateChanged;
        }

        private void InitializeTrayIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
                _trayIcon = new Forms.NotifyIcon
                {
                    Icon = File.Exists(iconPath) 
                        ? new Drawing.Icon(iconPath) 
                        : Drawing.Icon.ExtractAssociatedIcon(Forms.Application.ExecutablePath),
                    Visible = true,
                    Text = "剑网3工具箱"
                };

                // 添加托盘图标的点击事件处理
                _trayIcon.MouseClick += (sender, e) =>
                {
                    if (e.Button == Forms.MouseButtons.Left)
                    {
                        Application.Current.Dispatcher.Invoke(ShowMainWindow);
                    }
                };

                // 添加托盘菜单
                var contextMenu = new Forms.ContextMenuStrip();
                var showItem = new Forms.ToolStripMenuItem("显示主窗口");
                showItem.Click += (s, e) => Application.Current.Dispatcher.Invoke(ShowMainWindow);
                
                var exitItem = new Forms.ToolStripMenuItem("退出程序");
                exitItem.Click += (s, e) => Application.Current.Dispatcher.Invoke(() =>
                {
                    _isShuttingDown = true;
                    Close();
                });

                contextMenu.Items.Add(showItem);
                contextMenu.Items.Add(new Forms.ToolStripSeparator());
                contextMenu.Items.Add(exitItem);

                _trayIcon.ContextMenuStrip = contextMenu;
            }
            catch (Exception ex)
            {
                _logger.LogError("MainWindow", "初始化托盘图标失败", ex);
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
                            3000,  // 显示时间（毫秒）
                            "剑网3工具箱",  // 标题
                            "程序已最小化到系统托盘，双击托盘图标可以重新打开窗口。",  // 提示内容
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

        private void TrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
        }

        private void TrayIcon_TrayRightMouseDown(object sender, RoutedEventArgs e)
        {
            // 右键点击时显示上下文菜单
            // 由XAML中的ContextMenu自动处理
        }

        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _isShuttingDown = true;
            Close();
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Focus();
            _logger.LogDebug("MainWindow", "从托盘还原窗口");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _isShuttingDown = true;
            _logger.LogDebug("MainWindow", "正在关闭应用程序");
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
            if (_isClosing) return;
            _isClosing = true;

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
            
            base.OnClosed(e);
        }
    }
}