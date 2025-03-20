using System.Reflection;
using Newtonsoft.Json;
using System.Windows.Input;
using WpfApp.Services.Core;
using WpfApp.Services.Models;

namespace WpfApp.Services.Config;

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
    public string Version { get; set; } = ""; // 版本号
    public string DownloadUrl { get; set; } = ""; // oss下载链接
    public string? GithubUrl { get; set; } // github下载链接
    public string? ReleaseDate { get; set; } // 发布日期
    public string? MinVersion { get; set; } // 最小版本号
    public bool ForceUpdate { get; set; } // 是否强制更新
}

public class KeyConfig
{
    public LyKeysCode? Code { get; set; }  // 使用可空类型，坐标类型不需要此属性 
    public bool IsSelected { get; set; }
    public int KeyInterval { get; set; }
    public KeyItemType Type { get; set; } = KeyItemType.Keyboard;
    public int? X { get; set; }
    public int? Y { get; set; }

    // 无参构造函数，用于JSON反序列化
    public KeyConfig()
    {
        IsSelected = true;
        KeyInterval = 5;
        Type = KeyItemType.Keyboard;
    }

    // 键盘按键构造函数
    public KeyConfig(LyKeysCode code, bool isSelected = true, int keyInterval = 5)
    {
        Code = code;
        IsSelected = isSelected;
        KeyInterval = keyInterval;
        Type = KeyItemType.Keyboard;
        X = null;
        Y = null;
    }
    
    // 坐标构造函数
    public KeyConfig(int x, int y, bool isSelected = true, int keyInterval = 5)
    {
        // 验证坐标不能同时为0（只针对坐标类型）
        if (x == 0 && y == 0)
        {
            throw new ArgumentException("坐标不能同时为(0,0)");
        }
        
        X = x;
        Y = y;
        IsSelected = isSelected;
        KeyInterval = keyInterval;
        Type = KeyItemType.Coordinates;
        Code = null; // 坐标类型不使用Code属性
    }
    
    /// <summary>
    /// 将KeyConfig转换为LyKeysCode，用于与LyKeysService兼容
    /// 注意：坐标类型会被忽略，返回null
    /// </summary>
    public LyKeysCode? ToLyKeysCode()
    {
        // 只有键盘类型且被选中的按键才会返回有效值
        if (Type == KeyItemType.Keyboard && IsSelected && Code.HasValue)
        {
            return Code.Value;
        }
        return null;
    }
    
    /// <summary>
    /// 从KeyConfig列表中提取有效的LyKeysCode列表
    /// </summary>
    public static List<LyKeysCode> ExtractValidKeyCodes(List<KeyConfig> keyConfigs)
    {
        if (keyConfigs == null) return new List<LyKeysCode>();
        
        return keyConfigs
            .Where(k => k.Type == KeyItemType.Keyboard && k.IsSelected && k.Code.HasValue)
            .Select(k => k.Code.Value)
            .ToList();
    }
}

public class AppConfig
{
    [JsonIgnore] public AppInfo AppInfo { get; set; } = new();
    public UIConfig UI { get; set; } = new();
    public DebugConfig Debug { get; set; } = new();

    // 按键配置相关属性
    public LyKeysCode? startKey { get; set; }
    public ModifierKeys startMods { get; set; }
    public LyKeysCode? stopKey { get; set; }
    public ModifierKeys stopMods { get; set; }
    public List<KeyConfig> keys { get; set; } = new();

    public int keyMode { get; set; }

    // 默认按键间隔，仅用于新添加按键时的默认值
    public int interval { get; set; } = 10;
    
    // 是否启用声音
    public bool? soundEnabled { get; set; }
    
    // 是否开启降低卡位模式
    public bool? IsReduceKeyStuck { get; set; }
    
    // 音量设置，范围0.0-1.0，默认0.8(80%)
    public double? SoundVolume { get; set; } = 0.8;
    
    // 按键按下时长，单位毫秒
    public int? KeyPressInterval { get; set; }
    
    // 是否自动切换到英文输入法
    public bool? AutoSwitchToEnglishIME { get; set; } = true;

    // 热键总开关状态
    public bool? isHotkeyControlEnabled { get; set; } = true;

    // 窗口句柄相关信息
    public string? TargetWindowClassName { get; set; }
    public string? TargetWindowProcessName { get; set; }
    public string? TargetWindowTitle { get; set; }

    [JsonIgnore] public string Author { get; set; } = "慕长秋";

    public AppConfig()
    {
        Debug = new DebugConfig();
        UI = new UIConfig();
        keys = new List<KeyConfig>();
    }
}

public class AppInfo
{
    private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();

    [JsonIgnore] public string Title { get; set; } = "灵曜按键 (LingYao Keys)";

    [JsonIgnore] public string Version => Assembly.GetName().Version?.ToString() ?? "1.0.0";
    
    [JsonIgnore] public string GitHubUrl { get; } = "https://github.com/Cassianvale/LingYaoKeys";
}

public class UIConfig
{
    public WindowConfig MainWindow { get; set; } = new();
    public FloatingWindowConfig FloatingWindow { get; set; } = new();
}

public class WindowConfig
{
    [JsonIgnore] public double MinWidth { get; set; } = 800;
    [JsonIgnore] public double MinHeight { get; set; } = 660;

    // 当前窗口尺寸
    public double Width { get; set; } = 800;
    public double Height { get; set; } = 660;
}

public class FloatingWindowConfig
{
    public double Left { get; set; }
    public double Top { get; set; }
    public bool IsEnabled { get; set; } = true;
}