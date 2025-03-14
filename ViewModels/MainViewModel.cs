using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp.Views;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using WpfApp.Services.Config;
using WpfApp.Services.Core;
using WpfApp.Services.Utils;
using WpfApp.Services.Events;

namespace WpfApp.ViewModels;

public class MainViewModel : ViewModelBase
{
    private AppConfig? _config;
    private bool _isDisposed;
    private readonly object _disposeLock = new();
    private Page? _currentPage;
    private readonly LyKeysService _lyKeysService;
    private readonly Window _mainWindow;
    private readonly KeyMappingViewModel _keyMappingViewModel;
    private readonly FeedbackViewModel _feedbackViewModel;
    private readonly HotkeyService _hotkeyService;
    private readonly SerilogManager _logger = SerilogManager.Instance;
    private string _statusMessage = "就绪";
    private System.Windows.Media.Brush _statusMessageColor = System.Windows.Media.Brushes.Black;
    private readonly DispatcherTimer _statusMessageTimer;
    private const int STATUS_MESSAGE_TIMEOUT = 3000; // 3秒后消失
    private readonly AboutViewModel _aboutViewModel;
    private readonly Dictionary<string, Page> _pageCache = new();
    private readonly Dictionary<string, Storyboard> _fadeInCache = new();
    private readonly Dictionary<string, Storyboard> _fadeOutCache = new();
    private readonly Storyboard? _fadeInStoryboard;
    private readonly Storyboard? _fadeOutStoryboard;
    private bool _isInitializing = true;

    // 状态消息颜色
    private static readonly System.Windows.Media.Brush STATUS_COLOR_NORMAL = System.Windows.Media.Brushes.Black;
    private static readonly System.Windows.Media.Brush STATUS_COLOR_SUCCESS = System.Windows.Media.Brushes.Green;
    private static readonly System.Windows.Media.Brush STATUS_COLOR_WARNING = System.Windows.Media.Brushes.Orange;
    private static readonly System.Windows.Media.Brush STATUS_COLOR_ERROR = System.Windows.Media.Brushes.Red;
    private static readonly System.Windows.Media.Brush STATUS_COLOR_INFO = System.Windows.Media.Brushes.Blue;

    // 状态栏快捷方法
    public void ShowSuccessMessage(string message)
    {
        UpdateStatusMessage(message, STATUS_COLOR_SUCCESS);
    }

    public void ShowWarningMessage(string message)
    {
        UpdateStatusMessage(message, STATUS_COLOR_WARNING);
    }

    public void ShowErrorMessage(string message)
    {
        UpdateStatusMessage(message, STATUS_COLOR_ERROR);
    }

    public void ShowInfoMessage(string message)
    {
        UpdateStatusMessage(message, STATUS_COLOR_INFO);
    }

    // 导航栏状态相关属性
    private bool _isNavExpanded = true;

    public bool IsNavExpanded
    {
        get => _isNavExpanded;
        set
        {
            if (_isNavExpanded != value)
            {
                _isNavExpanded = value;
                OnPropertyChanged(nameof(IsNavExpanded));
                OnPropertyChanged(nameof(NavColumnWidth));
                OnPropertyChanged(nameof(NavTextVisibility));
                OnPropertyChanged(nameof(NavToggleIcon));
            }
        }
    }

    // 导航栏列宽度
    public GridLength NavColumnWidth => IsNavExpanded ? new GridLength(200) : new GridLength(50);

    // 导航文本可见性
    public Visibility NavTextVisibility => IsNavExpanded ? Visibility.Visible : Visibility.Collapsed;

    // 导航切换按钮图标
    public string NavToggleIcon => IsNavExpanded ? "\uE700" : "\uE701";

