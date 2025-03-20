using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using System.Drawing;
using System.Windows.Forms;
using WpfApp.ViewModels;
using WpfApp.Services.Core;
using WpfApp.Services.Models;
using WpfApp.Services.Utils;
using WpfApp.Behaviors;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Windows.Media.Animation;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using IO = System.IO; // 使用IO作为System.IO的别名，避免与Shapes.Path冲突
using Path = System.Windows.Shapes.Path; // 明确指定Path是指Windows.Shapes.Path

// 提供按键映射视图
namespace WpfApp.Views;

/// <summary>
/// KeyMappingView.xaml 的交互逻辑
/// </summary>
public partial class KeyMappingView : Page
{
    private readonly SerilogManager _logger = SerilogManager.Instance;
    private const string KEY_ERROR = "无法识别按键，请检查输入法是否关闭";
    private const string HOTKEY_CONFLICT = "无法设置与热键相同的按键";
    private HotkeyService? _hotkeyService;
    private readonly KeyMappingViewModel _viewModel;

    // 字典用于存储待确认删除的按钮和定时器
    private readonly Dictionary<System.Windows.Controls.Button, System.Windows.Threading.DispatcherTimer>
        _pendingDeleteButtons = new();

    // 定义删除按钮状态标记，用于识别按钮当前状态
    private static readonly DependencyProperty DeleteConfirmStateProperty =
        DependencyProperty.RegisterAttached(
            "DeleteConfirmState",
            typeof(bool),
            typeof(KeyMappingView),
            new PropertyMetadata(false));

    // 鼠标拖拽相关成员变量
    private bool _isDragging = false;
    private Ellipse _dragPoint = null;
    private Window _dragWindow = null;
    private System.Windows.Point _startPoint;
    // 添加拖拽距离阈值常量
    private const double DRAG_THRESHOLD = 5.0;
    // 添加标记是否超过阈值的字段
    private bool _isDragStarted = false;
    private bool _hasLoggedWarning = false;
    
    // 添加坐标点管理相关成员变量
    private bool _isCoordinateMarkersVisible = false;
    private readonly Dictionary<KeyItem, CoordinateMarker> _coordinateMarkers = new();
    private KeyItem _draggingKeyItem = null;
    
    // 添加编辑模式状态标志
    private bool _isEditMode = false;
    
    // 添加Win32 API相关代码 - 放在类的开始部分
    private static class Win32
    {
        // 扩展窗口样式常量
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_APPWINDOW = 0x00040000;
        
        // 导入Win32 API
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int GetWindowLong(System.IntPtr hwnd, int index);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SetWindowLong(System.IntPtr hwnd, int index, int newStyle);
        
        // 设置窗口为工具窗口，不在Alt+Tab列表中显示
        public static void HideFromAltTab(Window window)
        {
            try
            {
                // 等待窗口初始化完成
                window.SourceInitialized += (s, e) =>
                {
                    // 获取窗口句柄
                    System.IntPtr handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                    
                    // 获取当前扩展样式
                    int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
                    
                    // 添加工具窗口样式，移除应用窗口样式
                    exStyle |= WS_EX_TOOLWINDOW;
                    exStyle &= ~WS_EX_APPWINDOW;
                    
                    // 设置新样式
                    SetWindowLong(handle, GWL_EXSTYLE, exStyle);
                };
            }
            catch (Exception ex)
            {
                SerilogManager.Instance.Error("设置窗口样式时发生异常", ex);
            }
        }
    }
    
    private class CoordinateMarker
    {
        public Window MarkerWindow { get; private set; }
        public Window LabelWindow { get; private set; }
        public KeyItem KeyItem { get; private set; }
        public int Index { get; set; }
        public bool IsDragging { get; set; }
        
        public CoordinateMarker(KeyItem keyItem, int index)
        {
            KeyItem = keyItem;
            Index = index;
            CreateMarkerWindow();
            CreateLabelWindow();
        }
        
