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
using System.Windows.Media;
using System.Windows.Threading;
using System;

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
        private Brush _statusMessageColor = Brushes.Black;
        private AppConfig? _config;
        private readonly DispatcherTimer _statusMessageTimer;
        private const int STATUS_MESSAGE_TIMEOUT = 3000; // 3秒后消失

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
            set
            {
                if (_currentPage != value)
                {
                    _currentPage = value;
                    OnPropertyChanged();
                }
            }
        }

        public object? CurrentViewModel => CurrentPage?.DataContext;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public Brush StatusMessageColor
        {
            get => _statusMessageColor;
            set => SetProperty(ref _statusMessageColor, value);
        }

        public ICommand NavigateCommand { get; }

        public MainViewModel(DDDriverService ddDriver, Window mainWindow)
        {
            // 首先初始化定时器
            _statusMessageTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(STATUS_MESSAGE_TIMEOUT)
            };
            _statusMessageTimer.Tick += (s, e) =>
            {
                _statusMessageTimer.Stop();
                StatusMessage = "就绪";
                StatusMessageColor = Brushes.Black;
            };

            // 其他初始化
            _ddDriver = ddDriver;
            _mainWindow = mainWindow;
            _hotkeyService = new HotkeyService(mainWindow, ddDriver);
            _keyMappingViewModel = new KeyMappingViewModel(_ddDriver, App.ConfigService, _hotkeyService, this);
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
            SaveConfig();
            _hotkeyService?.Dispose();
            _statusMessageTimer.Stop(); // 停止定时器
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
                // 停止之前的定时器
                _statusMessageTimer.Stop();

                StatusMessage = e.Message;
                StatusMessageColor = e.IsError ? Brushes.Red : Brushes.Black;
                
                // 启动定时器
                _statusMessageTimer.Start();

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

        public void UpdateStatusMessage(string message, bool isError = false)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 停止之前的定时器
                _statusMessageTimer.Stop();

                // 更新消息
                StatusMessage = message;
                StatusMessageColor = isError ? Brushes.Red : Brushes.Black;

                // 启动定时器
                _statusMessageTimer.Start();
            });
        }
    }
} 