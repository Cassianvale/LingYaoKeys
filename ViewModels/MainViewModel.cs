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
using WpfApp.Models;

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
        private string _statusMessage = "就绪";
        private AppConfig? _config;

        public AppConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = AppConfigService.Config;
                    System.Diagnostics.Debug.WriteLine($"MainViewModel初始化配置 - 窗口尺寸: {_config.UI.MainWindow.DefaultWidth}x{_config.UI.MainWindow.DefaultHeight}");
                }
                return _config;
            }
        }
        
        public string WindowTitle => Config.AppInfo.Title;
        public string VersionInfo => $"v{Config.AppInfo.Version}";

        public Page? CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
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
            
            // 订阅状态消息事件
            _ddDriver.StatusMessageChanged += OnDriverStatusMessageChanged;
            
            // 确保配置已加载
            var config = Config;
            System.Diagnostics.Debug.WriteLine($"MainViewModel构造函数 - 窗口尺寸: {config.UI.MainWindow.DefaultWidth}x{config.UI.MainWindow.DefaultHeight}");
            
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

        // 订阅DDDriverService的事件，用于更新状态栏消息
        private void OnDriverStatusMessageChanged(object? sender, StatusMessageEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = e.Message;
                
                // 如果是错误消息，可以让它显示更长时间
                if (e.IsError)
                {
                    // 可以选择性地添加视觉反馈，比如改变状态栏颜色等
                }
            });
        }
    }
} 