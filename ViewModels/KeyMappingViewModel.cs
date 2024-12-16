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

            // 修改热键事件处理
            _hotkeyService.StartHotkeyPressed += () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("收到开始热键事件");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"处理开始热键 - 当前模式: {SelectedKeyMode}");
                        StartKeyMapping();
                        IsHotkeyEnabled = true;
                        System.Diagnostics.Debug.WriteLine("热键处理完成");
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"处理开始热键异常: {ex}");
                }
            };

            _hotkeyService.StartHotkeyReleased += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (SelectedKeyMode == 1) // 按压模式
                    {
                        SetHoldMode(false);
                    }
                    IsHotkeyEnabled = false;
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
            
            // 设置踪监听器
            Trace.Listeners.Add(new TextWriterTraceListener("debug.log"));
            Trace.AutoFlush = true;
            
            // 加载配置
            var config = _configService.LoadConfig();
            _keyList = new ObservableCollection<KeyItem>();
            
            // 设置热键
            if (config.startKey.HasValue)
            {
                SetStartHotkey(config.startKey.Value, config.startMods);
            }
            if (config.stopKey.HasValue)
            {
                SetStopHotkey(config.stopKey.Value, config.stopMods);
            }
            
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
            _startHotkey = keyCode;
            _startModifiers = modifiers;
            UpdateHotkeyText(keyCode, modifiers, true);
            _hotkeyService.RegisterStartHotkey(keyCode, modifiers);
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
            _stopHotkey = keyCode;
            _stopModifiers = modifiers;
            UpdateHotkeyText(keyCode, modifiers, false);
            _hotkeyService.RegisterStopHotkey(keyCode, modifiers);
        }

        private bool CanAddKey()
        {
            return _currentKey.HasValue;
        }

        private void AddKey()
        {
            if (_currentKey.HasValue)
            {
                KeyList.Add(new KeyItem(_currentKey.Value));
                _currentKey = null;
                CurrentKeyText = string.Empty;
            }
        }

        private void DeleteSelectedKeys()
        {
            var selectedKeys = KeyList.Where(k => k.IsSelected).ToList();
            foreach (var key in selectedKeys)
            {
                KeyList.Remove(key);
            }
        }

        // 添加保存配置的方法
        public void SaveConfig()
        {
            _configService.SaveConfig(
                _startHotkey,
                _startModifiers,
                _stopHotkey,
                _stopModifiers,
                KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList(),
                SelectedKeyMode,
                KeyInterval,
                true  // soundEnabled
            );
        }

        public void StartKeyMapping()
        {
            try
            {
                if (_ddDriver == null)
                {
                    System.Diagnostics.Debug.WriteLine("DDDriver 为空");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine("开始按键映射");
                
                // 获取选中的按键
                var keys = KeyList.Select(k => k.KeyCode).ToList();
                System.Diagnostics.Debug.WriteLine($"按键列表数量: {keys.Count}");
                
                if (keys.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("按键列表为空，无法启动映射");
                    return;
                }
                
                // 设置热键服务的按键序列
                _hotkeyService.SetKeySequence(keys, KeyInterval);
                
                System.Diagnostics.Debug.WriteLine($"按键映射已准备: 模式={SelectedKeyMode}, 间隔={KeyInterval}ms");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动按键映射异常: {ex}");
            }
        }

        public void StopKeyMapping()
        {
            _hotkeyService.StopSequence();
        }

        public void SetHoldMode(bool isHold)
        {
            _ddDriver?.SetHoldMode(isHold);
        }
    }
} 