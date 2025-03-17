using System.Windows.Input;
using System.Linq;
using WpfApp.Services;
using WpfApp.Services.Models;
using WpfApp.Services.Utils;
using WpfApp.Services.Config;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using WpfApp.Views;
using System.Runtime.InteropServices;
using System.Timers;
using System;
using WpfApp.Services.Core;
using System.Threading.Tasks;
using System.Windows.Threading;

// 定义KeyItemSettings结构用于传递按键设置
public class KeyItemSettings
{
    public LyKeysCode? KeyCode { get; set; }
    public int Interval { get; set; } = 5;
    public KeyItemType Type { get; set; } = KeyItemType.Keyboard;
    public int X { get; set; }
    public int Y { get; set; }
    
    // 创建键盘按键设置
    public static KeyItemSettings CreateKeyboard(LyKeysCode keyCode, int interval = 5)
    {
        return new KeyItemSettings
        {
            KeyCode = keyCode,
            Interval = interval,
            Type = KeyItemType.Keyboard
        };
    }
    
    // 创建坐标设置
    public static KeyItemSettings CreateCoordinates(int x, int y, int interval = 5)
    {
        return new KeyItemSettings
        {
            X = x,
            Y = y,
            Interval = interval,
            Type = KeyItemType.Coordinates
        };
    }
}

// 按键映射核心业务逻辑层
namespace WpfApp.ViewModels
{
    public class KeyMappingViewModel : ViewModelBase
    {
        private readonly LyKeysService _lyKeysService;
        private readonly ConfigService _configService;
        private LyKeysCode? _currentKey;
        private string _currentKeyText = string.Empty;
        private ObservableCollection<KeyItem> _keyList;
        private string _hotkeyText = string.Empty; // 简化为单一热键文本
        private LyKeysCode? _hotkey; // 主热键
        private ModifierKeys _hotkeyModifiers = ModifierKeys.None; // 修饰热键
        private int _selectedKeyMode;
        private readonly HotkeyService _hotkeyService;
        private bool _isHotkeyEnabled;
        private string _hotkeyStatus;
        private bool _isSequenceMode = true; // 默认为顺序模式
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly MainViewModel _mainViewModel;
        private MainWindow? _mainWindow;
        private bool _isSoundEnabled = true;
        private readonly AudioService _audioService;
        private double _soundVolume = 0.8; // 默认音量80%
        private bool _isReduceKeyStuck = true; // 默认开启
        private bool _isInitializing = true; // 添加一个标志来标识是否在初始化
        private bool _isExecuting = false; // 添加执行状态标志
        private bool _isFloatingWindowEnabled;
        private bool _autoSwitchToEnglishIME = true; // 默认开启自动切换输入法
        private bool _isHotkeyControlEnabled = true; // 热键总开关，默认开启
        private FloatingStatusWindow _floatingWindow;
        private FloatingStatusViewModel _floatingViewModel;
        private KeyItem? _selectedKeyItem;
        private string _selectedWindowTitle = "空";
        private IntPtr _selectedWindowHandle = IntPtr.Zero;
        private string _selectedWindowClassName = string.Empty;
        private string _selectedWindowProcessName = string.Empty;
        private System.Timers.Timer? _windowCheckTimer;
        private readonly object _windowCheckLock = new();
        private bool _isTargetWindowActive;
        private readonly System.Timers.Timer _activeWindowCheckTimer;
        private const int ACTIVE_WINDOW_CHECK_INTERVAL = 50; // 50ms检查一次活动窗口
        private int _keyInterval = 5;
        
        // 添加坐标属性
        private int? _currentX;
        private int? _currentY;

        // 添加浮窗状态更新节流控制变量
        private readonly object _floatingStatusUpdateLock = new object();
        private DateTime _lastFloatingStatusUpdateTime = DateTime.MinValue;
        private const int FLOATING_STATUS_UPDATE_THROTTLE_MS = 200; // 200毫秒节流间隔

        // 添加窗口句柄变化事件
        public event Action<IntPtr>? WindowHandleChanged;

        /// <summary>
        /// 获取当前是否处于初始化状态
        /// </summary>
        public bool IsInitializing => _isInitializing;

        // 选中的窗口标题
        public string SelectedWindowTitle
        {
            get => _selectedWindowTitle;
            set => SetProperty(ref _selectedWindowTitle, value);
        }

        // 选中的窗口句柄
        public IntPtr SelectedWindowHandle
        {
            get => _selectedWindowHandle;
            private set
            {
                if (_selectedWindowHandle != value)
                {
                    _selectedWindowHandle = value;
                    OnPropertyChanged();

                    // 触发窗口句柄变化事件
                    WindowHandleChanged?.Invoke(value);

                    // 同步到热键服务
                    _hotkeyService.TargetWindowHandle = value;

                    _logger.Debug($"窗口句柄已更新: {value}, 已同步到热键服务");
                }
            }
        }

        public string SelectedWindowProcessName
        {
            get => _selectedWindowProcessName;
            set => SetProperty(ref _selectedWindowProcessName, value);
        }

        public string SelectedWindowClassName
        {
            get => _selectedWindowClassName;
            set => SetProperty(ref _selectedWindowClassName, value);
        }

        // 更新选中的窗口句柄信息
        public void UpdateSelectedWindow(IntPtr handle, string title, string className, string processName)
        {
            SelectedWindowHandle = handle;
            SelectedWindowClassName = className;
            SelectedWindowProcessName = processName;
            SelectedWindowTitle = $"{title} (句柄: {handle.ToInt64()})";

            // 同步句柄到 HotkeyService
            _hotkeyService.TargetWindowHandle = handle;

            // 保存到配置
            AppConfigService.UpdateConfig(config =>
            {
                config.TargetWindowClassName = className;
                config.TargetWindowProcessName = processName;
                config.TargetWindowTitle = title;
            });

            // 启动定时检查
            StartWindowCheck();

            _logger.Info($"已选择窗口: {title}, 句柄: {handle.ToInt64()}, 类名: {className}, 进程名: {processName}");
        }

        // 清除选中的窗口句柄
        public void ClearSelectedWindow()
        {
            try
            {
                // 停止窗口检查
                StopWindowCheck();

                // 清除窗口信息
                _selectedWindowHandle = IntPtr.Zero;
                _selectedWindowTitle = "空";
                _selectedWindowClassName = string.Empty;
                _selectedWindowProcessName = string.Empty;

                // 更新热键服务的目标窗口
                if (_hotkeyService != null) _hotkeyService.TargetWindowHandle = IntPtr.Zero;

                // 通知UI更新
                OnPropertyChanged(nameof(SelectedWindowTitle));

                _logger.Debug("已清除窗口信息");

                // 使用AppConfigService.UpdateConfig清除配置中的窗口信息
                AppConfigService.UpdateConfig(config =>
                {
                    config.TargetWindowClassName = null;
                    config.TargetWindowProcessName = null;
                    config.TargetWindowTitle = null;
                });
                
                _logger.Debug("已清除窗口信息并保存到配置文件");
            }
            catch (Exception ex)
            {
                _logger.Error("清除窗口信息时发生异常", ex);
            }
        }

        // 按键列表
        public ObservableCollection<KeyItem> KeyList
        {
            get => _keyList;
            set => SetProperty(ref _keyList, value);
        }

        // 当前按键文本
        public string CurrentKeyText
        {
            get => _currentKeyText;
            set
            {
                _currentKeyText = value;
                OnPropertyChanged(nameof(CurrentKeyText));
            }
        }

        // 热键文本
        public string HotkeyText
        {
            get => _hotkeyText;
            set => SetProperty(ref _hotkeyText, value);
        }

        // 按键间隔，现在仅作为默认值使用
        public int KeyInterval
        {
            get => _keyInterval;
            set
            {
                if (SetProperty(ref _keyInterval, value))
                {
                    // 更新到驱动服务，让它保持与UI一致
                    _lyKeysService.KeyInterval = value;

                    // 实时保存到配置，作为配置管理的唯一入口
                    if (!_isInitializing)
                    {
                        AppConfigService.UpdateConfig(config => { config.interval = value; });
                        _logger.Debug($"已将默认按键间隔{value}ms保存到配置");
                    }
                }
            }
        }

        // 添加按键命令
        public ICommand AddKeyCommand { get; private set; } = null!;

        /// <summary>
        /// 添加坐标命令
        /// </summary>
        public ICommand AddCoordinateCommand { get; private set; } = null!;

        // 按键模式选项
        public List<string> KeyModes { get; } = new()
        {
            "顺序模式",
            "按压模式"
        };

        // 选中的按键模式
        public int SelectedKeyMode
        {
            get => _selectedKeyMode;
            set
            {
                if (SetProperty(ref _selectedKeyMode, value))
                {
                    // 如果正在执行，先停止当前循环
                    if (IsExecuting) StopKeyMapping();

                    IsSequenceMode = value == 0; // 0 表示顺序模式

                    // 恢复输入法
                    if (_lyKeysService != null) _lyKeysService.RestoreIME();

                    _logger.Debug($"按键模式已切换为: {(value == 0 ? "顺序模式" : "按压模式")}");
                }
            }
        }

        // 热键是否启用
        public bool IsHotkeyEnabled
        {
            get => _isHotkeyEnabled;
            set
            {
                SetProperty(ref _isHotkeyEnabled, value);
                HotkeyStatus = value ? "按键已启动" : "按键已停止";
            }
        }

