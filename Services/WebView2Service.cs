using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using System.Threading;

namespace WpfApp.Services
{
    public class WebView2Service
    {
        private static readonly Lazy<WebView2Service> _instance = new Lazy<WebView2Service>(() => new WebView2Service());
        public static WebView2Service Instance => _instance.Value;

        private static readonly string UserDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".lingyao",
            "WebView2"
        );

        private CoreWebView2Environment _sharedEnvironment;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private Task<CoreWebView2Environment> _initTask;

        private WebView2Service()
        {
            // 在构造函数中就开始初始化环境
            _initTask = InitializeEnvironmentAsync();
        }

        public Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            return _initTask ?? InitializeEnvironmentAsync();
        }

        private async Task<CoreWebView2Environment> InitializeEnvironmentAsync()
        {
            if (_sharedEnvironment != null) return _sharedEnvironment;

            await _initLock.WaitAsync();
            try
            {
                if (_sharedEnvironment != null) return _sharedEnvironment;

                // 确保用户数据目录存在
                Directory.CreateDirectory(UserDataFolder);

                var options = new CoreWebView2EnvironmentOptions()
                {
                    AllowSingleSignOnUsingOSPrimaryAccount = false,
                    ExclusiveUserDataFolderAccess = true
                };

                _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, UserDataFolder, options);
                return _sharedEnvironment;
            }
            finally
            {
                _initLock.Release();
            }
        }
    }
} 