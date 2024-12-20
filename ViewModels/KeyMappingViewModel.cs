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
        private readonly LogManager _logger = LogManager.Instance;
        private readonly MainViewModel _mainViewModel;
        private bool _isSoundEnabled = true;
        private readonly AudioService _audioService;
        private bool _isGameMode = true; // 默认开启

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
        public ICommand AddKeyCommand { get; }

        // 删除选中的按键命令
        public ICommand DeleteSelectedKeysCommand { get; }

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

        // 热键是否启用
        public bool IsHotkeyEnabled
        {
            get => _isHotkeyEnabled;
            set
            {
                SetProperty(ref _isHotkeyEnabled, value);
                HotkeyStatus = value ? "热键已启用" : "热键已禁用";
            }
        }

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
                    SaveConfig();
                }
            }
        }

        // 是否为游戏模式
        public bool IsGameMode
        {
            get => _isGameMode;
            set
            {
                if (SetProperty(ref _isGameMode, value))
                {
                    _ddDriver.KeyPressInterval = value ? 5 : 0;
                    SaveConfig();
                    _logger.LogDebug("Config", $"游戏模式已更改为: {value}");
                }
            }
        }

        /// <summary>
        /// 按键映射视图模型类
        /// </summary>
        /// <remarks>
        /// 主要职责:
        /// 1. 管理按键映射的核心业务逻辑
        /// 2. 处理热键注册和响应
        /// 3. 维护按键序列和映射状态
        /// 4. 与驱动服务和配置服务交互
        /// </remarks>
        /// <param name="ddDriver">DD驱动服务实例,用于模拟按键操作</param>
        /// <param name="configService">配置服务实例,用于加载和保存配置</param>
        /// <param name="hotkeyService">热键服务实例,用于注册和响应全局热键</param>
        /// <param name="mainViewModel">主视图模型实例,用于更新状态消息</param>
        public KeyMappingViewModel(DDDriverService ddDriver, ConfigService configService, 
            HotkeyService hotkeyService, MainViewModel mainViewModel)
        {
            _ddDriver = ddDriver;
            _configService = configService;
            _hotkeyService = hotkeyService;
            _mainViewModel = mainViewModel;
            _audioService = App.AudioService;

            // 订阅驱动服务的状态变化
            _ddDriver.EnableStatusChanged += (s, enabled) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsHotkeyEnabled = enabled;
                });
            };

            // 修改热键事件处理
            _hotkeyService.StartHotkeyPressed += OnStartHotkeyPressed;
            _hotkeyService.StopHotkeyPressed += OnStopHotkeyPressed;

            // 加载 JSON 配置
            try 
            {
                var appConfig = AppConfigService.Config;
                _keyList = new ObservableCollection<KeyItem>();
                
                // 加载按键列表和选中状态
                if (appConfig.keyList != null)
                {
                    for (int i = 0; i < appConfig.keyList.Count; i++)
                    {
                        var keyItem = new KeyItem(appConfig.keyList[i]);
                        keyItem.IsSelected = i < appConfig.keySelections.Count ? 
                            appConfig.keySelections[i] : true;
                        KeyList.Add(keyItem);
                    }
                }
                
                // 加载开始热键
                if (appConfig.startKey.HasValue)
                {
                    SetStartHotkey(appConfig.startKey.Value, appConfig.startMods);
                    _logger.LogDebug("Config", $"已加载开始热键: {appConfig.startKey.Value}, 修饰键: {appConfig.startMods}");
                }

                // 加载停止热键
                if (appConfig.stopKey.HasValue)
                {
                    SetStopHotkey(appConfig.stopKey.Value, appConfig.stopMods);
                    _logger.LogDebug("Config", $"已加载停止热键: {appConfig.stopKey.Value}, 修饰键: {appConfig.stopMods}");
                }
                
                // 设置其他选项
                _ddDriver.SetKeyInterval(appConfig.interval);
                SelectedKeyMode = appConfig.keyMode;
                IsSequenceMode = appConfig.keyMode == 0;
                
                // 从配置文件加载声音和游戏模式设置
                IsSoundEnabled = appConfig.soundEnabled;
                IsGameMode = appConfig.IsGameMode;
                
                // 根据游戏模式设置按键间隔
                _ddDriver.KeyPressInterval = _isGameMode ? 5 : 0;
                
                _logger.LogInitialization("ViewModel", $"已加载配置 - 按键模式: {appConfig.keyMode}, 游戏模式: {IsGameMode}, 声音: {IsSoundEnabled}");
            }
            catch (Exception ex)
            {
                _logger.LogError("ViewModel", "加载配置失败", ex);
                // 使用默认值，但保持IsGameMode的当前值
                IsSequenceMode = true;
                IsSoundEnabled = true;
                // 不重置IsGameMode，保持其当前值
                _ddDriver.KeyPressInterval = IsGameMode ? 5 : 0;
                _logger.LogDebug("Config", $"加载配置失败，保持游戏模式为: {IsGameMode}");
            }

            AddKeyCommand = new RelayCommand(AddKey, CanAddKey);
            DeleteSelectedKeysCommand = new RelayCommand(DeleteSelectedKeys);

            IsHotkeyEnabled = false;
            HotkeyStatus = "热键已禁用";

            // 订阅热键服务的事件
            _hotkeyService.SequenceModeStarted += () =>
            {
                IsHotkeyEnabled = true;
            };

            _hotkeyService.SequenceModeStopped += () =>
            {
                IsHotkeyEnabled = false;
            };

            // 加载声音设置
            _isSoundEnabled = _configService.GetSetting("SoundEnabled", true);
            
            // 在状态改变时播放声音
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

            // 订阅DDDriverService的KeyInterval变化
            _ddDriver.KeyIntervalChanged += (s, interval) =>
            {
                OnPropertyChanged(nameof(KeyInterval));
            };

            // 为现有的按键项添加事件订阅
            SubscribeToKeyItemEvents();
        }

        // 设置当前按键
        public void SetCurrentKey(DDKeyCode keyCode)
        {
            _logger.LogDebug("KeyMapping", $"设置当前按键: {keyCode}");
            _currentKey = keyCode;
            CurrentKeyText = keyCode.ToDisplayName();
        }

        // 设置开始热键
        public void SetStartHotkey(DDKeyCode keyCode, ModifierKeys modifiers)
        {
            if (IsKeyInList(keyCode))
            {
                _mainViewModel.UpdateStatusMessage("该按键已在按键列表中，请选择其他按键", true);
                return;
            }

            try
            {
                _startHotkey = keyCode;
                _startModifiers = modifiers;
                UpdateHotkeyText(keyCode, modifiers, true);
                
                bool result = _hotkeyService.RegisterStartHotkey(keyCode, modifiers);
                if (!result && !_hotkeyService.IsMouseButton(keyCode))
                {
                    _logger.LogError("KeyMapping", $"注册开始热键失败: {keyCode}");
                    _mainViewModel.UpdateStatusMessage("开始热键注册失败，请尝试其他按键", true);
                    return;
                }
                
                _mainViewModel.UpdateStatusMessage($"已设置开始热键: {keyCode.ToDisplayName()}");
                _logger.LogDebug("KeyMapping", $"设置开始热键: {keyCode}, 修饰键: {modifiers}");
            }
            catch (Exception ex)
            {
                _logger.LogError("KeyMapping", "设置开始热键失败", ex);
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
                _mainViewModel.UpdateStatusMessage("该按键已在按键列表中，请选择其他按键", true);
                return;
            }

            try
            {
                _stopHotkey = keyCode;
                _stopModifiers = modifiers;
                UpdateHotkeyText(keyCode, modifiers, false);
                
                bool result = _hotkeyService.RegisterStopHotkey(keyCode, modifiers);
                if (!result)
                {
                    _logger.LogError("Hotkey", $"注册停止热键失败: {keyCode}, 修饰键: {modifiers}");
                    _mainViewModel.UpdateStatusMessage("停止热键注册失败，请尝试其他按键", true);
                    return;
                }
                
                _mainViewModel.UpdateStatusMessage($"已设置停止热键: {keyCode.ToDisplayName()}");
                _logger.LogDebug("Hotkey", $"设置停止热键: {keyCode}, 修饰键: {modifiers}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Hotkey", "设置停止热键失败", ex);
                _mainViewModel.UpdateStatusMessage($"设置停止热键失败: {ex.Message}", true);
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
            _logger.LogDebug("KeyMapping", $"尝试添加按键，当前按键: {_currentKey}");
            
            if (!_currentKey.HasValue)
            {
                _logger.LogWarning("KeyMapping", "当前按键为空，无法添加");
                return;
            }

            if (_startHotkey.HasValue && _currentKey.Value == _startHotkey.Value)
            {
                _mainViewModel.UpdateStatusMessage("该按键已被设置为启动热键，请选择其他按键", true);
                return;
            }

            if (_stopHotkey.HasValue && _currentKey.Value == _stopHotkey.Value)
            {
                _mainViewModel.UpdateStatusMessage("该按键已被设置为停止热键，请选择其他按键", true);
                return;
            }

            if (IsKeyInList(_currentKey.Value))
            {
                _mainViewModel.UpdateStatusMessage("该按键已在列表中，请选择其他按键", true);
                return;
            }

            var newKeyItem = new KeyItem(_currentKey.Value);
            // 订阅选中状态变化事件
            newKeyItem.SelectionChanged += (s, isSelected) => SaveConfig();
            KeyList.Add(newKeyItem);
            _mainViewModel.UpdateStatusMessage("按键添加成功");
        }

        // 删除选中的按键
        private void DeleteSelectedKeys()
        {
            var selectedKeys = KeyList.Where(k => k.IsSelected).ToList();
            foreach (var key in selectedKeys)
            {
                KeyList.Remove(key);
            }
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
                    MessageBox.Show("启动热键与按键列表存在冲突，请修改后再保存", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_stopHotkey.HasValue && keyList.Contains(_stopHotkey.Value))
                {
                    MessageBox.Show("停止热键与按键列表存在冲突，请修改后再保存", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    _logger.LogDebug("Config", $"配置已保存 - 开始热键: {_startHotkey}, 停止热键: {_stopHotkey}, " +
                        $"按键数: {keyList.Count}, 选中按键数: {keySelections.Count(x => x)}, 游戏模式: {IsGameMode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Config", "保存配置失败", ex);
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 启动按键映射
        public void StartKeyMapping()
        {
            if (_ddDriver == null) return;
            
            try
            {
                // 只获取勾选的按键
                var keys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                if (keys.Count == 0)
                {
                    _logger.LogWarning("KeyMapping", "警告：没有选中任何按键");
                    MessageBox.Show("请至少选择一个按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    IsHotkeyEnabled = false;
                    return;
                }

                // 记录按键列表
                foreach (var key in keys)
                {
                    _logger.LogDebug("KeyMapping", $"选中的按键: {key} ({(int)key})");
                }

                // 设置按键列表和间隔时间
                _hotkeyService.SetKeySequence(keys, KeyInterval);
                
                // 设置驱动服务
                _ddDriver.SetKeyList(keys);
                _ddDriver.IsSequenceMode = SelectedKeyMode == 0;
                _ddDriver.SetKeyInterval(KeyInterval);
                _ddDriver.IsEnabled = true;
                IsHotkeyEnabled = true;

                _logger.LogDebug("KeyMapping", 
                    $"按键映射已启动: 模式={SelectedKeyMode}, 选中按键数={keys.Count}, 间隔={KeyInterval}ms");
            }
            catch (Exception ex)
            {
                _logger.LogError("KeyMapping", "启动按键映射失败", ex);
                IsHotkeyEnabled = false;
            }
        }

        // 停止按键映射
        public void StopKeyMapping()
        {
            try
            {
                if (_ddDriver == null) return;

                _logger.LogDebug("KeyMapping", "开始停止按键映射");
                
                // 先停止热键服务
                _hotkeyService?.StopSequence();
                
                // 然后停止驱动服务
                _ddDriver.IsEnabled = false;
                _ddDriver.SetHoldMode(false);
                
                // 最后更新UI状态
                IsHotkeyEnabled = false;
                
                _logger.LogDebug("KeyMapping", "按键映射已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError("KeyMapping", "停止按键映射失败", ex);
                IsHotkeyEnabled = false;
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
                _logger.LogDebug("Hotkey", $"开始热键按下 - 当前模式: {SelectedKeyMode}");
                
                // 只获取勾选的按键
                var keys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                if (!keys.Any())
                {
                    _logger.LogWarning("Hotkey", "没有选中任何按键");
                    MessageBox.Show("请至少选择一个按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 设置按键列表和参数
                _ddDriver.SetKeyList(keys);
                _ddDriver.IsSequenceMode = SelectedKeyMode == 0;
                _ddDriver.SetKeyInterval(KeyInterval);

                if (SelectedKeyMode == 0)
                {
                    _logger.LogDebug("Hotkey", "启动顺序模式");
                    _ddDriver.IsEnabled = true;
                }
                else
                {
                    _logger.LogDebug("Hotkey", "启动按压模式");
                    _ddDriver.SetHoldMode(true);
                }
                IsHotkeyEnabled = true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Hotkey", "启动按键映射异常", ex);
                IsHotkeyEnabled = false;
            }
        }

        // 停止热键按下事件处理
        private void OnStopHotkeyPressed()
        {
            try
            {
                _logger.LogDebug("Hotkey", "停止热键按下");
                _ddDriver.IsEnabled = false;
                _ddDriver.SetHoldMode(false);
                IsHotkeyEnabled = false;
            }
            catch (Exception ex)
            {
                _logger.LogError("Hotkey", "停止按键映射异常", ex);
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
                    _logger.LogDebug("KeyMapping", 
                        $"检测到热键冲突 - 按键: {keyCode}, 启动键冲突: {isStartConflict}, 停止键冲突: {isStopConflict}");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError("KeyMapping", "检查热键冲突时发生异常", ex);
                return false;
            }
        }

        // 为现有的KeyList项添加事件订阅
        private void SubscribeToKeyItemEvents()
        {
            foreach (var keyItem in KeyList)
            {
                keyItem.SelectionChanged += (s, isSelected) => SaveConfig();
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