using System;
using System.Reflection;
using Newtonsoft.Json;

namespace WpfApp.Models
{
    public class AppConfig
    {
        public AppInfo AppInfo { get; set; } = new AppInfo();
        public UIConfig UI { get; set; } = new UIConfig();
        public LoggingConfig Logging { get; set; } = new LoggingConfig();
        
        [JsonIgnore]
        public string Author { get; } = "慕长秋";
    }

    public class AppInfo
    {
        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        
        public string Title { get; set; } = "灵曜按键";
        
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
        public MainWindowConfig MainWindow { get; set; } = new MainWindowConfig();
    }

    public class MainWindowConfig
    {
        public int DefaultWidth { get; set; } = 600;
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