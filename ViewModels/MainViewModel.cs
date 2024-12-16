using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp.Commands;
using WpfApp.Views;
using System.IO;
using System.Threading;
using WpfApp.Services;
using WpfApp.ViewModels;
using System.Diagnostics;

namespace WpfApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private Page? _currentPage;
        private readonly DDDriverService _ddDriver;
        private readonly Window _mainWindow;
        private readonly KeyMappingViewModel _keyMappingViewModel;
        private readonly SyncSettingsViewModel _syncSettingsViewModel;
        private readonly HotkeyService _hotkeyService;

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
            _hotkeyService = new HotkeyService(mainWindow, ddDriver);
            _keyMappingViewModel = new KeyMappingViewModel(_ddDriver, App.ConfigService, _hotkeyService);
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
            System.Diagnostics.Debug.WriteLine("开始清理资源...");
            SaveConfig(); // 只在这里保存一次配置
            _hotkeyService?.Dispose();
            System.Diagnostics.Debug.WriteLine("资源清理完成");
        }

        public void SaveConfig()
        {
            System.Diagnostics.Debug.WriteLine("开始保存应用程序配置...");
            _keyMappingViewModel.SaveConfig();
            System.Diagnostics.Debug.WriteLine("配置保存完成");
            System.Diagnostics.Debug.WriteLine($"--------------------------------");
        }
    }
} 