using System.Reflection;
using Newtonsoft.Json;
using System.Windows.Input;
using WpfApp.Services.Core;

namespace WpfApp.Services.Config
{

    public class LogFileSettings
    {
        public int MaxFileSize { get; set; } = 10;
        public int MaxFileCount { get; set; } = 10;
        public string RollingInterval { get; set; } = "Day";
        public int RetainDays { get; set; } = 7;
    }

    public class DebugConfig
    {
        public bool IsDebugMode { get; set; } = false;
        public bool EnableLogging { get; set; } = false;
        public string LogLevel { get; set; } = "Debug";
        public LogFileSettings FileSettings { get; set; } = new();
        public List<string> ExcludedTags { get; set; } = new();
        public List<string> ExcludedSources { get; set; } = new();
        public List<string> ExcludedMethods { get; set; } = new();
        public List<string> ExcludedPatterns { get; set; } = new();

        // 当调试模式开启或关闭时，更新所有调试功能的状态
        public void UpdateDebugState()
        {
            if (IsDebugMode)
            {
                EnableLogging = true;
                LogLevel = "Debug";
            }
            else
            {
                EnableLogging = false;
                LogLevel = "Information";
            }
        }
    }

    public class UpdateInfo
    {
        public string CurrentVersion { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
    }

    public class VersionInfo
    {
        public string Version { get; set; } = "";  // 版本号
        public string DownloadUrl { get; set; } = "";  // oss下载链接
        public string? GithubUrl { get; set; }  // github下载链接
        public string? ReleaseDate { get; set; }  // 发布日期
        public string? MinVersion { get; set; }  // 最小版本号
        public bool ForceUpdate { get; set; }  // 是否强制更新
    }

    public class KeyConfig
    {
        public LyKeysCode Code { get; set; }
        public bool IsSelected { get; set; }
        public bool IsKeyBurst { get; set; }
        public int KeyInterval { get; set; } = 5;

        public KeyConfig(LyKeysCode code, bool isSelected = true, bool isKeyBurst = false, int keyInterval = 5)
        {
            Code = code;
            IsSelected = isSelected;
            IsKeyBurst = isKeyBurst;
            KeyInterval = keyInterval;
        }
    }

    public class KeyBurstConfig
    {
        public LyKeysCode Code { get; set; }
        public int RapidFireDelay { get; set; }
        public int PressTime { get; set; }

        public KeyBurstConfig(LyKeysCode code, int rapidFireDelay = 10, int pressTime = 5)
        {
            Code = code;
            RapidFireDelay = rapidFireDelay;
            PressTime = pressTime;
        }
    }

    public class AppConfig
    {
        [JsonIgnore]
        public AppInfo AppInfo { get; set; } = new AppInfo();
        public UIConfig UI { get; set; } = new UIConfig();
        public DebugConfig Debug { get; set; } = new DebugConfig();
        
        // 按键配置相关属性
        public LyKeysCode? startKey { get; set; }
        public ModifierKeys startMods { get; set; }
        public LyKeysCode? stopKey { get; set; }
        public ModifierKeys stopMods { get; set; }
        public List<KeyConfig> keys { get; set; } = new List<KeyConfig>();
        public List<KeyBurstConfig> KeyBurst { get; set; } = new List<KeyBurstConfig>();
        public bool IsRapidFire { get; set; }
        public int keyMode { get; set; }
        // 默认按键间隔，仅用于新添加按键时的默认值
        public int interval { get; set; } = 10;
        public bool? soundEnabled { get; set; }
        public bool? IsGameMode { get; set; }
        public int? KeyPressInterval { get; set; }
        public double FloatingWindowLeft { get; set; }
        public double FloatingWindowTop { get; set; }

        // 连发功能是否启用
        public bool? IsRapidFireEnabled { get; set; }

        // 是否自动切换到英文输入法
        public bool? AutoSwitchToEnglishIME { get; set; } = true;

        // 窗口句柄相关信息
        public string? TargetWindowClassName { get; set; }
        public string? TargetWindowProcessName { get; set; }
        public string? TargetWindowTitle { get; set; }

        [JsonIgnore]
        public string Author { get; set; } = "慕长秋";

        public AppConfig()
        {
            Debug = new DebugConfig();
            UI = new UIConfig();
            keys = new List<KeyConfig>();
            KeyBurst = new List<KeyBurstConfig>();
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
        }
        
        [JsonIgnore]
        public string Copyright { get; } = "Copyright © 2024";

        [JsonIgnore]
        public string GitHubUrl { get; } = "https://github.com/Cassianvale/LingYaoKeys";
    }

    public class UIConfig
    {
        public WindowConfig MainWindow { get; set; } = new WindowConfig();
        public FloatingWindowConfig FloatingWindow { get; set; } = new FloatingWindowConfig();
    }

    public class WindowConfig
    {
        [JsonIgnore]
        public double DefaultWidth { get; set; } = 970;
        [JsonIgnore]
        public double DefaultHeight { get; set; } = 650;
        [JsonIgnore]
        public double MinWidth { get; set; } = 630;
        [JsonIgnore]
        public double MinHeight { get; set; } = 530;
        
        // 当前窗口尺寸
        public double Width { get; set; } = 970;
        public double Height { get; set; } = 650;
    }

    public class FloatingWindowConfig
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
} 