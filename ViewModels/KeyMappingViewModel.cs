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

// 定义KeyItemSettings结构用于传递按键设置
public class KeyItemSettings
{
    public LyKeysCode KeyCode { get; set; }
    public int Interval { get; set; } = 5;
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
        private string _startHotkeyText = string.Empty;
        private string _stopHotkeyText = string.Empty;
        private LyKeysCode? _startHotkey;
        private LyKeysCode? _stopHotkey;
        private int _selectedKeyMode;
        private ModifierKeys _startModifiers = ModifierKeys.None;
        private ModifierKeys _stopModifiers = ModifierKeys.None;
        private readonly HotkeyService _hotkeyService;
        private bool _isHotkeyEnabled;
        private string _hotkeyStatus;
        private bool _isSequenceMode = true; // 默认为顺序模式
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly MainViewModel _mainViewModel;
        private MainWindow? _mainWindow;
        private bool _isSoundEnabled = true;
        private readonly AudioService _audioService;
        private bool _isGameMode = true; // 默认开启
        private bool _isInitializing = true; // 添加一个标志来标识是否在初始化
        private bool _isExecuting = false; // 添加执行状态标志
        private bool _isFloatingWindowEnabled;
        private bool _autoSwitchToEnglishIME = true; // 默认开启自动切换输入法
        private FloatingStatusWindow _floatingWindow;
        private FloatingStatusViewModel _floatingViewModel;
        private KeyItem? _selectedKeyItem;
        private KeyboardLayoutViewModel _keyboardLayoutViewModel;
        private string _selectedWindowTitle = "未选择窗口";
        private IntPtr _selectedWindowHandle = IntPtr.Zero;
        private string _selectedWindowClassName = string.Empty;
        private string _selectedWindowProcessName = string.Empty;
        private System.Timers.Timer? _windowCheckTimer;
        private readonly object _windowCheckLock = new object();
        private bool _isTargetWindowActive;
        private readonly System.Timers.Timer _activeWindowCheckTimer;
        private const int ACTIVE_WINDOW_CHECK_INTERVAL = 50; // 50ms检查一次活动窗口
        private int _keyInterval = 5;

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
                _selectedWindowTitle = "未选择窗口";
                _selectedWindowClassName = string.Empty;
                _selectedWindowProcessName = string.Empty;

                // 更新热键服务的目标窗口
                if (_hotkeyService != null)
                {
                    _hotkeyService.TargetWindowHandle = IntPtr.Zero;
                }

                // 通知UI更新
                OnPropertyChanged(nameof(SelectedWindowTitle));
                
                _logger.Debug("已清除窗口信息");

                // 保存配置
                SaveConfig();
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

        // 开始热键文本
        public string StartHotkeyText
        {
            get => _startHotkeyText;
            set => SetProperty(ref _startHotkeyText, value);
        }

