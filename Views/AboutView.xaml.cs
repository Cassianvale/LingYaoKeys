using System;
using System.IO;
using System.Windows.Controls;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Threading;
using System.Diagnostics;
using Microsoft.Web.WebView2.Wpf;

namespace WpfApp.Views
{
    /// <summary>
    /// AboutView.xaml 的交互逻辑
    /// </summary>
    public partial class AboutView : Page, IDisposable
    {
        // 静态缓存
        private static CoreWebView2Environment _webViewEnvironment;
        private static string _cachedHtmlContent;
        private static readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private static bool _isEnvironmentInitialized;
        private static Task _initializationTask;

        private readonly ViewModels.AboutViewModel _viewModel;
        private bool _isWebViewInitialized;
        private bool _disposedValue;
        private WebView2 _webView;
        private Border _loadingIndicator;
        private Border _errorMessage;
        private CancellationTokenSource _cts;
        private bool _isLoading;

        static AboutView()
        {
            // 在静态构造函数中开始初始化环境
            _initializationTask = InitializeEnvironmentAsync();
        }

        public AboutView()
        {
            InitializeComponent();
            _viewModel = new ViewModels.AboutViewModel();
            DataContext = _viewModel;
            _cts = new CancellationTokenSource();

            // 获取XAML中定义的控件引用
            _webView = FindName("WebView") as WebView2;
            _loadingIndicator = FindName("LoadingIndicator") as Border;
            _errorMessage = FindName("ErrorMessage") as Border;

            if (_webView != null && _loadingIndicator != null && _errorMessage != null)
            {
                // 设置初始可见性
                _loadingIndicator.Visibility = Visibility.Visible;
                _webView.Visibility = Visibility.Collapsed;
                _errorMessage.Visibility = Visibility.Collapsed;

                // 注册页面加载和卸载事件
                Loaded += AboutView_Loaded;
                Unloaded += AboutView_Unloaded;

                // 如果环境已经初始化，直接使用
                if (_isEnvironmentInitialized && _webViewEnvironment != null)
                {
                    _initializationTask = InitializeWebViewAsync(_cts.Token);
                }
            }
            else
            {
                Debug.WriteLine("无法获取必要的控件引用");
            }
        }

        private static async Task InitializeEnvironmentAsync()
        {
            try
            {
                await _initLock.WaitAsync();
                if (!_isEnvironmentInitialized)
                {
                    _webViewEnvironment = await Services.WebView2Service.Instance.GetEnvironmentAsync();
                    _isEnvironmentInitialized = true;
                }
            }
            finally
            {
                _initLock.Release();
            }
        }

        private void AboutView_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isLoading && !_isWebViewInitialized && !_disposedValue)
            {
                _isLoading = true;
                if (_initializationTask?.IsCompleted == false)
                {
                    // 等待环境初始化完成后再初始化WebView
                    _initializationTask.ContinueWith(async _ =>
                    {
                        await InitializeWebViewAsync(_cts.Token);
                    }, TaskScheduler.Current);
                }
                else
                {
                    InitializeWebViewAsync(_cts.Token).ContinueWith(task =>
                    {
                        _isLoading = false;
                        if (task.IsFaulted)
                        {
                            Dispatcher.InvokeAsync(() =>
                            {
                                if (!_disposedValue)
                                {
                                    _loadingIndicator.Visibility = Visibility.Collapsed;
                                    _errorMessage.Visibility = Visibility.Visible;
                                    _viewModel?.HandleWebViewError(task.Exception?.InnerException ?? task.Exception);
                                }
                            });
                        }
                    }, TaskScheduler.Current);
                }
            }
        }

        private void AboutView_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 取消正在进行的初始化
                _cts.Cancel();
                
                // 在页面卸载时主动清理资源
                if (_webView?.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                    _webView.CoreWebView2.Stop();
                    _webView.Source = null;
                }
                Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"页面卸载时清理资源发生错误: {ex.Message}");
            }
        }

        private async Task InitializeWebViewAsync(CancellationToken cancellationToken)
        {
            if (_isWebViewInitialized || _webView == null || _disposedValue) return;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 等待环境初始化完成
                if (!_isEnvironmentInitialized)
                {
                    await InitializeEnvironmentAsync();
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (_disposedValue) return;

                // 初始化WebView2
                await _webView.EnsureCoreWebView2Async(_webViewEnvironment);

                cancellationToken.ThrowIfCancellationRequested();
                if (_disposedValue) return;

                // 配置WebView2
                var webView = _webView.CoreWebView2;
                if (webView != null)
                {
                    webView.Settings.IsScriptEnabled = true;
                    webView.Settings.AreDefaultContextMenusEnabled = false;
                    webView.Settings.IsZoomControlEnabled = false;
                    webView.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView.Settings.IsStatusBarEnabled = false;

                    // 注册事件处理程序
                    webView.NavigationCompleted += CoreWebView2_NavigationCompleted;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (!_disposedValue)
                        {
                            // 显示WebView
                            _webView.Visibility = Visibility.Visible;
                            _loadingIndicator.Visibility = Visibility.Collapsed;

                            // 初始化ViewModel并开始加载内容
                            _viewModel?.Initialize(webView);
                        }
                    });

                    _isWebViewInitialized = true;
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("WebView2初始化被取消");
                throw;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine($"WebView2初始化失败: {ex.Message}");
                    _viewModel?.HandleWebViewError(ex);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (!_disposedValue && _loadingIndicator != null && _errorMessage != null)
                        {
                            _loadingIndicator.Visibility = Visibility.Collapsed;
                            _errorMessage.Visibility = Visibility.Visible;
                        }
                    });
                }
                throw;
            }
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!_disposedValue)
            {
                if (!e.IsSuccess)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (!_disposedValue && _webView != null && _errorMessage != null)
                        {
                            _webView.Visibility = Visibility.Collapsed;
                            _errorMessage.Visibility = Visibility.Visible;
                            _viewModel?.HandleWebViewError(e.WebErrorStatus);
                        }
                    });
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        // 取消所有正在进行的操作
                        _cts.Cancel();
                        _cts.Dispose();

                        // 清理 WebView2 资源
                        if (_webView != null)
                        {
                            if (_webView.CoreWebView2 != null)
                            {
                                _webView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                                _webView.CoreWebView2.Stop();
                                _webView.Source = null;
                            }
                            _webView = null;
                        }

                        // 释放ViewModel资源
                        if (_viewModel is IDisposable disposableViewModel)
                        {
                            disposableViewModel.Dispose();
                        }
                        
                        // 取消事件订阅
                        Loaded -= AboutView_Loaded;
                        Unloaded -= AboutView_Unloaded;

                        // 清理控件引用
                        _loadingIndicator = null;
                        _errorMessage = null;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"清理 WebView2 资源时发生错误: {ex.Message}");
                    }
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
} 