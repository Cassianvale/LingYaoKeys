using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using System.Threading;
using System.Diagnostics;
using Serilog;

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
        private readonly SerilogManager _logger = SerilogManager.Instance;

        private WebView2Service()
        {
            // 不在构造函数中初始化，改为延迟加载
        }

        public Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(WebView2Service));
            }

            // 延迟初始化
            return _initTask ??= InitializeEnvironmentAsync();
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

                _logger.Debug("开始初始化WebView2环境...");

                // 确保用户数据目录存在
                Directory.CreateDirectory(UserDataFolder);

                var options = new CoreWebView2EnvironmentOptions()
                {
                    AllowSingleSignOnUsingOSPrimaryAccount = false,
                    ExclusiveUserDataFolderAccess = true,
                    // 添加其他优化选项
                    AdditionalBrowserArguments = "--disable-features=msSmartScreenProtection --disable-features=msEdgeFeatures"
                };

                _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, UserDataFolder, options);
                _logger.Debug("WebView2环境初始化完成");
                return _sharedEnvironment;
            }
            catch (Exception ex)
            {
                _logger.Error("WebView2环境初始化失败", ex);
                throw;
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
                    _logger.Debug("正在清理WebView2服务...");
                    
                    // 释放信号量
                    _initLock?.Dispose();

                    // 清理环境
                    if (_sharedEnvironment != null)
                    {
                        // 清理运行时资源
                        try
                        {
                            // 将环境设置为null以释放资源
                            _sharedEnvironment = null;
                            
                            // 强制GC回收以确保资源被释放
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            
                            _logger.Debug("WebView2环境资源已释放");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("清理WebView2环境资源失败", ex);
                        }
                    }

                    // 取消初始化任务
                    _initTask = null;
                    
                    _logger.Debug("WebView2服务清理完成");
                }
                catch (Exception ex)
                {
                    _logger.Error("清理WebView2服务时发生错误", ex);
                }
            }
        }
    }
} 