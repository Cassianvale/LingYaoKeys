using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp.Commands;
using WpfApp.Views;
using System.IO;
using System.Threading;
using WpfApp.Services;
using WpfApp.ViewModels;
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
        private readonly LogManager _logger = LogManager.Instance;
        private string _statusMessage = "就绪";
        private AppConfig? _config;

        public AppConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = AppConfigService.Config;
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
            
            if (CurrentPage != null)
            {
                _logger.LogDebug("Navigation", $"页面切换到: {parameter}");
            }
        }

        public void Cleanup()
        {
            _logger.LogDebug("MainViewModel", "开始清理资源...");
            SaveConfig(); // 只在这里保存一次配置
            _hotkeyService?.Dispose();
            _logger.LogDebug("MainViewModel", "资源清理完成");
        }

        public void SaveConfig()
        {
            _logger.LogDebug("MainViewModel", "开始保存应用程序配置...");
            _keyMappingViewModel.SaveConfig();
            _logger.LogDebug("MainViewModel", "配置保存完成");
            _logger.LogDebug("MainViewModel", "--------------------------------");
        }

        // 订阅DDDriverService的事件，用于更新状态栏消息
        private void OnDriverStatusMessageChanged(object? sender, StatusMessageEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = e.Message;
                
                // 如果是错误消息，记录到日志
                if (e.IsError)
                {
                    _logger.LogError("MainViewModel", $"驱动状态错误: {e.Message}");
                }
                else
                {
                    _logger.LogDebug("MainViewModel", $"驱动状态更新: {e.Message}");
                }
            });
        }
    }
} 