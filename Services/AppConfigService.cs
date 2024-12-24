using System;
using System.IO;
using Newtonsoft.Json;
using WpfApp.Models;
using WpfApp.Services;

namespace WpfApp.Services
{
    public class AppConfigService
    {
        private static readonly LogManager _logger = LogManager.Instance;
        private static string _configPath = "AppConfig.json";  // 默认路径
        private static AppConfig? _config;
        private static readonly object _lockObject = new object();
        // 添加配置变更事件
        public static event EventHandler<AppConfig>? ConfigChanged;

        public static AppConfig Config
        {
            get
            {
                if (_config == null)
                {
                    lock (_lockObject)
                    {
                        if (_config == null)  // 双重检查锁定
                        {
                            LoadConfig();
                        }
                    }
                }
                return _config ?? new AppConfig();
            }
        }

        public static void Initialize(string userDataPath)
        {
            lock (_lockObject)
            {
                _configPath = Path.Combine(userDataPath, "AppConfig.json");
                _config = null; // 清除现有配置，强制重新加载
                LoadConfig();
            }
        }

        // 保留无参数版本以保持兼容性
        public static void Initialize()
        {
            Initialize("AppConfig.json");
        }

        private static void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    var jsonSettings = new JsonSerializerSettings
                    {
                        ObjectCreationHandling = ObjectCreationHandling.Replace
                    };
                    _config = JsonConvert.DeserializeObject<AppConfig>(json, jsonSettings);
                    
                    _logger.LogDebug("Config", $"从配置文件加载窗口尺寸: {_config?.UI.MainWindow.DefaultWidth}x{_config?.UI.MainWindow.DefaultHeight}");
                    _logger.LogInitialization("Config", $"配置文件加载成功: {_configPath}");
                    
                    ValidateConfig();
                }
                else
                {
                    _config = new AppConfig();
                    _logger.LogInitialization("Config", "使用默认配置");
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Config", $"加载配置文件失败: {ex.Message}");
                _config = new AppConfig();
            }
        }

        private static void ValidateConfig()
        {
            if (_config == null) return;

            bool configChanged = false;
            
            // 验证并修正窗口尺寸
            if (_config.UI.MainWindow.DefaultWidth < 500)
            {
                _logger.LogWarning("Config", $"窗口宽度 {_config.UI.MainWindow.DefaultWidth} 小于最小值，已修正为 500");
                _config.UI.MainWindow.DefaultWidth = 500;
                configChanged = true;
            }
            
            if (_config.UI.MainWindow.DefaultHeight < 450)
            {
                _logger.LogWarning("Config", $"窗口高度 {_config.UI.MainWindow.DefaultHeight} 小于最小值，已修正为 450");
                _config.UI.MainWindow.DefaultHeight = 450;
                configChanged = true;
            }

            // 验证热键模式配置
            if (_config.keyMode != 0 && _config.keyMode != 1)
            {
                _logger.LogWarning("Config", $"无效的按键模式 {_config.keyMode}，已修正为顺序模式(0)");
                _config.keyMode = 0;
                configChanged = true;
            }

            // 验证热键配置
            if (_config.startKey == null)
            {
                _logger.LogWarning("Config", "启动热键未设置，已设置为默认值");
                _config.startKey = DDKeyCode.None;
                configChanged = true;
            }

            if (_config.stopKey == null && _config.keyMode == 0)
            {
                _logger.LogWarning("Config", "停止热键未设置，已设置为默认值");
                _config.stopKey = DDKeyCode.None;
                configChanged = true;
            }

            if (configChanged)
            {
                _logger.LogDebug("Config", "配置已更新并验证");
                SaveConfig();
            }
        }

        // 优化保存配置方法
        public static void SaveConfig()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_config == null) return;

                    string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                    File.WriteAllText(_configPath, json);
                    
                    // 确保在保存后触发配置变更事件
                    ConfigChanged?.Invoke(null, _config);
                    
                    _logger.LogDebug("Config", $"配置已保存到: {_configPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Config", $"保存配置文件失败: {ex.Message}");
                throw; // 重新抛出异常，让调用者知道保存失败
            }
        }

        // 添加更新配置方法
        public static void UpdateConfig(Action<AppConfig> updateAction)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_config == null) return;

                    updateAction(_config);
                    ValidateConfig(); // 验证更新后的配置
                    SaveConfig(); // 保存并触发事件
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Config", $"更新配置失败: {ex.Message}");
                throw;
            }
        }
        
        // 添加资源清理方法
        public static void Cleanup()
        {
            lock (_lockObject)
            {
                ConfigChanged = null;
                _config = null;
            }
        }
    }
} 