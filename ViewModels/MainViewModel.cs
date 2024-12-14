using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp.Commands;
using WpfApp.Views;
using System.IO;
using System.Threading;
using WpfApp.Services;
using WpfApp.ViewModels;

namespace WpfApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private Page? _currentPage;
        private readonly DDDriverService _ddDriver;
        private readonly Window _mainWindow;
        private readonly KeyMappingViewModel _keyMappingViewModel;
        private readonly SyncSettingsViewModel _syncSettingsViewModel;

        public Page? CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public ICommand NavigateCommand { get; }

        public MainViewModel(DDDriverService ddDriver, Window mainWindow)
        {
            _ddDriver = ddDriver;
            _mainWindow = mainWindow;
            
            // 初始化子ViewModel
            // 初始化键盘映射
            _keyMappingViewModel = new KeyMappingViewModel(_ddDriver, App.ConfigService);
            // 初始化同步设置
            _syncSettingsViewModel = new SyncSettingsViewModel();
            
            NavigateCommand = new RelayCommand<string>(Navigate);
            
            // 初始化时设置默认页面
            Navigate("FrontKeys");
        }

        private void Navigate(string? parameter)
        {
            CurrentPage = parameter switch
            {
                "FrontKeys" => new KeyMappingView { DataContext = _keyMappingViewModel },
                "SyncSettings" => new SyncSettingsView { DataContext = _syncSettingsViewModel },
                _ => CurrentPage
            };
        }

        public void Cleanup()
        {
            // 清理资源
        }
    }
} 