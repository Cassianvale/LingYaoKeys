using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using WpfApp.Models;
using WpfApp.Services;
using WpfApp.Commands;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

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

        public KeyMappingViewModel(DDDriverService ddDriver, ConfigService configService, HotkeyService hotkeyService)
        {
            _ddDriver = ddDriver;
            _configService = configService;
            _hotkeyService = hotkeyService;

            // 注册热键事件处理
            _hotkeyService.StartHotkeyPressed += OnStartHotkeyPressed;
            _hotkeyService.StartHotkeyReleased += OnStartHotkeyReleased;
            _hotkeyService.StopHotkeyPressed += OnStopHotkeyPressed;
            
            // 设置跟踪监听器
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
            if (_ddDriver == null) return;
            
            // 设置按键列表
            var keys = KeyList.Select(k => k.KeyCode).ToList();
            _ddDriver.SetKeyList(keys);
            
            // 设置模式和间隔
            _ddDriver.IsSequenceMode = SelectedKeyMode == 0; // 0为顺序模式
            _ddDriver.SetKeyInterval(KeyInterval);
            
            // 启动服务
            if (_ddDriver.IsSequenceMode)
            {
                _ddDriver.IsEnabled = true;
            }
        }

        public void StopKeyMapping()
        {
            _ddDriver.IsEnabled = false;
        }

        public void SetHoldMode(bool isHold)
        {
            _ddDriver?.SetHoldMode(isHold);
        }

        private void OnStartHotkeyPressed()
        {
            if (SelectedKeyMode == 0) // 顺序模式
            {
                StartKeyMapping();
            }
            else // 按压模式
            {
                StartKeyMapping();
                SetHoldMode(true);
            }
        }

        private void OnStartHotkeyReleased()
        {
            if (SelectedKeyMode == 1) // 按压模式
            {
                SetHoldMode(false);
            }
        }

        private void OnStopHotkeyPressed()
        {
            StopKeyMapping();
        }
    }
} 