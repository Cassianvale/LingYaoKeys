using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp.ViewModels
{
    public class FloatingStatusViewModel : INotifyPropertyChanged
    {
        private string _statusText = "已停止";
        
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 