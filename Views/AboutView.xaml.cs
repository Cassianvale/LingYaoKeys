using System;
using System.IO;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace WpfApp.Views
{
    public partial class AboutView : Page
    {
        private readonly ViewModels.AboutViewModel _viewModel;
        private static readonly string UserDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".lingyao",
            "WebView2"
        );

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
                // 确保用户数据目录存在
                Directory.CreateDirectory(UserDataFolder);

                // 创建 WebView2 环境选项
                var options = new CoreWebView2EnvironmentOptions()
                {
                    AllowSingleSignOnUsingOSPrimaryAccount = false,
                    ExclusiveUserDataFolderAccess = true
                };

                // 创建 WebView2 环境
                var environment = await CoreWebView2Environment.CreateAsync(
                    null, 
                    UserDataFolder,
                    options
                );

                // 使用自定义环境初始化 WebView2
                await WebView.EnsureCoreWebView2Async(environment);

                // 配置WebView2
                WebView.CoreWebView2.Settings.IsScriptEnabled = true;
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                WebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                WebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

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