using System;
using System.IO;
using Newtonsoft.Json;
using WpfApp.Models;

namespace WpfApp.Services
{
    public class AppConfigService
    {
        private static readonly LogManager _logger = LogManager.Instance;
        private static readonly string ConfigPath = "AppConfig.json";
        private static AppConfig? _config;

        public static AppConfig Config
        {
            get
            {
                if (_config == null)
                {
                    LoadConfig();
                }
                return _config ?? new AppConfig();
            }
        }

        private static void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    _config = JsonConvert.DeserializeObject<AppConfig>(json);
                    
                    _logger.LogDebug("Config", $"从配置文件加载窗口尺寸: {_config?.UI.MainWindow.DefaultWidth}x{_config?.UI.MainWindow.DefaultHeight}");
                    
                    _logger.LogInitialization("Config", "配置文件加载成功");
                    
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

            bool sizeChanged = false;
            
            // 验证并修正窗口尺寸
            if (_config.UI.MainWindow.DefaultWidth < 500)
            {
                _logger.LogWarning("Config", $"窗口宽度 {_config.UI.MainWindow.DefaultWidth} 小于最小值，已修正为 500");
                _config.UI.MainWindow.DefaultWidth = 500;
                sizeChanged = true;
            }
            
            if (_config.UI.MainWindow.DefaultHeight < 420)
            {
                _logger.LogWarning("Config", $"窗口高度 {_config.UI.MainWindow.DefaultHeight} 小于最小值，已修正为 420");
                _config.UI.MainWindow.DefaultHeight = 420;
                sizeChanged = true;
            }

            if (sizeChanged)
            {
                _logger.LogDebug("Config", $"最终窗口尺寸: {_config.UI.MainWindow.DefaultWidth}x{_config.UI.MainWindow.DefaultHeight}");
                SaveConfig();
            }
        }

        private static void SaveConfig()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError("App", $"保存配置文件失败: {ex.Message}");
            }
        }
    }
} 