        // 停止热键文本
        public string StopHotkeyText
        {
            get => _stopHotkeyText;
            set => SetProperty(ref _stopHotkeyText, value);
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
                        AppConfigService.UpdateConfig(config =>
                        {
                            config.interval = value;
                        });
                        _logger.Debug($"已将默认按键间隔{value}ms保存到配置");
                    }
                }
            }
        }

        // 添加按键命令
        public ICommand AddKeyCommand { get; private set; } = null!;

        // 删除选中的按键命令
        public ICommand DeleteSelectedKeysCommand { get; private set; } = null!;

        // 按键模式选项
        public List<string> KeyModes { get; } = new List<string>
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
                    if (IsExecuting)
                    {
                        StopKeyMapping();
                    }

                    IsSequenceMode = value == 0; // 0 表示顺序模式

                    // 恢复输入法
                    if (_lyKeysService != null)
                    {
                        _lyKeysService.RestoreIME();
                    }

                    _logger.Debug($"按键模式已切换为: {(value == 0 ? "顺序模式" : "按压模式")}");
                }
            }
        }

        // 按键是否启用
        public bool IsHotkeyEnabled
        {
            get => _isHotkeyEnabled;
            set
            {
                SetProperty(ref _isHotkeyEnabled, value);
                HotkeyStatus = value ? "按键已启动" : "按键已停止";
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
                    {
                        AppConfigService.UpdateConfig(config =>
                        {
                            config.keyMode = value ? 0 : 1;
                        });
                    }

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
                {
                    if (!_isInitializing) // 只在非初始化时保存
                    {
                        SaveConfig();
                    }
                }
            }
        }

        // 判断是否为游戏模式，为true时按下抬起间隔为5ms，为false时间隔为0ms
        public bool IsGameMode
        {
            get => _isGameMode;
            set
            {
                if (SetProperty(ref _isGameMode, value))
                {
                    // 根据游戏模式设置按键间隔
                    int newInterval = value ? LyKeysService.DEFAULT_KEY_PRESS_INTERVAL : 0;
                    _lyKeysService.KeyPressInterval = newInterval;

                    if (!_isInitializing)
                    {
                        SaveConfig();
                    }
                    _logger.Debug($"游戏模式已更改为: {value}, 期望按键间隔: {newInterval}ms, " +
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
                    _isExecuting = value;
                    OnPropertyChanged(nameof(IsExecuting));
                    OnPropertyChanged(nameof(IsNotExecuting));
                    UpdateFloatingStatus();
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
                    if (!_isInitializing)
                    {
                        SaveConfig();
                    }
                    
                    if (value)
                    {
                        ShowFloatingWindow();
                    }
                    else
                    {
                        HideFloatingWindow();
                    }
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
                {
                    if (!_isInitializing)
                    {
                        SaveConfig();
                        
                        // 通知LyKeysService更新输入法切换设置
                        _lyKeysService.SetAutoSwitchIME(value);
                    }
                }
            }
        }

        // 选中的按键项
        public KeyItem? SelectedKeyItem
        {
            get => _selectedKeyItem;
            set => SetProperty(ref _selectedKeyItem, value);
        }

        public KeyboardLayoutViewModel KeyboardLayoutViewModel
        {
            get => _keyboardLayoutViewModel;
            private set
            {
                if (_keyboardLayoutViewModel != value)
                {
                    if (_keyboardLayoutViewModel != null)
                    {
                        // 取消订阅旧的事件
                        _keyboardLayoutViewModel.KeyBurstStateChanged -= OnKeyBurstStateChanged;
                    }
                    
                    _keyboardLayoutViewModel = value;
                    
                    if (_keyboardLayoutViewModel != null)
                    {
                        // 订阅新的事件
                        _keyboardLayoutViewModel.KeyBurstStateChanged += OnKeyBurstStateChanged;
                    }
                    
                    OnPropertyChanged();
                }
            }
        }

        // 处理连发状态变化
        private void OnKeyBurstStateChanged(LyKeysCode keyCode, bool isBurst)
        {
            var keyItem = KeyList.FirstOrDefault(k => k.KeyCode == keyCode);
            if (keyItem != null)
            {
                keyItem.IsKeyBurst = isBurst;
                _logger.Debug($"更新按键 {keyCode} 的连发状态为: {isBurst}");
            }
        }

        private void UpdateFloatingWindow()
        {
            if (AppConfigService.Config.UI.FloatingWindow.IsEnabled)
            {
                if (_floatingWindow == null)
                {
                    _floatingWindow = new FloatingStatusWindow(_mainWindow);
                    _floatingViewModel = _floatingWindow.DataContext as FloatingStatusViewModel;
                }
                _floatingWindow.Show();
                UpdateFloatingStatus(); // 确保显示后立即更新状态
            }
            else
            {
                _floatingWindow?.Hide();
            }
        }

        private void UpdateFloatingStatus()
        {
            if (_floatingWindow?.DataContext is FloatingStatusViewModel viewModel)
            {
                string statusText = IsExecuting ? "运行中" : "已停止";
                viewModel.StatusText = statusText;
                _logger.Debug($"更新浮窗状态: {statusText}");
            }
        }

        private void ShowFloatingWindow()
        {
            try
            {
                if (_mainWindow == null)
                {
                    _logger.Warning("MainWindow 引用为空，无法创建浮窗");
                    return;
                }

                if (_floatingWindow == null)
                {
                    // 先创建 ViewModel
                    _floatingViewModel = new FloatingStatusViewModel();
                    
                    // 创建浮窗并设置 DataContext
                    _floatingWindow = new FloatingStatusWindow(_mainWindow)
                    {
                        DataContext = _floatingViewModel
                    };
                    
                    _logger.Debug("浮窗已创建并设置 DataContext");
                }
                
                _floatingWindow.Show();
                UpdateFloatingStatus(); // 确保显示后立即更新状态
                _logger.Debug("浮窗已显示并更新状态");
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
                _floatingWindow.Hide();
                if (_floatingWindow.DataContext is FloatingStatusViewModel viewModel)
                {
                    viewModel.StatusText = "已停止";
                }
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

        public void SetMainWindow(MainWindow mainWindow)
        {
            if (mainWindow == null)
            {
                _logger.Warning("传入的 MainWindow 为空");
                return;
            }

            _mainWindow = mainWindow;
            _logger.Debug("已设置 MainWindow 引用");
            
            // 如果浮窗已启用，则创建浮窗
            if (IsFloatingWindowEnabled && _floatingWindow == null)
            {
                ShowFloatingWindow();
            }
        }

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

            // 2. 初始化键盘布局视图模型
            KeyboardLayoutViewModel = new KeyboardLayoutViewModel(lyKeysService, hotkeyService, _logger, _mainViewModel);

            // 3. 订阅事件
            SubscribeToEvents();

            // 4. 订阅驱动服务的状态变化
            _lyKeysService.EnableStatusChanged += (s, enabled) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsHotkeyEnabled = enabled;
                });
            };

            // 5. 修改热键事件处理
            _hotkeyService.StartHotkeyPressed += OnStartHotkeyPressed;
            _hotkeyService.StopHotkeyPressed += OnStopHotkeyPressed;

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
        }

        private void SyncConfigToServices()
        {
            try
            {
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
                    
                    _logger.Debug($"同步配置到服务 - 按键数量: {selectedKeys.Count}, 每个按键使用独立间隔");
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
                var appConfig = AppConfigService.Config;

                // 加载窗口配置
                if (!string.IsNullOrEmpty(appConfig.TargetWindowProcessName) && 
                    !string.IsNullOrEmpty(appConfig.TargetWindowTitle))
                {
                    _selectedWindowProcessName = appConfig.TargetWindowProcessName;
                    _selectedWindowClassName = appConfig.TargetWindowClassName ?? string.Empty;
                    _selectedWindowTitle = appConfig.TargetWindowTitle;
                    
                    _logger.Debug($"从配置加载窗口信息 - 进程名: {_selectedWindowProcessName}, " +
                                $"标题: {_selectedWindowTitle}, 类名: {_selectedWindowClassName}");
                }

                // 加载按键列表和选中状态
                if (appConfig.keys != null)
                {
                    KeyList.Clear();
                    foreach (var keyConfig in appConfig.keys)
                    {
                        var keyItem = new KeyItem(keyConfig.Code, _lyKeysService);
                        keyItem.IsSelected = keyConfig.IsSelected;
                        keyItem.IsKeyBurst = keyConfig.IsKeyBurst; // 同步连发状态
                        keyItem.KeyInterval = keyConfig.KeyInterval; // 同步每个按键的间隔
                        keyItem.SelectionChanged += (s, isSelected) => SaveConfig();
                        // 订阅KeyIntervalChanged事件，实时保存配置
                        keyItem.KeyIntervalChanged += (s, newInterval) => 
                        {
                            if (!_isInitializing)
                            {
                                SaveConfig();
                                _logger.Debug($"按键{keyItem.KeyCode}的间隔已更新为{newInterval}ms并保存到配置");
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

                // 加载热键配置
                if (appConfig.startKey.HasValue)
                {
                    SetStartHotkey(appConfig.startKey.Value, appConfig.startMods);
                }
                if (appConfig.stopKey.HasValue)
                {
                    SetStopHotkey(appConfig.stopKey.Value, appConfig.stopMods);
                }

                // 加载其他设置
                // 配置流程说明：
                // 1. 从AppConfig获取配置值，设置到ViewModel的属性中
                // 2. ViewModel的属性setter会自动将值同步到LyKeysService服务层
                // 3. 形成"配置 -> ViewModel -> 服务层"的单向数据流
                _keyInterval = appConfig.interval; // 先直接设置字段，避免触发属性变更事件
                _lyKeysService.KeyInterval = appConfig.interval; // 再同步到服务
                SelectedKeyMode = appConfig.keyMode;
                IsSequenceMode = appConfig.keyMode == 0;
                IsSoundEnabled = appConfig.soundEnabled ?? true;
                IsGameMode = appConfig.IsGameMode ?? true;
                IsFloatingWindowEnabled = appConfig.UI.FloatingWindow.IsEnabled;
                AutoSwitchToEnglishIME = appConfig.AutoSwitchToEnglishIME ?? true;

                _logger.Debug($"配置加载完成 - 模式: {(IsSequenceMode ? "顺序模式" : "按压模式")}, 游戏模式: {IsGameMode}");
            }
            catch (Exception ex)
            {
                _logger.Error("加载配置失败", ex);
                SetDefaultConfiguration();
            }
        }

        private void SetDefaultConfiguration()
        {
            IsSequenceMode = true;
            IsSoundEnabled = true;
            IsFloatingWindowEnabled = true;  // 默认开启浮窗
            _lyKeysService.KeyPressInterval = IsGameMode ? LyKeysService.DEFAULT_KEY_PRESS_INTERVAL : 0;
        }

        private void InitializeCommands()
        {
            AddKeyCommand = new RelayCommand(AddKey, CanAddKey);
            DeleteSelectedKeysCommand = new RelayCommand(() =>
            {
                try
                {
                    var keysToDelete = new List<KeyItem>();

                    // 如果有右键选中的项，优先删除该项
                    if (SelectedKeyItem != null)
                    {
                        keysToDelete.Add(SelectedKeyItem);
                        SelectedKeyItem = null;
                    }
                    else
                    {
                        // 否则删除所有勾选的项
                        keysToDelete.AddRange(KeyList.Where(k => k.IsSelected));
                    }

                    // 执行删除
                    foreach (var key in keysToDelete)
                    {
                        KeyList.Remove(key);
                        _logger.Debug($"删除按键: {key.KeyCode}");
                    }

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
                }
            });
        }

        private void InitializeHotkeyStatus()
        {
            IsHotkeyEnabled = false;
            HotkeyStatus = "初始化完成";
        }

        private void SubscribeToEvents()
        {
            // 订阅热键服务事件
            _hotkeyService.SequenceModeStarted += () => IsHotkeyEnabled = true;
            _hotkeyService.SequenceModeStopped += () => IsHotkeyEnabled = false;

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
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            _logger.Debug("SetCurrentKey", $"设置当前按键: {keyCode} | {CurrentKeyText}");
        }

        // 设置开始热键
        public void SetStartHotkey(LyKeysCode keyCode, ModifierKeys modifiers)
        {
            // 只检查与KeyList的冲突
            if (IsKeyInList(keyCode))
            {
                _logger.Debug($"该按键已在按键列表中，请选择其他按键: {keyCode}");
                _mainViewModel.UpdateStatusMessage("该按键已在按键列表中，请选择其他按键", true);
                return;
            }

            try
            {
                // 先停止当前运行的序列
                if (IsExecuting)
                {
                    StopKeyMapping();
                }

                bool result = _hotkeyService.RegisterStartHotkey(keyCode, modifiers);
                if (!result && !_hotkeyService.IsMouseButton(keyCode))
                {
                    _logger.Error($"注册开始热键失败: {keyCode}");
                    _mainViewModel.UpdateStatusMessage("开始热键注册失败，请尝试其他按键", true);
                    return;
                }

                // 只有在注册成功后才更新状态和显示
                _startHotkey = keyCode;
                _startModifiers = modifiers;
                UpdateHotkeyText(keyCode, modifiers, true);

                _mainViewModel.UpdateStatusMessage($"已设置开始热键: {_lyKeysService.GetKeyDescription(keyCode)}");
                _logger.Debug($"设置开始热键: {keyCode}, 修饰键: {modifiers}");
            }
            catch (Exception ex)
            {
                _logger.Error("设置开始热键失败", ex);
                _mainViewModel.UpdateStatusMessage($"设置开始热键失败: {ex.Message}", true);
            }
        }

        // 更新热键显示文本
        private void UpdateHotkeyText(LyKeysCode keyCode, ModifierKeys modifiers, bool isStart)
        {
            StringBuilder keyText = new StringBuilder();

            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                keyText.Append("Ctrl + ");
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                keyText.Append("Alt + ");
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                keyText.Append("Shift + ");

            keyText.Append(_lyKeysService.GetKeyDescription(keyCode));

            if (isStart)
                StartHotkeyText = keyText.ToString();
            else
                StopHotkeyText = keyText.ToString();
        }

        // 设置停止热键
        public void SetStopHotkey(LyKeysCode keyCode, ModifierKeys modifiers)
        {
            // 只检查与KeyList的冲突
            if (IsKeyInList(keyCode))
            {
                _logger.Debug($"该按键已在按键列表中，请选择其他按键: {keyCode}");
                _mainViewModel.UpdateStatusMessage("该按键已在按键列表中，请选择其他按键", true);
                return;
            }

            try
            {
                // 先停止当前运行的序列
                if (IsExecuting)
                {
                    StopKeyMapping();
                }

                bool result = _hotkeyService.RegisterStopHotkey(keyCode, modifiers);
                if (!result)
                {
                    _logger.Error($"注册停止热键失败: {keyCode}, 修饰键: {modifiers}");
                    _mainViewModel.UpdateStatusMessage("停止热键注册失败，请尝试其他按键", true);
                    return;
                }

                // 只有在注册成功后才更新状态和显示
                _stopHotkey = keyCode;
                _stopModifiers = modifiers;
                UpdateHotkeyText(keyCode, modifiers, false);

                _mainViewModel.UpdateStatusMessage($"已设置停止热键: {_lyKeysService.GetKeyDescription(keyCode)}");
                _logger.Debug($"设置停止热键: {keyCode}, 修饰键: {modifiers}");
            }
            catch (Exception ex)
            {
                _mainViewModel.UpdateStatusMessage($"设置停止热键失败: {ex.Message}", true);
                _logger.Error("设置停止热键失败", ex);
            }
        }

        // 检查是否可以添加按键
        private bool CanAddKey()
        {
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
                    return;
                }

                var keyCode = _currentKey.Value;
                if (!_lyKeysService.IsValidLyKeysCode(keyCode))
                {
                    _logger.Warning($"无效的按键码: {keyCode}");
                    return;
                }

                if (IsKeyInList(keyCode))
                {
                    _logger.Warning($"按键已存在: {keyCode}");
                    return;
                }

                if (IsHotkeyConflict(keyCode))
                {
                    _logger.Warning($"按键与热键冲突: {keyCode}");
                    return;
                }

                var newKey = new KeyItem(keyCode, _lyKeysService);
                newKey.KeyInterval = _keyInterval; // 使用当前默认间隔值
                newKey.SelectionChanged += (s, isSelected) => SaveConfig();
                // 订阅KeyIntervalChanged事件，实时保存配置
                newKey.KeyIntervalChanged += (s, newInterval) => 
                {
                    if (!_isInitializing)
                    {
                        SaveConfig();
                        _logger.Debug($"按键{newKey.KeyCode}的间隔已更新为{newInterval}ms并保存到配置");
                    }
                };
                KeyList.Add(newKey);

                // 更新HotkeyService的按键列表
                UpdateHotkeyServiceKeyList();

                _logger.Debug($"添加按键: {keyCode} | {newKey.DisplayName}");
                CurrentKeyText = string.Empty;
                _currentKey = null;

                // 实时保存按键列表
                if (!_isInitializing)
                {
                    AppConfigService.UpdateConfig(config =>
                    {
                        config.keys = KeyList.Select(k => new KeyConfig(k.KeyCode, k.IsSelected)).ToList();
                    });
                }

                _mainViewModel.UpdateStatusMessage($" {keyCode} 按键添加成功");
            }
            catch (Exception ex)
            {
                _logger.Error("添加按键时发生异常", ex);
                _mainViewModel.UpdateStatusMessage($"添加按键失败: {ex.Message}", true);
            }
        }

        // 删除选中的按键
        private void DeleteSelectedKeys()
        {
            try
            {
                var keysToDelete = new List<KeyItem>();

                // 如果有右键选中的项，优先删除该项
                if (SelectedKeyItem != null)
                {
                    keysToDelete.Add(SelectedKeyItem);
                    SelectedKeyItem = null;
                }
                else
                {
                    // 否则删除所有勾选的项
                    keysToDelete.AddRange(KeyList.Where(k => k.IsSelected));
                }

                // 执行删除
                foreach (var key in keysToDelete)
                {
                    KeyList.Remove(key);
                    _logger.Debug($"删除按键: {key.KeyCode}");
                }

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
            }
        }

        // 更新HotkeyService按键列表的辅助方法
        private void UpdateHotkeyServiceKeyList()
        {
            try
            {
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
                    
                    _logger.Debug($"同步配置到服务 - 按键数量: {selectedKeys.Count}, 每个按键使用独立间隔");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("同步配置到服务失败", ex);
            }
        }

        // 保存配置
        public void SaveConfig()
        {
            try
            {
                // 获取所有按键和它们的状态
                var keyConfigs = KeyList.Select(k => new KeyConfig(k.KeyCode, k.IsSelected)
                {
                    IsKeyBurst = k.IsKeyBurst, // 保存连发状态
                    KeyInterval = k.KeyInterval // 保存每个按键的间隔
                }).ToList();

                // 检查热键冲突
                if (_startHotkey.HasValue && keyConfigs.Any(k => k.Code == _startHotkey.Value))
                {
                    _mainViewModel.UpdateStatusMessage("启动热键与按键列表存在冲突，请修改后再保存", true);
                    return;
                }

                if (_stopHotkey.HasValue && keyConfigs.Any(k => k.Code == _stopHotkey.Value))
                {
                    _mainViewModel.UpdateStatusMessage("停止热键与按键列表存在冲突，请修改后再保存", true);
                    return;
                }

                // 获取当前配置
                var config = AppConfigService.Config;

                // 只更新需要保存的字段
                bool configChanged = false;

                // 检查并更新热键配置
                if (!config.startKey.Equals(_startHotkey) || config.startMods != _startModifiers)
                {
                    config.startKey = _startHotkey;
                    config.startMods = _startModifiers;
                    configChanged = true;
                }

                if (!config.stopKey.Equals(_stopHotkey) || config.stopMods != _stopModifiers)
                {
                    config.stopKey = _stopHotkey;
                    config.stopMods = _stopModifiers;
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

                if (config.IsGameMode != IsGameMode)
                {
                    config.IsGameMode = IsGameMode;
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
                System.Windows.MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool AreKeyConfigsEqual(List<KeyConfig> list1, List<KeyConfig> list2)
        {
            if (list1 == null || list2 == null)
                return list1 == list2;

            if (list1.Count != list2.Count)
                return false;

            for (int i = 0; i < list1.Count; i++)
            {
                if (list1[i].Code != list2[i].Code || 
                    list1[i].IsSelected != list2[i].IsSelected ||
                    list1[i].IsKeyBurst != list2[i].IsKeyBurst ||
                    list1[i].KeyInterval != list2[i].KeyInterval)
                    return false;
            }

            return true;
        }

        // 启动按键映射
        public void StartKeyMapping()
        {
            if (!IsExecuting)
            {
                try
                {
                    _logger.Debug("开始启动按键映射");
                    
                    // 检查是否选择了目标窗口
                    if (SelectedWindowHandle == IntPtr.Zero)
                    {
                        _logger.Warning("未选择目标窗口");
                        _mainViewModel.UpdateStatusMessage("请先选择目标窗口", true);
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
        }

        // 停止按键映射
        public void StopKeyMapping()
        {
            try
            {
                if (_lyKeysService == null) return;

                _logger.Debug($"开始停止按键映射，当前模式: {(SelectedKeyMode == 1 ? "按压模式" : "顺序模式")}");

                // 先停止热键服务
                _hotkeyService?.StopSequence();

                // 然后停止驱动服务
                _lyKeysService.IsEnabled = false;
                _lyKeysService.IsHoldMode = false;

                // 最后更新UI状态
                IsHotkeyEnabled = false;
                IsExecuting = false;
                UpdateFloatingStatus();

                _logger.Debug("按键映射已停止");
            }
            catch (Exception ex)
            {
                _logger.Error("停止按键映射失败", ex);
                IsHotkeyEnabled = false;
                IsExecuting = false;
                UpdateFloatingStatus();
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
            return KeyList.Any(k => k.KeyCode.Equals(keyCode));
        }

        // 开始热键按下事件处理
        private void OnStartHotkeyPressed()
        {
            try
            {
                _logger.Debug("🍎 ==》 启动热键按下 《== 🍎");

                // 获取选中的按键
                var selectedKeys = KeyList.Where(k => k.IsSelected).ToList();
                if (selectedKeys.Count == 0)
                {
                    _logger.Warning("没有选中任何按键，无法启动");
                    _mainViewModel.UpdateStatusMessage("请至少选择一个按键", true);
                    IsHotkeyEnabled = false;
                    IsExecuting = false;
                    return;
                }

                // 设置按键列表
                _lyKeysService.SetKeyList(selectedKeys.Select(k => k.KeyCode).ToList());
                
                // 将选中的按键及其间隔传递给HotkeyService
                _hotkeyService.SetKeySequence(
                    selectedKeys.Select(k => new KeyItemSettings 
                    { 
                        KeyCode = k.KeyCode, 
                        Interval = k.KeyInterval 
                    }).ToList());
                
                // 设置执行状态
                IsExecuting = true;

                if (SelectedKeyMode == 0)
                {
                    _logger.Debug("启动顺序模式");
                    _lyKeysService.IsHoldMode = false;
                    _lyKeysService.IsEnabled = true;    // 启用服务
                }
                else
                {
                    _logger.Debug("启动按压模式");
                    _lyKeysService.IsHoldMode = true;   // 设置为按压模式
                    _lyKeysService.IsEnabled = true;    // 启用服务
                }
                IsHotkeyEnabled = true;  // 按键是否启用
                UpdateFloatingStatus();   // 更新浮窗状态
            }
            catch (Exception ex)
            {
                _logger.Error("启动按键映射异常", ex);
                IsHotkeyEnabled = false;
                IsExecuting = false;
                UpdateFloatingStatus();
            }
        }

        // 停止热键按下事件处理
        private void OnStopHotkeyPressed()
        {
            try
            {
                _logger.Debug("🍋 ==》 停止热键按下 《== 🍋");
                _lyKeysService.IsEnabled = false;
                _lyKeysService.IsHoldMode = false;
                IsHotkeyEnabled = false;
                IsExecuting = false;
            }
            catch (Exception ex)
            {
                _logger.Error("停止按键映射异常", ex);
                // 确保状态被重置
                IsHotkeyEnabled = false;
                IsExecuting = false;
            }
        }

        // 获取热键服务
        public HotkeyService GetHotkeyService()
        {
            return _hotkeyService;
        }

        // 添加热键冲突检测方法
        public bool IsHotkeyConflict(LyKeysCode keyCode)
        {
            try
            {
                bool isStartConflict = _startHotkey.HasValue && keyCode.Equals(_startHotkey.Value);
                bool isStopConflict = _stopHotkey.HasValue && keyCode.Equals(_stopHotkey.Value);

                if (isStartConflict || isStopConflict)
                {
                    _logger.Debug(
                        $"检测到热键冲突 - 按键: {keyCode}, 启动键冲突: {isStartConflict}, 停止键冲突: {isStopConflict}");
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
            {
                keyItem.SelectionChanged += (s, isSelected) =>
                {
                    SaveConfig();
                    UpdateHotkeyServiceKeyList();
                };
            }
        }

        // 在加载配置时也需要添加事件订阅
        private void LoadKeyList(List<LyKeysCode> keyList, List<bool> keySelections)
        {
            KeyList.Clear();
            for (int i = 0; i < keyList.Count; i++)
            {
                var keyItem = new KeyItem(keyList[i], _lyKeysService);
                keyItem.IsSelected = i < keySelections.Count ? keySelections[i] : true;
                keyItem.SelectionChanged += (s, isSelected) => SaveConfig();
                KeyList.Add(keyItem);
            }
        }

        private void OnKeyModeChanged()
        {
            // This method is called when the key mode changes
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
                    
                    // 保持原有的窗口信息，但更新显示状态
                    SelectedWindowTitle = $"{_selectedWindowTitle} (进程未运行)";
                    
                    // 启动定时检查
                    StartWindowCheck();
                    _logger.Debug($"已启动定时检查 - 进程名: {_selectedWindowProcessName}, 标题: {_selectedWindowTitle} (进程未运行)");
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
            if (string.IsNullOrEmpty(processName) && string.IsNullOrEmpty(targetTitle))
            {
                return result;
            }

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
                {
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
                                    result.Add(new WindowInfo(process.MainWindowHandle, title, className, process.ProcessName));
                                    _logger.Debug($"找到匹配窗口 - 句柄: {process.MainWindowHandle}, 标题: {title}");
                                }
                            }
                            else
                            {
                                result.Add(new WindowInfo(process.MainWindowHandle, title, className, process.ProcessName));
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
            }
            catch (Exception ex)
            {
                _logger.Error($"查找窗口时发生异常: {ex.Message}");
            }

            if (result.Count == 0)
            {
                _logger.Debug($"未找到目标窗口 - 进程: {processName}, 目标标题: {targetTitle}");
            }

            return result;
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            StringBuilder title = new StringBuilder(256);
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
            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            return className.ToString().Trim();
        }

        private void WindowCheckTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedWindowProcessName) || string.IsNullOrEmpty(SelectedWindowTitle))
            {
                return;
            }

            try
            {
                lock (_windowCheckLock)
                {
                    // 获取原始标题（移除状态信息）
                    string originalTitle = SelectedWindowTitle.Split(new[] { " (句柄:", " (进程未运行)", " (未找到匹配窗口)" }, StringSplitOptions.None)[0];
                    
                    var windows = FindWindowsByProcessName(SelectedWindowProcessName, originalTitle);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (windows.Any())
                        {
                            var targetWindow = windows.First();
                            bool needsUpdate = false;

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

                                _logger.Info($"已更新窗口信息 - 句柄: {targetWindow.Handle.ToInt64()}, 类名: {targetWindow.ClassName}, 进程名: {targetWindow.ProcessName}, 标题: {targetWindow.Title}");
                            }
                        }
                        else if (SelectedWindowHandle != IntPtr.Zero)
                        {
                            // 目标进程已关闭
                            SelectedWindowHandle = IntPtr.Zero;
                            SelectedWindowTitle = $"{originalTitle} (进程未运行)";
                            _logger.Warning($"进程 {SelectedWindowProcessName} 已关闭");
                        }
                    });
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
        private void ActiveWindowCheckTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                // 如果没有选择窗口，则认为总是活动的
                if (SelectedWindowHandle == IntPtr.Zero)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsTargetWindowActive = true;
                        _hotkeyService.IsTargetWindowActive = true;
                    });
                    return;
                }

                // 如果选择了窗口，则检查是否是当前活动窗口
                var activeWindow = GetForegroundWindow();
                bool isActive = activeWindow == SelectedWindowHandle;
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsTargetWindowActive = isActive;
                    _hotkeyService.IsTargetWindowActive = isActive;
                });
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
    }
}