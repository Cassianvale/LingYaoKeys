using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Reflection;
using System.Diagnostics;
using Aliyun.OSS;
using System.IO;
using Microsoft.Extensions.Configuration;
using WpfApp.Services.Models;

namespace WpfApp.Services
{
    public class UpdateService
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly OssClient? _ossClient;
        private readonly OssConfig? _ossConfig;
        private readonly bool _isOfflineMode;

        public UpdateService(IConfiguration configuration)
        {
            try
            {
                _ossConfig = configuration.GetSection("OssConfig").Get<OssConfig>();
                if (_ossConfig == null)
                {
                    _logger.Warning("未找到 OSS 配置，将以离线模式运行");
                    _isOfflineMode = true;
                    return;
                }

                // 优先使用环境变量
                string? accessKeyId = Environment.GetEnvironmentVariable("OSS_ACCESS_KEY_ID") 
                    ?? _ossConfig.AccessKeyId;
                string? accessKeySecret = Environment.GetEnvironmentVariable("OSS_ACCESS_KEY_SECRET") 
                    ?? _ossConfig.AccessKeySecret;

                if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(accessKeySecret))
                {
                    _logger.Warning("未配置 OSS 访问凭证，将以离线模式运行");
                    _isOfflineMode = true;
                    return;
                }

                _ossClient = new OssClient(_ossConfig.Endpoint, accessKeyId, accessKeySecret);
                _isOfflineMode = false;
            }
            catch (Exception ex)
            {
                _logger.Warning($"初始化 OSS 客户端失败，将以离线模式运行: {ex.Message}");
                _isOfflineMode = true;
            }
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        /// <returns>如果有新版本返回版本信息，否则返回null</returns>
        public async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            if (_isOfflineMode)
            {
                _logger.Warning("当前为离线模式，无法检查更新");
                throw new InvalidOperationException("无法连接到更新服务器，请检查网络连接");
            }

            try
            {
                if (_ossClient == null || _ossConfig == null)
                {
                    throw new InvalidOperationException("更新服务未正确初始化");
                }

                // 从 OSS 获取版本信息文件
                var result = _ossClient.GetObject(_ossConfig.BucketName, _ossConfig.VersionFileKey);
                string versionContent;
                using (var reader = new StreamReader(result.Content))
                {
                    versionContent = reader.ReadToEnd();
                }

                _logger.Debug($"从 OSS 获取的版本信息: {versionContent}");

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
                            ReleaseNotes = versionInfo.ReleaseNotes?.Replace("\\n", "\n") ?? "",
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
    }
} 