        private void CreateMarkerWindow()
        {
            // 创建红点UI
            Grid mainGrid = new Grid
            {
                Width = 16,
                Height = 16,
                Background = null // 透明背景
            };
            
            // 创建红点
            Ellipse point = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0)), // 红色
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)), // 白色边框
                StrokeThickness = 1,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            
            mainGrid.Children.Add(point);
            
            // 创建红点窗口
            MarkerWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = null,
                Topmost = true,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                SizeToContent = SizeToContent.Manual,
                Content = mainGrid,
                Width = 16,
                Height = 16
            };
            
            // 设置为工具窗口，不在Alt+Tab列表中显示
            Win32.HideFromAltTab(MarkerWindow);
        }
        
        private void CreateLabelWindow()
        {
            // 创建缩放变换
            ScaleTransform scaleTransform = new ScaleTransform(0.8, 0.8);
            
            // 创建标签UI
            Border border = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 30, 30, 30)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4), // 增加内边距，使文本显示更美观
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 315,
                    ShadowDepth = 3,
                    Opacity = 0.7,
                    BlurRadius = 5
                },
                // 将变换应用到Border上，而不是Window
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                RenderTransform = scaleTransform,
                Opacity = 0.9
            };
            
            TextBlock label = new TextBlock
            {
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center
            };
            
            // 设置标签文本 - 格式为 "序号-(X,Y)"
            UpdateLabel(label);
            
            border.Child = label;
            
            // 创建标签窗口
            LabelWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = null,
                Topmost = true,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                SizeToContent = SizeToContent.WidthAndHeight,
                Content = border
            };
            
            // 设置为工具窗口，不在Alt+Tab列表中显示
            Win32.HideFromAltTab(LabelWindow);
            
            // 设置标签窗口初始不可见，等待位置计算完成后再显示
            LabelWindow.Visibility = Visibility.Collapsed;
            
            // 添加显示时的动画效果
            LabelWindow.IsVisibleChanged += (sender, e) =>
            {
                if (LabelWindow.IsVisible)
                {
                    // 创建缩放动画
                    DoubleAnimation scaleAnimation = new DoubleAnimation
                    {
                        From = 0.8,
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(150),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    
                    // 创建透明度动画
                    DoubleAnimation opacityAnimation = new DoubleAnimation
                    {
                        From = 0.7,
                        To = 0.95,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    
                    // 应用动画到Border而不是Window
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
                    border.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
                }
            };
        }
        
        public void UpdateLabel()
        {
            if (LabelWindow.Content is Border border && border.Child is TextBlock label)
            {
                // 更新标签文本
                UpdateLabel(label);
                
                // 确保标签窗口尺寸更新
                LabelWindow.UpdateLayout();
                
                // 重新计算位置，以适应新的尺寸
                UpdatePosition();
            }
        }
        
        private void UpdateLabel(TextBlock label)
        {
            // 设置标签文本
            // 格式为 "序号-(X,Y)"
            label.Text = $"{Index + 1}-({KeyItem.X ?? 0},{KeyItem.Y ?? 0})";
        }
        
        public void UpdatePosition()
        {
            try
            {
                if (KeyItem.Type != KeyItemType.Coordinates)
                    return;
                    
                // 使用用户坐标（从1开始）转换为系统坐标（从0开始）
                int x = (KeyItem.X ?? 1) - 1; // 用户坐标从1开始，系统坐标从0开始
                int y = (KeyItem.Y ?? 1) - 1;
                
                // 确保MarkerWindow尺寸已更新
                MarkerWindow.UpdateLayout();
                
                // 获取DPI感知的PresentationSource以进行转换
                PresentationSource source = PresentationSource.FromVisual(MarkerWindow);
                
                // 需要考虑DPI缩放的窗口位置
                double dpiAwareX = x;
                double dpiAwareY = y;
                
                // 应用DPI转换 - 从物理坐标转换为WPF逻辑坐标
                bool hasDpiTransform = false;
                
                if (source != null && source.CompositionTarget != null)
                {
                    hasDpiTransform = true;
                    Matrix transformMatrix = source.CompositionTarget.TransformFromDevice;
                    
                    // 将物理坐标点转换为WPF逻辑坐标
                    System.Windows.Point physicalPoint = new System.Windows.Point(x, y);
                    System.Windows.Point logicalPoint = transformMatrix.Transform(physicalPoint);
                    
                    dpiAwareX = logicalPoint.X;
                    dpiAwareY = logicalPoint.Y;
                    
                    // 记录坐标转换信息
                    SerilogManager.Instance.Debug($"坐标标记位置 - 原始: ({x},{y}), DPI转换后: ({dpiAwareX:F1},{dpiAwareY:F1})");
                }
                
                // 更新红点位置 - 居中显示，使用DPI感知坐标
                MarkerWindow.Left = dpiAwareX - MarkerWindow.Width / 2;
                MarkerWindow.Top = dpiAwareY - MarkerWindow.Height / 2;
                
                // 确保标签窗口尺寸已更新
                LabelWindow.UpdateLayout();
                
                // 获取当前屏幕工作区域
                System.Windows.Forms.Screen currentScreen = System.Windows.Forms.Screen.FromPoint(
                    new System.Drawing.Point(x, y));
                System.Drawing.Rectangle workingArea = currentScreen.WorkingArea;
                
                // 计算标签尺寸和位置
                double labelWidth = LabelWindow.ActualWidth;
                double labelHeight = LabelWindow.ActualHeight;
                
                // 记录原始尺寸信息用于调试
                SerilogManager.Instance.Debug($"标签窗口尺寸: 宽={labelWidth:F1}, 高={labelHeight:F1}, 坐标点: X={x}, Y={y}");
                
                // DPI缩放转换 - 将系统坐标的工作区域转换为WPF坐标
                Rect dpiAwareWorkingArea = new Rect(
                    workingArea.Left, 
                    workingArea.Top, 
                    workingArea.Width, 
                    workingArea.Height);
                
                if (source != null && source.CompositionTarget != null)
                {
                    Matrix transformMatrix = source.CompositionTarget.TransformFromDevice;
                    
                    // 转换工作区域的四个点
                    System.Windows.Point topLeft = transformMatrix.Transform(new System.Windows.Point(workingArea.Left, workingArea.Top));
                    System.Windows.Point bottomRight = transformMatrix.Transform(new System.Windows.Point(workingArea.Right, workingArea.Bottom));
                    
                    // 创建DPI感知的工作区域矩形
                    dpiAwareWorkingArea = new Rect(topLeft, bottomRight);
                }
                
                // 默认位置 - 优先考虑红点右侧，使用DPI感知坐标
                double labelLeft = dpiAwareX + 10;
                double labelTop = dpiAwareY - labelHeight / 2;
                
                // 记录初始位置信息
                SerilogManager.Instance.Debug($"默认标签位置: 左={labelLeft:F1}, 上={labelTop:F1}");
                
                // 智能边界检测 - 首先检查右边界
                if (labelLeft + labelWidth > dpiAwareWorkingArea.Right - 10)
                {
                    // 如果右侧放不下，尝试放在左侧
                    labelLeft = dpiAwareX - 10 - labelWidth;
                    SerilogManager.Instance.Debug($"右侧超出边界，调整到左侧: 左={labelLeft:F1}");
                }
                
                // 检查左边界
                if (labelLeft < dpiAwareWorkingArea.Left + 10)
                {
                    // 如果左侧也放不下，放在上方或下方
                    // 默认放在上方
                    labelLeft = dpiAwareX - labelWidth / 2;
                    labelTop = dpiAwareY - 10 - labelHeight;
                    
                    SerilogManager.Instance.Debug($"左侧超出边界，调整到上方: 左={labelLeft:F1}, 上={labelTop:F1}");
                    
                    // 如果上方放不下，放在下方
                    if (labelTop < dpiAwareWorkingArea.Top + 10)
                    {
                        labelTop = dpiAwareY + 10;
                        SerilogManager.Instance.Debug($"上方超出边界，调整到下方: 上={labelTop:F1}");
                    }
                }
                
                // 检查上边界
                if (labelTop < dpiAwareWorkingArea.Top + 10)
                {
                    // 如果已经进行了左右调整，则调整到下方
                    labelTop = dpiAwareY + 10;
                    SerilogManager.Instance.Debug($"上方超出边界，调整到下方: 上={labelTop:F1}");
                }
                
                // 检查下边界
                if (labelTop + labelHeight > dpiAwareWorkingArea.Bottom - 10)
                {
                    // 如果下方放不下，强制放在上方
                    labelTop = dpiAwareY - 10 - labelHeight;
                    SerilogManager.Instance.Debug($"下方超出边界，调整到上方: 上={labelTop:F1}");
                    
                    // 如果实在放不下，至少确保尽可能显示在屏幕内
                    if (labelTop < dpiAwareWorkingArea.Top + 10)
                    {
                        labelTop = Math.Max(dpiAwareWorkingArea.Top + 5, dpiAwareY - labelHeight / 2);
                        SerilogManager.Instance.Debug($"上下均超出边界，强制调整到最合适位置: 上={labelTop:F1}");
                    }
                }
                
                // 记录最终位置
                SerilogManager.Instance.Debug($"最终标签位置: 左={labelLeft:F1}, 上={labelTop:F1}, DPI转换={hasDpiTransform}");
                
                // 应用最终位置
                LabelWindow.Left = labelLeft;
                LabelWindow.Top = labelTop;
            }
            catch (Exception ex)
            {
                SerilogManager.Instance.Error("更新坐标标签位置时发生异常", ex);
            }
        }
        
        public void Show()
        {
            // 首先显示窗口
            MarkerWindow.Show();
            LabelWindow.Show();
            
            // 确保窗口内容已更新
            MarkerWindow.UpdateLayout();
            LabelWindow.UpdateLayout();
            
            // 计算并应用正确位置
            UpdatePosition();
            
            // 添加简单的出现动画效果
            if (LabelWindow.RenderTransform is ScaleTransform scaleTransform)
            {
                // 从缩小状态开始
                scaleTransform.ScaleX = 0.7;
                scaleTransform.ScaleY = 0.7;
                
                // 创建动画
                DoubleAnimation scaleAnimation = new DoubleAnimation
                {
                    From = 0.7,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                
                // 应用动画
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
            }
            
            // 最后确保标签窗口可见
            LabelWindow.Visibility = Visibility.Visible;
        }
        
        public void Hide()
        {
            MarkerWindow.Hide();
            LabelWindow.Hide();
        }
        
        public void Close()
        {
            MarkerWindow.Close();
            LabelWindow.Close();
        }
    }

    public KeyMappingView()
    {
        InitializeComponent();
        _viewModel = DataContext as KeyMappingViewModel;

        // 监听 DataContext 变化
        DataContextChanged += KeyMappingView_DataContextChanged;

        // 添加页面失去焦点事件，用于清除所有删除确认状态
        LostFocus += KeyMappingView_LostFocus;

        // 添加音量设置弹出窗口的事件处理
        if (soundSettingsPopup != null)
        {
            soundSettingsPopup.Opened += (s, e) => { _logger.Debug("音量设置弹出窗口已打开"); };

            soundSettingsPopup.Closed += (s, e) => { _logger.Debug("音量设置弹出窗口已关闭"); };
        }
        
        // 初始化鼠标拖拽按钮事件
        InitMouseDragEvents();
        
        // 添加页面卸载事件，确保正确清理资源
        Unloaded += KeyMappingView_Unloaded;
    }

    // 处理页面卸载事件
    private void KeyMappingView_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger.Debug("KeyMappingView 正在卸载，清理资源");
            
            // 清理所有坐标点
            ClearAllCoordinateMarkers();
            
            // 清理拖拽资源
            CleanupDragOperation();
            
            // 取消事件订阅
            if (ViewModel?.KeyList is ObservableCollection<KeyItem> keyList)
            {
                keyList.CollectionChanged -= KeyList_CollectionChanged;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("KeyMappingView 卸载时发生异常", ex);
        }
    }

    private KeyMappingViewModel ViewModel => DataContext as KeyMappingViewModel;

    // 添加 DataContext 变化事件处理
    private void KeyMappingView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            // 取消旧ViewModel的事件订阅
            if (e.OldValue is KeyMappingViewModel oldViewModel)
            {
                // 取消KeyList的变化通知订阅
                if (oldViewModel.KeyList != null)
                {
                    oldViewModel.KeyList.CollectionChanged -= KeyList_CollectionChanged;
                    _logger.Debug("已取消旧ViewModel的KeyList变化事件订阅");
                }
                
                // 取消坐标索引更新事件订阅
                oldViewModel.CoordinateIndicesNeedUpdate -= ViewModel_CoordinateIndicesNeedUpdate;
            }
            
            // 订阅新ViewModel的事件
            if (e.NewValue is KeyMappingViewModel newViewModel)
            {
                // 获取HotkeyService实例
                _hotkeyService = newViewModel.GetHotkeyService();
                
                // 订阅KeyList的变化通知
                if (newViewModel.KeyList != null)
                {
                    newViewModel.KeyList.CollectionChanged += KeyList_CollectionChanged;
                    _logger.Debug("已订阅新ViewModel的KeyList变化事件");
                }
                
                // 订阅坐标索引更新事件
                newViewModel.CoordinateIndicesNeedUpdate += ViewModel_CoordinateIndicesNeedUpdate;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("处理DataContext变化事件时发生异常", ex);
        }
    }
    
    // 处理坐标索引更新事件
    private void ViewModel_CoordinateIndicesNeedUpdate(object sender, EventArgs e)
    {
        try
        {
            _logger.Debug("收到坐标索引更新事件");
            
            // 调用更新所有坐标标记索引的方法
            UpdateAllCoordinateMarkerIndices();
        }
        catch (Exception ex)
        {
            _logger.Error("处理坐标索引更新事件时发生异常", ex);
        }
    }
    
    // 处理KeyList集合变化事件
    private void KeyList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            // 如果坐标点不可见且不在编辑模式下，无需更新
            if (!_isCoordinateMarkersVisible && !_isEditMode)
                return;
                
            _logger.Debug("KeyList集合发生变化，更新坐标点");
            
            // 根据变化类型处理
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    // 添加新坐标点
                    if (e.NewItems != null)
                    {
                        // 获取KeyList
                        var keyList = ViewModel?.KeyList;
                        if (keyList == null)
                            return;
                            
                        // 筛选出所有坐标类型的按键以计算正确的索引
                        var coordinateItems = keyList.Where(item => item.Type == KeyItemType.Coordinates).ToList();
                            
                        foreach (KeyItem newItem in e.NewItems)
                        {
                            // 只处理坐标类型的按键
                            if (newItem.Type != KeyItemType.Coordinates)
                                continue;
                                
                            // 查找项在坐标类型集合中的索引
                            int index = coordinateItems.IndexOf(newItem);
                            if (index < 0)
                                continue;
                                
                            // 更新KeyItem的坐标索引属性
                            newItem.CoordinateIndex = index;
                                
                            // 创建新标记
                            var marker = new CoordinateMarker(newItem, index);
                            
                            // 添加到集合
                            _coordinateMarkers[newItem] = marker;
                            
                            // 添加拖拽事件
                            AttachDragEvents(marker);
                            
                            // 显示标记
                            marker.Show();
                            
                            _logger.Debug($"添加新坐标点: {index + 1}-({newItem.X},{newItem.Y})");
                        }
                    }
                    break;
                    
                case NotifyCollectionChangedAction.Remove:
                    // 移除坐标点
                    if (e.OldItems != null)
                    {
                        foreach (KeyItem oldItem in e.OldItems)
                        {
                            // 只处理存在于集合中的项目
                            if (_coordinateMarkers.TryGetValue(oldItem, out var marker))
                            {
                                // 移除拖拽事件
                                DetachDragEvents(marker);
                                
                                // 关闭窗口
                                marker.Close();
                                
                                // 从集合中移除
                                _coordinateMarkers.Remove(oldItem);
                                
                                _logger.Debug($"移除坐标点: {marker.Index + 1}-({oldItem.X},{oldItem.Y})");
                            }
                        }
                        
                        // 在删除后，需要更新所有坐标的索引
                        UpdateAllCoordinateMarkerIndices();
                    }
                    break;
                    
                case NotifyCollectionChangedAction.Replace:
                    // 替换坐标点 - 简单处理，移除旧的，添加新的
                    if (e.OldItems != null)
                    {
                        foreach (KeyItem oldItem in e.OldItems)
                        {
                            if (_coordinateMarkers.TryGetValue(oldItem, out var marker))
                            {
                                DetachDragEvents(marker);
                                marker.Close();
                                _coordinateMarkers.Remove(oldItem);
                            }
                        }
                    }
                    
                    if (e.NewItems != null)
                    {
                        var keyList = ViewModel?.KeyList;
                        if (keyList == null)
                            return;
                            
                        // 筛选出所有坐标类型的按键以计算正确的索引
                        var coordinateItems = keyList.Where(item => item.Type == KeyItemType.Coordinates).ToList();
                            
                        foreach (KeyItem newItem in e.NewItems)
                        {
                            if (newItem.Type != KeyItemType.Coordinates)
                                continue;
                                
                            // 查找项在坐标类型集合中的索引
                            int index = coordinateItems.IndexOf(newItem);
                            if (index < 0)
                                continue;
                                
                            // 更新KeyItem的坐标索引属性
                            newItem.CoordinateIndex = index;
                                
                            var marker = new CoordinateMarker(newItem, index);
                            _coordinateMarkers[newItem] = marker;
                            AttachDragEvents(marker);
                            marker.Show();
                        }
                    }
                    break;
                    
                case NotifyCollectionChangedAction.Move:
                    // 移动坐标点 - 需要更新索引
                    UpdateAllCoordinateMarkerIndices();
                    break;
                    
                case NotifyCollectionChangedAction.Reset:
                    // 重置集合 - 清除所有坐标点并重新显示
                    ShowAllCoordinateMarkers();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("处理KeyList集合变化事件时发生异常", ex);
        }
    }
    
    // 更新所有坐标点的索引
    private void UpdateAllCoordinateMarkerIndices()
    {
        try
        {
            var keyList = ViewModel?.KeyList;
            if (keyList == null)
                return;
                
            // 首先筛选出所有坐标类型的按键
            var coordinateItems = keyList.Where(item => item.Type == KeyItemType.Coordinates).ToList();
            
            // 为所有坐标点更新索引并刷新标签
            for (int i = 0; i < coordinateItems.Count; i++)
            {
                var keyItem = coordinateItems[i];
                
                // 更新KeyItem的坐标索引属性
                keyItem.CoordinateIndex = i;
                
                if (_coordinateMarkers.TryGetValue(keyItem, out var marker))
                {
                    // 更新索引 - 使用在坐标类型集合中的索引，而不是在整个keyList中的索引
                    marker.Index = i;
                    // 刷新标签
                    marker.UpdateLabel();
                }
            }
            
            _logger.Debug("已更新所有坐标点索引（仅计算坐标类型按键的序号）");
        }
        catch (Exception ex)
        {
            _logger.Error("更新坐标点索引时发生异常", ex);
        }
    }

    // 添加页面焦点变化处理，用于清除所有确认状态
    private void KeyMappingView_LostFocus(object sender, RoutedEventArgs e)
    {
        ClearAllDeleteConfirmStates();
    }

    private void KeyInputBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox) return;

        e.Handled = true;

        // 处理IME输入
        if (e.Key == Key.ImeProcessed && e.SystemKey == Key.None)
        {
            ShowError(KEY_ERROR);
            return;
        }

        // 获取实际按键，优先使用SystemKey
        var key = e.SystemKey != Key.None ? e.SystemKey :
            e.Key == Key.ImeProcessed ? e.SystemKey : e.Key;

        // 过滤无效按键，但允许系统功能键
        if (key == Key.None) return;

        // 转换并检查按键
        if (TryConvertToLyKeysCode(key, out var lyKeysCode))
        {
            // 检查是否与热键冲突
            if (ViewModel.IsHotkeyConflict(lyKeysCode))
            {
                ShowError(HOTKEY_CONFLICT);
                return;
            }

            ViewModel?.SetCurrentKey(lyKeysCode);
            _logger.Debug($"按键已转换: {key} -> {lyKeysCode}");

            // 显示成功提示
            ShowMessage($"已选择按键: {ViewModel?.CurrentKeyText}");

            // 强制清除焦点
            var focusScope = FocusManager.GetFocusScope(textBox);
            FocusManager.SetFocusedElement(focusScope, null);
            Keyboard.ClearFocus();

            // 确保输入框失去焦点
            if (textBox.IsFocused)
            {
                var parent = textBox.Parent as UIElement;
                if (parent != null) parent.Focus();
            }
        }
        else
        {
            ShowError(KEY_ERROR);
            _logger.Warning($"无法转换按键: {key}");
        }
    }

    // 将WPF的Key映射到LyKeysCode
    private bool TryConvertToLyKeysCode(Key key, out LyKeysCode lyKeysCode)
    {
        // 将WPF的Key映射到LyKeysCode
        lyKeysCode = key switch
        {
            // 字母键
            Key.A => LyKeysCode.VK_A,
            Key.B => LyKeysCode.VK_B,
            Key.C => LyKeysCode.VK_C,
            Key.D => LyKeysCode.VK_D,
            Key.E => LyKeysCode.VK_E,
            Key.F => LyKeysCode.VK_F,
            Key.G => LyKeysCode.VK_G,
            Key.H => LyKeysCode.VK_H,
            Key.I => LyKeysCode.VK_I,
            Key.J => LyKeysCode.VK_J,
            Key.K => LyKeysCode.VK_K,
            Key.L => LyKeysCode.VK_L,
            Key.M => LyKeysCode.VK_M,
            Key.N => LyKeysCode.VK_N,
            Key.O => LyKeysCode.VK_O,
            Key.P => LyKeysCode.VK_P,
            Key.Q => LyKeysCode.VK_Q,
            Key.R => LyKeysCode.VK_R,
            Key.S => LyKeysCode.VK_S,
            Key.T => LyKeysCode.VK_T,
            Key.U => LyKeysCode.VK_U,
            Key.V => LyKeysCode.VK_V,
            Key.W => LyKeysCode.VK_W,
            Key.X => LyKeysCode.VK_X,
            Key.Y => LyKeysCode.VK_Y,
            Key.Z => LyKeysCode.VK_Z,

            // 数字键
            Key.D0 => LyKeysCode.VK_0,
            Key.D1 => LyKeysCode.VK_1,
            Key.D2 => LyKeysCode.VK_2,
            Key.D3 => LyKeysCode.VK_3,
            Key.D4 => LyKeysCode.VK_4,
            Key.D5 => LyKeysCode.VK_5,
            Key.D6 => LyKeysCode.VK_6,
            Key.D7 => LyKeysCode.VK_7,
            Key.D8 => LyKeysCode.VK_8,
            Key.D9 => LyKeysCode.VK_9,

            // 功能键
            Key.F1 => LyKeysCode.VK_F1,
            Key.F2 => LyKeysCode.VK_F2,
            Key.F3 => LyKeysCode.VK_F3,
            Key.F4 => LyKeysCode.VK_F4,
            Key.F5 => LyKeysCode.VK_F5,
            Key.F6 => LyKeysCode.VK_F6,
            Key.F7 => LyKeysCode.VK_F7,
            Key.F8 => LyKeysCode.VK_F8,
            Key.F9 => LyKeysCode.VK_F9,
            Key.F10 => LyKeysCode.VK_F10,
            Key.F11 => LyKeysCode.VK_F11,
            Key.F12 => LyKeysCode.VK_F12,

            // 特殊键
            Key.Escape => LyKeysCode.VK_ESCAPE,
            Key.Tab => LyKeysCode.VK_TAB,
            Key.CapsLock => LyKeysCode.VK_CAPITAL,
            Key.LeftShift => LyKeysCode.VK_LSHIFT,
            Key.RightShift => LyKeysCode.VK_RSHIFT,
            Key.LeftCtrl => LyKeysCode.VK_LCONTROL,
            Key.RightCtrl => LyKeysCode.VK_RCONTROL,
            Key.LeftAlt => LyKeysCode.VK_LMENU,
            Key.RightAlt => LyKeysCode.VK_RMENU,
            Key.Space => LyKeysCode.VK_SPACE,
            Key.Enter => LyKeysCode.VK_RETURN,
            Key.Back => LyKeysCode.VK_BACK,

            // 符号键
            Key.OemTilde => LyKeysCode.VK_OEM_3,
            Key.OemMinus => LyKeysCode.VK_OEM_MINUS,
            Key.OemPlus => LyKeysCode.VK_OEM_PLUS,
            Key.OemOpenBrackets => LyKeysCode.VK_OEM_4,
            Key.OemCloseBrackets => LyKeysCode.VK_OEM_6,
            Key.OemSemicolon => LyKeysCode.VK_OEM_1,
            Key.OemQuotes => LyKeysCode.VK_OEM_7,
            Key.OemComma => LyKeysCode.VK_OEM_COMMA,
            Key.OemPeriod => LyKeysCode.VK_OEM_PERIOD,
            Key.OemQuestion => LyKeysCode.VK_OEM_2,
            Key.OemBackslash => LyKeysCode.VK_OEM_5,

            // 添加方向键映射
            Key.Up => LyKeysCode.VK_UP,
            Key.Down => LyKeysCode.VK_DOWN,
            Key.Left => LyKeysCode.VK_LEFT,
            Key.Right => LyKeysCode.VK_RIGHT,

            _ => LyKeysCode.VK_ESCAPE
        };

        // 修改返回逻辑，添加方向键判断
        return key == Key.Escape ||
               (key >= Key.Left && key <= Key.Down) || // 方向键
               lyKeysCode != LyKeysCode.VK_ESCAPE;
    }

    // 处理按键输入框获得焦点
    private void KeyInputBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
            if (_hotkeyService != null)
                _hotkeyService.IsInputFocused = true;
    }

    // 处理按键输入框失去焦点
    private void KeyInputBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
            if (_hotkeyService != null)
                _hotkeyService.IsInputFocused = false;
    }

    // 处理超链接请求导航
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
            { UseShellExecute = true });
        e.Handled = true;
    }

    // 显示错误信息到状态栏
    private void ShowError(string message)
    {
        ShowMessage(message, true);
    }

    // 显示提示信息到状态栏（通用方法）
    private void ShowMessage(string message, bool isError = false)
    {
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
            mainViewModel.UpdateStatusMessage(message, isError);
    }

    // 处理热键输入框获得焦点
    private void HotkeyInputBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
            if (_hotkeyService != null)
            {
                _hotkeyService.IsInputFocused = true;
                _logger.Debug($"输入框获得焦点: {textBox.Name}");
            }
    }

    // 处理热键输入框失去焦点
    private void HotkeyInputBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
            if (_hotkeyService != null)
            {
                _hotkeyService.IsInputFocused = false;
                _logger.Debug($"输入框失去焦点: {textBox.Name}");
            }
    }

    // 处理鼠标按键
    private void HotkeyInputBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            var keyCode = e.ChangedButton switch
            {
                MouseButton.Middle => LyKeysCode.VK_MBUTTON,
                MouseButton.XButton1 => LyKeysCode.VK_XBUTTON1,
                MouseButton.XButton2 => LyKeysCode.VK_XBUTTON2,
                _ => LyKeysCode.VK_ESCAPE
            };

            if (keyCode != LyKeysCode.VK_ESCAPE)
            {
                ViewModel.SetCurrentKey(keyCode);
                e.Handled = true;

                // 显示成功提示
                if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                    mainViewModel.UpdateStatusMessage($"已选择按键: {ViewModel?.CurrentKeyText}", false);
            }
        }
    }

    // 统一的热键处理方法
    private void HandleHotkeyInput(System.Windows.Controls.TextBox textBox, LyKeysCode keyCode, ModifierKeys modifiers,
        bool isStartHotkey)
    {
        if (textBox == null)
        {
            _logger.Warning("HandleHotkeyInput: textBox is null");
            return;
        }

        // 只过滤修饰键
        if (IsModifierKey(keyCode)) return;

        // 记录热键输入处理
        _logger.Debug($"处理热键输入 - keyCode: {keyCode}, 修饰键: {modifiers}");

        try
        {
            // 检查热键是否与按键列表冲突
            if (ViewModel?.IsHotkeyConflict(keyCode) == true)
            {
                ShowError("热键与按键序列冲突，请选择其他键");
                _logger.Warning($"热键({keyCode})与当前按键序列冲突，无法设置");
                return;
            }

            // 使用统一的热键设置方法
            bool success = ViewModel?.SetHotkey(keyCode, modifiers) ?? false;
            
            // 根据设置结果决定是否显示成功消息（SetHotkey内部已经设置了状态消息，这里不需要再次设置）
            if (!success)
            {
                _logger.Debug($"热键({keyCode})设置失败");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"设置热键时发生异常: {ex.Message}", ex);
            ShowError($"设置热键失败: {ex.Message}");
            return;
        }
        
        // 添加失焦处理，与按键输入保持一致的行为
        // 强制清除焦点
        var focusScope = FocusManager.GetFocusScope(textBox);
        FocusManager.SetFocusedElement(focusScope, null);
        Keyboard.ClearFocus();

        // 确保输入框失去焦点
        if (textBox.IsFocused)
        {
            var parent = textBox.Parent as UIElement;
            if (parent != null) parent.Focus();
        }
        
        // 记录日志
        _logger.Debug("热键设置后已自动清除焦点");
    }

    // 判断是否为修饰键
    private bool IsModifierKey(LyKeysCode keyCode)
    {
        return keyCode == LyKeysCode.VK_LCONTROL
               || keyCode == LyKeysCode.VK_RCONTROL
               || keyCode == LyKeysCode.VK_LMENU
               || keyCode == LyKeysCode.VK_RMENU
               || keyCode == LyKeysCode.VK_LSHIFT
               || keyCode == LyKeysCode.VK_RSHIFT;
    }

    // 处理开始热键
    private void StartHotkeyInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        _logger.Debug("热键 Keyboard 按下 已触发");
        _logger.Debug($"Key: {e.Key}, SystemKey: {e.SystemKey}, KeyStates: {e.KeyStates}");
        StartHotkeyInput_PreviewKeyDown(sender, e);
    }

    private void StartHotkeyInput_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _logger.Debug("热键 Mouse 按下 已触发");
        _logger.Debug($"ChangedButton: {e.ChangedButton}");
        StartHotkeyInput_PreviewMouseDown(sender, e);
    }

    // 添加滚轮事件处理
    private void StartHotkeyInput_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            e.Handled = true;
            var keyCode = e.Delta > 0 ? LyKeysCode.VK_WHEELUP : LyKeysCode.VK_WHEELDOWN;
            _logger.Debug($"检测到滚轮事件: {keyCode}, Delta: {e.Delta}");
            HandleHotkeyInput(textBox, keyCode, Keyboard.Modifiers, true);
        }
    }

    // 处理热键的鼠标释放
    private void StartHotkeyInput_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _logger.Debug("热键 Mouse 释放 已触发");
        _logger.Debug($"ChangedButton: {e.ChangedButton}");
    }

    private void StartHotkeyInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox) return;

        try
        {
            e.Handled = true;

            if (e.Key == Key.ImeProcessed && e.SystemKey == Key.None)
            {
                ShowError(KEY_ERROR);
                return;
            }

            // 获取实际按键，优先使用SystemKey
            var key = e.SystemKey != Key.None ? e.SystemKey :
                e.Key == Key.ImeProcessed ? e.SystemKey : e.Key;

            if (key == Key.None) return;

            if (TryConvertToLyKeysCode(key, out var lyKeysCode))
            {
                // 只调用HandleHotkeyInput，由它处理是否显示成功消息
                HandleHotkeyInput(textBox, lyKeysCode, Keyboard.Modifiers, true);
                _logger.Debug($"热键已转换: {key} -> {lyKeysCode}");
            }
            else
            {
                ShowError(KEY_ERROR);
                _logger.Warning($"无法转换热键: {key}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("StartHotkeyInput_PreviewKeyDown 处理异常", ex);
        }
    }

    // 处理热键的鼠标点击
    private void StartHotkeyInput_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            LyKeysCode? keyCode = e.ChangedButton switch
            {
                MouseButton.Middle => LyKeysCode.VK_MBUTTON,
                MouseButton.XButton1 => LyKeysCode.VK_XBUTTON1,
                MouseButton.XButton2 => LyKeysCode.VK_XBUTTON2,
                _ => null // 对于左键和右键不处理，让输入框正常获取焦点以接收键盘输入
            };

            if (keyCode.HasValue)
            {
                _logger.Debug($"检测到 Mouse 按键点击: {keyCode.Value}");
                HandleHotkeyInput(textBox, keyCode.Value, Keyboard.Modifiers, true);
                e.Handled = true; // 阻止事件继续传播

                // 显示成功提示
                if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                    mainViewModel.UpdateStatusMessage($"已选择按键: {ViewModel?.CurrentKeyText}", false);
            }
        }
    }

    private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
    {
        // 只验证输入是否为数字，允许输入任何整数
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private void NumberInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            // 处理焦点状态
            if (_hotkeyService != null)
            {
                _hotkeyService.IsInputFocused = false;
                _logger.Debug("数字输入框失去焦点");
            }

            // 对于坐标输入框，允许为空
            if (textBox.Name == "XCoordinateInputBox" || textBox.Name == "YCoordinateInputBox")
            {
                // 对坐标输入框，允许为空，不设置默认值
                _logger.Debug($"坐标输入框 {textBox.Name} 失去焦点，当前值为: {textBox.Text}");
                
                // 确保绑定更新
                textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                
                // 当ViewModel中的CurrentX和CurrentY都有值时，更新坐标列表到执行层
                if (ViewModel?.CurrentX.HasValue == true && ViewModel?.CurrentY.HasValue == true)
                {
                    // 添加延迟执行，确保绑定已完成
                    Dispatcher.BeginInvoke(new Action(() => {
                        // 查找是否有选中的坐标项需要更新
                        if (ViewModel.SelectedKeyItem?.Type == KeyItemType.Coordinates)
                        {
                            // 更新选中的坐标项
                            ViewModel.SelectedKeyItem.X = ViewModel.CurrentX.Value;
                            ViewModel.SelectedKeyItem.Y = ViewModel.CurrentY.Value;
                            
                            // 保存配置并更新执行层
                            ViewModel.SaveConfig();
                            ViewModel.SyncKeyListToHotkeyService();
                            
                            _logger.Debug($"通过输入框更新坐标至 ({ViewModel.CurrentX.Value}, {ViewModel.CurrentY.Value}) 并同步到执行层");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                
                return;
            }

            // 处理其他数字输入框的空值情况，设置默认值
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "5"; // 设置默认值为5
                _logger.Debug("输入框为空，设置默认值5");
            }

            // 验证并纠正值
            if (int.TryParse(textBox.Text, out var value))
            {
                if (value <= 0)
                {
                    // 处理无效值（小于等于0），自动设置为1
                    _logger.Debug($"按键间隔值 {value} 无效，自动设置为1ms");
                    value = 1; // 自动设置为1

                    // 区分是默认间隔输入框还是按键列表中的间隔输入框
                    if (textBox.Name == "txtKeyInterval") // 默认间隔输入框
                    {
                        if (ViewModel != null)
                        {
                            // 更新为1
                            ViewModel.KeyInterval = value;
                            textBox.Text = value.ToString();

                            if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel
                                mainViewModel) mainViewModel.UpdateStatusMessage("按键间隔必须大于0毫秒，已自动设置为1ms", true);
                        }
                    }
                    else // 按键列表中的间隔输入框
                    {
                        // 获取绑定的KeyItem对象
                        if (textBox.DataContext is KeyItem keyItem)
                        {
                            // 更新为1
                            keyItem.KeyInterval = value;
                            textBox.Text = value.ToString();

                            if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel
                                mainViewModel) mainViewModel.UpdateStatusMessage("按键间隔必须大于0毫秒，已自动设置为1ms", true);
                        }
                    }
                }
                else
                {
                    // 值有效，直接更新到ViewModel，不显示自动调整提示
                    // 区分是默认间隔输入框还是按键列表中的间隔输入框
                    if (textBox.Name == "txtKeyInterval") // 默认间隔输入框
                        if (ViewModel != null)
                        {
                            ViewModel.KeyInterval = value;
                            _logger.Debug($"更新默认间隔值为: {value}ms");
                        }
                    // 按键列表中的间隔输入框由数据绑定自动更新，不需要额外处理
                }
            }
            else
            {
                // 输入的不是有效数字，但由于我们已经在PreviewTextInput事件中验证，
                // 理论上不应该出现这种情况，除非是空字符串，但我们已经在前面处理了
                _logger.Debug("输入的不是有效数字");

                // 区分是默认间隔输入框还是按键列表中的间隔输入框
                if (textBox.Name == "txtKeyInterval") // 默认间隔输入框
                {
                    if (ViewModel != null)
                    {
                        textBox.Text = ViewModel.KeyInterval.ToString(); // 恢复为当前值
                        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel
                            mainViewModel) mainViewModel.UpdateStatusMessage("请输入有效的数字", true);
                    }
                }
                else // 按键列表中的间隔输入框
                {
                    // 获取绑定的KeyItem对象
                    if (textBox.DataContext is KeyItem keyItem)
                    {
                        textBox.Text = keyItem.KeyInterval.ToString(); // 恢复为当前值
                        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel
                            mainViewModel) mainViewModel.UpdateStatusMessage("请输入有效的数字", true);
                    }
                }
            }
        }
    }

    // 处理开始热键
    private void HandleStartHotkey(bool isKeyDown)
    {
        if (ViewModel == null)
        {
            _logger.Warning("ViewModel is null");
            return;
        }

        try
        {
            // 如果输入框有焦点，则不处理热键
            if (_hotkeyService != null && _hotkeyService.IsInputFocused)
            {
                _logger.Debug("输入框有焦点，忽略热键触发");
                return;
            }

            // 检查热键总开关是否开启
            if (!ViewModel.IsHotkeyControlEnabled)
            {
                _logger.Debug("热键总开关已关闭，忽略热键触发");
                return;
            }

            if (ViewModel.SelectedKeyMode == 0) // 顺序模式
            {
                _logger.Debug($"顺序模式 - 按键{(isKeyDown ? "按下" : "释放")}");
                if (isKeyDown) // 按下时启动
                    ViewModel.StartKeyMapping();
            }
            else // 按压模式
            {
                _logger.Debug($"按压模式 - 按键{(isKeyDown ? "按下" : "释放")}");
                if (isKeyDown)
                {
                    ViewModel.StartKeyMapping();
                    ViewModel.SetHoldMode(true);
                }
                else
                {
                    ViewModel.SetHoldMode(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("处理开始热键异常", ex);
        }
    }

    private void HandleStopHotkey()
    {
        try
        {
            if (ViewModel == null)
            {
                _logger.Warning("ViewModel is null");
                return;
            }

            // 如果输入框有焦点，则不处理热键
            if (_hotkeyService != null && _hotkeyService.IsInputFocused)
            {
                _logger.Debug("输入框有焦点，忽略热键触发");
                return;
            }

            // 检查热键总开关是否开启
            if (!ViewModel.IsHotkeyControlEnabled)
            {
                _logger.Debug("热键总开关已关闭，忽略热键触发");
                return;
            }

            _logger.Debug("处理停止热键");
            ViewModel.StopKeyMapping();
        }
        catch (Exception ex)
        {
            _logger.Error("处理理停止热键异常", ex);
        }
    }

    // 添加数字输入框焦点事件处理
    private void NumberInput_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_hotkeyService != null)
        {
            _hotkeyService.IsInputFocused = true;
            _logger.Debug("数字输入框获得焦点");
        }
    }

    private void KeysList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox listBox)
        {
            // 检查点击是否在ScrollBar上
            var scrollBar =
                FindParent<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource as DependencyObject);
            if (scrollBar != null)
                // 如果点击在滚动条上，不处理事件
                return;

            // 检查点击是否在ListBox的空白区域
            var hitTest = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
            if (hitTest == null ||
                (hitTest.VisualHit != null &&
                 FindParent<ListBoxItem>(hitTest.VisualHit as DependencyObject) == null))
            {
                // 点击在ListBox的空白区域，清除选中项和高亮
                ClearListBoxSelection(listBox);
                e.Handled = true;
            }
        }
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        if (child == null) return null;

        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && !(parent is T)) parent = VisualTreeHelper.GetParent(parent);

        return parent as T;
    }

    private void Page_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 检查点击是否在ListBox区域内
        if (e.OriginalSource is DependencyObject depObj)
        {
            var listBox = FindParent<System.Windows.Controls.ListBox>(depObj);
            if (listBox == null) // 点击在ListBox外部
            {
                // 查找页面中的ListBox
                var pageListBox = FindChild<System.Windows.Controls.ListBox>((sender as Page)!);
                if (pageListBox != null) ClearListBoxSelection(pageListBox);
            }
        }
    }

    // 添加一个通用的清除方法
    private void ClearListBoxSelection(System.Windows.Controls.ListBox listBox)
    {
        listBox.SelectedItem = null;

        // 清除所有项的拖拽高亮显示
        foreach (var item in listBox.Items)
            if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem)
                DragDropProperties.SetIsDragTarget(listBoxItem, false);
    }

    // 添加FindChild辅助方法
    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found)
                return found;

            var result = FindChild<T>(child);
            if (result != null)
                return result;
        }

        return null;
    }

    private void IntervalHelp_Click(object sender, RoutedEventArgs e)
    {
        // 使用FindName获取Popup控件引用
        var helpPopup = FindName("helpPopup") as Popup;

        // 切换帮助浮窗的显示状态
        if (helpPopup != null) helpPopup.IsOpen = !helpPopup.IsOpen;
    }

    private void ModeHelp_Click(object sender, RoutedEventArgs e)
    {
        // 使用FindName获取Popup控件引用
        var modeHelpPopup = FindName("modeHelpPopup") as Popup;

        // 切换帮助浮窗的显示状态
        if (modeHelpPopup != null) modeHelpPopup.IsOpen = !modeHelpPopup.IsOpen;
    }

    private void VolumeHelp_Click(object sender, RoutedEventArgs e)
    {
        // 使用FindName获取Popup控件引用
        var volumeHelpPopup = FindName("volumeHelpPopup") as Popup;

        // 切换音量帮助浮窗的显示状态
        if (volumeHelpPopup != null) volumeHelpPopup.IsOpen = !volumeHelpPopup.IsOpen;
    }

    private void KeyInputBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox) return;

        // 如果输入框没有焦点，优先获取焦点
        if (!textBox.IsFocused)
        {
            textBox.Focus();
            e.Handled = true;
            return;
        }

        // 已有焦点时，处理所有鼠标按键
        LyKeysCode? keyCode = e.ChangedButton switch
        {
            MouseButton.Left => LyKeysCode.VK_LBUTTON,
            MouseButton.Right => LyKeysCode.VK_RBUTTON,
            MouseButton.Middle => LyKeysCode.VK_MBUTTON,
            MouseButton.XButton1 => LyKeysCode.VK_XBUTTON1,
            MouseButton.XButton2 => LyKeysCode.VK_XBUTTON2,
            _ => null
        };

        if (keyCode.HasValue)
        {
            e.Handled = true;

            // 检查是否与热键冲突
            if (ViewModel.IsHotkeyConflict(keyCode.Value))
            {
                ShowError(HOTKEY_CONFLICT);
                return;
            }

            ViewModel?.SetCurrentKey(keyCode.Value);
            _logger.Debug($"鼠标按键已转换: {e.ChangedButton} -> {keyCode.Value}");

            // 显示成功提示
            ShowMessage($"已选择按键: {ViewModel?.CurrentKeyText}");

            // 强制清除焦点
            var focusScope = FocusManager.GetFocusScope(textBox);
            FocusManager.SetFocusedElement(focusScope, null);
            Keyboard.ClearFocus();

            // 确保输入框失去焦点
            if (textBox.IsFocused)
            {
                var parent = textBox.Parent as UIElement;
                if (parent != null) parent.Focus();
            }
        }
    }

    private void GetWindowHandle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 创建一个新的窗口来显示所有可见窗口的句柄
            var windowHandleDialog = new WindowHandleDialog();
            windowHandleDialog.Owner = Window.GetWindow(this);
            windowHandleDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (windowHandleDialog.ShowDialog() == true)
            {
                // 获取选中的窗口信息
                var handle = windowHandleDialog.SelectedHandle;
                var title = windowHandleDialog.SelectedTitle;
                var className = windowHandleDialog.SelectedClassName;
                var processName = windowHandleDialog.SelectedProcessName;

                // 更新ViewModel中的窗口信息
                ViewModel.UpdateSelectedWindow(handle, title, className, processName);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("获取窗口句柄时发生异常", ex);
            ShowError("获取窗口句柄失败，请查看日志");
        }
    }

    private void ClearWindowHandle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
            return;

        try
        {
            // 获取按钮是否已处于确认状态
            var isConfirmState = (bool)button.GetValue(DeleteConfirmStateProperty);

            if (isConfirmState)
            {
                // 已经是确认状态，执行清除窗口句柄操作
                try
                {
                    // 停止并移除定时器
                    if (_pendingDeleteButtons.TryGetValue(button, out var timer))
                    {
                        timer.Stop();
                        _pendingDeleteButtons.Remove(button);
                    }

                    // 重置按钮状态
                    button.SetValue(DeleteConfirmStateProperty, false);

                    // 清除窗口句柄
                    ViewModel.ClearSelectedWindow();
                    _logger.Debug("已清除窗口句柄");
                    
                    // 恢复按钮为原始状态
                    ResetDeleteButton(button);
                }
                catch (Exception ex)
                {
                    _logger.Error("清除窗口句柄时发生异常", ex);
                    ShowError("清除窗口句柄失败，请查看日志");

                    // 恢复按钮原始状态
                    ResetDeleteButton(button);
                }
            }
            else
            {
                // 清除其他所有按钮的确认状态
                ClearAllDeleteConfirmStates();

                // 将按钮设置为确认状态
                button.SetValue(DeleteConfirmStateProperty, true);
                ConvertToConfirmButton(button);

                // 创建3秒定时器
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };

                timer.Tick += (s, args) =>
                {
                    // 3秒后恢复按钮原始状态
                    timer.Stop();
                    if (_pendingDeleteButtons.ContainsKey(button)) _pendingDeleteButtons.Remove(button);
                    button.SetValue(DeleteConfirmStateProperty, false);
                    ResetDeleteButton(button);
                };

                // 添加到字典并启动定时器
                if (_pendingDeleteButtons.ContainsKey(button)) _pendingDeleteButtons[button].Stop();
                _pendingDeleteButtons[button] = timer;
                timer.Start();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("处理清除窗口句柄按钮点击事件时发生异常", ex);
            // 确保按钮恢复原状
            if (sender is System.Windows.Controls.Button btn)
                ResetDeleteButton(btn);
        }
    }

    /// <summary>
    /// 将按钮转换为确认删除状态（红色X）
    /// </summary>
    private void ConvertToConfirmButton(System.Windows.Controls.Button button)
    {
        if (button == null) return;

        try
        {
            // 查找按钮内的Path元素
            if (button.Content is Path path)
            {
                // 保存原始图标数据
                button.SetValue(DeleteConfirmStateProperty, true);

                // 记录原始Path数据，用于恢复
                path.Tag = path.Data;

                // 设置为X图标
                path.Data = Geometry.Parse(
                    "M12,2C17.53,2 22,6.47 22,12C22,17.53 17.53,22 12,22C6.47,22 2,17.53 2,12C2,6.47 6.47,2 12,2M15.59,7L12,10.59L8.41,7L7,8.41L10.59,12L7,15.59L8.41,17L12,13.41L15.59,17L17,15.59L13.41,12L17,8.41L15.59,7Z");

                // 设置为红色
                path.Fill = new SolidColorBrush(Colors.Red);

                // 修改按钮背景为淡红色，提供更明显的视觉提示
                button.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 255, 0, 0));
                button.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 0, 0));

                // 添加动画效果增强用户体验
                var animation = new System.Windows.Media.Animation.ColorAnimation
                {
                    To = System.Windows.Media.Color.FromArgb(40, 255, 0, 0),
                    Duration = TimeSpan.FromMilliseconds(200),
                    AutoReverse = true,
                    RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior(3)
                };

                var brush = button.Background as SolidColorBrush;
                if (brush != null) brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);

                // 更改按钮提示为确认删除
                if (button.ToolTip is System.Windows.Controls.ToolTip toolTip && toolTip.Content is TextBlock textBlock)
                    textBlock.Text = "点击确认删除";
            }
        }
        catch (Exception ex)
        {
            _logger.Error("转换删除按钮到确认状态时发生异常", ex);
        }
    }

    /// <summary>
    /// 重置按钮到原始状态
    /// </summary>
    private void ResetDeleteButton(System.Windows.Controls.Button button)
    {
        if (button == null) return;

        try
        {
            // 清除确认状态标记
            button.SetValue(DeleteConfirmStateProperty, false);

            // 查找按钮内的Path元素
            if (button.Content is Path path)
            {
                // 如果有保存的原始数据，则恢复
                if (path.Tag is Geometry originalGeometry)
                {
                    // 恢复原始图标数据
                    path.Data = originalGeometry;
                    path.Tag = null;
                }

                // 恢复原始颜色
                path.Fill = new SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666666"));

                // 恢复原始背景
                button.Background = new SolidColorBrush(Colors.Transparent);
                button.BorderBrush = new SolidColorBrush(Colors.Transparent);

                // 恢复原始提示文本
                if (button.ToolTip is System.Windows.Controls.ToolTip toolTip && toolTip.Content is TextBlock textBlock)
                    textBlock.Text = "删除此按键";
            }

            // 确保移除定时器
            if (_pendingDeleteButtons.TryGetValue(button, out var timer))
            {
                timer.Stop();
                _pendingDeleteButtons.Remove(button);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("重置删除按钮到原始状态时发生异常", ex);
        }
    }

    /// <summary>
    /// 清除所有按钮的确认删除状态
    /// </summary>
    private void ClearAllDeleteConfirmStates()
    {
        try
        {
            // 创建一个临时列表存储所有按钮，避免在遍历过程中修改集合
            var buttonsToReset = new List<System.Windows.Controls.Button>(_pendingDeleteButtons.Keys);

            foreach (var button in buttonsToReset) ResetDeleteButton(button);

            // 清空字典
            _pendingDeleteButtons.Clear();
        }
        catch (Exception ex)
        {
            _logger.Error("清除所有删除确认状态时发生异常", ex);
        }
    }

    /// <summary>
    /// 音量设置按钮点击事件处理
    /// </summary>
    private void SoundSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (soundSettingsPopup == null)
            {
                _logger.Warning("音量设置弹出窗口未初始化");
                return;
            }

            // 切换弹出窗口的显示状态
            soundSettingsPopup.IsOpen = !soundSettingsPopup.IsOpen;
            _logger.Debug($"音量设置弹出窗口状态: {soundSettingsPopup.IsOpen}");

            // 如果弹出窗口打开，设置焦点到音量滑块
            if (soundSettingsPopup.IsOpen && volumeSlider != null) volumeSlider.Focus();
        }
        catch (Exception ex)
        {
            _logger.Error("处理音量设置按钮点击事件时发生异常", ex);
        }
    }

    /// <summary>
    /// 删除按键按钮点击事件处理
    /// </summary>
    private void DeleteKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
            return;

        try
        {
            // 获取按钮是否已处于确认状态
            var isConfirmState = (bool)button.GetValue(DeleteConfirmStateProperty);
            var keyItem = button.Tag as KeyItem;

            // 检查KeyItem是否有效
            if (keyItem == null)
            {
                _logger.Error("按钮Tag不是KeyItem类型或为空");
                return;
            }

            if (isConfirmState)
            {
                // 已经是确认状态，执行删除操作
                try
                {
                    // 停止并移除定时器
                    if (_pendingDeleteButtons.TryGetValue(button, out var timer))
                    {
                        timer.Stop();
                        _pendingDeleteButtons.Remove(button);
                    }

                    // 重置按钮状态
                    button.SetValue(DeleteConfirmStateProperty, false);

                    // 删除按键
                    ViewModel.DeleteKey(keyItem);
                    _logger.Debug($"已删除按键: {keyItem.DisplayName}");
                }
                catch (Exception ex)
                {
                    _logger.Error("删除按键时发生异常", ex);
                    System.Windows.MessageBox.Show($"删除按键失败: {ex.Message}", "错误", MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    // 恢复按钮原始状态
                    ResetDeleteButton(button);
                }
            }
            else
            {
                // 清除其他所有按钮的确认状态
                ClearAllDeleteConfirmStates();

                // 将按钮设置为确认状态
                button.SetValue(DeleteConfirmStateProperty, true);
                ConvertToConfirmButton(button);

                // 创建3秒定时器
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };

                timer.Tick += (s, args) =>
                {
                    // 3秒后恢复按钮原始状态
                    timer.Stop();
                    if (_pendingDeleteButtons.ContainsKey(button)) _pendingDeleteButtons.Remove(button);
                    button.SetValue(DeleteConfirmStateProperty, false);
                    ResetDeleteButton(button);
                };

                // 添加到字典并启动定时器
                if (_pendingDeleteButtons.ContainsKey(button)) _pendingDeleteButtons[button].Stop();
                _pendingDeleteButtons[button] = timer;
                timer.Start();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("处理删除按钮点击事件时发生异常", ex);
            // 确保按钮恢复原状
            ResetDeleteButton(button);
        }
    }

    /// <summary>
    /// 添加按键后清空输入框
    /// </summary>
    private void AddKey_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (KeyInputBox != null)
            {
                // 等待绑定的Command执行完成，使用Dispatcher延迟执行
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 清空输入框显示内容
                    KeyInputBox.Clear();
                    
                    // 通知用户界面已更新
                    KeyInputBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                    
                    // 确保ViewModel中的值也被清空（双重保险）
                    if (ViewModel != null && ViewModel.GetType().GetProperty("CurrentKey") != null)
                    {
                        // 通过反射清空CurrentKey，因为它可能是private
                        ViewModel.GetType().GetProperty("CurrentKey")?.SetValue(ViewModel, null);
                    }
                    
                    // 记录日志
                    _logger.Debug("添加按键后已清空输入框和ViewModel属性");
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("清空按键输入框时发生异常", ex);
        }
    }

    /// <summary>
    /// 添加坐标后清空坐标输入框
    /// </summary>
    private void AddCoordinate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            bool wasInEditMode = _isEditMode;
            
            // 等待绑定的Command执行完成，使用Dispatcher延迟执行
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 清空X坐标输入框
                if (XCoordinateInputBox != null)
                {
                    XCoordinateInputBox.Clear();
                    // 强制更新绑定
                    XCoordinateInputBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                }
                
                // 清空Y坐标输入框
                if (YCoordinateInputBox != null)
                {
                    YCoordinateInputBox.Clear();
                    // 强制更新绑定
                    YCoordinateInputBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                }
                
                // 确保ViewModel中的值也被清空（双重保险）
                if (ViewModel != null)
                {
                    ViewModel.CurrentX = null;
                    ViewModel.CurrentY = null;
                }
                
                // 记录日志
                _logger.Debug("添加坐标后已清空坐标输入框和ViewModel属性");
                
                // 在编辑模式下，确保新添加的坐标点也能显示出来
                if (wasInEditMode && _isEditMode)
                {
                    _logger.Debug("编辑模式下添加坐标后刷新显示");
                    ShowAllCoordinateMarkers();
                }
                
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            _logger.Error("清空坐标输入框时发生异常", ex);
        }
    }

    /// <summary>
    /// 坐标模式与按键模式切换
    /// </summary>
    private void ToggleCoordinateMode_Click(object sender, RoutedEventArgs e)
    {
        // 切换输入模式
        bool isCoordinateMode = CoordinateInputArea.Visibility == Visibility.Visible;
        
        // 切换视图可见性 - 现在直接切换两个面板的可见性
        KeyInputArea.Visibility = isCoordinateMode ? Visibility.Visible : Visibility.Collapsed;
        CoordinateInputArea.Visibility = isCoordinateMode ? Visibility.Collapsed : Visibility.Visible;
        
        // 输出日志记录当前切换的模式
        _logger.Debug($"输入模式已切换为: {(isCoordinateMode ? "按键模式" : "坐标模式")}");
        
        // 提示信息不需要更新，因为每个模式下有自己的按钮，按钮在XAML中已经设置了固定的提示
    }

    // 初始化鼠标拖拽事件
    private void InitMouseDragEvents()
    {
        try
        {
            if (btnMouseIcon != null)
            {
                // 注册鼠标按钮相关事件
                btnMouseIcon.PreviewMouseDown += MouseIcon_PreviewMouseDown;
                btnMouseIcon.PreviewMouseMove += MouseIcon_PreviewMouseMove;
                btnMouseIcon.PreviewMouseUp += MouseIcon_PreviewMouseUp;
                
                _logger.Debug("鼠标坐标按钮事件已初始化");
            }
            else
            {
                _logger.Warning("鼠标坐标按钮未找到，无法初始化拖拽事件");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("初始化鼠标拖拽事件失败", ex);
        }
    }
    
    // 切换坐标点显示状态
    private void ToggleCoordinateMarkers()
    {
        try
        {
            // 如果正在编辑模式，不做任何操作（编辑模式下由ToggleEditMode控制坐标标记）
            if (_isEditMode)
                return;
                
            // 切换坐标标记的可见性状态
            _isCoordinateMarkersVisible = !_isCoordinateMarkersVisible;
            
            if (_isCoordinateMarkersVisible)
            {
                _logger.Debug("显示所有坐标标记");
                ShowAllCoordinateMarkers();
            }
            else
            {
                _logger.Debug("隐藏所有坐标标记");
                HideAllCoordinateMarkers();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("切换坐标标记可见性时出错", ex);
        }
    }
    
    // 显示所有坐标点
    private void ShowAllCoordinateMarkers()
    {
        try
        {
            // 清理旧的坐标点
            ClearAllCoordinateMarkers();
            
            // 获取KeyList
            var keyList = ViewModel?.KeyList;
            if (keyList == null || keyList.Count == 0)
            {
                _logger.Debug("没有可显示的坐标点");
                return;
            }
            
            // 筛选出所有坐标类型的按键
            var coordinateItems = keyList.Where(item => item.Type == KeyItemType.Coordinates).ToList();
            
            // 创建并显示所有坐标点
            for (int i = 0; i < coordinateItems.Count; i++)
            {
                var keyItem = coordinateItems[i];
                
                // 更新KeyItem的坐标索引属性
                keyItem.CoordinateIndex = i;
                
                // 创建坐标标记 - 使用在坐标类型集合中的索引
                var marker = new CoordinateMarker(keyItem, i);
                
                // 添加到集合中
                _coordinateMarkers[keyItem] = marker;
                
                // 添加拖拽事件处理
                AttachDragEvents(marker);
                
                // 显示坐标点
                marker.Show();
                
                _logger.Debug($"显示坐标点: {i + 1}-({keyItem.X},{keyItem.Y})");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("显示所有坐标点时发生异常", ex);
            // 确保清理所有资源
            ClearAllCoordinateMarkers();
            throw;
        }
    }
    
    // 隐藏所有坐标点
    private void HideAllCoordinateMarkers()
    {
        try
        {
            _logger.Debug("隐藏所有坐标点");
            
            // 隐藏所有坐标点
            foreach (var marker in _coordinateMarkers.Values)
            {
                marker.Hide();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("隐藏所有坐标点时发生异常", ex);
        }
    }
    
    // 清理所有坐标点
    private void ClearAllCoordinateMarkers()
    {
        try
        {
            _logger.Debug("清理所有坐标点");
            
            // 关闭所有坐标点窗口
            foreach (var marker in _coordinateMarkers.Values)
            {
                DetachDragEvents(marker);
                marker.Close();
            }
            
            // 清空集合
            _coordinateMarkers.Clear();
        }
        catch (Exception ex)
        {
            _logger.Error("清理所有坐标点时发生异常", ex);
        }
    }
    
    // 添加拖拽事件处理
    private void AttachDragEvents(CoordinateMarker marker)
    {
        if (marker == null || marker.MarkerWindow == null || marker.MarkerWindow.Content == null)
            return;
            
        var grid = marker.MarkerWindow.Content as Grid;
        if (grid == null)
            return;
            
        // 添加鼠标按下事件
        grid.MouseLeftButtonDown += (s, e) => Marker_MouseLeftButtonDown(marker, e);
        
        // 添加鼠标移动事件
        grid.MouseMove += (s, e) => Marker_MouseMove(marker, e);
        
        // 添加鼠标释放事件
        grid.MouseLeftButtonUp += (s, e) => Marker_MouseLeftButtonUp(marker, e);
    }
    
    // 移除拖拽事件处理
    private void DetachDragEvents(CoordinateMarker marker)
    {
        if (marker == null || marker.MarkerWindow == null || marker.MarkerWindow.Content == null)
            return;
            
        var grid = marker.MarkerWindow.Content as Grid;
        if (grid == null)
            return;
            
        // 移除所有事件处理器
        grid.MouseLeftButtonDown -= (s, e) => Marker_MouseLeftButtonDown(marker, e);
        grid.MouseMove -= (s, e) => Marker_MouseMove(marker, e);
        grid.MouseLeftButtonUp -= (s, e) => Marker_MouseLeftButtonUp(marker, e);
    }
    
    // 坐标点鼠标按下事件
    private void Marker_MouseLeftButtonDown(CoordinateMarker marker, MouseButtonEventArgs e)
    {
        try
        {
            if (marker == null || marker.KeyItem == null)
                return;
                
            // 记录拖拽状态
            marker.IsDragging = true;
            _draggingKeyItem = marker.KeyItem;
            
            // 捕获鼠标
            var grid = marker.MarkerWindow.Content as Grid;
            if (grid != null)
                grid.CaptureMouse();
                
            // 记录起始位置
            _startPoint = e.GetPosition(null);
            
            _logger.Debug($"开始拖拽坐标点: {marker.Index + 1}-({marker.KeyItem.X},{marker.KeyItem.Y})");
            e.Handled = true;
        }
        catch (Exception ex)
        {
            _logger.Error("处理坐标点鼠标按下事件时发生异常", ex);
        }
    }
    
    /// <summary>
    /// 获取原始屏幕坐标(不进行DPI转换)
    /// </summary>
    /// <returns>包含原始X,Y坐标的元组</returns>
    private (int X, int Y) GetRawScreenPosition()
    {
        try
        {
            // 获取鼠标的屏幕绝对位置（Windows Forms坐标系）
            System.Drawing.Point cursorPos = System.Windows.Forms.Cursor.Position;
            
            // 直接返回原始物理坐标
            return (cursorPos.X, cursorPos.Y);
        }
        catch (Exception ex)
        {
            _logger.Error("获取原始屏幕坐标时发生异常", ex);
            
            // 出错时返回(0,0)，调用方需要处理这种异常情况
            return (0, 0);
        }
    }
    
    // 坐标点鼠标移动事件
    private void Marker_MouseMove(CoordinateMarker marker, System.Windows.Input.MouseEventArgs e)
    {
        try
        {
            // 只有在拖拽状态下才处理移动
            if (!marker.IsDragging || e.LeftButton != MouseButtonState.Pressed)
                return;
                
            // 使用DPI感知坐标获取方法 - 用于UI位置计算
            var (dpiX, dpiY) = GetDpiAwareScreenPosition(marker.MarkerWindow);
            
            // 使用原始坐标 - 用于数据存储
            var (rawX, rawY) = GetRawScreenPosition();
            
            // 转换为用户坐标(从1开始)
            int userX = rawX + 1;
            int userY = rawY + 1;
            
            // 更新KeyItem中的坐标 - 使用原始坐标
            marker.KeyItem.X = userX;
            marker.KeyItem.Y = userY;
            
            // 更新坐标点位置 - UpdatePosition方法会处理DPI感知定位
            marker.UpdatePosition();
            
            // 更新标签显示
            marker.UpdateLabel();
            
            // 记录拖拽过程
            _logger.Debug($"拖拽坐标点: {marker.Index + 1} 到 ({userX},{userY}) [原始坐标: {rawX},{rawY}]");
        }
        catch (Exception ex)
        {
            _logger.Error("处理坐标点鼠标移动事件时发生异常", ex);
        }
    }
    
    // 坐标点鼠标释放事件
    private void Marker_MouseLeftButtonUp(CoordinateMarker marker, MouseButtonEventArgs e)
    {
        try
        {
            if (!marker.IsDragging)
                return;
                
            // 释放鼠标捕获
            var grid = marker.MarkerWindow.Content as Grid;
            if (grid != null && grid.IsMouseCaptured)
                grid.ReleaseMouseCapture();
                
            // 重置拖拽状态
            marker.IsDragging = false;
            _draggingKeyItem = null;
            
            // 使用原始坐标获取方法获取最终位置 - 用于数据存储
            var (rawX, rawY) = GetRawScreenPosition();
            
            // 转换为用户坐标(从1开始)
            int userX = rawX + 1;
            int userY = rawY + 1;
            
            // 最终更新KeyItem中的坐标
            marker.KeyItem.X = userX;
            marker.KeyItem.Y = userY;
            
            // 更新坐标点位置
            marker.UpdatePosition();
            
            // 更新标签显示
            marker.UpdateLabel();
            
            // 通知ViewModel保存配置
            ViewModel?.SaveConfig();
            
            // 更新热键服务中的按键列表，确保坐标更改立即生效
            ViewModel?.SyncKeyListToHotkeyService();
            
            _logger.Debug($"完成拖拽坐标点: {marker.Index + 1} 到最终位置 ({userX},{userY}) [原始坐标: {rawX},{rawY}]");
            e.Handled = true;
        }
        catch (Exception ex)
        {
            _logger.Error("处理坐标点鼠标释放事件时发生异常", ex);
        }
    }
    
    // 鼠标按钮按下事件 - 开始拖拽
    private void MouseIcon_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            // 如果处于编辑模式，阻止拖拽操作
            if (_isEditMode)
            {
                // 只允许点击事件，不启动拖拽操作
                e.Handled = true;
                return;
            }
            
            // 只处理左键按下
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _logger.Debug("鼠标图标按钮左键按下");
                
                // 记录起始点位置
                _startPoint = e.GetPosition(null);
                
                // 标记为可能的拖拽操作，但尚未确认
                _isDragging = true;
                _isDragStarted = false;

                // 捕获鼠标
                btnMouseIcon.CaptureMouse();
                
                // 阻止事件传递，避免触发点击事件
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("处理鼠标按钮按下事件时发生异常", ex);
            CleanupDragOperation();
        }
    }
    
    // 鼠标移动事件 - 拖拽过程
    private void MouseIcon_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        try
        {
            // 如果处于编辑模式，阻止拖拽操作
            if (_isEditMode)
            {
                return;
            }
            
            // 只有当鼠标左键按下且已经开始拖拽时才处理移动事件
            if (e.LeftButton == MouseButtonState.Pressed && _isDragging)
            {
                // 获取当前位置
                System.Windows.Point currentPosition = e.GetPosition(null);
                
                // 计算移动距离
                Vector moveDistance = currentPosition - _startPoint;
                double distance = Math.Sqrt(moveDistance.X * moveDistance.X + moveDistance.Y * moveDistance.Y);
                
                // 只有当移动距离超过阈值才认为是拖拽操作
                if (!_isDragStarted && distance > DRAG_THRESHOLD)
                {
                    _isDragStarted = true;
                    _logger.Debug($"鼠标拖拽开始，距离：{distance:F2}");
                    
                    // 确认是拖拽操作后再创建拖拽指示器
                    CreateDragPoint();
                }
                
                // 如果确认是拖拽操作，更新拖拽点位置
                if (_isDragStarted && _dragWindow != null)
                {
                    // 使用DPI感知坐标获取方法 - 用于UI定位
                    var (dpiX, dpiY) = GetDpiAwareScreenPosition(_dragWindow);
                    
                    // 应用坐标，并考虑十字准星尺寸居中偏移
                    _dragWindow.Left = dpiX - 10; // 居中显示，偏移准星尺寸的一半
                    _dragWindow.Top = dpiY - 10;  // 居中显示，偏移准星尺寸的一半
                    
                    // 确保拖拽窗口可见
                    if (!_dragWindow.IsVisible)
                    {
                        _dragWindow.Show();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("处理鼠标移动事件时发生异常", ex);
            CleanupDragOperation();
        }
    }
    
    // 鼠标释放事件 - 结束拖拽并获取坐标或切换编辑模式
    private void MouseIcon_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            // 只处理左键释放
            if (e.LeftButton == MouseButtonState.Released)
            {
                // 如果处于拖拽状态
                if (_isDragging)
                {
                    // 释放鼠标捕获
                    btnMouseIcon.ReleaseMouseCapture();
                    
                    // 如果确认是拖拽操作，则更新坐标
                    if (_isDragStarted)
                    {
                        // 获取原始物理坐标 - 用于数据存储
                        var (rawX, rawY) = GetRawScreenPosition();
                        
                        // 获取DPI感知坐标 - 仅用于日志对比
                        var (dpiX, dpiY) = GetDpiAwareScreenPosition(btnMouseIcon);
                        
                        // 记录详细的坐标获取日志，只在松开鼠标时记录一次
                        _logger.Debug($"鼠标拖拽结束，获取坐标位置 - 原始坐标: X={rawX}, Y={rawY}, DPI转换坐标: X={dpiX}, Y={dpiY}");
                        
                        // 更新坐标输入框 - 传入原始物理坐标
                        UpdateCoordinateInputs(rawX, rawY);
                        
                        // 阻止Click事件触发
                        e.Handled = true;
                    }
                    else
                    {
                        _logger.Debug("鼠标拖拽未超过阈值，视为点击操作");
                        // 切换编辑模式和图标样式
                        ToggleEditMode();
                        
                        // 阻止Click事件触发，因为我们已经手动处理了点击功能
                        e.Handled = true;
                    }
                    
                    // 清理拖拽操作相关资源
                    CleanupDragOperation();
                }
                else
                {
                    // 如果不是从拖拽状态释放，则是纯点击事件
                    // 切换编辑模式和图标样式
                    ToggleEditMode();
                    
                    // 阻止后续Click事件处理
                    e.Handled = true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("鼠标释放事件处理异常", ex);
            CleanupDragOperation();
        }
    }
    
    // 创建拖拽时显示的红点
    private void CreateDragPoint()
    {
        try
        {
            // 如果拖拽点已存在，直接返回
            if (_dragWindow != null)
                return;
            
            // 创建拖拽时的十字准星UI
            Grid mainGrid = new Grid
            {
                Width = 20,
                Height = 20,
                Background = null // 透明背景
            };
            
            // 添加水平线
            System.Windows.Shapes.Rectangle horizontalLine = new System.Windows.Shapes.Rectangle
            {
                Width = 20,
                Height = 1,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0)), // 红色
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // 添加垂直线
            System.Windows.Shapes.Rectangle verticalLine = new System.Windows.Shapes.Rectangle
            {
                Width = 1,
                Height = 20,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0)), // 红色
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // 添加中心点（半径5像素的圆）
            Ellipse centerPoint = new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0)), // 红色
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // 将元素添加到Grid
            mainGrid.Children.Add(horizontalLine);
            mainGrid.Children.Add(verticalLine);
            mainGrid.Children.Add(centerPoint);
            
            // 创建窗口
            _dragWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = null,
                Topmost = true,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                SizeToContent = SizeToContent.Manual, // 固定尺寸更精确
                Content = mainGrid,
                Width = 20,     // 与Grid尺寸一致
                Height = 20     // 与Grid尺寸一致
            };
            
            // 设置为工具窗口，不在Alt+Tab列表中显示
            Win32.HideFromAltTab(_dragWindow);
            
            // 初始位置设为屏幕外，避免闪烁
            _dragWindow.Left = -100;
            _dragWindow.Top = -100;
            
            // 显示拖拽窗口
            _dragWindow.Show();
            
            _logger.Debug("创建十字准星拖拽指示器成功");
        }
        catch (Exception ex)
        {
            _logger.Error("创建拖拽红点失败", ex);
            CleanupDragOperation();
        }
    }
    
    // 更新坐标输入框
    private void UpdateCoordinateInputs(int x, int y)
    {
        try
        {
            if (ViewModel != null)
            {
                // 计算用户坐标（从1开始）
                int userX = x + 1;
                int userY = y + 1;
                
                // 将原始物理坐标设置到ViewModel
                ViewModel.CurrentX = userX;
                ViewModel.CurrentY = userY;
                
                _logger.Debug($"最终坐标值 - 用户坐标: X={userX}, Y={userY}, 原始物理坐标: X={x}, Y={y}");
                
                // 使用Dispatcher.BeginInvoke确保在UI线程上更新，并降低优先级
                Dispatcher.BeginInvoke(new Action(() => {
                    if (XCoordinateInputBox != null)
                    {
                        XCoordinateInputBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateTarget();
                    }
                    
                    if (YCoordinateInputBox != null)
                    {
                        YCoordinateInputBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateTarget();
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("更新坐标输入框时发生异常", ex);
        }
    }
    
    // 清理拖拽操作相关资源
    private void CleanupDragOperation()
    {
        try
        {
            // 释放鼠标捕获
            if (btnMouseIcon != null && btnMouseIcon.IsMouseCaptured)
            {
                btnMouseIcon.ReleaseMouseCapture();
            }
            
            // 关闭拖拽窗口
            if (_dragWindow != null)
            {
                _dragWindow.Close();
                _dragWindow = null;
            }
            
            // 清理红点
            _dragPoint = null;
            
            // 重置拖拽状态
            _isDragging = false;
            _isDragStarted = false;
            // 重置警告日志状态
            _hasLoggedWarning = false;
        }
        catch (Exception ex)
        {
            _logger.Error("清理拖拽操作时发生异常", ex);
        }
    }
    
    /// <summary>
    /// 获取考虑DPI缩放的屏幕坐标
    /// </summary>
    /// <param name="referenceVisual">用于DPI转换的参考视觉元素</param>
    /// <returns>包含经过DPI转换的X,Y坐标的元组</returns>
    private (int X, int Y) GetDpiAwareScreenPosition(Visual referenceVisual)
    {
        try
        {
            // 获取鼠标的屏幕绝对位置（Windows Forms坐标系）
            System.Drawing.Point cursorPos = System.Windows.Forms.Cursor.Position;
            
            // 为了解决DPI缩放问题，需要将Windows Forms坐标转换为WPF坐标
            // 获取PresentationSource以进行坐标转换
            PresentationSource source = PresentationSource.FromVisual(referenceVisual);
            if (source != null && source.CompositionTarget != null)
            {
                // 获取设备到逻辑单位的转换矩阵
                Matrix transformMatrix = source.CompositionTarget.TransformFromDevice;
                
                // 将设备坐标点转换为WPF逻辑坐标
                System.Windows.Point devicePoint = new System.Windows.Point(cursorPos.X, cursorPos.Y);
                System.Windows.Point wpfPoint = transformMatrix.Transform(devicePoint);
                
                // 记录转换前后坐标差异
                double dpiScaleX = transformMatrix.M11;
                double dpiScaleY = transformMatrix.M22;
                _logger.Debug($"DPI坐标转换: 原始=({cursorPos.X},{cursorPos.Y}), 转换后=({wpfPoint.X:F1},{wpfPoint.Y:F1}), 缩放比例: {dpiScaleX:F2}x{dpiScaleY:F2}");
                
                // 返回转换后的整数坐标
                return ((int)wpfPoint.X, (int)wpfPoint.Y);
            }
            else
            {
                // 如果无法获取转换矩阵，则使用原始坐标
                if (!_hasLoggedWarning)
                {
                    _logger.Warning("无法获取坐标转换信息，使用原始位置计算方法");
                    _hasLoggedWarning = true;
                }
                
                // 返回原始整数坐标
                return (cursorPos.X, cursorPos.Y);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("获取DPI感知屏幕坐标时发生异常", ex);
            
            // 出错时返回(0,0)，调用方需要处理这种异常情况
            return (0, 0);
        }
    }

    // 添加切换编辑模式的方法
    private void ToggleEditMode()
    {
        try
        {
            // 切换编辑模式状态
            _isEditMode = !_isEditMode;
            
            if (_isEditMode)
            {
                // 进入编辑模式
                _logger.Debug("进入坐标点编辑模式");
                
                // 切换为退出图标
                ChangeMouseIconToExit();
                
                // 显示所有坐标标记
                ShowAllCoordinateMarkers();
            }
            else
            {
                // 退出编辑模式
                _logger.Debug("退出坐标点编辑模式");
                
                // 恢复原始图标
                RestoreOriginalMouseIcon();
                
                // 隐藏所有坐标标记如果之前是隐藏状态
                if (!_isCoordinateMarkersVisible)
                {
                    HideAllCoordinateMarkers();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("切换编辑模式异常", ex);
            // 确保出错时恢复正常状态
            _isEditMode = false;
            RestoreOriginalMouseIcon();
        }
    }
    
    // 切换为退出图标
    private void ChangeMouseIconToExit()
    {
        try
        {
            // 查找按钮中的Path元素
            if (btnMouseIcon.Content is Path path)
            {
                // 保存原始路径数据供恢复使用
                path.Tag = path.Data;
                
                // 设置为退出图标（X形状）
                path.Data = Geometry.Parse("M512 456.310154L325.15799 269.469166c-16.662774-16.662774-43.677083-16.662774-60.339857 0s-16.662774 43.677083 0 60.339857L451.656143 516.650011 264.818154 703.490999c-16.662774 16.662774-16.662774 43.677083 0 60.339857s43.677083 16.662774 60.339857 0l186.840988-186.840988 186.840988 186.840988c16.662774 16.662774 43.677083 16.662774 60.339857 0s16.662774-43.677083 0-60.339857L572.340834 516.650011l186.840988-186.840988c16.662774-16.662774 16.662774-43.677083 0-60.339857s-43.677083-16.662774-60.339857 0L512 456.310154z");
                
                // 更改颜色为红色，表示退出
                path.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232,75,108));
                
                // 更新工具提示
                btnMouseIcon.ToolTip = "退出编辑模式";
            }
        }
        catch (Exception ex)
        {
            _logger.Error("更改为退出图标时出错", ex);
        }
    }
    
    // 恢复原始图标
    private void RestoreOriginalMouseIcon()
    {
        try
        {
            // 查找按钮中的Path元素
            if (btnMouseIcon.Content is Path path && path.Tag is Geometry originalData)
            {
                // 恢复原始路径数据
                path.Data = originalData;
                path.Tag = null;
                
                // 恢复原始颜色
                path.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#707070"));
                
                // 恢复原始工具提示
                btnMouseIcon.ToolTip = "鼠标位置";
            }
        }
        catch (Exception ex)
        {
            _logger.Error("恢复原始图标时出错", ex);
        }
    }
}