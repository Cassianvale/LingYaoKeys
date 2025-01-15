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

                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(versionContent);

                if (versionInfo == null)
                {
                    _logger.Warning("获取版本信息失败：返回数据为空");
                    return null;
                }

                var currentVersion = GetCurrentVersion();
                
                // 尝试解析版本号，如果失败则使用简单的字符串比较
                Version? latestVersion = null;
                Version? parsedCurrentVersion = null;
                
                try
                {
                    latestVersion = Version.Parse(versionInfo.Version);
                    parsedCurrentVersion = Version.Parse(currentVersion.ToString());
                }
                catch (Exception ex)
                {
                    _logger.Warning($"版本号解析失败，将使用字符串比较: {ex.Message}");
                }

                bool hasNewVersion = false;
                if (latestVersion != null && parsedCurrentVersion != null)
                {
                    hasNewVersion = latestVersion > parsedCurrentVersion;
                }
                else
                {
                    // 使用字符串比较作为后备方案
                    hasNewVersion = string.Compare(versionInfo.Version, currentVersion.ToString(), StringComparison.Ordinal) > 0;
                }

                if (hasNewVersion)
                {
                    return new UpdateInfo
                    {
                        CurrentVersion = currentVersion.ToString(),
                        LatestVersion = versionInfo.Version,
                        ReleaseNotes = versionInfo.ReleaseNotes,
                        DownloadUrl = versionInfo.DownloadUrl
                    };
                }

                return null;
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
            return version ?? new Version(1, 0, 0);
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