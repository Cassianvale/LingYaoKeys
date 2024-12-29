using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using WpfApp.Models;
using WpfApp.Services;
using WpfApp.Commands;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using WpfApp.Views;


// 按键映射核心业务逻辑层
namespace WpfApp.ViewModels
{
    public class KeyMappingViewModel : ViewModelBase
    {
        private readonly DDDriverService _ddDriver;
        private readonly ConfigService _configService;
        private DDKeyCode? _currentKey;
        private string _currentKeyText = string.Empty;
        private ObservableCollection<KeyItem> _keyList;
        private string _startHotkeyText = string.Empty;
        private string _stopHotkeyText = string.Empty;
        private DDKeyCode? _startHotkey;
        private DDKeyCode? _stopHotkey;
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
            get => _ddDriver.KeyInterval;
            set
            {
                if (_ddDriver.KeyInterval != value)
                {
                    _ddDriver.KeyInterval = value;
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
                    _ddDriver.IsSequenceMode = value;
                    
                    // 更新HotkeyService的按键列表
                    var selectedKeys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                    _hotkeyService.SetKeySequence(selectedKeys, KeyInterval);
                    
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
                    int newInterval = value ? DDDriverService.DEFAULT_KEY_PRESS_INTERVAL : 0;
                    _ddDriver.KeyPressInterval = newInterval;

                    if (!_isInitializing)
                    {
                        SaveConfig();
                    }
                    _logger.Debug($"游戏模式已更改为: {value}, 期望按键间隔: {newInterval}ms, " +
                        $"实际按键间隔: {_ddDriver.KeyPressInterval}ms, 默认按键间隔值: {DDDriverService.DEFAULT_KEY_PRESS_INTERVAL}ms");
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

        public KeyMappingViewModel(DDDriverService ddDriver, ConfigService configService, 
            HotkeyService hotkeyService, MainViewModel mainViewModel, AudioService audioService)
        {
            _ddDriver = ddDriver;
            _configService = configService;
            _hotkeyService = hotkeyService;
            _mainViewModel = mainViewModel;
            _audioService = audioService;
            _hotkeyStatus = "初始化中...";
            _isInitializing = true;

            // 初始化按键列表
            _keyList = new ObservableCollection<KeyItem>();

            // 订阅驱动服务的状态变化
            _ddDriver.EnableStatusChanged += (s, enabled) =>
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
        }

        private void SyncConfigToServices()
        {
            try
            {
                var selectedKeys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                if (selectedKeys.Any())
                {
                    _ddDriver.SetKeyList(selectedKeys);
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
                if (appConfig.keyList != null)
                {
                    KeyList.Clear();
                    for (int i = 0; i < appConfig.keyList.Count; i++)
                    {
                        var keyItem = new KeyItem(appConfig.keyList[i]);
                        keyItem.IsSelected = i < appConfig.keySelections.Count ? 
                            appConfig.keySelections[i] : true;
                        keyItem.SelectionChanged += (s, isSelected) => SaveConfig();
                        KeyList.Add(keyItem);
                    }

                    // 立即同步选中的按键到服务
                    var selectedKeys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                    if (selectedKeys.Any())
                    {
                        _ddDriver.SetKeyList(selectedKeys);
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
                _ddDriver.SetKeyInterval(appConfig.interval);
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
            _ddDriver.KeyPressInterval = IsGameMode ? DDDriverService.DEFAULT_KEY_PRESS_INTERVAL : 0;
        }

        private void InitializeCommands()
        {
            AddKeyCommand = new RelayCommand(AddKey, CanAddKey);
            DeleteSelectedKeysCommand = new RelayCommand(DeleteSelectedKeys);
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
            _ddDriver.KeyIntervalChanged += (s, interval) => OnPropertyChanged(nameof(KeyInterval));

            // 订阅按键项事件
            SubscribeToKeyItemEvents();
        }

        // 设置当前按键
        public void SetCurrentKey(DDKeyCode keyCode)
        {
            _currentKey = keyCode;
            CurrentKeyText = keyCode.ToDisplayName();
            // 通知命令状态更新
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            _logger.Debug("SetCurrentKey", $"设置当前按键: {keyCode} | {CurrentKeyText}");
            

        }

        // 设置开始热键
        public void SetStartHotkey(DDKeyCode keyCode, ModifierKeys modifiers)
        {
            if (IsKeyInList(keyCode))
            {
                _logger.Debug($"该按键已在按键列表中，请选择其他按键: {keyCode}");
                _mainViewModel.UpdateStatusMessage("该按键已在按键列表中，请选择其他按键", true);
                return;
            }

            try
            {
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
                
                _mainViewModel.UpdateStatusMessage($"已设置开始热键: {keyCode.ToDisplayName()}");
                _logger.Debug($"设置开始热键: {keyCode}, 修饰键: {modifiers}");
            }
            catch (Exception ex)
            {
                _logger.Error("设置开始热键失败", ex);
                _mainViewModel.UpdateStatusMessage($"设置开始热键失败: {ex.Message}", true);
            }
        }

        // 更新热键显示文本
        private void UpdateHotkeyText(DDKeyCode keyCode, ModifierKeys modifiers, bool isStart)
        {
            StringBuilder keyText = new StringBuilder();
            
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                keyText.Append("Ctrl + ");
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                keyText.Append("Alt + ");
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                keyText.Append("Shift + ");
            
            keyText.Append(keyCode.ToDisplayName());
            
            if (isStart)
                StartHotkeyText = keyText.ToString();
            else
                StopHotkeyText = keyText.ToString();
        }

        // 设置停止热键
        public void SetStopHotkey(DDKeyCode keyCode, ModifierKeys modifiers)
        {
            if (IsKeyInList(keyCode))
            {
                _logger.Debug($"该按键已在按键列表中，请选择其他按键: {keyCode}");
                _mainViewModel.UpdateStatusMessage("该按键已在按键列表中，请选择其他按键", true);
                return;
            }

            try
            {
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
                
                _mainViewModel.UpdateStatusMessage($"已设置停止热键: {keyCode.ToDisplayName()}");
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

            var newKeyItem = new KeyItem(_currentKey.Value);

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
            
            _mainViewModel.UpdateStatusMessage($" {_currentKey.Value} 按键添加成功");
            _logger.Debug($" {_currentKey.Value} 按键添加成功");
            
            // 清空当前按键状态
            _currentKey = null;
            CurrentKeyText = string.Empty;
        }

        // 删除选中的按键
        private void DeleteSelectedKeys()
        {
            var selectedKeys = KeyList.Where(k => k.IsSelected).ToList();
            foreach (var key in selectedKeys)
            {
                KeyList.Remove(key);
            }
            
            // 更新HotkeyService的按键列表
            UpdateHotkeyServiceKeyList();
        }

        // 添加更新HotkeyService按键列表的辅助方法
        private void UpdateHotkeyServiceKeyList()
        {
            var selectedKeys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
            _hotkeyService.SetKeySequence(selectedKeys, KeyInterval);
            _ddDriver.SetKeyList(selectedKeys);
            _logger.Debug($"更新按键列表 - 选中按键数: {selectedKeys.Count}, 按键间隔: {KeyInterval}ms");
        }

        // 保存配置
        public void SaveConfig()
        {
            try
            {
                // 获取所有按键和它们的选中状态
                var keyList = KeyList.Select(k => k.KeyCode).ToList();
                var keySelections = KeyList.Select(k => k.IsSelected).ToList();
                
                // 检查热键冲突
                if (_startHotkey.HasValue && keyList.Contains(_startHotkey.Value))
                {
                    _mainViewModel.UpdateStatusMessage("启动热键与按键列表存在冲突，请修改后再保存", true);
                    return;
                }

                if (_stopHotkey.HasValue && keyList.Contains(_stopHotkey.Value))
                {
                    _mainViewModel.UpdateStatusMessage("停止热键与按键列表存在冲突，请修改后再保存", true);
                    return;
                }

                // 获取当前配置
                var config = AppConfigService.Config;
                
                // 只更新需要保存的字段
                bool configChanged = false;
                
                // 检查并更新热键配置
                if (config.startKey != _startHotkey || config.startMods != _startModifiers)
                {
                    config.startKey = _startHotkey;
                    config.startMods = _startModifiers;
                    configChanged = true;
                }
                
                if (config.stopKey != _stopHotkey || config.stopMods != _stopModifiers)
                {
                    config.stopKey = _stopHotkey;
                    config.stopMods = _stopModifiers;
                    configChanged = true;
                }
                
                // 检查并更新按键列表
                if (!Enumerable.SequenceEqual(config.keyList ?? new List<DDKeyCode>(), keyList))
                {
                    config.keyList = keyList;
                    configChanged = true;
                }
                
                if (!Enumerable.SequenceEqual(config.keySelections ?? new List<bool>(), keySelections))
                {
                    config.keySelections = keySelections;
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

                // 只有在配置发生变化时才保存
                if (configChanged)
                {
                    AppConfigService.SaveConfig();
                    // 确保同步按键列表状态
                    UpdateHotkeyServiceKeyList();
                    _logger.Debug($"配置已保存 - 声音模式: {IsSoundEnabled}, 游戏模式: {IsGameMode}, 开始热键: {_startHotkey}, 停止热键: {_stopHotkey}, " +
                        $"按键数: {keyList.Count}, 选中按键数: {keySelections.Count(x => x)}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("保存配置失败", ex);
                System.Windows.MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 启动按键映射
        public void StartKeyMapping()
        {
            if (!IsExecuting)
            {
                try
                {
                    // 只获取勾选的按键
                    var keys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                    if (keys.Count == 0)
                    {
                        _logger.Warning("警告：没有选中任何按键");
                        _mainViewModel.UpdateStatusMessage("请至少选择一个按键", true);
                        IsHotkeyEnabled = false;
                        IsExecuting = false;
                        return;
                    }

                    // 记录按键列表
                    foreach (var key in keys)
                    {
                        _logger.Debug($"选中的按键: {key} ({(int)key})");
                    }

                    IsExecuting = true;
                    if (_ddDriver == null) return;

                    // 确保先同步按键列表到服务
                    _ddDriver.SetKeyList(keys);
                    _hotkeyService.SetKeySequence(keys, KeyInterval);
                    
                    // 设置驱动服务
                    _ddDriver.IsSequenceMode = SelectedKeyMode == 0;
                    _ddDriver.SetKeyInterval(KeyInterval);
                    _ddDriver.IsEnabled = true;
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
        }

        // 停止按键映射
        public void StopKeyMapping()
        {
            if (IsExecuting)
            {
                IsExecuting = false;
                try
                {
                    if (_ddDriver == null) return;

                    _logger.Debug("开始停止按键映射");
                    
                    // 先停止热键服务
                    _hotkeyService?.StopSequence();
                    
                    // 然后停止驱动服务
                    _ddDriver.IsEnabled = false;
                    _ddDriver.SetHoldMode(false);
                    
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
            _ddDriver?.SetHoldMode(isHold);
        }

        // 检查按键是否已在列表中
        private bool IsKeyInList(DDKeyCode keyCode)
        {
            return KeyList.Any(k => k.KeyCode == keyCode);
        }

        // 开始热键按下事件处理
        private void OnStartHotkeyPressed()
        {
            try
            {
                // 先检查是否有选中的按键，避免不必要的状态更新
                var keys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                if (!keys.Any())
                {
                    _logger.Warning("没有选中任何按键");
                    _mainViewModel.UpdateStatusMessage("请至少选择一个按键", true);
                    return;
                }
                // 只有在确实有选中按键时才更新UI状态和执行后续操作
                IsExecuting = true;
                _logger.Debug("================================================");
                _logger.Debug($"🍏 ==》 启动热键按下 《==  🍏 | 当前模式: {(SelectedKeyMode == 0 ? "顺序模式" : "按压模式")} ");

                // 设置按键列表参数
                _ddDriver.SetKeyList(keys);
                _hotkeyService.SetKeySequence(keys, KeyInterval);  // 重要: 重置按键列表
                _ddDriver.IsSequenceMode = SelectedKeyMode == 0;
                _ddDriver.SetKeyInterval(KeyInterval);

                if (SelectedKeyMode == 0)
                {
                    _logger.Debug("启动顺序模式");
                    _ddDriver.IsEnabled = true;
                }
                else
                {
                    _logger.Debug("启动按压模式");
                    _ddDriver.SetHoldMode(true);
                }
                IsHotkeyEnabled = true;
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
                _ddDriver.IsEnabled = false;
                _ddDriver.SetHoldMode(false);
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
        public bool IsHotkeyConflict(DDKeyCode keyCode)
        {
            try
            {
                bool isStartConflict = _startHotkey.HasValue && keyCode == _startHotkey.Value;
                bool isStopConflict = _stopHotkey.HasValue && keyCode == _stopHotkey.Value;
                
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
        private void LoadKeyList(List<DDKeyCode> keyList, List<bool> keySelections)
        {
            KeyList.Clear();
            for (int i = 0; i < keyList.Count; i++)
            {
                var keyItem = new KeyItem(keyList[i]);
                keyItem.IsSelected = i < keySelections.Count ? keySelections[i] : true;
                keyItem.SelectionChanged += (s, isSelected) => SaveConfig();
                KeyList.Add(keyItem);
            }
        }


    }
} 