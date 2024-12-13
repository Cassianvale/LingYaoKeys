using System.Windows.Controls;
using System.Windows.Input;
using WpfApp.Commands;
using WpfApp.Views;

namespace WpfApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private Page _currentPage = new KeyMappingView();
        private readonly KeyMappingViewModel _keyMappingViewModel;
        private readonly SyncSettingsViewModel _syncSettingsViewModel;

        public Page CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public ICommand NavigateCommand { get; }

        public MainViewModel()
        {
            _keyMappingViewModel = new KeyMappingViewModel();
            _syncSettingsViewModel = new SyncSettingsViewModel();
            
            NavigateCommand = new RelayCommand<string>(Navigate);
            
            // 默认显示前台按键页面
            Navigate("FrontKeys");
        }

        private void Navigate(string page)
        {
            CurrentPage = page switch
            {
                "FrontKeys" => new KeyMappingView() { DataContext = _keyMappingViewModel },
                "SyncSettings" => new SyncSettingsView() { DataContext = _syncSettingsViewModel },
                _ => CurrentPage
            };
        }
    }
} 