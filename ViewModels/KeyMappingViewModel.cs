using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfApp.Commands;

namespace WpfApp.ViewModels
{
    public class KeyMappingViewModel : ViewModelBase
    {
        private ObservableCollection<KeyMapping> _keyMappings = new();
        private string _startKey = "UNKNOWN";
        private string _stopKey = "UNKNOWN";
        private string _pauseKey = "UNKNOWN";
        private int _keyInterval = 50;
        private bool _playSound = true;
        private string _selectedMode = "顺序模式";

        public ObservableCollection<KeyMapping> KeyMappings
        {
            get => _keyMappings;
            set => SetProperty(ref _keyMappings, value);
        }

        public string StartKey
        {
            get => _startKey;
            set => SetProperty(ref _startKey, value);
        }

        public string StopKey
        {
            get => _stopKey;
            set => SetProperty(ref _stopKey, value);
        }

        public string PauseKey
        {
            get => _pauseKey;
            set => SetProperty(ref _pauseKey, value);
        }

        public int KeyInterval
        {
            get => _keyInterval;
            set => SetProperty(ref _keyInterval, value);
        }

        public bool PlaySound
        {
            get => _playSound;
            set => SetProperty(ref _playSound, value);
        }

        public string SelectedMode
        {
            get => _selectedMode;
            set => SetProperty(ref _selectedMode, value);
        }

        public ICommand AddKeyCommand { get; }
        public ICommand RemoveSelectedCommand { get; }
        public ICommand SetHotkeyCommand { get; }
        public ICommand QueryLeftCommand { get; }

        public KeyMappingViewModel()
        {
            KeyMappings = new ObservableCollection<KeyMapping>
            {
                new KeyMapping { Key = "F9", IsEnabled = true },
                new KeyMapping { Key = "F10", IsEnabled = false }
            };

            AddKeyCommand = new RelayCommand(AddKey);
            RemoveSelectedCommand = new RelayCommand(RemoveSelected);
            SetHotkeyCommand = new RelayCommand<string>(SetHotkey);
            QueryLeftCommand = new RelayCommand(QueryLeft);
        }

        private void AddKey()
        {
            // 实现添加按键逻辑
        }

        private void RemoveSelected()
        {
            // 实现删除选中按键逻辑
        }

        private void SetHotkey(string keyType)
        {
            // 实现设置热键逻辑
        }

        private void QueryLeft()
        {
            // 实现一键宏查询逻辑
        }
    }

    public class KeyMapping : ViewModelBase
    {
        private string _key = string.Empty;
        private bool _isEnabled;

        public string Key
        {
            get => _key;
            set => SetProperty(ref _key, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }
    }
} 