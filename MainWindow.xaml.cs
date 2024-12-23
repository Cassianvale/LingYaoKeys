using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using WpfApp.ViewModels;
using WpfApp.Services;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WpfApp
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly LogManager _logger = LogManager.Instance;
        private readonly MainViewModel _viewModel;
        private bool _isClosing;
        private bool _isShuttingDown;

        public MainWindow()
        {
            _viewModel = new MainViewModel(App.DDDriver, this);
            DataContext = _viewModel;
            Width = _viewModel.Config.UI.MainWindow.DefaultWidth;
            Height = _viewModel.Config.UI.MainWindow.DefaultHeight;
            InitializeComponent();

            UpdateMaximizeButtonState();
            
            // 注册窗口状态改变事件
            StateChanged += MainWindow_StateChanged;
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
                _logger.LogDebug("MainWindow", "窗口已最小化到托盘");
            }
            else
            {
                if (WindowState == WindowState.Maximized)
                {
                    // 最大化时移除圆角
                    MainBorder.CornerRadius = new CornerRadius(0);
                    // 调整边距以防止窗口内容溢出屏幕
                    Padding = new Thickness(7);
                }
                else
                {
                    // 还原时恢复圆角
                    MainBorder.CornerRadius = new CornerRadius(8);
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
            if (sender is Button button)
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
            _isShuttingDown = true;
            Application.Current.Shutdown();
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
            var maximizeButton = FindName("MaximizeButton") as Button;
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