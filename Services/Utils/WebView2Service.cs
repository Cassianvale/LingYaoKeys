using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using System.Threading;
using System.Diagnostics;

namespace WpfApp.Services
{
    public class WebView2Service : IDisposable
    {
        private static readonly Lazy<WebView2Service> _instance = new Lazy<WebView2Service>(() => new WebView2Service());
        public static WebView2Service Instance => _instance.Value;

        private static readonly string UserDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".lykeys",
            "WebView2"
        );

        private CoreWebView2Environment _sharedEnvironment;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private Task<CoreWebView2Environment> _initTask;
        private bool _isDisposed;

        private WebView2Service()
        {
            // 在构造函数中就开始初始化环境
            _initTask = InitializeEnvironmentAsync();
        }

        public Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(WebView2Service));
            }
            return _initTask ?? InitializeEnvironmentAsync();
        }

        private async Task<CoreWebView2Environment> InitializeEnvironmentAsync()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(WebView2Service));
            }

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

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;

                try
                {
                    // 释放信号量
                    _initLock?.Dispose();

                    // 清理环境
                    if (_sharedEnvironment != null)
                    {
                        // 清理 WebView2 用户数据目录
                        try
                        {
                            if (Directory.Exists(UserDataFolder))
                            {
                                Directory.Delete(UserDataFolder, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"清理 WebView2 用户数据目录失败: {ex.Message}");
                        }

                        _sharedEnvironment = null;
                    }

                    // 取消初始化任务
                    _initTask = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"清理 WebView2Service 时发生错误: {ex.Message}");
                }
            }
        }
    }
} 