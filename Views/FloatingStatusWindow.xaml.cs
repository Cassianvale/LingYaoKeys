using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using WpfApp.Services.Config;
using WpfApp.Services.Utils;
using System.Windows.Media.Animation;
using System.Windows.Media;
using WpfApp.Converters;
using System.ComponentModel;
using System.Windows.Data;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
// 使用静态导入
using static System.Windows.Media.RenderOptions;

namespace WpfApp.Views;

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
    private RotateTransform _borderRotateTransform;
    private string _currentStatus;

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

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // 启用硬件加速和优化渲染设置
            SetupHardwareAcceleration();
            
            // 初始化边框样式
            UpdateBorderStyle();
            
            // 订阅数据上下文变化事件
            DataContextChanged += FloatingStatusWindow_DataContextChanged;
            
            // 初始化完成日志
            _logger.Debug("浮窗动画和边框样式初始化完成");
        }
        catch (Exception ex)
        {
            _logger.Error("初始化浮窗动画时出错", ex);
        }
    }
    
    private void FloatingStatusWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            // 数据上下文变化时更新边框样式
            UpdateBorderStyle();
            
            // 尝试适配INotifyPropertyChanged接口
            if (e.NewValue is INotifyPropertyChanged notifyPropertyChanged)
            {
                // 移除旧的事件处理程序（如果有）
                if (e.OldValue is INotifyPropertyChanged oldNotify)
                {
                    oldNotify.PropertyChanged -= ViewModel_PropertyChanged;
                }
                
                // 添加新的事件处理程序
                notifyPropertyChanged.PropertyChanged += ViewModel_PropertyChanged;
                _logger.Debug("已注册INotifyPropertyChanged事件监听");
            }
            else
            {
                _logger.Warning("ViewModel未实现INotifyPropertyChanged接口，状态更新可能不同步");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("更新浮窗边框样式时出错", ex);
        }
    }
    
    /// <summary>
    /// ViewModel属性变化事件处理
    /// </summary>
    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        try
        {
            // 如果是StatusText属性变化，更新边框样式
            if (e.PropertyName == "StatusText")
            {
                UpdateBorderStyle();
                _logger.Debug("检测到StatusText属性变化，边框样式已更新");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("处理属性变化事件时出错", ex);
        }
    }
    
    /// <summary>
    /// 外部接口：直接更新浮窗边框样式
    /// 由ViewModel在调用UpdateFloatingStatus时调用
    /// </summary>
    /// <param name="statusText">状态文本</param>
    public void UpdateBorderStyle(string statusText)
    {
        if (BorderContainer == null)
            return;
            
        try
        {
            // 如果状态没变化，不需要更新
            if (statusText == _currentStatus)
                return;
                
            _currentStatus = statusText;
            
            // 确定状态对应的颜色索引
            int colorIndex;
            switch (statusText)
            {
                case "运行中":
                    colorIndex = 0;
                    break;
                case "已禁用":
                    colorIndex = 1;
                    break;
                default: // 已停止或其他
                    colorIndex = 2;
                    break;
            }
            
            // 获取对应的颜色数组
            var colors = StatusToColorConverter.GetBorderColors(colorIndex);
            
            if (BorderContainer.BorderBrush is LinearGradientBrush existingBrush)
            {
                // 如果已有LinearGradientBrush，则只更新颜色
                if (existingBrush.GradientStops.Count == 3)
                {
                    // 更新现有GradientStops的Color属性，保持动画和变换不变
                    existingBrush.GradientStops[0].Color = colors[0];
                    existingBrush.GradientStops[1].Color = colors[1];
                    existingBrush.GradientStops[2].Color = colors[2];
                    
                    _logger.Debug($"浮窗边框样式已更新（保留动画），当前状态: {statusText}");
                }
                else
                {
                    // GradientStops数量不匹配，重新创建GradientStops
                    existingBrush.GradientStops.Clear();
                    existingBrush.GradientStops.Add(new GradientStop(colors[0], 0.0));
                    existingBrush.GradientStops.Add(new GradientStop(colors[1], 0.5));
                    existingBrush.GradientStops.Add(new GradientStop(colors[2], 1.0));
                    
                    _logger.Debug($"浮窗边框样式已重建，当前状态: {statusText}");
                }
            }
            else
            {
                // 如果没有现有画刷，则创建新的
                var gradientBrush = new LinearGradientBrush();
                gradientBrush.StartPoint = new System.Windows.Point(0, 0);
                gradientBrush.EndPoint = new System.Windows.Point(1, 1);
                
                gradientBrush.GradientStops.Add(new GradientStop(colors[0], 0.0));
                gradientBrush.GradientStops.Add(new GradientStop(colors[1], 0.5));
                gradientBrush.GradientStops.Add(new GradientStop(colors[2], 1.0));
                
                // 设置旋转变换
                var rotateTransform = new RotateTransform
                {
                    CenterX = 0.5,
                    CenterY = 0.5
                };
                gradientBrush.RelativeTransform = rotateTransform;
                
                // 设置边框画刷
                BorderContainer.BorderBrush = gradientBrush;
                
                _logger.Debug($"浮窗边框样式已创建新画刷，当前状态: {statusText}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("更新边框样式时出错", ex);
        }
    }
    
    /// <summary>
    /// 根据当前状态更新边框样式
    /// </summary>
    private void UpdateBorderStyle()
    {
        if (BorderContainer == null || DataContext == null)
            return;
            
        try
        {
            // 获取当前状态文本
            string statusText = null;
            
            // 尝试获取状态文本
            try
            {
                dynamic viewModel = DataContext;
                statusText = viewModel?.StatusText as string;
            }
            catch
            {
                // 如果动态访问失败，尝试使用反射
                try
                {
                    var property = DataContext.GetType().GetProperty("StatusText");
                    if (property != null)
                    {
                        statusText = property.GetValue(DataContext) as string;
                    }
                }
                catch
                {
                    _logger.Warning("无法获取StatusText属性值");
                }
            }
            
            // 更新边框样式
            if (statusText != null)
            {
                UpdateBorderStyle(statusText);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("更新边框样式时出错", ex);
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
            var currentPosition = e.GetPosition(this);
            var diff = currentPosition - _dragStartPoint;

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
                    if (_mainWindow?._trayContextMenu != null) _mainWindow._trayContextMenu.StaysOpen = false;
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
            _logger.Error("保存浮窗位置时出错", ex);
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

    /// <summary>
    /// 为当前窗口设置硬件加速和渲染优化
    /// </summary>
    private void SetupHardwareAcceleration()
    {
        try
        {
            // 设置缓存模式为位图缓存，提高渲染性能
            this.CacheMode = new BitmapCache
            {
                EnableClearType = true,
                SnapsToDevicePixels = true,
                RenderAtScale = 1.0
            };
            
            // 启用布局舍入以确保渲染精确
            this.UseLayoutRounding = true;
            
            // 设置边框为位图缓存，优化渐变动画性能
            if (BorderContainer != null)
            {
                // 优化边框渲染
                BorderContainer.CacheMode = new BitmapCache { 
                    EnableClearType = true, 
                    SnapsToDevicePixels = true
                };
                RenderOptions.SetCachingHint(BorderContainer, CachingHint.Cache);
            }
            
            // 强制为此窗口使用硬件加速
            HwndSource hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource != null)
            {
                hwndSource.CompositionTarget.RenderMode = RenderMode.Default;
                _logger.Debug("已为浮窗启用硬件渲染模式");
            }
            
            // 设置渲染动画的线程优先级
            if (Dispatcher != null && Dispatcher.Thread != null)
            {
                Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
                _logger.Debug("已优化浮窗渲染线程优先级");
            }
            
            _logger.Debug("浮窗硬件加速已配置完成");
        }
        catch (Exception ex)
        {
            _logger.Error("配置窗口硬件加速时出错", ex);
        }
    }
}