        // 热键总开关
        public bool IsHotkeyControlEnabled
        {
            get => _isHotkeyControlEnabled;
            set
            {
                if (SetProperty(ref _isHotkeyControlEnabled, value))
                {
                    _logger.Debug($"热键总开关已{(value ? "启用" : "禁用")}");

                    // 同步状态到HotkeyService
                    if (_hotkeyService != null) _hotkeyService.IsHotkeyControlEnabled = value;

                    // 更新浮窗状态
                    UpdateFloatingStatus();

                    // 如果禁用总开关，同时停止当前的按键映射
                    if (!value && IsExecuting) StopKeyMapping();

                    // 实时保存到配置
                    if (!_isInitializing)
                    {
                        AppConfigService.UpdateConfig(config => { config.isHotkeyControlEnabled = value; });
                        _logger.Debug($"已将热键总开关状态({value})保存到配置");
                    }
                }
            }
        }

        // 按键状态
        public string HotkeyStatus
        {
            get => _hotkeyStatus;
            set => SetProperty(ref _hotkeyStatus, value);
        }

        // 是否为顺序模式
        public bool IsSequenceMode
        {
            get => _isSequenceMode;
            set
            {
                if (SetProperty(ref _isSequenceMode, value))
                {
                    // 当模式改变时更新驱动服务
                    _lyKeysService.IsHoldMode = !value;

                    // 更新HotkeyService的按键列表
                    var selectedKeys = KeyList.Where(k => k.IsSelected).ToList();
                    _hotkeyService.SetKeySequence(
                        selectedKeys.Select(k => new KeyItemSettings
                        {
                            KeyCode = k.KeyCode,
                            Interval = k.KeyInterval
                        }).ToList());

                    // 实时保存模式设置
                    if (!_isInitializing)
                        AppConfigService.UpdateConfig(config => { config.keyMode = value ? 0 : 1; });

                    _logger.Debug($"模式切换 - 当前模式: {(value ? "顺序模式" : "按压模式")}, " +
                                  $"选中按键数: {selectedKeys.Count}, " +
                                  $"按键间隔: {KeyInterval}ms");
                }
            }
        }

        // 声音是否启用
        public bool IsSoundEnabled
        {
            get => _isSoundEnabled;
            set
            {
                if (SetProperty(ref _isSoundEnabled, value))
                    if (!_isInitializing) // 只在非初始化时保存
                        SaveConfig();
            }
        }

        // 音量属性
        public double SoundVolume
        {
            get => _soundVolume;
            set
            {
                if (SetProperty(ref _soundVolume, value))
                {
                    if (_audioService != null)
                    {
                        _audioService.Volume = value;
                        _logger.Debug($"已设置音量为: {value:P0}");
                        
                        // 非初始化状态下自动保存音量设置
                        if (!_isInitializing)
                        {
                            SaveSoundVolume(value);
                        }
                    }
                }
            }
        }

        // 单独保存音量设置的方法
        private void SaveSoundVolume(double volume)
        {
            try
            {
                AppConfigService.UpdateConfig(config => 
                {
                    config.SoundVolume = volume;
                });
                _logger.Debug($"音量设置已保存: {volume:P0}");
            }
            catch (Exception ex)
            {
                _logger.Error($"保存音量设置失败: {ex.Message}", ex);
            }
        }

        // 是否可以调整音量（基于音频设备是否可用）
        public bool CanAdjustVolume => _audioService.AudioDeviceAvailable;

        // 判断是否为为降低卡位模式，为true时按下抬起间隔为5ms，为false时间隔为0ms
        public bool IsReduceKeyStuck
        {
            get => _isReduceKeyStuck;
            set
            {
                if (SetProperty(ref _isReduceKeyStuck, value))
                {
                    // 根据降低卡位模式设置按键间隔
                    var newInterval = value ? LyKeysService.DEFAULT_KEY_PRESS_INTERVAL : 0;
                    _lyKeysService.KeyPressInterval = newInterval;

                    if (!_isInitializing) SaveConfig();
                    _logger.Debug($"降低卡位模式已更改为: {value}, 期望按键间隔: {newInterval}ms, " +
                                  $"实际按键间隔: {_lyKeysService.KeyPressInterval}ms, 默认按键间隔值: {LyKeysService.DEFAULT_KEY_PRESS_INTERVAL}ms");
                }
            }
        }