    public AppConfig Config
    {
        get
        {
            if (_config == null) _config = AppConfigService.Config;
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

    public System.Windows.Media.Brush StatusMessageColor
    {
        get => _statusMessageColor;
        set => SetProperty(ref _statusMessageColor, value);
    }

    public ICommand NavigateCommand { get; }

    // 添加KeyMappingViewModel的公共属性
    public KeyMappingViewModel KeyMappingViewModel => _keyMappingViewModel;

    // 定义页面配置类，用于配置页面的特性
    private class PageConfig
    {
        public bool UseCaching { get; set; } = true;          // 是否使用缓存
        public bool LogCreation { get; set; } = true;         // 是否记录创建日志
        public bool IsFrequentlyAccessed { get; set; } = false; // 是否为频繁访问的页面
        public Func<Page> CreatePageFunc { get; set; }        // 创建页面的函数
    }

    // 页面配置字典，用于存储不同页面的配置
    private readonly Dictionary<string, PageConfig> _pageConfigs;

    // 在构造函数中初始化页面配置字典
    public MainViewModel(LyKeysService lyKeysService, Window mainWindow)
    {
        _isInitializing = true;
        _logger.Debug("MainViewModel开始初始化");
        
        _lyKeysService = lyKeysService;
        _mainWindow = mainWindow;

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
            StatusMessageColor = System.Windows.Media.Brushes.Black;
        };

        // 设置DataContext
        mainWindow.DataContext = this;
        _logger.Debug("MainWindow的DataContext已设置为MainViewModel");

        // 初始化HotkeyService，并传递窗口和驱动服务
        _hotkeyService = new HotkeyService(mainWindow, lyKeysService);
        _logger.Debug("HotkeyService已初始化");
        
        // 先标记初始化完成，避免循环依赖问题
        _isInitializing = false;
        _logger.Debug("MainViewModel基础初始化完成，_isInitializing = false");
        
        // 初始化各个ViewModel
        _keyMappingViewModel =
            new KeyMappingViewModel(_lyKeysService, App.ConfigService, _hotkeyService, this, App.AudioService);
        _logger.Debug("KeyMappingViewModel已初始化");
        
        _feedbackViewModel = new FeedbackViewModel(this);
        _aboutViewModel = new AboutViewModel();

        NavigateCommand = new RelayCommand<string>(Navigate);

        // 订阅状态消息事件
        _lyKeysService.StatusMessageChanged += OnDriverStatusMessageChanged;

        // 初始化页面配置
        _pageConfigs = new Dictionary<string, PageConfig>
        {
            // FrontKeys 页面：使用缓存，不记录创建日志，频繁访问
            ["FrontKeys"] = new PageConfig
            {
                UseCaching = true,
                LogCreation = false,
                IsFrequentlyAccessed = true,
                CreatePageFunc = () => {
                    var page = new KeyMappingView();
                    page.DataContext = _keyMappingViewModel;
                    return page;
                }
            },
            
            // About 页面：使用缓存，记录创建日志，非频繁访问
            ["About"] = new PageConfig
            {
                UseCaching = true,
                LogCreation = true,
                IsFrequentlyAccessed = false,
                CreatePageFunc = () => {
                    var page = new AboutView();
                    page.DataContext = _aboutViewModel;
                    return page;
                }
            },
            
            // Feedback 页面：使用缓存，记录创建日志，非频繁访问
            ["Feedback"] = new PageConfig
            {
                UseCaching = true,
                LogCreation = true,
                IsFrequentlyAccessed = false,
                CreatePageFunc = () => {
                    var page = new FeedbackView();
                    page.DataContext = _feedbackViewModel;
                    return page;
                }
            },
            
            // QRCode 页面：使用缓存，记录创建日志，非频繁访问
            ["QRCode"] = new PageConfig
            {
                UseCaching = true,
                LogCreation = true,
                IsFrequentlyAccessed = false,
                CreatePageFunc = () => new QRCodeView()
            },
            
            // Settings 页面：使用缓存，记录创建日志，非频繁访问
            ["Settings"] = new PageConfig
            {
                UseCaching = true,
                LogCreation = true,
                IsFrequentlyAccessed = false,
                CreatePageFunc = () => {
                    var page = new SettingsView();
                    page.DataContext = new SettingsViewModel();
                    return page;
                }
            }
        };

        // 最后设置默认页面
        _logger.Debug("MainViewModel完全初始化完成，准备导航到默认页面");
        Navigate("FrontKeys");
    }

    /// <summary>
    /// 获取当前是否处于初始化状态
    /// </summary>
    public bool IsInitializing => _isInitializing;

    // 导航到指定页面
    private void Navigate(string? parameter)
    {
        try
        {
            if (string.IsNullOrEmpty(parameter))
            {
                _logger.Debug("导航参数为空");
                return;
            }

            _logger.Debug($"开始导航到页面: {parameter}");

            // 如果当前页面是 KeyMappingView 并且正在执行按键操作，先停止它
            if (CurrentPage?.DataContext is KeyMappingViewModel keyMappingVM && keyMappingVM.IsExecuting)
            {
                _logger.Debug("检测到按键正在执行，正在停止...");
                keyMappingVM.StopKeyMapping();
            }

            // 创建或获取页面
            Page? newPage = null;
            try
            {
                newPage = GetOrCreatePage(parameter);
            }
            catch (Exception ex)
            {
                _logger.Error($"获取页面失败: {parameter}", ex);
                return;
            }

            if (newPage != null)
            {
                _logger.Debug($"成功创建页面: {parameter}");
                var oldPage = CurrentPage;

                try
                {
                    // 如果有旧页面，先播放淡出动画
                    if (oldPage != null && _fadeOutStoryboard != null)
                    {
                        _logger.Debug("开始播放页面切换动画");
                        // 获取或创建动画
                        var fadeOut = GetOrCreateFadeOutAnimation(parameter);
                        var fadeIn = GetOrCreateFadeInAnimation(parameter);

                        fadeOut.Completed += (s, e) =>
                        {
                            try
                            {
                                // 动画完成后切换页面
                                CurrentPage = newPage;
                                // 播放淡入动画
                                fadeIn.Begin(newPage);
                                _logger.Debug($"页面切换动画完成: {parameter}");
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"页面切换动画完成回调失败: {parameter}", ex);
                            }
                        };
                        fadeOut.Begin(oldPage);
                    }
                    else
                    {
                        // 没有旧页面，直接切换并播放淡入动画
                        _logger.Debug("直接切换页面（无动画）");
                        CurrentPage = newPage;
                        GetOrCreateFadeInAnimation(parameter).Begin(newPage);
                    }

                    _logger.Debug($"页面切换完成: {parameter}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"页面切换过程失败: {parameter}", ex);
                    // 如果动画失败，尝试直接切换
                    try
                    {
                        CurrentPage = newPage;
                        _logger.Debug("已尝试直接切换页面（跳过动画）");
                    }
                    catch (Exception innerEx)
                    {
                        _logger.Error("直接切换页面也失败", innerEx);
                    }
                }
            }
            else
            {
                _logger.Error($"创建页面失败: {parameter}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Navigate 方法执行失败: {parameter}", ex);
        }
    }

    private Page? GetOrCreatePage(string parameter)
    {
        try
        {
            // 1. 检查页面配置是否存在
            if (!_pageConfigs.TryGetValue(parameter, out var pageConfig))
            {
                _logger.Error($"未知的页面类型: {parameter}");
                return null;
            }

            // 2. 记录创建日志（如果需要）
            if (pageConfig.LogCreation)
            {
                _logger.Debug($"尝试获取或创建页面: {parameter}");
            }

            // 特殊处理About页面 - 即使启用了缓存，也强制刷新
            if (parameter == "About")
            {
                _logger.Debug("About页面需要特殊处理：强制刷新");
                
                // 如果已存在于缓存中，先移除
                if (_pageCache.ContainsKey(parameter))
                {
                    _logger.Debug("从缓存中移除旧的About页面实例");
                    _pageCache.Remove(parameter);
                }
                
                // 创建新实例
                var freshPage = pageConfig.CreatePageFunc();
                if (pageConfig.LogCreation)
                {
                    _logger.Debug($"创建了新的About页面实例");
                }
                
                // 设置初始不透明度为0，为动画做准备
                freshPage.Opacity = 0;
                
                // 将页面添加到缓存
                _pageCache[parameter] = freshPage;
                
                return freshPage;
            }

            // 3. 如果不使用缓存，直接创建新实例
            if (!pageConfig.UseCaching)
            {
                var freshPage = pageConfig.CreatePageFunc();
                if (pageConfig.LogCreation)
                {
                    _logger.Debug($"创建了新的页面实例: {parameter}");
                }
                return freshPage;
            }

            // 4. 从缓存中获取页面
            if (_pageCache.TryGetValue(parameter, out var cachedPage))
            {
                if (pageConfig.LogCreation && !pageConfig.IsFrequentlyAccessed)
                {
                    _logger.Debug($"从缓存中获取页面: {parameter}");
                }
                return cachedPage;
            }

            // 5. 创建新页面
            Page? createdPage = null;
            try
            {
                createdPage = pageConfig.CreatePageFunc();
            }
            catch (Exception ex)
            {
                _logger.Error($"创建页面时发生异常: {parameter}", ex);
                throw;
            }

            // 6. 处理新创建的页面
            if (createdPage != null)
            {
                // 设置初始不透明度为0，为动画做准备
                createdPage.Opacity = 0;
                
                // 将页面添加到缓存
                _pageCache[parameter] = createdPage;
                
                // 记录创建成功日志（如果需要）
                if (pageConfig.LogCreation && !pageConfig.IsFrequentlyAccessed)
                {
                    _logger.Debug($"页面创建成功: {parameter}");
                }
            }
            else
            {
                _logger.Error($"页面创建失败: {parameter}");
            }

            return createdPage;
        }
        catch (Exception ex)
        {
            _logger.Error($"GetOrCreatePage 方法执行失败: {parameter}", ex);
            throw;
        }
    }
    
    // 预加载方法，在应用启动时预先加载常用页面
    public void PreloadCommonPages()
    {
        try
        {
            // 在后台线程中预加载频繁访问的页面
            Task.Run(() =>
            {
                try
                {
                    // 找出所有标记为频繁访问的页面
                    var frequentPages = _pageConfigs
                        .Where(kvp => kvp.Value.IsFrequentlyAccessed && kvp.Value.UseCaching)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var pageName in frequentPages)
                    {
                        if (!_pageCache.ContainsKey(pageName))
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    var page = GetOrCreatePage(pageName);
                                    if (page != null)
                                    {
                                        _logger.Debug($"已预加载页面: {pageName}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error($"预加载页面 {pageName} 失败", ex);
                                }
                            }, System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("预加载页面失败", ex);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error("启动预加载任务失败", ex);
        }
    }

    private Storyboard GetOrCreateFadeInAnimation(string parameter)
    {
        if (_fadeInCache.TryGetValue(parameter, out var fadeIn)) return fadeIn;

        var newFadeIn = _fadeInStoryboard!.Clone();
        _fadeInCache[parameter] = newFadeIn;
        return newFadeIn;
    }

    private Storyboard GetOrCreateFadeOutAnimation(string parameter)
    {
        if (_fadeOutCache.TryGetValue(parameter, out var fadeOut)) return fadeOut;

        var newFadeOut = _fadeOutStoryboard!.Clone();
        _fadeOutCache[parameter] = newFadeOut;
        return newFadeOut;
    }

    public void Cleanup()
    {
        _logger.Debug("开始清理资源...");
        _logger.Debug("开始保存应用程序配置...");

        _keyMappingViewModel.SaveConfig(); // 保存配置
        _logger.Debug("配置保存完成");
        _logger.Debug("--------------------------------");

        _hotkeyService?.Dispose();
        _statusMessageTimer.Stop(); // 停止定时器
        _logger.Debug("资源清理完成");

        // 清理动画缓存
        _fadeInCache.Clear();
        _fadeOutCache.Clear();
        _pageCache.Clear();
    }

    // 用于更新状态栏消息
    private void OnDriverStatusMessageChanged(object? sender, StatusMessageEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // 停止之前的定时器
            _statusMessageTimer.Stop();

            StatusMessage = e.Message;
            StatusMessageColor = e.IsError ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Black;

            // 启动定时器
            _statusMessageTimer.Start();

            // 如果是错误消息，记录到日志
            if (e.IsError)
                _logger.Error($"驱动状态错误: {e.Message}");
            else
                _logger.Debug($"驱动状态更新: {e.Message}");
        });
    }

    public void UpdateStatusMessage(string message, bool isError = false)
    {
        UpdateStatusMessage(message, isError ? STATUS_COLOR_ERROR : STATUS_COLOR_NORMAL);
    }

    public void UpdateStatusMessage(string message, System.Windows.Media.Brush color)
    {
        if (System.Windows.Application.Current?.Dispatcher == null) return;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                _statusMessageTimer?.Stop();
                StatusMessage = message;
                StatusMessageColor = color;
                _statusMessageTimer?.Start();
            }
            catch (Exception ex)
            {
                _logger.Error("更新状态消息失败", ex);
            }
        });
    }
}