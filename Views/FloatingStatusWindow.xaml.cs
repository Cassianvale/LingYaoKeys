using System.Windows;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using WpfApp.Services.Config;
using WpfApp.Services.Utils;

namespace WpfApp.Views
{
    public partial class FloatingStatusWindow
    {
        private readonly AppConfig _config;
        private System.Windows.Point _dragStartPoint;
        private bool _isDragging;
        private const double DRAG_THRESHOLD = 5.0; // 拖拽阈值（像素）
        private MainWindow _mainWindow;
        private DateTime _lastClickTime;
        private const double DOUBLE_CLICK_THRESHOLD = 300; // 双击时间阈值（毫秒）
        private readonly SerilogManager _logger = SerilogManager.Instance;

        public FloatingStatusWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _config = AppConfigService.Config;
            _mainWindow = mainWindow;
            _logger.Debug("浮窗初始化完成");
            
            // 设置为工具窗口
            SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var extendedStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
                Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, extendedStyle | Win32.WS_EX_TOOLWINDOW);
                _logger.Debug("浮窗工具窗口样式设置完成");
            };
            
            // 加载上次保存的位置
            var left = _config.UI.FloatingWindow.Left;
            var top = _config.UI.FloatingWindow.Top;
            
            // 如果是首次运行或位置无效，设置默认位置（右下角）
            if (left == 0 && top == 0)
            {
                left = SystemParameters.WorkArea.Right - Width - 10;
                top = SystemParameters.WorkArea.Bottom - Height - 10;
                
                // 保存默认位置
                AppConfigService.UpdateConfig(config =>
                {
                    config.UI.FloatingWindow.Left = left;
                    config.UI.FloatingWindow.Top = top;
                });
                _logger.Debug($"浮窗位置初始化为右下角: Left={left}, Top={top}");
            }
            
            // 确保窗口在屏幕范围内
            if (left >= 0 && top >= 0 && 
                left <= SystemParameters.WorkArea.Right - Width &&
                top <= SystemParameters.WorkArea.Bottom - Height)
            {
                Left = left;
                Top = top;
                _logger.Debug($"浮窗位置设置完成: Left={left}, Top={top}");
            }
            else
            {
                // 如果位置超出屏幕范围，重置到右下角
                Left = SystemParameters.WorkArea.Right - Width - 10;
                Top = SystemParameters.WorkArea.Bottom - Height - 10;
                
                // 保存新位置
                AppConfigService.UpdateConfig(config =>
                {
                    config.UI.FloatingWindow.Left = Left;
                    config.UI.FloatingWindow.Top = Top;
                });
                _logger.Debug($"浮窗位置重置到右下角: Left={Left}, Top={Top}");
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
            _isDragging = false;
            CaptureMouse();
            _logger.Debug("浮窗开始拖拽");
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
                    _logger.Debug("浮窗拖拽开始");
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
            
            try
            {
                // 如果发生了拖拽，保存新位置
                if (_isDragging)
                {
                    AppConfigService.UpdateConfig(config =>
                    {
                        config.UI.FloatingWindow.Left = Math.Round(Left, 2);
                        config.UI.FloatingWindow.Top = Math.Round(Top, 2);
                    });
                    _logger.Debug($"保存浮窗位置: Left={Left}, Top={Top}");
                }
                else if (_mainWindow != null)
                {
                    var currentTime = DateTime.Now;
                    var timeSinceLastClick = (currentTime - _lastClickTime).TotalMilliseconds;

                    if (timeSinceLastClick <= DOUBLE_CLICK_THRESHOLD)
                    {
                        _logger.Debug("检测到浮窗双击，准备显示主窗口");
                        // 双击，显示主窗口
                        _mainWindow.RestoreFromMinimized();
                        _lastClickTime = DateTime.MinValue; // 重置点击时间
                    }
                    else
                    {
                        _logger.Debug("记录单击时间");
                        _lastClickTime = currentTime;
                    }
                }
                else
                {
                    _logger.Warning("MainWindow 引用为空，无法处理点击事件");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("处理鼠标抬起事件时发生错误", ex);
            }
            finally
            {
                _isDragging = false;
            }
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                _logger.Debug("浮窗接收到右键点击");
                
                // 确保窗口获得焦点
                Focus();
                
                // 显示托盘菜单
                if (_mainWindow?._trayContextMenu != null)
                {
                    _logger.Debug("准备显示托盘菜单");
                    
                    // 设置菜单位置和目标
                    _mainWindow._trayContextMenu.PlacementTarget = this;
                    _mainWindow._trayContextMenu.Placement = PlacementMode.MousePoint;
                    
                    // 确保菜单在显示时不会自动关闭
                    _mainWindow._trayContextMenu.StaysOpen = true;
                    _mainWindow._trayContextMenu.IsOpen = true;
                    
                    // 订阅菜单关闭事件，以便在关闭时重置 StaysOpen
                    _mainWindow._trayContextMenu.Closed += (s, args) =>
                    {
                        if (_mainWindow?._trayContextMenu != null)
                        {
                            _mainWindow._trayContextMenu.StaysOpen = false;
                        }
                    };
                    
                    e.Handled = true;
                    _logger.Debug("托盘菜单已显示");
                }
                else
                {
                    _logger.Warning("MainWindow 或托盘菜单为空，无法显示菜单");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("显示托盘菜单时发生错误", ex);
            }
        }

        private void FloatingStatusWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 保存窗口位置
                AppConfigService.UpdateConfig(config =>
                {
                    config.UI.FloatingWindow.Left = Left;
                    config.UI.FloatingWindow.Top = Top;
                });
                _logger.Debug($"保存浮窗关闭前位置: Left={Left}, Top={Top}");
            }
            catch (Exception ex)
            {
                _logger.Error("保存浮窗位置时发生错误", ex);
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