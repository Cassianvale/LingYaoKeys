using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using WpfApp.Models;
using WpfApp.Services;
using WpfApp.Commands;
using System.Text;

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

        public KeyMappingViewModel(DDDriverService ddDriver, ConfigService configService)
        {
            _ddDriver = ddDriver;
            _configService = configService;
            
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
            // ... 设置其他属性 ...

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
            System.Diagnostics.Debug.WriteLine($"SetStartHotkey called with keyCode: {keyCode}, modifiers: {modifiers}");
            _startHotkey = keyCode;
            StringBuilder keyText = new StringBuilder();
            
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                keyText.Append("Ctrl + ");
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                keyText.Append("Alt + ");
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                keyText.Append("Shift + ");
            
            keyText.Append(keyCode.ToDisplayName());
            StartHotkeyText = keyText.ToString();
            
            // 强制触发UI更新
            OnPropertyChanged(nameof(StartHotkeyText));
        }

        public void SetStopHotkey(DDKeyCode keyCode, ModifierKeys modifiers)
        {
            System.Diagnostics.Debug.WriteLine($"SetStopHotkey called with keyCode: {keyCode}, modifiers: {modifiers}");
            _stopHotkey = keyCode;
            StringBuilder keyText = new StringBuilder();
            
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                keyText.Append("Ctrl + ");
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                keyText.Append("Alt + ");
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                keyText.Append("Shift + ");
            
            keyText.Append(keyCode.ToDisplayName());
            StopHotkeyText = keyText.ToString();
            
            // 强制触发UI更新
            OnPropertyChanged(nameof(StopHotkeyText));
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
            var keyList = KeyList.Select(k => k.KeyCode).ToList();
            _configService.SaveConfig(
                _startHotkey, Keyboard.Modifiers,
                _stopHotkey, Keyboard.Modifiers,
                keyList,
                0, // keyMode
                KeyInterval,
                true // soundEnabled
            );
        }
    }
} 