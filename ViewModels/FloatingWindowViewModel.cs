using System.Windows;
using System.Windows.Input;
using WpfApp.Commands;
using WpfApp.Services;

namespace WpfApp.ViewModels
{
    public class FloatingWindowViewModel : ViewModelBase
    {
        private readonly DDDriverService _ddDriver;
        private bool _isExecuting;
        private string _statusText;
        private bool _isTopmost;
        private double _left;
        private double _top;

        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                if (SetProperty(ref _isExecuting, value))
                {
                    StatusText = value ? "运行中" : "已停止";
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool IsTopmost
        {
            get => _isTopmost;
            set => SetProperty(ref _isTopmost, value);
        }

        public double Left
        {
            get => _left;
            set => SetProperty(ref _left, value);
        }

        public double Top
        {
            get => _top;
            set => SetProperty(ref _top, value);
        }

        public ICommand ToggleTopmostCommand { get; }

        public FloatingWindowViewModel(DDDriverService ddDriver)
        {
            _ddDriver = ddDriver;
            _statusText = "已停止";
            _isTopmost = true;

            ToggleTopmostCommand = new RelayCommand(() => IsTopmost = !IsTopmost);

            // 设置初始位置（屏幕右下角）
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            Left = screenWidth - 100;
            Top = screenHeight - 100;
        }

        public void UpdateExecutionStatus(bool isExecuting)
        {
            IsExecuting = isExecuting;
        }
    }
} 