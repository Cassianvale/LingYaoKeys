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
using System.Collections.Generic;
using System.Windows.Media.Animation;

namespace WpfApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private Page? _currentPage;
        private readonly DDDriverService _ddDriver;
        private readonly Window _mainWindow;
        private readonly KeyMappingViewModel _keyMappingViewModel;
        private readonly FeedbackViewModel _feedbackViewModel;
        private readonly HotkeyService _hotkeyService;
        private readonly LogManager _logger = LogManager.Instance;
        private string _statusMessage = "就绪";
        private Brush _statusMessageColor = Brushes.Black;
        private AppConfig? _config;
        private readonly DispatcherTimer _statusMessageTimer;
        private const int STATUS_MESSAGE_TIMEOUT = 3000; // 3秒后消失
        private readonly AboutViewModel _aboutViewModel;
        private readonly Dictionary<string, Page> _pageCache = new();
        private readonly Dictionary<string, Storyboard> _fadeInCache = new();
        private readonly Dictionary<string, Storyboard> _fadeOutCache = new();
        private readonly Storyboard? _fadeInStoryboard;
        private readonly Storyboard? _fadeOutStoryboard;
        private bool _isFloatingWindowEnabled;
        private FloatingWindow? _floatingWindow;
        private FloatingWindowViewModel? _floatingWindowViewModel;

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
        public string AuthorInfo => $"By: {Config.Author} | {VersionInfo}";

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

        public bool IsFloatingWindowEnabled
        {
            get => _isFloatingWindowEnabled;
            set
            {
                if (SetProperty(ref _isFloatingWindowEnabled, value))
                {
                    UpdateFloatingWindowVisibility();
                    // 保存到配置文件
                    AppConfigService.UpdateConfig(config =>
                    {
                        config.IsFloatingWindowEnabled = value;
                    });
                }
            }
        }

        public MainViewModel(DDDriverService ddDriver, Window mainWindow)
        {
            _ddDriver = ddDriver;
            _mainWindow = mainWindow;

            // 从配置文件加载浮窗状态
            _isFloatingWindowEnabled = AppConfigService.Config.IsFloatingWindowEnabled ?? false;

            // 先获取动画资源
            _fadeInStoryboard = mainWindow.FindResource("PageFadeIn") as Storyboard;
            _fadeOutStoryboard = mainWindow.FindResource("PageFadeOut") as Storyboard;

            // 初始化定时器
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

            // 设置DataContext
            mainWindow.DataContext = this;

            // 其他初始化
            _hotkeyService = new HotkeyService(mainWindow, ddDriver);
            _keyMappingViewModel = new KeyMappingViewModel(_ddDriver, App.ConfigService, _hotkeyService, this, App.AudioService);
            _feedbackViewModel = new FeedbackViewModel(this);
            _aboutViewModel = new AboutViewModel();
            
            NavigateCommand = new RelayCommand<string>(Navigate);
            
            // 订阅状态消息事件
            _ddDriver.StatusMessageChanged += OnDriverStatusMessageChanged;
            
            // 初始化浮窗（如果启用）
            if (_isFloatingWindowEnabled)
            {
                UpdateFloatingWindowVisibility();
            }

            // 最后设置默认页面
            Navigate("FrontKeys");
        }

        // 导航到指定页面
        private void Navigate(string? parameter)
        {
            if (string.IsNullOrEmpty(parameter)) return;

            // 如果当前页面是 KeyMappingView 并且正在执行按键操作，先停止它
            if (CurrentPage?.DataContext is KeyMappingViewModel keyMappingVM && keyMappingVM.IsExecuting)
            {
                _logger.LogDebug("Navigation", "检测到按键正在执行，正在停止...");
                keyMappingVM.StopKeyMapping();
            }

            // 创建或获取页面
            Page? newPage = GetOrCreatePage(parameter);

            if (newPage != null)
            {
                var oldPage = CurrentPage;
                
                // 如果有旧页面，先播放淡出动画
                if (oldPage != null && _fadeOutStoryboard != null)
                {
                    // 获取或创建动画
                    var fadeOut = GetOrCreateFadeOutAnimation(parameter);
                    var fadeIn = GetOrCreateFadeInAnimation(parameter);

                    fadeOut.Completed += (s, e) =>
                    {
                        // 动画完成后切换页面
                        CurrentPage = newPage;
                        // 播放淡入动画
                        fadeIn.Begin(newPage);
                    };
                    fadeOut.Begin(oldPage);
                }
                else
                {
                    // 没有旧页面，直接切换并播放淡入动画
                    CurrentPage = newPage;
                    GetOrCreateFadeInAnimation(parameter).Begin(newPage);
                }

                _logger.LogDebug("Navigation", $"页面切换到: {parameter}");
            }
        }

        private Page? GetOrCreatePage(string parameter)
        {
            if (_pageCache.TryGetValue(parameter, out var page))
            {
                return page;
            }

            Page? newPage = parameter switch
            {
                "FrontKeys" => new KeyMappingView { DataContext = _keyMappingViewModel },
                "Feedback" => new FeedbackView { DataContext = _feedbackViewModel },
                "About" => new AboutView { DataContext = _aboutViewModel },
                _ => null
            };

            if (newPage != null)
            {
                newPage.Opacity = 0;
                _pageCache[parameter] = newPage;
            }

            return newPage;
        }

        private Storyboard GetOrCreateFadeInAnimation(string parameter)
        {
            if (_fadeInCache.TryGetValue(parameter, out var fadeIn))
            {
                return fadeIn;
            }

            var newFadeIn = _fadeInStoryboard!.Clone();
            _fadeInCache[parameter] = newFadeIn;
            return newFadeIn;
        }

        private Storyboard GetOrCreateFadeOutAnimation(string parameter)
        {
            if (_fadeOutCache.TryGetValue(parameter, out var fadeOut))
            {
                return fadeOut;
            }

            var newFadeOut = _fadeOutStoryboard!.Clone();
            _fadeOutCache[parameter] = newFadeOut;
            return newFadeOut;
        }

        public void Cleanup()
        {
            _logger.LogDebug("MainViewModel", "开始清理资源...");
            _logger.LogDebug("MainViewModel", "开始保存应用程序配置...");

            // 关闭并清理浮窗
            if (_floatingWindow != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _floatingWindow.Close();
                    _floatingWindow = null;
                    _floatingWindowViewModel = null;
                });
            }

            _keyMappingViewModel.SaveConfig();  // 保存配置
            _logger.LogDebug("MainViewModel", "配置保存完成");
            _logger.LogDebug("MainViewModel", "--------------------------------");

            _hotkeyService?.Dispose();
            _statusMessageTimer.Stop(); // 停止定时器
            _logger.LogDebug("MainViewModel", "资源清理完成");

            // 清理动画缓存
            _fadeInCache.Clear();
            _fadeOutCache.Clear();
            _pageCache.Clear();
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
            if (Application.Current?.Dispatcher == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 停止之前的定时器
                    _statusMessageTimer?.Stop();

                    // 更新消息
                    StatusMessage = message;
                    StatusMessageColor = isError ? Brushes.Red : Brushes.Black;

                    // 启动定时器
                    _statusMessageTimer?.Start();
                }
                catch (Exception ex)
                {
                    _logger.LogError("MainViewModel", "更新状态消息失败", ex);
                }
            });
        }

        private void UpdateFloatingWindowVisibility()
        {
            if (IsFloatingWindowEnabled)
            {
                if (_floatingWindow == null)
                {
                    _floatingWindowViewModel = new FloatingWindowViewModel(_ddDriver);
                    _floatingWindow = new FloatingWindow(_mainWindow as MainWindow)
                    {
                        DataContext = _floatingWindowViewModel,
                        ShowInTaskbar = false
                    };
                }
                _floatingWindow.Show();
            }
            else
            {
                _floatingWindow?.Hide();
            }
        }

        public void UpdateExecutionStatus(bool isExecuting)
        {
            if (_floatingWindowViewModel != null)
            {
                _floatingWindowViewModel.UpdateExecutionStatus(isExecuting);
            }
        }

        public void HandleMainWindowStateChanged(WindowState state)
        {
            if (state == WindowState.Minimized && _floatingWindow != null && IsFloatingWindowEnabled)
            {
                _floatingWindow.Show();
                _floatingWindow.Topmost = true;
            }
        }
    }
} 