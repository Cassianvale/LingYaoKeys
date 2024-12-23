using System;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace WpfApp.Views
{
    public partial class AboutView : Page
    {
        private readonly ViewModels.AboutViewModel _viewModel;

        public AboutView()
        {
            InitializeComponent();
            _viewModel = new ViewModels.AboutViewModel();
            DataContext = _viewModel;
            
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            try
            {
                // 初始化WebView2
                await WebView.EnsureCoreWebView2Async();

                // 配置WebView2
                WebView.CoreWebView2.Settings.IsScriptEnabled = true;
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                WebView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // 设置内容
                WebView.CoreWebView2.NavigateToString(_viewModel.HtmlContent);

                // 注册事件处理程序
                WebView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    // 导航完成后的处理
                    if (!e.IsSuccess)
                    {
                        _viewModel.HandleWebViewError(e.WebErrorStatus);
                    }
                };
            }
            catch (Exception ex)
            {
                _viewModel.HandleWebViewError(ex);
            }
        }
    }
} 