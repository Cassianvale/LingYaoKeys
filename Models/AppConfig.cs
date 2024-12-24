using System;
using System.Reflection;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Windows.Input;
using WpfApp.Services;

namespace WpfApp.Models
{
    public class AppConfig
    {
        [JsonIgnore]
        public AppInfo AppInfo { get; set; } = new AppInfo();
        [JsonIgnore]
        public UIConfig UI { get; set; } = new UIConfig();
        public LoggingConfig Logging { get; set; } = new LoggingConfig();
        
        // 按键配置相关属性
        public DDKeyCode? startKey { get; set; }
        public ModifierKeys startMods { get; set; }
        public DDKeyCode? stopKey { get; set; }
        public ModifierKeys stopMods { get; set; }
        public List<DDKeyCode> keyList { get; set; } = new List<DDKeyCode>();
        public List<bool> keySelections { get; set; } = new List<bool>();
        public int keyMode { get; set; }
        public int interval { get; set; } = 10;
        public bool? soundEnabled { get; set; }
        public bool? IsGameMode { get; set; }
        public int? KeyPressInterval { get; set; }

        // 浮窗状态
        public bool? IsFloatingWindowEnabled { get; set; }

        [JsonIgnore]
        public string Author { get; set; } = "慕长秋";

        public AppConfig()
        {
            IsFloatingWindowEnabled = false; // 默认关闭浮窗
        }
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
    }

    public class UIConfig
    {
        [JsonIgnore]
        public MainWindowConfig MainWindow { get; set; } = new MainWindowConfig();
    }

    public class MainWindowConfig
    {
        [JsonIgnore]
        public int DefaultWidth { get; set; } = 500;
        [JsonIgnore]
        public int DefaultHeight { get; set; } = 450;
    }

    public class LoggingConfig
    {
        public bool Enabled { get; set; } = true;
        public string LogLevel { get; set; } = "Debug";
        public FileSettings FileSettings { get; set; } = new FileSettings();
        public LogCategories Categories { get; set; } = new LogCategories();
    }

    public class FileSettings
    {
        public string Directory { get; set; } = "Logs";
        public int MaxFileSize { get; set; } = 10;  // 以MB为单位
        public int MaxFileCount { get; set; } = 10;
        public string RollingInterval { get; set; } = "Day";
        public int RetainDays { get; set; } = 7;
    }

    public class LogCategories
    {
        public bool KeyOperation { get; set; } = true;
        public bool Performance { get; set; } = true;
        public bool Driver { get; set; } = true;
        public bool Config { get; set; } = true;
    }
} 