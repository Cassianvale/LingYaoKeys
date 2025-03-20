using System;
using System.IO;
using System.Reflection;

namespace WpfApp.Services.Utils;

/// <summary>
/// 路径服务类，负责管理应用程序所有路径相关的操作
/// </summary>
public class PathService
{
    private static readonly Lazy<PathService> _instance = new(() => new PathService());
    private readonly SerilogManager _logger = SerilogManager.Instance;
    
    // 默认应用数据目录名称
    private const string APP_DATA_FOLDER_NAME = ".lykeys";
    
    // 应用数据根目录
    private string _appDataPath;
    
    // 配置文件目录
    private string _configPath;
    
    // 日志文件目录
    private string _logPath;
    
    // 资源文件目录
    private string _resourcePath;
    
    // 音频文件目录
    private string _soundPath;
    
    // 驱动文件目录
    private string _driverPath;
    
    /// <summary>
    /// 获取单例实例
    /// </summary>
    public static PathService Instance => _instance.Value;
    
    /// <summary>
    /// 应用数据根目录
    /// </summary>
    public string AppDataPath => _appDataPath;
    
    /// <summary>
    /// 配置文件目录
    /// </summary>
    public string ConfigPath => _configPath;
    
    /// <summary>
    /// 日志文件目录
    /// </summary>
    public string LogPath => _logPath;
    
    /// <summary>
    /// 资源文件目录
    /// </summary>
    public string ResourcePath => _resourcePath;
    
    /// <summary>
    /// 音频文件目录
    /// </summary>
    public string SoundPath => _soundPath;
    
    /// <summary>
    /// 驱动文件目录
    /// </summary>
    public string DriverPath => _driverPath;
    
    private PathService()
    {
        // 在构造函数中初始化基本路径
        InitializePaths();
    }
    
    /// <summary>
    /// 初始化所有应用程序路径
    /// </summary>
    private void InitializePaths()
    {
        try
        {
            // 首先尝试使用用户配置文件路径
            string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            // 检查用户配置文件路径是否有效
            if (Directory.Exists(userProfilePath))
            {
                _appDataPath = Path.Combine(userProfilePath, APP_DATA_FOLDER_NAME);
            }
            else
            {
                // 如果用户配置文件路径无效，则使用应用程序目录
                _appDataPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory,
                    APP_DATA_FOLDER_NAME);
            }
            
            // 初始化各个子目录
            _configPath = Path.Combine(_appDataPath, "Config");
            _logPath = Path.Combine(_appDataPath, "Logs");
            _resourcePath = Path.Combine(_appDataPath, "Resource");
            _soundPath = Path.Combine(_resourcePath, "sound");
            _driverPath = Path.Combine(_resourcePath, "lykeysdll");
            
            // 确保所有目录都存在
            EnsureDirectoriesExist();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"初始化路径服务失败: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 确保所有必要的目录都存在
    /// </summary>
    private void EnsureDirectoriesExist()
    {
        try
        {
            Directory.CreateDirectory(_appDataPath);
            Directory.CreateDirectory(_configPath);
            Directory.CreateDirectory(_logPath);
            Directory.CreateDirectory(_resourcePath);
            Directory.CreateDirectory(_soundPath);
            Directory.CreateDirectory(_driverPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"创建应用程序目录失败: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 获取配置文件的完整路径
    /// </summary>
    /// <param name="fileName">配置文件名</param>
    /// <returns>配置文件的完整路径</returns>
    public string GetConfigFilePath(string fileName)
    {
        return Path.Combine(_configPath, fileName);
    }
    
    /// <summary>
    /// 获取默认的AppConfig.json路径
    /// </summary>
    /// <returns>AppConfig.json的完整路径</returns>
    public string GetAppConfigPath()
    {
        return Path.Combine(_configPath, "AppConfig.json");
    }
    
    /// <summary>
    /// 获取日志文件的目录路径
    /// </summary>
    /// <returns>日志文件的目录路径</returns>
    public string GetLogDirectoryPath()
    {
        return _logPath;
    }
    
    /// <summary>
    /// 获取资源文件的完整路径
    /// </summary>
    /// <param name="relativePath">相对于资源目录的路径</param>
    /// <returns>资源文件的完整路径</returns>
    public string GetResourcePath(string relativePath)
    {
        return Path.Combine(_resourcePath, relativePath);
    }
    
    /// <summary>
    /// 获取音频文件的完整路径
    /// </summary>
    /// <param name="fileName">音频文件名</param>
    /// <returns>音频文件的完整路径</returns>
    public string GetSoundFilePath(string fileName)
    {
        return Path.Combine(_soundPath, fileName);
    }
    
    /// <summary>
    /// 获取驱动文件的完整路径
    /// </summary>
    /// <param name="fileName">驱动文件名</param>
    /// <returns>驱动文件的完整路径</returns>
    public string GetDriverFilePath(string fileName)
    {
        return Path.Combine(_driverPath, fileName);
    }
}