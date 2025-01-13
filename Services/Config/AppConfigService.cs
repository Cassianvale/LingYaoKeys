using System;
using System.IO;
using Newtonsoft.Json;
using WpfApp.Services.Models;
using System.Reflection;
using System.Windows.Input;
using System.Windows;

namespace WpfApp.Services
{
    public class ConfigChangedEventArgs : EventArgs
    {
        public string Section { get; }
        public AppConfig Config { get; }

        public ConfigChangedEventArgs(string section, AppConfig config)
        {
            Section = section;
            Config = config;
        }
    }

    public class AppConfigService
    {
        private static string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".lykeys",
            "AppConfig.json"
        );
        private static AppConfig? _config;
        private static readonly object _lockObject = new object();
        public static event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

        public static AppConfig Config
        {
            get
            {
                if (_config == null)
                {
                    lock (_lockObject)
                    {
                        if (_config == null)
                        {
                            LoadConfig();
                        }
                    }
                }
                return _config ?? CreateDefaultConfig();
            }
        }

        public static void Initialize(string? userDataPath = null)
        {
            Console.WriteLine("开始初始化配置服务...");
            
            lock (_lockObject)
            {
                if (!string.IsNullOrEmpty(userDataPath))
                {
                    _configPath = Path.Combine(userDataPath, "AppConfig.json");
                    Console.WriteLine($"使用自定义配置路径: {_configPath}");
                }
                
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
                
                if (!File.Exists(_configPath))
                {
                    _config = CreateDefaultConfig();
                    SaveConfig();
                    Console.WriteLine("已创建新的默认配置文件");
                }
                else
                {
                    LoadConfig();
                }
                
                Console.WriteLine("配置服务初始化完成");
            }
        }

        private static void LoadConfig()
        {
            try
            {
                // 如果配置文件不存在，创建新的配置
                if (!File.Exists(_configPath))
                {
                    _config = CreateDefaultConfig();
                    SaveConfig();
                    Console.WriteLine("创建新的默认配置文件");
                    return;
                }

                // 读取现有配置
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
                    Console.WriteLine("使用默认配置初始化");
                }
                
                Console.WriteLine($"从配置文件加载成功: {_configPath}");
                ValidateConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载配置文件失败: {ex.Message}");
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
                        MaxFileSize = 10,
                        MaxFileCount = 10,
                        RollingInterval = "Day",
                        RetainDays = 7
                    },
                    // 排除一些常见的调试和性能日志
                    ExcludedTags = new List<string>
                    {
                        // // 日志级别标签
                        // "Debug",      // 调试信息
                        // "Trace",      // 跟踪信息
                        // "Info",       // 普通信息
                        
                        // // 功能模块标签
                        // "Sequence",   // 按键序列相关
                        // "Driver",     // 驱动相关
                        // "Init",       // 初始化相关
                        // "UI",         // UI相关
                        // "Config",     // 配置相关
                        // "Performance" // 性能相关
                    },
                    // 排除一些不太重要的类的日志
                    ExcludedSources = new List<string>{
                        "*.xaml*",
                        "ControlStyles.xaml",
                        "App.xaml",
                        "MainWindow.xaml"
                    },
                    // 排除一些常见的方法日志
                    ExcludedMethods = new List<string>{},
                    // 排除一些常见的消息模式
                    ExcludedPatterns = new List<string>{
                        "窗口初始化完成*",
                        "已获取*实例",
                        "鼠标点击元素: *",
                        "*获取句柄*",
                        "*_DataContextChanged*",
                        "*触发: *",
                        "控件加载完成: *",
                        "页面切换到: *"
                    }
                },
                startKey = LyKeysCode.VK_F9,    
                startMods = 0,
                stopKey = LyKeysCode.VK_F10,
                stopMods = 0,
                keys = new List<KeyConfig> 
                { 
                    new KeyConfig(LyKeysCode.VK_F, true, true),
                    new KeyConfig(LyKeysCode.VK_1, false, false),
                    new KeyConfig(LyKeysCode.VK_2, false, false)
                },
                KeyBurst = new List<KeyBurstConfig>
                {
                    new KeyBurstConfig(LyKeysCode.VK_F, 10, 5)
                },
                IsRapidFire = false,
                keyMode = 0,
                interval = 5,
                soundEnabled = true,
                IsGameMode = true,
                KeyPressInterval = 5,
                FloatingWindowLeft = SystemParameters.WorkArea.Right - 100 - 10,
                FloatingWindowTop = SystemParameters.WorkArea.Bottom - 40 - 10,
                IsFloatingWindowEnabled = true,
            };
        }

        private static void ValidateConfig()
        {
            if (_config == null) return;

            bool configChanged = false;
            
            // 验证并修正窗口尺寸
            if (_config.UI.MainWindow.DefaultWidth < 510)
            {
                Console.WriteLine($"窗口宽度 {_config.UI.MainWindow.DefaultWidth} 小于最小值，已修正为 510");
                _config.UI.MainWindow.DefaultWidth = 510;
                configChanged = true;
            }
            
            if (_config.UI.MainWindow.DefaultHeight < 450)
            {
                Console.WriteLine($"窗口高度 {_config.UI.MainWindow.DefaultHeight} 小于最小值，已修正为 450");
                _config.UI.MainWindow.DefaultHeight = 450;
                configChanged = true;
            }

            // 验证热键模式配置
            if (_config.keyMode != 0 && _config.keyMode != 1)
            {
                Console.WriteLine($"无效的按键模式 {_config.keyMode}，已修正为顺序模式(0)");
                _config.keyMode = 0;
                configChanged = true;
            }

            // 验证热键配置
            if (_config.startKey == null)
            {
                Console.WriteLine("启动热键未设置，已设置为默认值");
                _config.startKey = LyKeysCode.VK_F9;
                configChanged = true;
            }

            if (_config.stopKey == null && _config.keyMode == 0)
            {
                Console.WriteLine("停止热键未设置，已设置为默认值");
                _config.stopKey = LyKeysCode.VK_F10;
                configChanged = true;
            }

            if (configChanged)
            {
                Console.WriteLine("配置已更新并验证");
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
                    ConfigChanged?.Invoke(null, new ConfigChangedEventArgs("AppConfig", _config));
                    
                    Console.WriteLine($"配置已更新并保存到: {_configPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存配置文件失败: {ex.Message}");
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
                Console.WriteLine($"更新配置失败: {ex.Message}");
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