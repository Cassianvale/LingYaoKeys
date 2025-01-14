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
        private bool _isSoundEnabled = true;
        private readonly AudioService _audioService;
        private bool _isGameMode = true; // 默认开启
        private bool _isInitializing = true; // 添加一个标志来标识是否在初始化
        private bool _isExecuting = false; // 添加执行状态标志
        private bool _isFloatingWindowEnabled;
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
            private set => SetProperty(ref _selectedWindowHandle, value);
        }

        private string SelectedWindowClassName
        {
            get => _selectedWindowClassName;
            set => SetProperty(ref _selectedWindowClassName, value);
        }

        private string SelectedWindowProcessName
        {
            get => _selectedWindowProcessName;
            set => SetProperty(ref _selectedWindowProcessName, value);
        }

        // 更新选中的窗口句柄信息
        public void UpdateSelectedWindow(IntPtr handle, string title, string className, string processName)
        {
            SelectedWindowHandle = handle;
            SelectedWindowClassName = className;
            SelectedWindowProcessName = processName;
            SelectedWindowTitle = $"{title} (句柄: {handle.ToInt64()})";

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
            SelectedWindowHandle = IntPtr.Zero;
            SelectedWindowTitle = "未选择窗口";
            SelectedWindowClassName = string.Empty;
            SelectedWindowProcessName = string.Empty;

            // 清除配置
            AppConfigService.UpdateConfig(config =>
            {
                config.TargetWindowClassName = null;
                config.TargetWindowProcessName = null;
                config.TargetWindowTitle = null;
            });

            // 停止定时检查
            StopWindowCheck();

            _logger.Info("已清除选中的窗口");
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

        // 按键间隔
        public int KeyInterval
        {
            get => _lyKeysService.KeyInterval;
            set
            {
                if (_lyKeysService.KeyInterval != value)
                {
                    _lyKeysService.KeyInterval = value;
                    OnPropertyChanged(nameof(KeyInterval));
                    SaveConfig();
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
                    IsSequenceMode = value == 0; // 0 表示顺序模式
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
                    var selectedKeys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                    _hotkeyService.SetKeySequence(selectedKeys, KeyInterval);

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
                if (_isFloatingWindowEnabled != value)
                {
                    _isFloatingWindowEnabled = value;
                    OnPropertyChanged();
                    UpdateFloatingWindow();

                    // 保存到配置
                    if (!_isInitializing)
                    {
                        AppConfigService.UpdateConfig(config =>
                        {
                            config.IsFloatingWindowEnabled = value;
                        });
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
            if (IsFloatingWindowEnabled)
            {
                if (_floatingWindow == null)
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                    _floatingWindow = new FloatingStatusWindow(mainWindow);
                    _floatingViewModel = _floatingWindow.DataContext as FloatingStatusViewModel;
                    UpdateFloatingStatus();
                }
                _floatingWindow.Show();
            }
            else
            {
                _floatingWindow?.Hide();
            }
        }

        private void UpdateFloatingStatus()
        {
            if (_floatingViewModel != null)
            {
                _floatingViewModel.StatusText = IsExecuting ? "运行中" : "已停止";
            }
        }

        public KeyMappingViewModel(LyKeysService lyKeysService, ConfigService configService,
            HotkeyService hotkeyService, MainViewModel mainViewModel, AudioService audioService)
        {
            _lyKeysService = lyKeysService;
            _configService = configService;
            _hotkeyService = hotkeyService;
            _mainViewModel = mainViewModel;
            _audioService = audioService;
            _hotkeyStatus = "初始化中...";
            _isInitializing = true;

            // 初始化按键列表
            _keyList = new ObservableCollection<KeyItem>();

            // 订阅驱动服务的状态变化
            _lyKeysService.EnableStatusChanged += (s, enabled) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsHotkeyEnabled = enabled;
                });
            };

            // 修改热键事件处理
            _hotkeyService.StartHotkeyPressed += OnStartHotkeyPressed;
            _hotkeyService.StopHotkeyPressed += OnStopHotkeyPressed;

            // 初始化命令
            InitializeCommands();

            // 加载配置
            LoadConfiguration();

            // 初始化热键状态
            InitializeHotkeyStatus();

            // 订阅事件
            SubscribeToEvents();

            // 确保配置同步到服务
            SyncConfigToServices();

            // 在所有初始化完成后
            _isInitializing = false;

            // 初始化键盘布局视图模型
            KeyboardLayoutViewModel = new KeyboardLayoutViewModel(lyKeysService, hotkeyService, _logger, _mainViewModel);

            // 加载窗口配置
            LoadWindowConfig();
        }

        private void SyncConfigToServices()
        {
            try
            {
                var selectedKeys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                if (selectedKeys.Any())
                {
                    _lyKeysService.SetKeyList(selectedKeys);
                    _hotkeyService.SetKeySequence(selectedKeys, KeyInterval);
                    _logger.Debug($"同步配置到服务 - 按键数量: {selectedKeys.Count}, 间隔: {KeyInterval}ms");
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

                // 加载按键列表和选中状态
                if (appConfig.keys != null)
                {
                    KeyList.Clear();
                    foreach (var keyConfig in appConfig.keys)
                    {
                        var keyItem = new KeyItem(keyConfig.Code, _lyKeysService);
                        keyItem.IsSelected = keyConfig.IsSelected;
                        keyItem.IsKeyBurst = keyConfig.IsKeyBurst; // 同步连发状态
                        keyItem.SelectionChanged += (s, isSelected) => SaveConfig();
                        KeyList.Add(keyItem);
                    }

                    // 立即同步选中的按键到服务
                    var selectedKeys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                    if (selectedKeys.Any())
                    {
                        _lyKeysService.SetKeyList(selectedKeys);
                        _hotkeyService.SetKeySequence(selectedKeys, appConfig.interval);
                        _logger.Debug($"已加载按键列表 - 按键数量: {selectedKeys.Count}, 间隔: {appConfig.interval}ms");
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
                _lyKeysService.KeyInterval = appConfig.interval;
                SelectedKeyMode = appConfig.keyMode;
                IsSequenceMode = appConfig.keyMode == 0;
                IsSoundEnabled = appConfig.soundEnabled ?? true;
                IsGameMode = appConfig.IsGameMode ?? true;
                IsFloatingWindowEnabled = appConfig.IsFloatingWindowEnabled;

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
            _logger.Debug($"尝试添加按键，当前按键: {_currentKey}");

            if (!_currentKey.HasValue)
            {
                _logger.Warning("当前按键为空，无法添加");
                return;
            }

            if (_startHotkey.HasValue && _currentKey.Value == _startHotkey.Value)
            {
                _logger.Warning("该按键已被设置为启动热键，请选择其他按键");
                _mainViewModel.UpdateStatusMessage("该按键已被设置为启动热键，请选择其他按键", true);
                return;
            }

            if (_stopHotkey.HasValue && _currentKey.Value == _stopHotkey.Value)
            {
                _logger.Warning("该按键已被设置为停止热键，请选择其他按键");
                _mainViewModel.UpdateStatusMessage("该按键已被设置为停止热键，请选择其他按键", true);
                return;
            }

            if (IsKeyInList(_currentKey.Value))
            {
                _currentKey = null;
                CurrentKeyText = string.Empty;
                _logger.Warning("该按键已在列表中，请选择其他按键");
                _mainViewModel.UpdateStatusMessage("该按键已在列表中，请选择其他按键", true);
                return;
            }

            var newKeyItem = new KeyItem(_currentKey.Value, _lyKeysService);

            // 订阅选中状态变化事件
            newKeyItem.SelectionChanged += (s, isSelected) =>
            {
                SaveConfig();
                UpdateHotkeyServiceKeyList();
                _logger.Debug("按键选中状态变化，保存配置并更新按键列表");
            };

            KeyList.Add(newKeyItem);

            // 更新HotkeyService的按键列表
            UpdateHotkeyServiceKeyList();

            // 实时保存按键列表
            if (!_isInitializing)
            {
                AppConfigService.UpdateConfig(config =>
                {
                    config.keys = KeyList.Select(k => new KeyConfig(k.KeyCode, k.IsSelected)).ToList();
                });
            }

            _mainViewModel.UpdateStatusMessage($" {_currentKey.Value} 按键添加成功");
            _logger.Debug($" {_currentKey.Value} 按键添加成功");

            // 清空当前按键状态
            _currentKey = null;
            CurrentKeyText = string.Empty;
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
            var selectedKeys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
            _hotkeyService.SetKeySequence(selectedKeys, KeyInterval);
            _lyKeysService.SetKeyList(selectedKeys);
            _logger.Debug($"更新按键列表 - 选中按键数: {selectedKeys.Count}, 按键间隔: {KeyInterval}ms");
        }

        // 保存配置
        public void SaveConfig()
        {
            try
            {
                // 获取所有按键和它们的状态
                var keyConfigs = KeyList.Select(k => new KeyConfig(k.KeyCode, k.IsSelected)
                {
                    IsKeyBurst = k.IsKeyBurst // 保存连发状态
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

                if (config.IsFloatingWindowEnabled != IsFloatingWindowEnabled)
                {
                    config.IsFloatingWindowEnabled = IsFloatingWindowEnabled;
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
                    list1[i].IsKeyBurst != list2[i].IsKeyBurst)
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
                    
                    // 只获取勾选的按键
                    var keys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                    if (keys.Count == 0)
                    {
                        _logger.Warning("没有选中任何按键");
                        _mainViewModel.UpdateStatusMessage("请至少选择一个按键", true);
                        IsHotkeyEnabled = false;
                        IsExecuting = false;
                        return;
                    }

                    // 记录按键列表
                    _logger.Debug($"选中的按键列表:");
                    foreach (var key in keys)
                    {
                        _logger.Debug($"- {key} ({_lyKeysService.GetKeyDescription(key)})");
                    }

                    IsExecuting = true;
                    if (_lyKeysService == null)
                    {
                        _logger.Error("LyKeysService未初始化");
                        return;
                    }

                    // 确保先同步按键列表到服务
                    _lyKeysService.SetKeyList(keys);
                    _hotkeyService.SetKeySequence(keys, KeyInterval);

                    // 设置驱动服务
                    _lyKeysService.IsHoldMode = SelectedKeyMode == 1;
                    _lyKeysService.KeyInterval = KeyInterval;
                    
                    // 启用服务
                    _lyKeysService.IsEnabled = true;
                    IsHotkeyEnabled = true;

                    _logger.Debug(
                        $"按键映射已启动: 模式={SelectedKeyMode}, 选中按键数={keys.Count}, 间隔={KeyInterval}ms");
                }
                catch (Exception ex)
                {
                    _logger.Error("启动按键映射失败", ex);
                    IsHotkeyEnabled = false;
                    IsExecuting = false;
                    _mainViewModel.UpdateStatusMessage($"启动按键映射失败: {ex.Message}", true);
                }
            }
            else
            {
                _logger.Debug("按键映射已在执行中，忽略启动请求");
            }
        }

        // 停止按键映射
        public void StopKeyMapping()
        {
            if (IsExecuting)
            {
                IsExecuting = false;
                try
                {
                    if (_lyKeysService == null) return;

                    _logger.Debug("开始停止按键映射");

                    // 先停止热键服务
                    _hotkeyService?.StopSequence();

                    // 然后停止驱动服务
                    _lyKeysService.IsEnabled = false;
                    _lyKeysService.IsHoldMode = false;

                    // 最后更新UI状态
                    IsHotkeyEnabled = false;
                    IsExecuting = false;

                    _logger.Debug("按键映射已停止");
                }
                catch (Exception ex)
                {
                    _logger.Error("停止按键映射失败", ex);
                    IsHotkeyEnabled = false;
                    IsExecuting = false;
                }
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
                var keys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                if (keys.Count == 0)
                {
                    _logger.Warning("没有选中任何按键，无法启动");
                    _mainViewModel.UpdateStatusMessage("请至少选择一个按键", true);
                    IsHotkeyEnabled = false;
                    IsExecuting = false;
                    return;
                }

                // 设置按键列表参数
                _lyKeysService.SetKeyList(keys);
                _hotkeyService.SetKeySequence(keys, KeyInterval);  // 重要: 重置按键列表
                _lyKeysService.IsHoldMode = SelectedKeyMode == 1;
                _lyKeysService.KeyInterval = KeyInterval;
                
                // 设置执行状态
                IsExecuting = true;

                if (SelectedKeyMode == 0)
                {
                    _logger.Debug("启动顺序模式");
                    _lyKeysService.IsEnabled = true;    // 启用服务
                }
                else
                {
                    _logger.Debug("启动按压模式");
                    _lyKeysService.IsHoldMode = true;   // 设置为按压模式
                    _lyKeysService.IsEnabled = true;    // 启用服务
                }
                IsHotkeyEnabled = true;  // 按键是否启用
            }
            catch (Exception ex)
            {
                _logger.Error("启动按键映射异常", ex);
                IsHotkeyEnabled = false;
                IsExecuting = false;
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
            var config = AppConfigService.Config;
            if (!string.IsNullOrEmpty(config.TargetWindowProcessName) && !string.IsNullOrEmpty(config.TargetWindowTitle))
            {
                SelectedWindowProcessName = config.TargetWindowProcessName;
                SelectedWindowClassName = config.TargetWindowClassName ?? string.Empty;
                SelectedWindowTitle = config.TargetWindowTitle;
                
                _logger.Debug($"开始加载窗口配置 - 进程名: {SelectedWindowProcessName}, 标题: {SelectedWindowTitle}");

                // 尝试查找匹配的进程窗口，直接使用目标标题
                var windows = FindWindowsByProcessName(SelectedWindowProcessName, config.TargetWindowTitle);
                if (windows.Any())
                {
                    // 由于FindWindowsByProcessName已经按标题过滤，这里直接取第一个
                    var targetWindow = windows.First();
                    
                    // 在初始化时始终更新配置，确保类名等信息是最新的
                    SelectedWindowHandle = targetWindow.Handle;
                    SelectedWindowClassName = targetWindow.ClassName;
                    SelectedWindowTitle = $"{targetWindow.Title} (句柄: {targetWindow.Handle.ToInt64()})";
                    
                    // 更新配置
                    AppConfigService.UpdateConfig(config =>
                    {
                        config.TargetWindowClassName = targetWindow.ClassName;
                        config.TargetWindowProcessName = targetWindow.ProcessName;
                        config.TargetWindowTitle = targetWindow.Title;
                    });

                    _logger.Info($"已加载并更新窗口配置 - 句柄: {targetWindow.Handle.ToInt64()}, 类名: {targetWindow.ClassName}, 进程名: {targetWindow.ProcessName}, 标题: {targetWindow.Title}");
                }
                else
                {
                    SelectedWindowTitle = $"{SelectedWindowTitle} (进程未运行)";
                    _logger.Warning($"未找到进程 {SelectedWindowProcessName} 的窗口");
                }

                // 只要配置中有窗口信息，就启动定时检查
                StartWindowCheck();
                _logger.Debug($"已启动定时检查 - 进程名: {SelectedWindowProcessName}, 标题: {SelectedWindowTitle}");
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

        private List<WindowInfo> FindWindowsByProcessName(string processName, string targetTitle = null)
        {
            var windows = new List<WindowInfo>();
            
            try
            {
                // 获取所有指定进程名的进程
                var processes = Process.GetProcessesByName(processName);
                if (!processes.Any())
                {
                    _logger.Debug($"未找到进程: {processName}");
                    return windows;
                }

                // 获取所有进程的ID
                var processIds = processes.Select(p => p.Id).ToHashSet();

                // 如果没有提供目标标题，则使用当前选中的窗口标题（移除所有状态信息）
                if (targetTitle == null && !string.IsNullOrEmpty(SelectedWindowTitle))
                {
                    targetTitle = SelectedWindowTitle.Split(new[] { " (句柄:", " (进程未运行)", " (未找到匹配窗口)" }, StringSplitOptions.None)[0];
                }

                bool hasTargetTitle = !string.IsNullOrEmpty(targetTitle);
                if (hasTargetTitle)
                {
                    _logger.Debug($"正在查找窗口 - 进程名: {processName}, 目标标题: {targetTitle}");
                }

                bool foundTarget = false;
                // 枚举窗口并匹配进程ID
                EnumWindows((hWnd, lParam) =>
                {
                    if (IsWindowVisible(hWnd))
                    {
                        uint windowProcessId;
                        GetWindowThreadProcessId(hWnd, out windowProcessId);

                        if (processIds.Contains((int)windowProcessId))
                        {
                            var title = GetWindowTitle(hWnd);
                            var className = GetWindowClassName(hWnd);
                            
                            if (!string.IsNullOrWhiteSpace(title))
                            {
                                // 如果有目标标题，只添加匹配的窗口
                                if (!hasTargetTitle || title.Equals(targetTitle, StringComparison.Ordinal))
                                {
                                    var windowInfo = new WindowInfo(hWnd, title, className, processName);
                                    windows.Add(windowInfo);

                                    if (hasTargetTitle && title.Equals(targetTitle, StringComparison.Ordinal))
                                    {
                                        foundTarget = true;
                                        _logger.Debug($"找到目标窗口 - 进程: {processName}, 标题: {title}, 类名: {className}");
                                    }
                                }
                            }
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                if (hasTargetTitle && !foundTarget)
                {
                    _logger.Debug($"未找到目标窗口 - 进程: {processName}, 目标标题: {targetTitle}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"查找进程 {processName} 的窗口时发生异常", ex);
            }

            return windows;
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
                            // 由于FindWindowsByProcessName已经按标题过滤，这里直接取第一个
                            var targetWindow = windows.First();
                            bool needsUpdate = false;

                            // 检查句柄是否变化
                            if (targetWindow.Handle != SelectedWindowHandle)
                            {
                                SelectedWindowHandle = targetWindow.Handle;
                                needsUpdate = true;
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
                _windowCheckTimer = new System.Timers.Timer(30000); // 30秒
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
        }
    }
}