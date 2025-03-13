using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfApp.Services.Core;

namespace WpfApp.Services.Models
{
    public class KeyItem : INotifyPropertyChanged
    {
        private readonly LyKeysService _lyKeysService;
        private bool _isSelected = true;
        private LyKeysCode _keyCode;
        private int _keyInterval = 5;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<bool>? SelectionChanged;
        public event EventHandler<int>? KeyIntervalChanged;

        public KeyItem(LyKeysCode keyCode, LyKeysService lyKeysService)
        {
            _keyCode = keyCode;
            _lyKeysService = lyKeysService ?? throw new ArgumentNullException(nameof(lyKeysService));
        }

        public LyKeysCode KeyCode
        {
            get => _keyCode;
            set
            {
                if (_keyCode != value)
                {
                    _keyCode = value;
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    SelectionChanged?.Invoke(this, value);
                }
            }
        }

        public int KeyInterval
        {
            get => _keyInterval;
            set
            {
                if (_keyInterval != value)
                {
                    _keyInterval = value;
                    OnPropertyChanged();
                    KeyIntervalChanged?.Invoke(this, value);
                }
            }
        }

        public string DisplayName => _lyKeysService.GetKeyDescription(_keyCode);

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 