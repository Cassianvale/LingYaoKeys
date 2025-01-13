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
    public partial class AboutView : Page, IDisposable
    {
        private readonly ViewModels.AboutViewModel _viewModel;
        private bool _isWebViewInitialized;
        private bool _disposedValue;
        private WebView2 _webView;
        private Border _loadingIndicator;
        private Border _errorMessage;

        public AboutView()
        {
            InitializeComponent();
            _viewModel = new ViewModels.AboutViewModel();
            DataContext = _viewModel;

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

                // 注册页面卸载事件
                Unloaded += AboutView_Unloaded;

                // 立即开始初始化
                InitializeWebViewAsync().ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            _loadingIndicator.Visibility = Visibility.Collapsed;
                            _errorMessage.Visibility = Visibility.Visible;
                        });
                    }
                }, TaskScheduler.Current);
            }
            else
            {
                Debug.WriteLine("无法获取必要的控件引用");
            }
        }

        private void AboutView_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
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

        private async Task InitializeWebViewAsync()
        {
            if (_isWebViewInitialized || _webView == null) return;

            try
            {
                // 使用预加载的环境
                var environment = await Services.WebView2Service.Instance.GetEnvironmentAsync();
                
                // 初始化WebView2
                await _webView.EnsureCoreWebView2Async(environment);

                // 配置WebView2
                var webView = _webView.CoreWebView2;
                webView.Settings.IsScriptEnabled = true;
                webView.Settings.AreDefaultContextMenusEnabled = false;
                webView.Settings.IsZoomControlEnabled = false;
                webView.Settings.AreBrowserAcceleratorKeysEnabled = false;
                webView.Settings.IsStatusBarEnabled = false;

                // 注册事件处理程序
                webView.NavigationCompleted += CoreWebView2_NavigationCompleted;

                // 显示WebView
                _webView.Visibility = Visibility.Visible;
                _loadingIndicator.Visibility = Visibility.Collapsed;

                // 初始化ViewModel并开始加载内容
                _viewModel.Initialize(webView);

                _isWebViewInitialized = true;
            }
            catch (Exception ex)
            {
                _viewModel.HandleWebViewError(ex);
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_loadingIndicator != null && _errorMessage != null)
                    {
                        _loadingIndicator.Visibility = Visibility.Collapsed;
                        _errorMessage.Visibility = Visibility.Visible;
                    }
                });
            }
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (_webView != null && _errorMessage != null)
                    {
                        _webView.Visibility = Visibility.Collapsed;
                        _errorMessage.Visibility = Visibility.Visible;
                        _viewModel.HandleWebViewError(e.WebErrorStatus);
                    }
                });
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