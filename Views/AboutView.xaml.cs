using System;
using System.IO;
using System.Windows.Controls;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Threading;

namespace WpfApp.Views
{
    public partial class AboutView : Page, IDisposable
    {
        private readonly ViewModels.AboutViewModel _viewModel;
        private bool _isWebViewInitialized;
        private bool _disposedValue;

        public AboutView()
        {
            InitializeComponent();
            _viewModel = new ViewModels.AboutViewModel();
            DataContext = _viewModel;

            // 设置初始可见性
            LoadingIndicator.Visibility = Visibility.Visible;
            WebView.Visibility = Visibility.Collapsed;
            ErrorMessage.Visibility = Visibility.Collapsed;

            // 立即开始初始化
            InitializeWebViewAsync().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        LoadingIndicator.Visibility = Visibility.Collapsed;
                        ErrorMessage.Visibility = Visibility.Visible;
                    });
                }
            }, TaskScheduler.Current);
        }

        private async Task InitializeWebViewAsync()
        {
            if (_isWebViewInitialized) return;

            try
            {
                // 使用预加载的环境
                var environment = await Services.WebView2Service.Instance.GetEnvironmentAsync();
                
                // 初始化WebView2
                await WebView.EnsureCoreWebView2Async(environment);

                // 配置WebView2
                var webView = WebView.CoreWebView2;
                webView.Settings.IsScriptEnabled = true;
                webView.Settings.AreDefaultContextMenusEnabled = false;
                webView.Settings.IsZoomControlEnabled = false;
                webView.Settings.AreBrowserAcceleratorKeysEnabled = false;
                webView.Settings.IsStatusBarEnabled = false;

                // 注册事件处理程序
                webView.NavigationCompleted += CoreWebView2_NavigationCompleted;

                // 显示WebView
                WebView.Visibility = Visibility.Visible;
                LoadingIndicator.Visibility = Visibility.Collapsed;

                // 初始化ViewModel并开始加载内容
                _viewModel.Initialize(webView);

                _isWebViewInitialized = true;
            }
            catch (Exception ex)
            {
                _viewModel.HandleWebViewError(ex);
                await Dispatcher.InvokeAsync(() =>
                {
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                    ErrorMessage.Visibility = Visibility.Visible;
                });
            }
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    WebView.Visibility = Visibility.Collapsed;
                    ErrorMessage.Visibility = Visibility.Visible;
                    _viewModel.HandleWebViewError(e.WebErrorStatus);
                });
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // 清理事件订阅
                    if (WebView?.CoreWebView2 != null)
                    {
                        WebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                        WebView.Source = null;
                    }

                    // 释放ViewModel资源
                    if (_viewModel is IDisposable disposableViewModel)
                    {
                        disposableViewModel.Dispose();
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