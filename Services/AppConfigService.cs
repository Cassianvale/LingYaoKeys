using System;
using System.IO;
using Newtonsoft.Json;
using WpfApp.Models;
using System.Reflection;
using System.Windows.Input;

namespace WpfApp.Services
{
    public class AppConfigService
    {
        private static readonly LogManager _logger = LogManager.Instance;
        private static string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".lingyao",
            "AppConfig.json"
        );
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

        public static void Initialize(string? userDataPath = null)
        {
            _logger.LogDebug("Config", "开始初始化配置服务..."); // 添加初始化开始日志
            
            lock (_lockObject)
            {
                // 如果指定了自定义路径,则使用自定义路径
                if (!string.IsNullOrEmpty(userDataPath))
                {
                    _configPath = Path.Combine(userDataPath, "AppConfig.json");
                    _logger.LogDebug("Config", $"使用自定义配置路径: {_configPath}");
                }
                
                // 确保配置目录存在
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
                
                // 如果用户目录下不存在配置文件,则从嵌入资源复制默认配置
                if (!File.Exists(_configPath))
                {
                    _logger.LogDebug("Config", "配置文件不存在，尝试从嵌入资源创建...");
                    try
                    {
                        // 从嵌入资源读取默认配置
                        var assembly = Assembly.GetExecutingAssembly();
                        var resourceName = "WpfApp.AppConfig.json";
                        
                        // 调试用：列出所有嵌入资源
                        var resources = assembly.GetManifestResourceNames();
                        _logger.LogDebug("Config", $"可用的嵌入资源: {string.Join(", ", resources)}");
                        
                        using var stream = assembly.GetManifestResourceStream(resourceName);
                        if (stream != null)
                        {
                            using var reader = new StreamReader(stream);
                            string defaultConfig = reader.ReadToEnd();
                            _config = JsonConvert.DeserializeObject<AppConfig>(defaultConfig);
                            
                            // 保存到用户目录
                            SaveConfig();
                            _logger.LogInitialization("Config", "已从默认模板创建配置文件");
                        }
                        else
                        {
                            _logger.LogError("Config", $"找不到嵌入资源: {resourceName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Config", $"加载默认配置模板失败: {ex.Message}");
                        _config = new AppConfig { /* 默认配置 */ };
                        SaveConfig();
                    }
                }
                else
                {
                    LoadConfig();
                }
                
                _logger.LogDebug("Config", "配置服务初始化完成");
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
                        ObjectCreationHandling = ObjectCreationHandling.Replace,
                        NullValueHandling = NullValueHandling.Include
                    };
                    _config = JsonConvert.DeserializeObject<AppConfig>(json, jsonSettings);
                    
                    // 如果配置为空或关键值为null，使用默认值初始化
                    if (_config == null || HasNullValues(_config))
                    {
                        _config = CreateDefaultConfig();
                        SaveConfig();
                        _logger.LogInitialization("Config", "使用默认配置初始化");
                    }
                    
                    _logger.LogDebug("Config", $"从配置文件加载成功: {_configPath}");
                    ValidateConfig();
                }
                else
                {
                    _config = CreateDefaultConfig();
                    SaveConfig();
                    _logger.LogInitialization("Config", "创建默认配置文件");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Config", $"加载配置文件失败: {ex.Message}");
                _config = CreateDefaultConfig();
                SaveConfig();
            }
        }

        private static bool HasNullValues(AppConfig config)
        {
            return config.soundEnabled == null 
                || config.IsGameMode == null 
                || config.KeyPressInterval == null;
        }

        private static AppConfig CreateDefaultConfig()
        {
            return new AppConfig
            {
                Logging = new LoggingConfig
                {
                    Enabled = false,
                    LogLevel = "Debug",
                    FileSettings = new LogFileSettings
                    {
                        Directory = "Logs",
                        MaxFileSize = 10,
                        MaxFileCount = 10,
                        RollingInterval = "Day",
                        RetainDays = 7
                    },
                    Categories = new LogCategories
                    {
                        KeyOperation = true,
                        Performance = true,
                        Driver = true,
                        Config = true
                    }
                },
                startKey = (DDKeyCode)109,
                startMods = 0,
                stopKey = (DDKeyCode)110,
                stopMods = 0,
                keyList = new List<DDKeyCode> { (DDKeyCode)404, (DDKeyCode)201, (DDKeyCode)202 },
                keySelections = new List<bool> { true, false, false },
                keyMode = 0,
                interval = 5,
                soundEnabled = true,
                IsGameMode = true,
                KeyPressInterval = 5,
                // IsFloatingWindowEnabled = false
            };
        }

        private static void ValidateConfig()
        {
            if (_config == null) return;

            bool configChanged = false;
            
            // 验证并修正窗口尺寸
            if (_config.UI.MainWindow.DefaultWidth < 510)
            {
                _logger.LogWarning("Config", $"窗口宽度 {_config.UI.MainWindow.DefaultWidth} 小于最小值，已修正为 510");
                _config.UI.MainWindow.DefaultWidth = 510;
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

                    string newJson = JsonConvert.SerializeObject(_config, Formatting.Indented);
                    
                    // 检查配置是否真的发生了变化
                    if (File.Exists(_configPath))
                    {
                        string existingJson = File.ReadAllText(_configPath);
                        if (existingJson == newJson)
                        {
                            // 配置没有变化，不需要保存和触发事件
                            return;
                        }
                    }
                    
                    File.WriteAllText(_configPath, newJson);
                    
                    // 只在配置真正发生变化时触发事件
                    ConfigChanged?.Invoke(null, _config);
                    
                    _logger.LogDebug("Config", $"配置已更新并保存到: {_configPath}");
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