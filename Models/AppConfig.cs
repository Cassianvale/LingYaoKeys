using System;
using Newtonsoft.Json;

namespace WpfApp.Models
{
    public class AppConfig
    {
        public AppInfo AppInfo { get; set; } = new AppInfo();
        public UIConfig UI { get; set; } = new UIConfig();
        public LoggingConfig Logging { get; set; } = new LoggingConfig();
    }

    public class AppInfo
    {
        public string Name { get; set; } = "jx3wpf";
        public string Title { get; set; } = "剑网3按键助手";
        public string Version { get; set; } = "1.0.0";
        public string Copyright { get; set; } = "© 2024";
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