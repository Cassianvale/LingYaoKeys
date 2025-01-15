using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfApp.Services;

namespace WpfApp.Services.Models
{
    public class KeyItem : INotifyPropertyChanged
    {
        private readonly LyKeysService _lyKeysService;
        private bool _isSelected = true;
        private LyKeysCode _keyCode;
        private bool _isKeyBurst;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<bool>? SelectionChanged;

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

        public bool IsKeyBurst
        {
            get => _isKeyBurst;
            set
            {
                if (_isKeyBurst != value)
                {
                    _isKeyBurst = value;
                    OnPropertyChanged();
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