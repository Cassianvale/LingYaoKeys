using System;
using System.Windows;
using System.Windows.Input;
using System.ComponentModel;
using WpfApp.ViewModels;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using WpfApp.Services;

namespace WpfApp.Views
{
    public partial class FloatingWindow : Window
    {
        private readonly LogManager _logger = LogManager.Instance;
        private bool _isApplicationShutdown;
        private bool _isDragging;
        private readonly MainWindow _mainWindow;
        private Point _mouseDownPosition;
        private const double DRAG_THRESHOLD = 3.0; // 拖动阈值（像素）

        public FloatingWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _logger.LogDebug("FloatingWindow", "浮窗已初始化");
            Closing += FloatingWindow_Closing;
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _mouseDownPosition = e.GetPosition(this);
            _isDragging = false;

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                _logger.LogDebug("FloatingWindow", "开始拖动浮窗");
                DragMove();
            }
        }

        private void StatusButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point currentPosition = e.GetPosition(this);
            Vector movement = currentPosition - _mouseDownPosition;
            bool wasDragging = movement.Length > DRAG_THRESHOLD;

            if (wasDragging)
            {
                _logger.LogDebug("FloatingWindow", "结束拖动");
                return;
            }

            // 单击处理
            if (DataContext is FloatingWindowViewModel viewModel)
            {
                _logger.LogDebug("FloatingWindow", "检测到浮窗单击事件，切换置顶状态");
                viewModel.ToggleTopmostCommand.Execute(null);
            }
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _logger.LogDebug("FloatingWindow", "检测到浮窗双击事件，准备还原主窗口");
                e.Handled = true;
                ShowMainWindow();
            }
        }

        private void ShowMainWindow()
        {
            try
            {
                _logger.LogDebug("FloatingWindow", "开始调用主窗口的还原方法");
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 使用新的恢复方法
                    _mainWindow.RestoreFromMinimized();
                });
                
                _logger.LogDebug("FloatingWindow", "主窗口还原方法调用完成");
            }
            catch (Exception ex)
            {
                _logger.LogError("FloatingWindow", "还原主窗口时发生错误", ex);
                MessageBox.Show("还原主窗口时发生错误，请尝试重启应用程序。", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);

            // 获取鼠标位置
            var mousePosition = PointToScreen(e.GetPosition(this));

            // 显示托盘菜单
            if (_mainWindow._trayContextMenu != null)
            {
                _mainWindow._trayContextMenu.Placement = PlacementMode.AbsolutePoint;
                _mainWindow._trayContextMenu.PlacementRectangle = new Rect(mousePosition, new Size(0, 0));
                _mainWindow._trayContextMenu.IsOpen = true;
            }
        }

        protected override void OnSourceInitialized(System.EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var exStyle = (int)System.Windows.Interop.NativeMethods.GetWindowLong(helper.Handle, -20);
            // 添加 WS_EX_TOOLWINDOW 样式，使窗口不显示在任务栏
            exStyle |= 0x80;
            // 禁用 Aero Snap 功能
            var style = (int)System.Windows.Interop.NativeMethods.GetWindowLong(helper.Handle, -16);
            style &= ~(0x00400000); // WS_MAXIMIZEBOX
            style &= ~(0x00040000); // WS_THICKFRAME
            System.Windows.Interop.NativeMethods.SetWindowLong(helper.Handle, -16, style);
            System.Windows.Interop.NativeMethods.SetWindowLong(helper.Handle, -20, exStyle);
        }

        private void FloatingWindow_Closing(object sender, CancelEventArgs e)
        {
            // 如果应用程序正在关闭，允许窗口关闭
            if (Application.Current.ShutdownMode == ShutdownMode.OnExplicitShutdown)
            {
                _isApplicationShutdown = true;
                return;
            }

            // 否则，只是隐藏窗口而不是关闭
            if (!_isApplicationShutdown)
            {
                e.Cancel = true;
                Hide();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // 如果是应用程序关闭导致的窗口关闭，不做额外处理
            if (_isApplicationShutdown)
            {
                return;
            }
        }
    }
} 