        // 是否正在执行
        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                if (_isExecuting != value)
                {
                    _logger.Debug($"执行状态改变: {_isExecuting} -> {value}");
                    _isExecuting = value;
                    OnPropertyChanged(nameof(IsExecuting));
                    OnPropertyChanged(nameof(IsNotExecuting));
                    
                    // 确保状态变更能直接同步到浮窗ViewModel
                    try {
                        if (_floatingWindow != null)
                        {
                            // 反射获取DataContext
                            var type = _floatingWindow.GetType();
                            var propInfo = type.GetProperty("DataContext");
                            if (propInfo != null)
                            {
                                var dataContext = propInfo.GetValue(_floatingWindow);
                                if (dataContext is FloatingStatusViewModel viewModel)
                                {
                                    viewModel.IsExecuting = value;
                                    _logger.Debug($"已直接更新浮窗ViewModel状态: IsExecuting={value}");
                                }
                            }
                        }
                    } catch (Exception ex) {
                        _logger.Error("在执行状态变更时更新浮窗状态失败", ex);
                    }
                }
            }
        }

        // 是否未在执行（用于绑定）
        public bool IsNotExecuting => !IsExecuting;

        public bool IsFloatingWindowEnabled
        {
            get => _isFloatingWindowEnabled;
            set
            {
                if (SetProperty(ref _isFloatingWindowEnabled, value))
                {
                    if (!_isInitializing) SaveConfig();

                    if (value)
                        ShowFloatingWindow();
                    else
                        HideFloatingWindow();
                }
            }
        }

        /// <summary>
        /// 获取或设置是否自动切换到英文输入法
        /// </summary>
        public bool AutoSwitchToEnglishIME
        {
            get => _autoSwitchToEnglishIME;
            set
            {
                if (SetProperty(ref _autoSwitchToEnglishIME, value))
                    if (!_isInitializing)
                    {
                        SaveConfig();

                        // 通知LyKeysService更新输入法切换设置
                        _lyKeysService.SetAutoSwitchIME(value);
                    }
            }
        }

        // 选中的按键项
        public KeyItem? SelectedKeyItem
        {
            get => _selectedKeyItem;
            set => SetProperty(ref _selectedKeyItem, value);
        }

        public void SetMainWindow(MainWindow mainWindow)
        {
            if (mainWindow == null)
            {
                _logger.Warning("传入的 MainWindow 为空");
                return;
            }

            _mainWindow = mainWindow;
            _logger.Debug("已设置 MainWindow 引用");

            // 延迟初始化浮窗，避免在主窗口显示时阻塞UI
            if (IsFloatingWindowEnabled)
            {
                // 使用延迟，避免在UI初始化阶段占用主线程
                Task.Delay(500).ContinueWith(_ => {
                    try 
                    {
                        // 在UI线程上执行但使用低优先级
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                            try 
                            {
                                if (_floatingWindow == null)
                                {
                                    InitializeFloatingWindow();
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error("延迟初始化浮窗时发生错误", ex);
                            }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("延迟初始化浮窗任务失败", ex);
                    }
                });
            }
        }

        // 初始化浮窗的方法
        private void InitializeFloatingWindow()
        {
            try
            {
                if (_mainWindow == null)
                {
                    _logger.Warning("初始化浮窗: MainWindow 引用为空");
                    return;
                }

                // 先创建 ViewModel
                _floatingViewModel = new FloatingStatusViewModel();

                // 初始化ViewModel属性
                _floatingViewModel.IsHotkeyControlEnabled = _isHotkeyControlEnabled;
                _floatingViewModel.IsExecuting = _isExecuting;

                // 创建浮窗
                _floatingWindow = new FloatingStatusWindow(_mainWindow);
                
                // 设置数据上下文
                var dataContext = _floatingViewModel;
                if (_floatingWindow != null)
                {
                    _logger.Debug("设置浮窗数据上下文");
                    // 使用反射设置DataContext避免类型问题
                    System.Type type = _floatingWindow.GetType();
                    System.Reflection.PropertyInfo propInfo = type.GetProperty("DataContext");
                    if (propInfo != null)
                    {
                        propInfo.SetValue(_floatingWindow, dataContext);
                        _logger.Debug("浮窗数据上下文设置成功");
                    }
                    else
                    {
                        _logger.Warning("未找到浮窗的DataContext属性");
                    }
                    
                    // 使用反射调用Show方法
                    System.Reflection.MethodInfo showMethod = type.GetMethod("Show");
                    if (showMethod != null)
                    {
                        showMethod.Invoke(_floatingWindow, null);
                        UpdateFloatingStatusInternal();
                        _logger.Debug("浮窗已显示并更新状态");
                    }
                    else
                    {
                        _logger.Warning("未找到浮窗的Show方法");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("初始化浮窗过程中发生错误", ex);
            }
        }

        private void ShowFloatingWindow()
        {
            try
            {
                if (_mainWindow == null)
                {
                    return;
                }

                // 通过异步方式初始化和显示浮窗
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    try
                    {
                        if (_floatingWindow == null)
                        {
                            InitializeFloatingWindow();
                        }
                        else
                        {
                            // 对于已存在的浮窗，使用反射调用Show方法
                            System.Type type = _floatingWindow.GetType();
                            System.Reflection.MethodInfo showMethod = type.GetMethod("Show");
                            if (showMethod != null)
                            {
                                showMethod.Invoke(_floatingWindow, null);
                                UpdateFloatingStatusInternal();
                                _logger.Debug("已有浮窗显示成功");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("显示浮窗时发生错误", ex);
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                _logger.Error("创建或显示浮窗时发生错误", ex);
            }
        }

        private void HideFloatingWindow()
        {
            if (_floatingWindow != null)
            {
                try
                {
                    // 使用反射调用Hide方法
                    System.Type type = _floatingWindow.GetType();
                    System.Reflection.MethodInfo hideMethod = type.GetMethod("Hide");
                    if (hideMethod != null)
                    {
                        hideMethod.Invoke(_floatingWindow, null);
                        
                        // 使用反射获取DataContext属性
                        System.Reflection.PropertyInfo propInfo = type.GetProperty("DataContext");
                        if (propInfo != null)
                        {
                            var dataContext = propInfo.GetValue(_floatingWindow);
                            if (dataContext is FloatingStatusViewModel viewModel)
                            {
                                viewModel.StatusText = "已停止";
                            }
                        }
                        
                        _logger.Debug("浮窗已隐藏");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("隐藏浮窗时发生错误", ex);
                }
            }
        }

        private void UpdateFloatingStatus(bool forceUpdate = false)
        {
            try
            {
                // 添加节流控制
                lock (_floatingStatusUpdateLock)
                {
                    var now = DateTime.Now;
                    // 如果不是强制更新且距离上次更新不足节流间隔，则跳过本次更新
                    if (!forceUpdate && (now - _lastFloatingStatusUpdateTime).TotalMilliseconds < FLOATING_STATUS_UPDATE_THROTTLE_MS)
                    {
                        _logger.Debug("浮窗状态更新被节流控制跳过");
                        return;
                    }
                    
                    // 更新时间戳
                    _lastFloatingStatusUpdateTime = now;
                }
                
                _logger.Debug($"执行浮窗状态更新(forceUpdate={forceUpdate})");
                UpdateFloatingStatusInternal();
            }
            catch (Exception ex)
            {
                _logger.Error("更新浮窗状态时发生错误", ex);
            }
        }

        private void UpdateFloatingStatusInternal()
        {
            if (_floatingWindow != null)
            {
                try
                {
                    // 尝试直接获取和保存ViewModel引用，减少后续反射操作
                    if (_floatingViewModel == null)
                    {
                        // 使用反射获取DataContext属性
                        System.Type type = _floatingWindow.GetType();
                        System.Reflection.PropertyInfo propInfo = type.GetProperty("DataContext");
                        if (propInfo != null)
                        {
                            var dataContext = propInfo.GetValue(_floatingWindow);
                            if (dataContext is FloatingStatusViewModel viewModel)
                            {
                                _floatingViewModel = viewModel;
                                _logger.Debug("成功获取并缓存浮窗ViewModel引用");
                            }
                        }
                    }
                    
                    // 如果已有缓存的ViewModel引用，直接使用
                    if (_floatingViewModel != null)
                    {
                        _logger.Debug($"更新浮窗前状态: IsHotkeyControlEnabled={_floatingViewModel.IsHotkeyControlEnabled}, IsExecuting={_floatingViewModel.IsExecuting}");
                        
                        // 更新ViewModel状态
                        _floatingViewModel.IsHotkeyControlEnabled = _isHotkeyControlEnabled; // 同步热键总开关状态
                        _floatingViewModel.IsExecuting = _isExecuting; // 同步执行状态
                        
                        _logger.Debug($"更新浮窗状态完成: 热键总开关={_isHotkeyControlEnabled}, 执行状态={_isExecuting}, 当前状态文本={_floatingViewModel.StatusText}");
                        
                        try
                        {
                            // 直接更新边框颜色，确保边框样式与状态同步
                            _floatingWindow.UpdateBorderStyle(_floatingViewModel.StatusText);
                            _logger.Debug($"已更新浮窗边框样式，状态文本: {_floatingViewModel.StatusText}");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("更新浮窗边框样式时出错", ex);
                        }
                    }
                    // 如果没有缓存的ViewModel，则使用反射方式
                    else
                    {
                        // 使用反射获取DataContext属性
                        System.Type type = _floatingWindow.GetType();
                        System.Reflection.PropertyInfo propInfo = type.GetProperty("DataContext");
                        if (propInfo != null)
                        {
                            var dataContext = propInfo.GetValue(_floatingWindow);
                            if (dataContext is FloatingStatusViewModel viewModel)
                            {
                                _logger.Debug($"更新浮窗前状态: IsHotkeyControlEnabled={viewModel.IsHotkeyControlEnabled}, IsExecuting={viewModel.IsExecuting}");
                                
                                // 更新ViewMode状态
                                viewModel.IsHotkeyControlEnabled = _isHotkeyControlEnabled; // 同步热键总开关状态
                                viewModel.IsExecuting = _isExecuting; // 同步执行状态
                                
                                _logger.Debug($"更新浮窗状态完成: 热键总开关={_isHotkeyControlEnabled}, 执行状态={_isExecuting}, 当前状态文本={viewModel.StatusText}");
                                
                                // 缓存ViewModel引用，减少后续反射操作
                                _floatingViewModel = viewModel;
                                
                                try
                                {
                                    // 直接更新边框颜色，确保边框样式与状态同步
                                    _floatingWindow.UpdateBorderStyle(viewModel.StatusText);
                                    _logger.Debug($"已更新浮窗边框样式，状态文本: {viewModel.StatusText}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error("更新浮窗边框样式时出错", ex);
                                }
                            }
                            else
                            {
                                _logger.Warning($"浮窗DataContext类型错误: {dataContext?.GetType().Name ?? "null"}");
                            }
                        }
                        else
                        {
                            _logger.Warning("无法获取浮窗DataContext属性");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("更新浮窗状态内部处理时出错", ex);
                    
                    // 尝试重新创建浮窗ViewModel以修复问题
                    try {
                        if (_floatingViewModel == null)
                        {
                            _floatingViewModel = new FloatingStatusViewModel();
                            _floatingViewModel.IsHotkeyControlEnabled = _isHotkeyControlEnabled;
                            _floatingViewModel.IsExecuting = _isExecuting;
                            _logger.Debug("已创建新的浮窗ViewModel实例");
                            
                            // 尝试更新浮窗DataContext
                            var type = _floatingWindow.GetType();
                            var propInfo = type.GetProperty("DataContext");
                            if (propInfo != null)
                            {
                                propInfo.SetValue(_floatingWindow, _floatingViewModel);
                                _logger.Debug("已成功重置浮窗DataContext");
                            }
                        }
                    } catch (Exception innerEx) {
                        _logger.Error("尝试修复浮窗ViewModel失败", innerEx);
                    }
                }
            }
            else
            {
                _logger.Debug("浮窗对象为null，无法更新状态");
            }
        }

        public bool IsTargetWindowActive
        {
            get => _isTargetWindowActive;
            private set
            {
                if (_isTargetWindowActive != value)
                {
                    _isTargetWindowActive = value;
                    OnPropertyChanged();

                    // 只在窗口变为非活动状态时停止按键映射
                    if (!value && IsExecuting)
                    {
                        _lyKeysService.EmergencyStop(); // 使用紧急停止
                        StopKeyMapping();
                        UpdateFloatingStatus(); // 更新浮窗状态
                        _logger.Debug("目标窗口切换为非活动状态，停止按键映射，已更新浮窗状态");
                    }
                    else if (value && IsExecuting)
                    {
                        // 如果窗口重新激活，且之前在执行中，更新浮窗状态
                        UpdateFloatingStatus();
                        _logger.Debug("目标窗口重新激活，更新浮窗状态");
                    }
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public KeyMappingViewModel(LyKeysService lyKeysService, ConfigService configService,
            HotkeyService hotkeyService, MainViewModel mainViewModel, AudioService audioService)
        {
            _isInitializing = true;
            _lyKeysService = lyKeysService;
            _configService = configService;
            _hotkeyService = hotkeyService;
            _mainViewModel = mainViewModel;
            _audioService = audioService;
            _hotkeyStatus = "初始化中...";

            // 1. 初始化基础组件
            _keyList = new ObservableCollection<KeyItem>();
            InitializeCommands();
            InitializeHotkeyStatus();

            // 3. 订阅事件
            SubscribeToEvents();

            // 4. 订阅驱动服务的状态变化
            _lyKeysService.EnableStatusChanged += (s, enabled) =>
            {
                // 添加对Application.Current的空检查
                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => { IsHotkeyEnabled = enabled; });
                }
                else
                {
                    // 直接在当前线程更新状态
                    IsHotkeyEnabled = enabled;
                    _logger.Debug("Application.Current为空，直接在当前线程更新IsHotkeyEnabled状态");
                }
            };

            // 5. 修改热键事件处理
            _hotkeyService.StartHotkeyPressed += OnStartHotkeyPressed;  // 启动热键按下事件
            _hotkeyService.StartHotkeyReleased += OnStartHotkeyReleased; // 启动热键释放事件

            // 6. 加载配置
            LoadConfiguration();

            // 7. 确保配置同步到服务
            SyncConfigToServices();

            // 8. 加载窗口配置
            LoadWindowConfig();

            // 9. 初始化活动窗口检查定时器
            _activeWindowCheckTimer = new System.Timers.Timer(ACTIVE_WINDOW_CHECK_INTERVAL);
            _activeWindowCheckTimer.Elapsed += ActiveWindowCheckTimer_Elapsed;
            _activeWindowCheckTimer.Start();

            // 添加窗口句柄变化事件订阅
            WindowHandleChanged += (handle) =>
            {
                _hotkeyService.TargetWindowHandle = handle;
                _logger.Debug($"窗口句柄变化事件处理完成: {handle}");
            };

            // 最后标记初始化完成
            _isInitializing = false;
            _logger.Debug("KeyMappingViewModel初始化完成");
            
            // 初始化完成后重新同步一次按键配置，确保正确加载完毕
            try 
            {
                SyncConfigToServices();
                _logger.Debug("初始化完成后额外同步配置完成");
            }
            catch (Exception ex)
            {
                _logger.Error("初始化完成后同步配置失败", ex);
            }
        }

        private void SyncConfigToServices()
        {
            try
            {
                // 同步按键列表到服务
                UpdateHotkeyServiceKeyList();

                // 同步按键模式设置
                _lyKeysService.IsHoldMode = !_isSequenceMode;

                // 同步热键设置
                if (_hotkey.HasValue)
                {
                    _hotkeyService.RegisterHotkey(_hotkey.Value, _hotkeyModifiers);
                }

                // 同步其他设置
                _lyKeysService.KeyPressInterval = IsReduceKeyStuck ? LyKeysService.DEFAULT_KEY_PRESS_INTERVAL : 0;
                _lyKeysService.IsEnabled = IsHotkeyEnabled;
                _audioService.Volume = SoundVolume;
                _lyKeysService.KeyInterval = KeyInterval;
                _lyKeysService.IsHoldMode = !IsSequenceMode;

                // 同步热键总开关状态到服务
                if (_hotkeyService != null)
                {
                    _hotkeyService.IsHotkeyControlEnabled = IsHotkeyControlEnabled;
                    _logger.Debug($"已将热键总开关状态({IsHotkeyControlEnabled})同步到HotkeyService");
                }

                // 同步热键总开关状态到浮窗
                if (_floatingViewModel != null)
                {
                    _floatingViewModel.IsHotkeyControlEnabled = IsHotkeyControlEnabled;
                    _logger.Debug($"已将热键总开关状态({IsHotkeyControlEnabled})同步到浮窗");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("同步配置到服务失败", ex);
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                _isInitializing = true;
                var config = AppConfigService.Config;

                // 加载热键设置
                if (config.startKey.HasValue)
                {
                    _hotkey = config.startKey;
                    _hotkeyModifiers = config.startMods;
                    UpdateHotkeyText(_hotkey.Value, config.startMods);
                }

                // 加载窗口配置
                if (!string.IsNullOrEmpty(config.TargetWindowProcessName) &&
                    !string.IsNullOrEmpty(config.TargetWindowTitle))
                {
                    _selectedWindowProcessName = config.TargetWindowProcessName;
                    _selectedWindowClassName = config.TargetWindowClassName ?? string.Empty;
                    _selectedWindowTitle = config.TargetWindowTitle;

                    _logger.Debug($"从配置加载窗口信息 - 进程名: {_selectedWindowProcessName}, " +
                                  $"标题: {_selectedWindowTitle}, 类名: {_selectedWindowClassName}");
                }

                // 加载按键列表和选中状态
                if (config.keys != null)
                {
                    // 验证并修正配置中的按键项
                    foreach (var keyConfig in config.keys)
                    {
                        // 验证坐标类型按键配置
                        if (keyConfig.Type == KeyItemType.Coordinates)
                        {
                            // 坐标类型必须将Code设为null
                            keyConfig.Code = null;
                            
                            // 坐标不能同时为0,0（只针对坐标类型）
                            if (keyConfig.X == 0 && keyConfig.Y == 0)
                            {
                                _logger.Warning($"跳过无效的坐标配置: ({keyConfig.X}, {keyConfig.Y})，坐标不能同时为(0,0)");
                                continue; // 跳过这一项，不添加到KeyList
                            }
                        }
                        // 验证键盘类型按键配置
                        else if (keyConfig.Type == KeyItemType.Keyboard)
                        {
                            // 键盘类型的X和Y应设为0
                            keyConfig.X = 0;
                            keyConfig.Y = 0;

                            // 如果键盘类型但Code为null，则记录警告
                            if (!keyConfig.Code.HasValue)
                            {
                                _logger.Warning($"跳过无效的键盘配置: Code为null");
                            }
                        }
                    }

                    KeyList.Clear();
                    foreach (var keyConfig in config.keys)
                    {
                        KeyItem keyItem;
                        
                        // 根据类型创建不同的KeyItem
                        if (keyConfig.Type == KeyItemType.Keyboard && keyConfig.Code.HasValue)
                        {
                            // 创建键盘按键项
                            keyItem = new KeyItem(keyConfig.Code.Value, _lyKeysService);
                            
                            // 同步到LyKeysService的缓存
                            _lyKeysService.SetKeyIntervalForKey(keyConfig.Code.Value, keyConfig.KeyInterval);
                        }
                        else if (keyConfig.Type == KeyItemType.Coordinates)
                        {
                            // 创建坐标项
                            keyItem = new KeyItem(keyConfig.X, keyConfig.Y, _lyKeysService);
                            
                            // 同步到LyKeysService的坐标缓存
                            _lyKeysService.SetCoordinateInterval(keyConfig.X, keyConfig.Y, keyConfig.KeyInterval);
                        }
                        else
                        {
                            // 跳过无效的配置项
                            _logger.Warning($"跳过无效的按键配置: 类型={keyConfig.Type}, Code={keyConfig.Code}");
                            continue;
                        }
                        
                        // 设置公共属性
                        keyItem.IsSelected = keyConfig.IsSelected;
                        keyItem.KeyInterval = keyConfig.KeyInterval;
                        
                        // 添加事件处理
                        keyItem.SelectionChanged += (s, isSelected) => 
                        {
                            SaveConfig();
                            UpdateHotkeyServiceKeyList();
                        };
                        // 订阅KeyIntervalChanged事件，实时保存配置
                        keyItem.KeyIntervalChanged += (s, newInterval) =>
                        {
                            if (!_isInitializing)
                            {
                                if (keyItem.Type == KeyItemType.Keyboard)
                                {
                                    // 更新LyKeysService中的按键间隔缓存，仅对键盘按键处理
                                    _lyKeysService.SetKeyIntervalForKey(keyItem.KeyCode, newInterval);
                                }
                                else if (keyItem.Type == KeyItemType.Coordinates)
                                {
                                    // 更新LyKeysService中的坐标间隔缓存
                                    _lyKeysService.SetCoordinateInterval(keyItem.X, keyItem.Y, newInterval);
                                }
                                
                                SaveConfig();
                                
                                if (keyItem.Type == KeyItemType.Keyboard)
                                {
                                    _logger.Debug($"按键{keyItem.KeyCode}间隔已更新为{newInterval}ms并保存到配置");
                                }
                                else
                                {
                                    _logger.Debug($"坐标[{keyItem.X},{keyItem.Y}]间隔已更新为{newInterval}ms并保存到配置");
                                }
                            }
                        };
                        
                        KeyList.Add(keyItem);
                    }

                    // 立即同步选中的按键到服务
                    var selectedKeys = KeyList.Where(k => k.IsSelected).ToList();
                    if (selectedKeys.Any())
                    {
                        // 设置按键列表到驱动服务
                        _lyKeysService.SetKeyList(selectedKeys.Select(k => k.KeyCode).ToList());

                        // 将选中的按键及其间隔传递给HotkeyService
                        _hotkeyService.SetKeySequence(
                            selectedKeys.Select(k => new KeyItemSettings
                            {
                                KeyCode = k.KeyCode,
                                Interval = k.KeyInterval
                            }).ToList());

                        _logger.Debug($"已加载按键列表 - 按键数量: {selectedKeys.Count}, 使用独立按键间隔");
                    }
                }

                // 加载其他设置
                // 配置流程说明：
                // 1. 从AppConfig获取配置值，设置到ViewModel的属性中
                // 2. ViewModel的属性setter会自动将值同步到LyKeysService服务层
                // 3. 形成"配置 -> ViewModel -> 服务层"的单向数据流
                // 加载按键间隔
                _keyInterval = config.interval; // 先直接设置字段，避免触发属性变更事件
                _lyKeysService.KeyInterval = config.interval; 
                // 加载按键模式
                SelectedKeyMode = config.keyMode;   
                IsSequenceMode = config.keyMode == 0;   
                // 加载音量设置
                IsSoundEnabled = config.soundEnabled ?? true;
                // 加载降低卡位模式
                IsReduceKeyStuck = config.IsReduceKeyStuck ?? true;
                // 加载浮窗状态
                IsFloatingWindowEnabled = config.UI.FloatingWindow.IsEnabled;
                // 加载自动切换输入法状态
                AutoSwitchToEnglishIME = config.AutoSwitchToEnglishIME ?? true;
                // 加载热键总开关状态
                IsHotkeyControlEnabled = config.isHotkeyControlEnabled ?? true;

                // 加载音量大小
                if (config.SoundVolume.HasValue)
                {
                    _soundVolume = config.SoundVolume.Value;
                    _audioService.Volume = _soundVolume;
                    _logger.Debug($"已加载音量设置: {_soundVolume:P0}");
                }

                _logger.Debug($"配置加载完成 - 模式: {(IsSequenceMode ? "顺序模式" : "按压模式")}, 降低卡位模式: {IsReduceKeyStuck}");

                _isInitializing = false;
            }
            catch (Exception ex)
            {
                _logger.Error("加载配置失败", ex);
                SetDefaultConfiguration();
                _isInitializing = false;
            }
        }

        private void SetDefaultConfiguration()
        {
            // 此函数用于在配置加载失败或不完整时设置ViewModel的默认值
            // 注意：这些默认值应当与AppConfigService.CreateDefaultConfig中相应配置保持一致
            
            // 基本配置
            IsSequenceMode = true;                // 默认顺序模式
            _selectedKeyMode = 0;                 // 0=顺序模式
            _keyInterval = 5;                     // 默认按键间隔
            
            // 功能开关
            IsSoundEnabled = true;                // 默认开启声音
            IsReduceKeyStuck = true;                    // 默认开启降低卡位模式
            AutoSwitchToEnglishIME = true;        // 默认开启自动切换输入法
            IsFloatingWindowEnabled = true;       // 默认开启浮窗
            IsHotkeyControlEnabled = true;        // 默认启用热键总开关
            
            // 设置音量
            _soundVolume = 0.8;                   // 默认音量80%
            if (_audioService != null)
                _audioService.Volume = _soundVolume;
            
            // 设置按键按下时长
            if (_lyKeysService != null)
                _lyKeysService.KeyPressInterval = IsReduceKeyStuck ? LyKeysService.DEFAULT_KEY_PRESS_INTERVAL : 0;
            
            // 重置当前状态
            IsHotkeyEnabled = false;              // 默认未启动按键
            HotkeyStatus = "初始化完成";           // 重置状态提示
            
            _logger.Debug("已应用SetDefaultConfiguration()函数中的默认配置");
        }

        private void InitializeCommands()
        {
            AddKeyCommand = new RelayCommand(AddKey, () => CanAddKey());
            AddCoordinateCommand = new RelayCommand(AddCoordinate, () => CanAddCoordinate());
        }

        private void InitializeHotkeyStatus()
        {
            IsHotkeyEnabled = false;
            HotkeyStatus = "初始化完成";
        }

        private void SubscribeToEvents()
        {
            // 订阅热键服务事件
            if (_hotkeyService != null)
            {
                _hotkeyService.SequenceModeStarted += () =>
                {
                    // 直接设置字段而非属性，避免触发额外的更新
                    _isExecuting = true;
                    _mainViewModel.UpdateStatusMessage("已开始按键序列", false);
                    
                    // 触发属性变更通知
                    OnPropertyChanged(nameof(IsExecuting));
                    OnPropertyChanged(nameof(IsNotExecuting));
                    
                    // 一次性更新浮窗状态
                    UpdateFloatingStatus();
                };
                _hotkeyService.SequenceModeStopped += () =>
                {
                    // 只需更新消息，避免重复操作
                    // IsExecuting和更新浮窗已在StopKeyMapping中集中处理
                    if (_isExecuting)
                    {
                        _logger.Debug("收到序列停止事件，但UI仍显示运行中状态，进行状态同步");
                        _mainViewModel.UpdateStatusMessage("已停止按键序列", false);
                        
                        // 确保UI状态一致性，即使StopKeyMapping已被调用，也需要强制更新一次状态
                        _isExecuting = false;
                        OnPropertyChanged(nameof(IsExecuting));
                        OnPropertyChanged(nameof(IsNotExecuting));
                        
                        // 强制更新浮窗状态，不受节流控制
                        UpdateFloatingStatus(true);
                        _logger.Debug("已强制更新浮窗状态，确保显示已停止");
                    }
                    else
                    {
                        _logger.Debug("收到序列停止事件，UI状态已是停止状态");
                        _mainViewModel.UpdateStatusMessage("已停止按键序列", false);
                    }
                };
            }

            // 订阅状态变化事件
            PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(IsHotkeyEnabled) && IsSoundEnabled)
                {
                    if (IsHotkeyEnabled)
                        await _audioService.PlayStartSound();
                    else
                        await _audioService.PlayStopSound();
                }
            };

            // 订阅按键间隔变化事件
            _lyKeysService.KeyIntervalChanged += (s, interval) => OnPropertyChanged(nameof(KeyInterval));

            // 订阅按键项事件
            SubscribeToKeyItemEvents();
        }

        // 设置当前按键
        public void SetCurrentKey(LyKeysCode keyCode)
        {
            _currentKey = keyCode;
            CurrentKeyText = _lyKeysService.GetKeyDescription(keyCode);
            // 通知命令状态更新
            CommandManager.InvalidateRequerySuggested();
            _logger.Debug("SetCurrentKey", $"设置当前按键: {keyCode} | {CurrentKeyText}");
        }

        // 设置热键
        public void SetHotkey(LyKeysCode keyCode, ModifierKeys modifiers)
        {
            // 检查是否与当前按键序列冲突
            if (IsKeyInList(keyCode))
            {
                _logger.Warning($"热键({keyCode})与当前按键序列冲突，无法设置");
                _mainViewModel.UpdateStatusMessage("热键与按键序列冲突，请选择其他键", true);
                return;
            }

            _hotkey = keyCode;
            _hotkeyModifiers = modifiers;
            UpdateHotkeyText(keyCode, modifiers);

            // 保存到配置
            AppConfigService.UpdateConfig(config =>
            {
                config.startKey = keyCode;
                config.startMods = modifiers;
                config.stopKey = keyCode; // 保持一致
                config.stopMods = modifiers; // 保持一致
            });

            _logger.Debug($"已设置热键: {keyCode}, 修饰键: {modifiers}");

            // 注册热键
            if (_hotkeyService.RegisterHotkey(keyCode, modifiers))
            {
                _logger.Debug("热键注册成功");
                _mainViewModel.UpdateStatusMessage("热键设置成功", false);
            }
            else
            {
                _logger.Error("热键注册失败");
                _mainViewModel.UpdateStatusMessage("热键设置失败", true);
            }
        }

        // 更新热键文本
        private void UpdateHotkeyText(LyKeysCode keyCode, ModifierKeys modifiers)
        {
            var sb = new StringBuilder();

            // 添加修饰键
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl + ");
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt + ");
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift + ");
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win + ");

            // 添加主键
            sb.Append(_lyKeysService.GetKeyDescription(keyCode));

            HotkeyText = sb.ToString();
        }

        // 检查是否可以添加按键
        private bool CanAddKey()
        {
            // 移除了重复键检查(!IsKeyInList)，现在只检查有没有选中按键
            return _currentKey.HasValue;
        }

        // 添加按键
        private void AddKey()
        {
            try
            {
                if (!_currentKey.HasValue)
                {
                    _logger.Warning("没有有效的按键可添加");
                    _mainViewModel.UpdateStatusMessage("没有有效的按键可添加", true);
                    return;
                }

                var keyCode = _currentKey.Value;
                if (!_lyKeysService.IsValidLyKeysCode(keyCode))
                {
                    _logger.Warning($"无效的按键码: {_lyKeysService.GetKeyDescription(keyCode)}");
                    _mainViewModel.UpdateStatusMessage($"无效的按键码: {_lyKeysService.GetKeyDescription(keyCode)}", true);
                    return;
                }

                if (IsHotkeyConflict(keyCode))
                {
                    _logger.Warning($"按键与热键冲突: {_lyKeysService.GetKeyDescription(keyCode)}");
                    _mainViewModel.UpdateStatusMessage($"按键 {_lyKeysService.GetKeyDescription(keyCode)} 与热键冲突，请选择其他按键",
                        true);
                    return;
                }

                var newKey = new KeyItem(keyCode, _lyKeysService);
                newKey.KeyInterval = _keyInterval; // 使用当前默认间隔值
                newKey.SelectionChanged += (s, isSelected) => 
                {
                    SaveConfig();
                    UpdateHotkeyServiceKeyList();
                };
                // 订阅KeyIntervalChanged事件，实时保存配置
                newKey.KeyIntervalChanged += (s, newInterval) =>
                {
                    if (!_isInitializing)
                    {
                        // 更新LyKeysService中的按键间隔缓存
                        _lyKeysService.SetKeyIntervalForKey(newKey.KeyCode, newInterval);
                        
                        SaveConfig();
                        _logger.Debug($"按键{newKey.KeyCode}的间隔已更新为{newInterval}ms并保存到配置");
                    }
                };

                KeyList.Add(newKey);
                
                // 添加：更新HotkeyService的按键列表，确保添加按键后立即更新循环
                UpdateHotkeyServiceKeyList();
                
                SaveConfig();

                // 重置输入状态
                _mainViewModel.UpdateStatusMessage($"已添加按键: {_lyKeysService.GetKeyDescription(keyCode)}", false);
                _logger.Debug($"已添加按键: {keyCode}");
                _currentKey = null;
                // 通知UI显示已更新
                OnPropertyChanged(nameof(CurrentKeyText));
            }
            catch (Exception ex)
            {
                _logger.Error("添加按键时发生异常", ex);
                _mainViewModel.UpdateStatusMessage($"添加按键失败: {ex.Message}", true);
            }
        }

        /// <summary>
        /// 删除指定的按键
        /// </summary>
        /// <param name="keyItem">要删除的按键项</param>
        public void DeleteKey(KeyItem keyItem)
        {
            if (keyItem == null)
                throw new ArgumentNullException(nameof(keyItem));

            try
            {
                // 从列表中移除
                KeyList.Remove(keyItem);
                _logger.Debug($"删除按键: {keyItem.KeyCode}");

                // 如果是当前选中的项，清除选中状态
                if (SelectedKeyItem == keyItem) SelectedKeyItem = null;

                // 更新HotkeyService的按键列表
                UpdateHotkeyServiceKeyList();

                // 实时保存按键列表
                if (!_isInitializing)
                {
                    SaveConfig();
                    _logger.Debug("配置已保存");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("删除按键时发生异常", ex);
                throw new InvalidOperationException("删除按键失败", ex);
            }
        }

        // 更新HotkeyService按键列表的辅助方法
        private void UpdateHotkeyServiceKeyList()
        {
            try
            {
                // 准备设置序列的按键列表
                var selectedItems = KeyList.Where(k => k.IsSelected).ToList();
                
                if (selectedItems.Count > 0)
                {
                    _logger.Debug($"更新热键服务的按键列表 - 总按键数: {selectedItems.Count}");

                    // 创建包含所有类型的设置列表
                    var keySettings = selectedItems.Select(k => 
                    {
                        if (k.Type == KeyItemType.Keyboard)
                        {
                            return KeyItemSettings.CreateKeyboard(k.KeyCode, k.KeyInterval);
                        }
                        else // KeyItemType.Coordinates
                        {
                            return KeyItemSettings.CreateCoordinates(k.X, k.Y, k.KeyInterval);
                        }
                    }).ToList();

                    // 将设置列表传递给HotkeyService
                    _hotkeyService.SetKeySequence(keySettings);

                    // 分离键盘按键和坐标设置
                    var keyboardKeys = selectedItems
                        .Where(k => k.Type == KeyItemType.Keyboard)
                        .Select(k => k.KeyCode)
                        .ToList();
                    
                    var coordinateSettings = selectedItems
                        .Where(k => k.Type == KeyItemType.Coordinates)
                        .Select(k => (k.X, k.Y, k.KeyInterval))
                        .ToList();

                    // 传递给LyKeysService分离的列表
                    _lyKeysService.SetKeyItemsListWithCoordinates(keyboardKeys, coordinateSettings);

                    _logger.Debug($"按键列表已更新 - 键盘按键: {keyboardKeys.Count}, 坐标点: {coordinateSettings.Count}");
                }
                else
                {
                    _logger.Debug("没有选择任何按键，设置空列表");
                    _lyKeysService.SetKeyList(new List<LyKeysCode>());
                    _hotkeyService.SetKeySequence(new List<KeyItemSettings>());
                }
            }
            catch (Exception ex)
            {
                _logger.Error("更新热键服务按键列表失败", ex);
            }
        }

        // 保存配置
        public void SaveConfig()
        {
            try
            {
                // 获取所有按键和它们的状态，根据类型创建不同的配置
                var keyConfigs = new List<KeyConfig>();
                
                foreach (var item in KeyList)
                {
                    KeyConfig keyConfig;
                    
                    if (item.Type == KeyItemType.Keyboard)
                    {
                        // 创建键盘按键配置
                        keyConfig = new KeyConfig(item.KeyCode, item.IsSelected)
                        {
                            KeyInterval = item.KeyInterval,
                            Type = KeyItemType.Keyboard,
                            X = 0,  // 确保键盘类型的X和Y为0
                            Y = 0
                        };
                    }
                    else // KeyItemType.Coordinates
                    {
                        // 确保坐标不能同时为0
                        int x = item.X;
                        int y = item.Y;
                        
                        if (x == 0 && y == 0)
                        {
                            _logger.Warning($"修正无效的坐标配置: ({x}, {y}) => (1, 1)");
                            x = 1;
                            y = 1;
                        }
                        
                        // 创建坐标配置
                        keyConfig = new KeyConfig(x, y, item.IsSelected)
                        {
                            KeyInterval = item.KeyInterval,
                            Type = KeyItemType.Coordinates,
                            Code = null  // 确保坐标类型的Code为null
                        };
                    }
                    
                    keyConfigs.Add(keyConfig);
                }

                // 检查热键冲突（仅需检查键盘类型的按键）
                if (_hotkey.HasValue)
                {
                    var keyboardConfigs = keyConfigs.Where(k => k.Type == KeyItemType.Keyboard && k.Code.HasValue);
                    if (keyboardConfigs.Any(k => k.Code == _hotkey.Value))
                    {
                        _mainViewModel.UpdateStatusMessage("热键与按键列表存在冲突，请修改后再保存", true);
                        return;
                    }
                }

                // 获取当前配置
                var config = AppConfigService.Config;

                // 更新音量设置
                config.SoundVolume = SoundVolume;

                // 只更新需要保存的字段
                var configChanged = false;

                // 检查并更新热键配置
                if (!config.startKey.Equals(_hotkey) || config.startMods != _hotkeyModifiers)
                {
                    config.startKey = _hotkey;
                    config.startMods = _hotkeyModifiers;
                    config.stopKey = _hotkey;
                    config.stopMods = _hotkeyModifiers;
                    configChanged = true;
                }

                // 检查并更新按键列表
                if (!AreKeyConfigsEqual(config.keys, keyConfigs))
                {
                    config.keys = keyConfigs;
                    configChanged = true;
                }

                // 检查并更新其他设置
                if (config.keyMode != SelectedKeyMode)
                {
                    config.keyMode = SelectedKeyMode;
                    configChanged = true;
                }

                if (config.interval != KeyInterval)
                {
                    config.interval = KeyInterval;
                    configChanged = true;
                }

                if (config.soundEnabled != IsSoundEnabled)
                {
                    config.soundEnabled = IsSoundEnabled;
                    configChanged = true;
                }

                if (config.IsReduceKeyStuck != IsReduceKeyStuck)
                {
                    config.IsReduceKeyStuck = IsReduceKeyStuck;
                    configChanged = true;
                }

                if (config.UI.FloatingWindow.IsEnabled != IsFloatingWindowEnabled)
                {
                    config.UI.FloatingWindow.IsEnabled = IsFloatingWindowEnabled;
                    configChanged = true;
                }

                if (config.AutoSwitchToEnglishIME != AutoSwitchToEnglishIME)
                {
                    config.AutoSwitchToEnglishIME = AutoSwitchToEnglishIME;
                    configChanged = true;
                }

                // 检查并更新热键控制开关
                if (config.isHotkeyControlEnabled != IsHotkeyControlEnabled)
                {
                    config.isHotkeyControlEnabled = IsHotkeyControlEnabled;
                    configChanged = true;
                }

                // 检查音量是否变化
                if (Math.Abs(config.SoundVolume.GetValueOrDefault(_soundVolume) - _soundVolume) > 0.001)
                {
                    configChanged = true;
                }

                // 只有在配置发生变化时才保存
                if (configChanged)
                {
                    AppConfigService.SaveConfig();
                    _logger.Debug("配置已保存");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("保存配置失败", ex);
                System.Windows.MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // 检查两个按键配置列表是否相等
        private bool AreKeyConfigsEqual(List<KeyConfig> list1, List<KeyConfig> list2)
        {
            if (list1 == null || list2 == null)
                return list1 == list2;

            if (list1.Count != list2.Count)
                return false;

            for (var i = 0; i < list1.Count; i++)
                if (list1[i].Code != list2[i].Code ||
                    list1[i].IsSelected != list2[i].IsSelected ||
                    list1[i].KeyInterval != list2[i].KeyInterval ||
                    list1[i].Type != list2[i].Type ||
                    list1[i].X != list2[i].X ||
                    list1[i].Y != list2[i].Y)
                    return false;

            return true;
        }

        // 启动按键映射
        public void StartKeyMapping()
        {
            if (!IsExecuting)
                try
                {
                    _logger.Debug("开始启动按键映射");

                    // 检查是否选择了目标窗口
                    if (SelectedWindowHandle == IntPtr.Zero)
                    {
                        // 区分未选择窗口和进程未运行的情况
                        if (!string.IsNullOrEmpty(_selectedWindowProcessName) && SelectedWindowTitle.Contains("进程未运行"))
                        {
                            _logger.Warning($"目标窗口进程未运行: {_selectedWindowProcessName}");
                            _mainViewModel.UpdateStatusMessage($"目标应用程序未运行: {_selectedWindowProcessName}", true);
                        }
                        
                        IsHotkeyEnabled = false;
                        IsExecuting = false;
                        return;
                    }

                    _hotkeyService.TargetWindowHandle = SelectedWindowHandle;

                    // 选择的按键列表
                    var selectedKeys = KeyList.Where(k => k.IsSelected).ToList();
                    if (!selectedKeys.Any())
                    {
                        _logger.Warning("没有选择任何按键");
                        _mainViewModel.UpdateStatusMessage("请至少选择一个按键", true);
                        IsHotkeyEnabled = false;
                        IsExecuting = false;
                        return;
                    }

                    // 设置按键列表到驱动服务
                    _lyKeysService.SetKeyList(selectedKeys.Select(k => k.KeyCode).ToList());

                    // 将选中的按键及其间隔传递给HotkeyService
                    _hotkeyService.SetKeySequence(
                        selectedKeys.Select(k => new KeyItemSettings
                        {
                            KeyCode = k.KeyCode,
                            Interval = k.KeyInterval
                        }).ToList());

                    // 设置按键模式并启动
                    _lyKeysService.IsHoldMode = !IsSequenceMode;
                    _hotkeyService.StartSequence();

                    // 更新执行状态
                    IsExecuting = true;
                    UpdateFloatingStatus();

                    _logger.Debug($"按键映射已启动 - 模式: {(SelectedKeyMode == 1 ? "按压模式" : "顺序模式")}, " +
                                  $"按键数量: {selectedKeys.Count}, 目标窗口: {SelectedWindowTitle}");
                }
                catch (Exception ex)
                {
                    _logger.Error("启动按键映射失败", ex);
                    StopKeyMapping();
                    _mainViewModel.UpdateStatusMessage("启动按键映射失败", true);
                }
        }

        // 停止按键映射
        public void StopKeyMapping()
        {
            try
            {
                if (_lyKeysService == null) return;

                _logger.Debug($"开始停止按键映射，当前模式: {(SelectedKeyMode == 1 ? "按压模式" : "顺序模式")}, 当前执行状态: {_isExecuting}");

                // 标记状态变更，但暂不触发更新
                bool wasExecuting = IsExecuting;
                _logger.Debug($"当前执行状态标记: wasExecuting={wasExecuting}");

                // 先更新UI状态变量，但不触发UI更新和事件
                _isExecuting = false;
                IsHotkeyEnabled = false;
                _logger.Debug("UI状态变量已更新: _isExecuting=false, IsHotkeyEnabled=false");

                // 再停止热键服务
                _logger.Debug("即将调用热键服务的StopSequence()方法");
                _hotkeyService?.StopSequence();
                _logger.Debug("热键服务StopSequence()调用完成");

                // 然后停止驱动服务，但保留模式设置
                _lyKeysService.IsEnabled = false;
                _logger.Debug("按键服务已禁用: _lyKeysService.IsEnabled=false");

                // 最后在已执行状态发生变化时一次性更新UI
                if (wasExecuting)
                {
                    _logger.Debug("检测到之前的执行状态为true，即将更新UI");
                    // 通知属性变更
                    OnPropertyChanged(nameof(IsExecuting));
                    OnPropertyChanged(nameof(IsNotExecuting));
                    
                    // 一次性更新浮窗状态 - 使用强制更新确保状态立即生效
                    UpdateFloatingStatus(true);
                    _logger.Debug("UI状态更新完成：属性变更通知已发送，浮窗状态已强制更新");
                }
                else
                {
                    _logger.Debug("之前的执行状态为false，跳过UI更新");
                }

                _logger.Debug("按键映射已停止");
            }
            catch (Exception ex)
            {
                _logger.Error("停止按键映射失败", ex);
                // 确保状态一致性
                _isExecuting = false;
                IsHotkeyEnabled = false;
                OnPropertyChanged(nameof(IsExecuting));
                OnPropertyChanged(nameof(IsNotExecuting));
                UpdateFloatingStatus(true); // 使用强制更新确保异常情况下也能更新状态
                _logger.Debug("异常处理：已重置所有状态并强制更新UI");
            }
        }

        // 设置按压模式
        public void SetHoldMode(bool isHold)
        {
            _lyKeysService.IsHoldMode = isHold;
        }

        // 检查按键是否已在列表中
        private bool IsKeyInList(LyKeysCode keyCode)
        {
            // 仅检查键盘类型的按键
            return KeyList.Any(k => k.Type == KeyItemType.Keyboard && k.KeyCode.Equals(keyCode));
        }

        // 开始热键按下事件处理
        private void OnStartHotkeyPressed()
        {
            _logger.Debug("热键已按下");
            
            // 确保UI模式与服务模式保持一致
            bool isCurrentModeSequence = _isSequenceMode;
            bool isServiceModeHold = _lyKeysService.IsHoldMode;
            
            // 如果发现不一致，记录日志
            if (isCurrentModeSequence == isServiceModeHold)
            {
                _logger.Warning($"模式状态不一致 - UI模式:{(isCurrentModeSequence ? "顺序" : "按压")}, 服务模式:{(isServiceModeHold ? "按压" : "顺序")}");
                // 使用服务模式作为最终依据
                _isSequenceMode = !isServiceModeHold;
            }
            
            if (_isSequenceMode)
            {
                if (_isExecuting)
                {
                    // 如果已在执行中，则停止
                    _logger.Debug("顺序模式 - 热键再次按下，停止序列");
                    StopKeyMapping();
                }
                else
                {
                    // 否则开始执行
                    _logger.Debug("顺序模式 - 热键按下，开始序列");
                    StartKeyMapping();
                }
            }
            else
            {
                // 按压模式下，按下热键开始执行
                _logger.Debug("按压模式 - 热键按下，开始序列");
                StartKeyMapping();
            }
        }

        // 开始热键释放事件处理
        private void OnStartHotkeyReleased()
        {
            _logger.Debug("热键已释放");
            if (!_isSequenceMode)
            {
                // 在按压模式下，释放热键时停止执行
                _logger.Debug("按压模式 - 热键释放，停止序列");
                StopKeyMapping();
            }
        }

        // 获取热键服务
        public HotkeyService GetHotkeyService()
        {
            return _hotkeyService;
        }

        // 为拖拽操作提供的公共方法，用于更新HotkeyService中的按键列表
        public void SyncKeyListToHotkeyService()
        {
            UpdateHotkeyServiceKeyList();
        }

        // 添加热键冲突检测方法
        public bool IsHotkeyConflict(LyKeysCode keyCode)
        {
            try
            {
                var isStartConflict = _hotkey.HasValue && keyCode.Equals(_hotkey.Value);

                if (isStartConflict)
                {
                    _logger.Debug(
                        $"检测到热键冲突 - 按键: {keyCode}, 启动键冲突: {isStartConflict}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.Error("检查热键冲突时发生异常", ex);
                return false;
            }
        }

        // 为现有的KeyList项添加事件订阅
        private void SubscribeToKeyItemEvents()
        {
            foreach (var keyItem in KeyList)
                keyItem.SelectionChanged += (s, isSelected) =>
                {
                    SaveConfig();
                    UpdateHotkeyServiceKeyList();
                };
        }

        private void LoadWindowConfig()
        {
            try
            {
                _logger.Debug($"开始加载窗口配置 - 进程名: {_selectedWindowProcessName}, 标题: {_selectedWindowTitle}");

                // 如果没有保存的窗口信息，直接返回
                if (string.IsNullOrEmpty(_selectedWindowProcessName))
                {
                    _logger.Debug("没有保存的窗口进程信息，跳过加载");
                    return;
                }

                var windows = FindWindowsByProcessName(_selectedWindowProcessName, _selectedWindowTitle);
                if (windows != null && windows.Count > 0)
                {
                    // 找到匹配的窗口，使用第一个匹配的窗口
                    var window = windows[0];
                    UpdateSelectedWindow(window.Handle, window.Title, window.ClassName, window.ProcessName);
                    _logger.Debug($"已找到并更新窗口信息 - 句柄: {window.Handle}, 标题: {window.Title}");
                }
                else
                {
                    _logger.Warning($"未找到进程 {_selectedWindowProcessName} 的窗口");

                    // 清除窗口句柄但保留进程信息
                    SelectedWindowHandle = IntPtr.Zero;
                    SelectedWindowTitle = $"{_selectedWindowTitle} (进程未运行)";

                    // 同步窗口句柄为空到热键服务
                    _hotkeyService.TargetWindowHandle = IntPtr.Zero;

                    // 启动定时检查
                    StartWindowCheck();
                    _logger.Debug($"已清除窗口句柄，启动定时检查 - 进程名: {_selectedWindowProcessName}, 标题: {_selectedWindowTitle} (进程未运行)");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("加载窗口配置时发生异常", ex);
                ClearSelectedWindow();
            }
        }

        private List<WindowInfo> FindWindowsByProcessName(string processName, string targetTitle = null)
        {
            var result = new List<WindowInfo>();
            if (string.IsNullOrEmpty(processName) && string.IsNullOrEmpty(targetTitle)) return result;

            _logger.Debug($"正在查找窗口 - 进程名: {processName}, 目标标题: {targetTitle}");

            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                {
                    _logger.Debug($"未找到进程: {processName}");
                    return result;
                }

                foreach (var process in processes)
                    try
                    {
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            var title = GetWindowTitle(process.MainWindowHandle);
                            var className = GetWindowClassName(process.MainWindowHandle);

                            // 如果指定了目标标题，则进行匹配
                            if (!string.IsNullOrEmpty(targetTitle))
                            {
                                if (title.Contains(targetTitle, StringComparison.OrdinalIgnoreCase))
                                {
                                    result.Add(new WindowInfo(process.MainWindowHandle, title, className,
                                        process.ProcessName));
                                    _logger.Debug($"找到匹配窗口 - 句柄: {process.MainWindowHandle}, 标题: {title}");
                                }
                            }
                            else
                            {
                                result.Add(new WindowInfo(process.MainWindowHandle, title, className,
                                    process.ProcessName));
                                _logger.Debug($"找到窗口 - 句柄: {process.MainWindowHandle}, 标题: {title}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"处理进程窗口时发生异常: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
            }
            catch (Exception ex)
            {
                _logger.Error($"查找窗口时发生异常: {ex.Message}");
            }

            if (result.Count == 0) _logger.Debug($"未找到目标窗口 - 进程: {processName}, 目标标题: {targetTitle}");

            return result;
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            var title = new StringBuilder(256);
            GetWindowText(hWnd, title, title.Capacity);
            return title.ToString().Trim();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private string GetWindowClassName(IntPtr hWnd)
        {
            var className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            return className.ToString().Trim();
        }

        private void WindowCheckTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedWindowProcessName) || string.IsNullOrEmpty(SelectedWindowTitle)) return;

            try
            {
                lock (_windowCheckLock)
                {
                    // 获取原始标题（移除状态信息）
                    var originalTitle = SelectedWindowTitle.Split(new[] { " (句柄:", " (进程未运行)", " (未找到匹配窗口)" },
                        StringSplitOptions.None)[0];

                    var windows = FindWindowsByProcessName(SelectedWindowProcessName, originalTitle);

                    // 添加对Application.Current的空检查
                    if (System.Windows.Application.Current != null)
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (windows.Any())
                            {
                                var targetWindow = windows.First();
                                var needsUpdate = false;

                                // 检查句柄是否变化
                                if (targetWindow.Handle != SelectedWindowHandle)
                                {
                                    SelectedWindowHandle = targetWindow.Handle;
                                    needsUpdate = true;
                                    _logger.Debug($"检测到窗口句柄变化: {targetWindow.Handle}");
                                }

                                // 检查类名是否变化
                                if (targetWindow.ClassName != SelectedWindowClassName)
                                {
                                    SelectedWindowClassName = targetWindow.ClassName;
                                    needsUpdate = true;
                                }

                                // 如果需要更新，则更新标题和配置
                                if (needsUpdate)
                                {
                                    SelectedWindowTitle = $"{targetWindow.Title} (句柄: {targetWindow.Handle.ToInt64()})";

                                    // 更新配置
                                    AppConfigService.UpdateConfig(config =>
                                    {
                                        config.TargetWindowClassName = targetWindow.ClassName;
                                        config.TargetWindowProcessName = targetWindow.ProcessName;
                                        config.TargetWindowTitle = targetWindow.Title;
                                    });

                                    _logger.Info(
                                        $"已更新窗口信息 - 句柄: {targetWindow.Handle.ToInt64()}, 类名: {targetWindow.ClassName}, 进程名: {targetWindow.ProcessName}, 标题: {targetWindow.Title}");
                                }
                            }
                            else if (SelectedWindowHandle != IntPtr.Zero)
                            {
                                // 目标进程已关闭，清除窗口句柄但保留进程信息
                                SelectedWindowHandle = IntPtr.Zero;
                                SelectedWindowTitle = $"{originalTitle} (进程未运行)";
                                
                                // 同步窗口句柄到热键服务
                                _hotkeyService.TargetWindowHandle = IntPtr.Zero;
                                
                                // 如果正在执行，停止按键映射
                                if (IsExecuting)
                                {
                                    StopKeyMapping();
                                    _mainViewModel.UpdateStatusMessage($"目标应用程序已关闭，已停止按键映射", false);
                                }
                                
                                _logger.Warning($"进程 {SelectedWindowProcessName} 已关闭，已清除窗口句柄");
                            }
                        });
                    else
                        // 直接在当前线程处理窗口更新
                        _logger.Debug("Application.Current为空，跳过窗口状态更新");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("检查窗口状态时发生异常", ex);
            }
        }

        private void StartWindowCheck()
        {
            if (_windowCheckTimer == null)
            {
                _windowCheckTimer = new System.Timers.Timer(5000); // 5秒
                _windowCheckTimer.Elapsed += WindowCheckTimer_Elapsed;
            }

            _windowCheckTimer.Start();
            _logger.Debug("开始定时检查窗口状态");
        }

        private void StopWindowCheck()
        {
            _windowCheckTimer?.Stop();
            _logger.Debug("停止定时检查窗口状态");
        }

        // 在析构函数或Dispose方法中清理定时器
        ~KeyMappingViewModel()
        {
            _windowCheckTimer?.Dispose();
            _activeWindowCheckTimer?.Dispose();
        }

        // 添加活动窗口检查方法
        private void ActiveWindowCheckTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // 如果没有选择窗口，则不需要检查活动状态
                if (SelectedWindowHandle == IntPtr.Zero)
                {
                    // 添加对Application.Current的空检查
                    if (System.Windows.Application.Current != null)
                    {
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // 更新为非活动状态
                            if (IsTargetWindowActive)
                            {
                                IsTargetWindowActive = false;
                                _hotkeyService.IsTargetWindowActive = false;
                                
                                // 如果正在执行，则停止
                                if (IsExecuting)
                                {
                                    _lyKeysService.EmergencyStop();
                                    StopKeyMapping();
                                    // 不显示消息，因为窗口状态检查器会处理
                                }
                            }
                        }), DispatcherPriority.Background);
                    }
                    else
                    {
                        // 直接在当前线程更新状态
                        if (IsTargetWindowActive)
                        {
                            IsTargetWindowActive = false;
                            _hotkeyService.IsTargetWindowActive = false;
                            
                            // 如果正在执行，则停止
                            if (IsExecuting)
                            {
                                _lyKeysService.EmergencyStop();
                                StopKeyMapping();
                            }
                        }
                    }

                    return;
                }

                // 如果选择了窗口，则检查是否是当前活动窗口
                var activeWindow = GetForegroundWindow();
                var isActive = activeWindow == SelectedWindowHandle;

                // 添加对Application.Current的空检查
                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsTargetWindowActive = isActive;
                        _hotkeyService.IsTargetWindowActive = isActive;
                        
                        // 只在窗口变为非活动状态时停止按键映射
                        if (!isActive && IsExecuting)
                        {
                            _lyKeysService.EmergencyStop(); // 使用紧急停止
                            StopKeyMapping();
                            UpdateFloatingStatus(); // 更新浮窗状态
                            _logger.Debug("目标窗口切换为非活动状态，停止按键映射，已更新浮窗状态");
                        }
                        else if (isActive && IsExecuting)
                        {
                            // 如果窗口重新激活，且之前在执行中，更新浮窗状态
                            UpdateFloatingStatus();
                            _logger.Debug("目标窗口重新激活，更新浮窗状态");
                        }
                    }), DispatcherPriority.Background);
                }
                else
                {
                    // 直接在当前线程更新状态
                    IsTargetWindowActive = isActive;
                    _hotkeyService.IsTargetWindowActive = isActive;
                    
                    // 只在窗口变为非活动状态时停止按键映射
                    if (!isActive && IsExecuting)
                    {
                        _lyKeysService.EmergencyStop(); // 使用紧急停止
                        StopKeyMapping();
                        _logger.Debug("目标窗口切换为非活动状态，停止按键映射");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("检查活动窗口状态时发生异常", ex);
            }
        }

        private class WindowInfo
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; }
            public string ClassName { get; set; }
            public string ProcessName { get; set; }

            public WindowInfo(IntPtr handle, string title, string className, string processName)
            {
                Handle = handle;
                Title = title;
                ClassName = className;
                ProcessName = processName;
            }
        }

        private bool CanAddCoordinate()
        {
            // 检查X和Y坐标是否已设置
            if (!_currentX.HasValue || !_currentY.HasValue)
                return false;
                
            // 检查坐标是否为(0,0)
            // 注意：这里是添加坐标按钮，所以一定是坐标类型
            if (_currentX.Value == 0 && _currentY.Value == 0)
                return false;
                
            return true;
        }
        
        // 添加坐标点
        private void AddCoordinate()
        {
            try
            {
                // 验证输入
                if (!_currentX.HasValue || !_currentY.HasValue)
                {
                    _logger.Warning("X或Y坐标没有填写，无法添加坐标");
                    _mainViewModel.UpdateStatusMessage("请填写X和Y坐标", true);
                    return;
                }

                // 确保坐标不能同时为0
                int x = _currentX.Value;
                int y = _currentY.Value;
                
                // 坐标类型的坐标不能同时为(0,0)
                if (x == 0 && y == 0)
                {
                    _logger.Warning("坐标不能同时为(0,0)，添加操作被拒绝");
                    _mainViewModel.UpdateStatusMessage("坐标不能同时为(0,0)，请修改后再添加", true);
                    return; // 直接返回，不添加坐标
                }

                // 创建坐标类型的KeyItem
                var newCoordinate = new KeyItem(x, y, _lyKeysService);
                newCoordinate.KeyInterval = _keyInterval; // 使用当前默认间隔值
                
                // 添加事件订阅
                newCoordinate.SelectionChanged += (s, isSelected) => 
                {
                    SaveConfig();
                    UpdateHotkeyServiceKeyList();
                };
                newCoordinate.KeyIntervalChanged += (s, newInterval) =>
                {
                    if (!_isInitializing)
                    {
                        // 更新LyKeysService中的坐标间隔缓存
                        _lyKeysService.SetCoordinateInterval(newCoordinate.X, newCoordinate.Y, newInterval);
                        
                        SaveConfig();
                        _logger.Debug($"坐标[{newCoordinate.X},{newCoordinate.Y}]的间隔已更新为{newInterval}ms并保存到配置");
                    }
                };
                
                // 添加到列表
                KeyList.Add(newCoordinate);
                
                // 更新HotkeyService的按键列表
                UpdateHotkeyServiceKeyList();
                
                // 保存配置
                SaveConfig();
                
                // 提示和日志
                _mainViewModel.UpdateStatusMessage($"已添加坐标: ({x}, {y})", false);
                _logger.Debug($"已添加坐标: ({x}, {y})");
                
                // 重置输入状态
                _currentX = null;
                _currentY = null;
                OnPropertyChanged(nameof(CurrentX));
                OnPropertyChanged(nameof(CurrentY));
            }
            catch (Exception ex)
            {
                _logger.Error("添加坐标时发生异常", ex);
                _mainViewModel.UpdateStatusMessage($"添加坐标失败: {ex.Message}", true);
            }
        }

        // X坐标
        public int? CurrentX
        {
            get => _currentX;
            set => SetProperty(ref _currentX, value);
        }
        
        // Y坐标
        public int? CurrentY
        {
            get => _currentY;
            set => SetProperty(ref _currentY, value);
        }
    }
}