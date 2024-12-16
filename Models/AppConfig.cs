using System;
using Newtonsoft.Json;

namespace WpfApp.Models
{
    public class AppConfig
    {
        public AppInfo AppInfo { get; set; } = new AppInfo();
        public UIConfig UI { get; set; } = new UIConfig();
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
} 