using System;
using System.Reflection;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Windows.Input;
using WpfApp.Services;

namespace WpfApp.Services.Models
{
    public class AppConfig
    {
        [JsonIgnore]
        public AppInfo AppInfo { get; set; } = new AppInfo();
        [JsonIgnore]
        public UIConfig UI { get; set; } = new UIConfig();
        public LoggingConfig Logging { get; set; } = new LoggingConfig();
        
        // 按键配置相关属性
        public LyKeysCode? startKey { get; set; }
        public ModifierKeys startMods { get; set; }
        public LyKeysCode? stopKey { get; set; }
        public ModifierKeys stopMods { get; set; }
        public List<LyKeysCode> keyList { get; set; } = new List<LyKeysCode>();
        public List<bool> keySelections { get; set; } = new List<bool>();
        public int keyMode { get; set; }
        public int interval { get; set; } = 10;
        public bool? soundEnabled { get; set; }
        public bool? IsGameMode { get; set; }
        public int? KeyPressInterval { get; set; }
        public double FloatingWindowLeft { get; set; }
        public double FloatingWindowTop { get; set; }
        public bool IsFloatingWindowEnabled { get; set; }

        [JsonIgnore]
        public string Author { get; set; } = "慕长秋";

    }

    public class AppInfo
    {
        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        
        [JsonIgnore]
        public string Title { get; set; } = "灵曜按键 (LingYao Keys)";
        
        [JsonIgnore]
        public string Version 
        { 
            get => Assembly.GetName().Version?.ToString() ?? "1.0.0";
            set { /* 允许从配置文件加载但忽略 */ } 
        }
        
        [JsonIgnore]
        public string Copyright { get; } = "Copyright © 2024";

        [JsonIgnore]
        public string GitHubUrl { get; } = "https://github.com/Cassianvale/LingYaoKeys";
    }

    public class UIConfig
    {
        [JsonIgnore]
        public MainWindowConfig MainWindow { get; set; } = new MainWindowConfig();
    }

    public class MainWindowConfig
    {
        [JsonIgnore]
        public int DefaultWidth { get; set; } = 510;
        [JsonIgnore]
        public int DefaultHeight { get; set; } = 450;
    }

    public class LogFileSettings
    {
        public int MaxFileSize { get; set; } = 10;
        public int MaxFileCount { get; set; } = 10;
        public string RollingInterval { get; set; } = "Day";
        public int RetainDays { get; set; } = 7;
    }

    public class LoggingConfig
    {
        public bool Enabled { get; set; }
        public string LogLevel { get; set; } = "Debug";

        // 日志：文件配置
        public LogFileSettings FileSettings { get; set; } = new();

        // 日志：排除特定标签
        public List<string> ExcludedTags { get; set; } = new();
        
        // 日志：排除特定源上下文日志（类名）
        public List<string> ExcludedSources { get; set; } = new();
        
        // 日志：排除特定方法名
        public List<string> ExcludedMethods { get; set; } = new();
        
        // 日志：排除特定消息模式（支持通配符 * ）
        public List<string> ExcludedPatterns { get; set; } = new();
    }
} 