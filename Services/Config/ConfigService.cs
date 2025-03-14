using System.IO;
using System.Diagnostics;
using Newtonsoft.Json;
using WpfApp.Services.Utils;

namespace WpfApp.Services.Config;

public class ConfigService
{
    private readonly SerilogManager _logger = SerilogManager.Instance;

    private readonly string _configDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".lykeys");

    private const int MAX_BACKUP_FILES = 5;
    private Dictionary<string, object> _settings;

    public ConfigService()
    {
        _settings = LoadSettings();
    }

    private Dictionary<string, object> LoadSettings()
    {
        try
        {
            var appConfigPath = Path.Combine(_configDir, "AppConfig.json");
            if (File.Exists(appConfigPath))
            {
                var json = File.ReadAllText(appConfigPath);
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(json)
                       ?? new Dictionary<string, object>();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("加载设置失败", ex);
        }

        return new Dictionary<string, object>();
    }

    public T GetSetting<T>(string key, T defaultValue)
    {
        if (_settings.TryGetValue(key, out var value))
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }

        return defaultValue;
    }

    public void SaveSetting(string key, object value)
    {
        _settings[key] = value;
        SaveSettings();
    }

    private void SaveSettings()
    {
        try
        {
            var appConfigPath = Path.Combine(_configDir, "AppConfig.json");
            Directory.CreateDirectory(_configDir);
            var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            File.WriteAllText(appConfigPath, json);
        }
        catch (Exception ex)
        {
            _logger.Error("保存设置失败", ex);
        }
    }

    public void ImportConfig(string sourceFile)
    {
        try
        {
            var configContent = File.ReadAllText(sourceFile);
            var appConfigPath = Path.Combine(_configDir, "AppConfig.json");

            Directory.CreateDirectory(_configDir);

            if (File.Exists(appConfigPath))
            {
                var backupPath = Path.Combine(
                    _configDir,
                    $"AppConfig_backup_{DateTime.Now:yyyyMMddHHmmss}.json");
                File.Copy(appConfigPath, backupPath);
                CleanupOldBackups();
            }

            File.WriteAllText(appConfigPath, configContent);
            _settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(configContent)
                        ?? new Dictionary<string, object>();
            RestartApplication();
        }
        catch (Exception ex)
        {
            _logger.Error("导入配置文件失败", ex);
            throw;
        }
    }

    public void ExportConfig(string targetFile)
    {
        try
        {
            var appConfigPath = Path.Combine(_configDir, "AppConfig.json");
            if (!File.Exists(appConfigPath)) throw new FileNotFoundException("配置文件不存在", appConfigPath);
            File.Copy(appConfigPath, targetFile, true);
        }
        catch (Exception ex)
        {
            _logger.Error("导出配置文件失败", ex);
            throw;
        }
    }

    private void CleanupOldBackups()
    {
        try
        {
            var backupFiles = Directory.GetFiles(_configDir, "AppConfig_backup_*.json")
                .OrderByDescending(f => f)
                .Skip(MAX_BACKUP_FILES);

            foreach (var file in backupFiles)
                try
                {
                    File.Delete(file);
                    _logger.Debug($"已删除旧的备份文件: {file}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"删除备份文件失败: {file}", ex);
                }
        }
        catch (Exception ex)
        {
            _logger.Error("清理备份文件失败", ex);
        }
    }

    private void RestartApplication()
    {
        try
        {
            var appPath = Process.GetCurrentProcess().MainModule?.FileName
                          ?? throw new InvalidOperationException("无法获取应用程序路径");

            var startInfo = new ProcessStartInfo
            {
                FileName = appPath,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.Error("重启应用程序失败", ex);
            throw;
        }
    }
}