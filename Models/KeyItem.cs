using WpfApp.Services;
using System.ComponentModel;

namespace WpfApp.Models
{
    public class KeyItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        private readonly DDKeyCode _keyCode;

        public event PropertyChangedEventHandler? PropertyChanged;

        public KeyItem(DDKeyCode keyCode)
        {
            _keyCode = keyCode;
        }

        public string DisplayName => _keyCode.ToDisplayName();
        
        public DDKeyCode KeyCode => _keyCode;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    // 触发选中状态变化事件
                    SelectionChanged?.Invoke(this, value);
                }
            }
        }

        // 添加选中状态变化事件
        public event EventHandler<bool>? SelectionChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 