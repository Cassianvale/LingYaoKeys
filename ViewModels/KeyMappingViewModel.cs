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
        private int _keyInterval = 50; // 默认按键间隔为50
        private int _selectedKeyMode;
        private ModifierKeys _startModifiers = ModifierKeys.None;
        private ModifierKeys _stopModifiers = ModifierKeys.None;
        private readonly HotkeyService _hotkeyService;
        private bool _isHotkeyEnabled;
        private string _hotkeyStatus;

        public ObservableCollection<KeyItem> KeyList
        {
            get => _keyList;
            set => SetProperty(ref _keyList, value);
        }

        public string CurrentKeyText
        {
            get => _currentKeyText;
            set
            {
                _currentKeyText = value;
                OnPropertyChanged(nameof(CurrentKeyText));
            }
        }

        public string StartHotkeyText
        {
            get => _startHotkeyText;
            set => SetProperty(ref _startHotkeyText, value);
        }

        public string StopHotkeyText
        {
            get => _stopHotkeyText;
            set => SetProperty(ref _stopHotkeyText, value);
        }

        public int KeyInterval
        {
            get => _keyInterval;
            set => SetProperty(ref _keyInterval, value);
        }

        public ICommand AddKeyCommand { get; }
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
                    Trace.WriteLine($"Selected key mode changed to: {value}");
                }
            }
        }

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

        public KeyMappingViewModel(DDDriverService ddDriver, ConfigService configService, HotkeyService hotkeyService)
        {
            _ddDriver = ddDriver;
            _configService = configService;
            _hotkeyService = hotkeyService;

            // 订阅驱动服务的状态变化
            _ddDriver.EnableStatusChanged += (s, enabled) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsHotkeyEnabled = enabled;
                });
            };

            // 修改热键事件处理
            _hotkeyService.StartHotkeyPressed += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"开始热键触发，当前模式: {SelectedKeyMode}");
                        if (!IsHotkeyEnabled && KeyList.Any())
                        {
                            StartKeyMapping();
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("无法启动：按键列表为空或已在运行中");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"启动按键映射异常: {ex.Message}");
                        IsHotkeyEnabled = false;
                    }
                });
            };

            _hotkeyService.StopHotkeyPressed += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StopKeyMapping();
                    IsHotkeyEnabled = false;
                });
            };

            // 加载配置并自动注册热键
            var config = _configService.LoadConfig();
            _keyList = new ObservableCollection<KeyItem>();
            
            // 加载开始热键
            if (config.startKey.HasValue)
            {
                SetStartHotkey(config.startKey.Value, config.startMods);
                System.Diagnostics.Debug.WriteLine($"已加载开始热键: {config.startKey.Value}, 修饰键: {config.startMods}");
            }

            // 加载停止热键
            if (config.stopKey.HasValue)
            {
                SetStopHotkey(config.stopKey.Value, config.stopMods);
                System.Diagnostics.Debug.WriteLine($"已加载停止热键: {config.stopKey.Value}, 修饰键: {config.stopMods}");
            }
            
            // 设置跟踪监听器
            Trace.Listeners.Add(new TextWriterTraceListener("debug.log"));
            Trace.AutoFlush = true;
            
            // 设置按键列表
            foreach (var key in config.keyList)
            {
                KeyList.Add(new KeyItem(key));
            }
            
            // 设置其他选项
            KeyInterval = config.interval;
            SelectedKeyMode = config.keyMode;
            Trace.WriteLine($"Initialized key mode to: {config.keyMode}");

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
        }

        public void SetCurrentKey(DDKeyCode keyCode)
        {
            _currentKey = keyCode;
            CurrentKeyText = keyCode.ToDisplayName();
        }

        public void SetStartHotkey(DDKeyCode keyCode, ModifierKeys modifiers)
        {
            if (IsKeyInList(keyCode))
            {
                MessageBox.Show("该按键已在按键列表中，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_stopHotkey.HasValue && _stopHotkey.Value == keyCode && _stopModifiers == modifiers)
            {
                MessageBox.Show("该按键已被设置为停止键，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _startHotkey = keyCode;
            _startModifiers = modifiers;
            UpdateHotkeyText(keyCode, modifiers, true);
            _hotkeyService.RegisterStartHotkey(keyCode, modifiers);
            System.Diagnostics.Debug.WriteLine($"设置开始热键: {keyCode}, 修饰键: {modifiers}");
        }

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

        public void SetStopHotkey(DDKeyCode keyCode, ModifierKeys modifiers)
        {
            if (IsKeyInList(keyCode))
            {
                MessageBox.Show("该按键已在按键列表中，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_startHotkey.HasValue && _startHotkey.Value == keyCode && _startModifiers == modifiers)
            {
                MessageBox.Show("该按键已被设置为启动键，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    System.Diagnostics.Debug.WriteLine($"注册停止热键失败: {keyCode}, 修饰键: {modifiers}");
                    MessageBox.Show("停止热键注册失败，请尝试其他按键", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"设置停止热键: {keyCode}, 修饰键: {modifiers}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置停止热键异常: {ex.Message}");
                MessageBox.Show($"设置停止热键失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanAddKey()
        {
            return _currentKey.HasValue;
        }

        private void AddKey()
        {
            if (!_currentKey.HasValue) return;

            if (_startHotkey.HasValue && _currentKey.Value == _startHotkey.Value)
            {
                MessageBox.Show("该按键已被设置为启动热键，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_stopHotkey.HasValue && _currentKey.Value == _stopHotkey.Value)
            {
                MessageBox.Show("该按键已被设置为停止热键，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsKeyInList(_currentKey.Value))
            {
                MessageBox.Show("该按键已在列表中，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            KeyList.Add(new KeyItem(_currentKey.Value));
            _currentKey = null;
            CurrentKeyText = string.Empty;
            System.Diagnostics.Debug.WriteLine($"添加按键到列表: {_currentKey}");
        }

        private void DeleteSelectedKeys()
        {
            var selectedKeys = KeyList.Where(k => k.IsSelected).ToList();
            foreach (var key in selectedKeys)
            {
                KeyList.Remove(key);
            }
        }

        public void SaveConfig()
        {
            try
            {
                var selectedKeys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                
                // 检查热键冲突
                if (_startHotkey.HasValue && selectedKeys.Contains(_startHotkey.Value))
                {
                    MessageBox.Show("启动热键与按键列表存在冲突，请修改后再保存", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_stopHotkey.HasValue && selectedKeys.Contains(_stopHotkey.Value))
                {
                    MessageBox.Show("停止热键与按键列表存在冲突，请修改后再保存", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _configService.SaveConfig(
                    _startHotkey,
                    _startModifiers,
                    _stopHotkey,  // 确保停止热键被保存
                    _stopModifiers,
                    selectedKeys,
                    SelectedKeyMode,
                    KeyInterval,
                    true  // soundEnabled
                );

                System.Diagnostics.Debug.WriteLine($"配置已保存 - 开始热键: {_startHotkey}, 停止热键: {_stopHotkey}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置异常: {ex.Message}");
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void StartKeyMapping()
        {
            if (_ddDriver == null) return;
            
            try
            {
                var keys = KeyList.Select(k => k.KeyCode).ToList();
                if (keys.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("警告：按键列表为空");
                    IsHotkeyEnabled = false; // 确保状态正确
                    return;
                }

                // 设置热键服务的按键序列
                _hotkeyService.SetKeySequence(keys, KeyInterval);
                
                // 设置驱动服务
                _ddDriver.SetKeyList(keys);
                _ddDriver.IsSequenceMode = SelectedKeyMode == 0;
                _ddDriver.SetKeyInterval(KeyInterval);
                _ddDriver.IsEnabled = true;
                IsHotkeyEnabled = true; // 更新UI状态

                System.Diagnostics.Debug.WriteLine($"按键映射已启动: 模式={SelectedKeyMode}, 按键数={keys.Count}, 间隔={KeyInterval}ms");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动按键映射失败: {ex.Message}");
                IsHotkeyEnabled = false; // 确保错误���状态正确
            }
        }

        public void StopKeyMapping()
        {
            try
            {
                _ddDriver.IsEnabled = false;
                _hotkeyService.StopSequence(); // 确保热键服务也停止
                IsHotkeyEnabled = false; // 更新UI状态
                System.Diagnostics.Debug.WriteLine("按键映射已停止");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止按键映射失败: {ex.Message}");
                IsHotkeyEnabled = false; // 确保错误时状态正确
            }
        }

        public void SetHoldMode(bool isHold)
        {
            _ddDriver?.SetHoldMode(isHold);
        }

        private bool IsKeyInList(DDKeyCode keyCode)
        {
            return KeyList.Any(k => k.KeyCode == keyCode);
        }
    }
} 