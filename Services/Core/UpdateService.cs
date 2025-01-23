using System.Net.Http;
using System.Text.Json;
using System.Reflection;
using System.Diagnostics;
using WpfApp.Services.Config;
using WpfApp.Services.Utils;

namespace WpfApp.Services.Core
{
    public class UpdateService : IDisposable
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly HttpClient _httpClient;
        private const string VERSION_FILE_URL = "https://lykeys-remote.oss-cn-shanghai.aliyuncs.com/version.json";
        private const string RELEASE_BASE_URL = "https://lykeys-remote.oss-cn-shanghai.aliyuncs.com/release";

        public UpdateService()
        {
            _httpClient = new HttpClient();
            // 设置超时时间为10秒
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        /// <returns>如果有新版本返回版本信息，否则返回null</returns>
        public async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                // 获取版本信息
                var response = await _httpClient.GetAsync(VERSION_FILE_URL);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Warning($"获取版本信息失败: HTTP {(int)response.StatusCode} {response.StatusCode}");
                    throw new InvalidOperationException("无法连接到更新服务器，请检查网络连接");
                }

                var versionContent = await response.Content.ReadAsStringAsync();
                _logger.Debug($"获取的版本信息: {versionContent}");

                // 使用 JsonSerializerOptions 确保大小写不敏感
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(versionContent, options);

                if (versionInfo == null)
                {
                    _logger.Warning("获取版本信息失败：返回数据为空");
                    return null;
                }

                if (string.IsNullOrEmpty(versionInfo.Version))
                {
                    _logger.Warning("获取版本信息失败：版本号为空");
                    return null;
                }

                var currentVersion = GetCurrentVersion();
                _logger.Debug($"当前版本: {FormatVersion(currentVersion)}");
                _logger.Debug($"最新版本: {versionInfo.Version}");

                // 统一版本号格式为 x.x.x.0
                string NormalizeVersion(string version)
                {
                    version = version.TrimStart('v');
                    var parts = version.Split('.');
                    if (parts.Length == 3)
                    {
                        return $"{version}.0";
                    }
                    else if (parts.Length == 2)
                    {
                        return $"{version}.0.0";
                    }
                    else if (parts.Length == 1)
                    {
                        return $"{version}.0.0.0";
                    }
                    return version;
                }

                try
                {
                    string normalizedLatestVersion = NormalizeVersion(versionInfo.Version);
                    string normalizedCurrentVersion = NormalizeVersion(currentVersion.ToString());
                    
                    _logger.Debug($"标准化后的当前版本: {normalizedCurrentVersion}");
                    _logger.Debug($"标准化后的最新版本: {normalizedLatestVersion}");

                    var latestVersion = Version.Parse(normalizedLatestVersion);
                    var parsedCurrentVersion = Version.Parse(normalizedCurrentVersion);

                    bool hasNewVersion = latestVersion > parsedCurrentVersion;
                    _logger.Debug($"版本比较结果: {hasNewVersion}");

                    if (hasNewVersion)
                    {
                        _logger.Debug("检测到新版本");
                        return new UpdateInfo
                        {
                            CurrentVersion = FormatVersion(currentVersion),
                            LatestVersion = versionInfo.Version,
                            DownloadUrl = versionInfo.DownloadUrl
                        };
                    }

                    _logger.Debug("未检测到新版本");
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.Error($"版本号解析失败: {ex.Message}");
                    throw;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Error("网络请求失败", ex);
                throw new InvalidOperationException("无法连接到更新服务器，请检查网络连接", ex);
            }
            catch (TaskCanceledException)
            {
                _logger.Error("请求超时");
                throw new InvalidOperationException("更新服务器响应超时，请稍后重试");
            }
            catch (Exception ex)
            {
                _logger.Error("检查更新失败", ex);
                throw;
            }
        }

        /// <summary>
        /// 获取当前版本号
        /// </summary>
        private Version GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            if (version == null) return new Version(1, 0, 0, 0);
            
            // 只保留前三段版本号
            return new Version(version.Major, version.Minor, version.Build, 0);
        }

        /// <summary>
        /// 格式化版本号为 x.x.x 格式
        /// </summary>
        private string FormatVersion(Version version)
        {
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        /// <summary>
        /// 打开下载页面
        /// </summary>
        public void OpenDownloadPage(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Error("打开下载页面失败", ex);
                throw;
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
} 