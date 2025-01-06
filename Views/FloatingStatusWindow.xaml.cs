using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using WpfApp.Models;
using WpfApp.Services;

namespace WpfApp.Views
{
    public partial class FloatingStatusWindow : Window
    {
        private readonly AppConfig _config;
        private System.Windows.Point _dragStartPoint;
        private bool _isDragging;
        private const double DRAG_THRESHOLD = 5.0; // 拖拽阈值（像素）
        private MainWindow _mainWindow;
        private DateTime _lastClickTime;
        private const double DOUBLE_CLICK_THRESHOLD = 300; // 双击时间阈值（毫秒）

        public FloatingStatusWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _config = AppConfigService.Config;
            _mainWindow = mainWindow;
            
            // 设置为工具窗口
            SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var extendedStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
                Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, extendedStyle | Win32.WS_EX_TOOLWINDOW);
            };
            
            // 加载上次保存的位置
            var left = _config.FloatingWindowLeft;
            var top = _config.FloatingWindowTop;
            
            // 确保窗口在屏幕范围内
            if (left >= 0 && top >= 0 && 
                left <= SystemParameters.WorkArea.Right - Width &&
                top <= SystemParameters.WorkArea.Bottom - Height)
            {
                Left = left;
                Top = top;
            }
            else
            {
                // 默认位置：屏幕右下角
                Left = SystemParameters.WorkArea.Right - Width - 10;
                Top = SystemParameters.WorkArea.Bottom - Height - 10;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
            _isDragging = false;
            CaptureMouse();
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && IsMouseCaptured)
            {
                System.Windows.Point currentPosition = e.GetPosition(this);
                Vector diff = currentPosition - _dragStartPoint;

                // 如果移动距离超过阈值，开始拖拽
                if (!_isDragging && (Math.Abs(diff.X) > DRAG_THRESHOLD || Math.Abs(diff.Y) > DRAG_THRESHOLD))
                {
                    _isDragging = true;
                }

                if (_isDragging)
                {
                    // 执行拖拽
                    Left += diff.X;
                    Top += diff.Y;
                }
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ReleaseMouseCapture();
            
            // 如果发生了拖拽，保存新位置
            if (_isDragging)
            {
                AppConfigService.UpdateConfig(config =>
                {
                    config.FloatingWindowLeft = Math.Round(Left, 2);
                    config.FloatingWindowTop = Math.Round(Top, 2);
                });
            }
            else
            {
                var currentTime = DateTime.Now;
                var timeSinceLastClick = (currentTime - _lastClickTime).TotalMilliseconds;

                if (timeSinceLastClick <= DOUBLE_CLICK_THRESHOLD)
                {
                    // 双击，显示主窗口
                    _mainWindow.RestoreFromMinimized();
                    _lastClickTime = DateTime.MinValue; // 重置点击时间
                }
                else
                {
                    _lastClickTime = currentTime;
                }
            }
            _isDragging = false;
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 显示托盘菜单
            if (_mainWindow._trayContextMenu != null)
            {
                _mainWindow._trayContextMenu.PlacementTarget = this;
                _mainWindow._trayContextMenu.Placement = PlacementMode.MousePoint;
                _mainWindow._trayContextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        // 添加 Win32 API 定义
        private static class Win32
        {
            public const int GWL_EXSTYLE = -20;
            public const int WS_EX_TOOLWINDOW = 0x00000080;

            [DllImport("user32.dll")]
            public static extern int GetWindowLong(IntPtr hwnd, int index);

            [DllImport("user32.dll")]
            public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        }
    }
} 