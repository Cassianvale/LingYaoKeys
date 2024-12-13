public class KeyMappingService
{
    private readonly Dictionary<string, string> _keyDisplayNames = new()
    {
        // 鼠标按键
        { "LBUTTON", "鼠标左键" },
        { "RBUTTON", "鼠标右键" },
        { "MBUTTON", "鼠标中键" },
        { "MWHEELU", "滚轮上" },
        { "MWHEELD", "滚轮下" },
        { "XBUTTON1", "侧键1" },
        { "XBUTTON2", "侧键2" },
        
        // 小键盘
        { "DIVIDE", "小键盘/" },
        { "MULTIPLY", "小键盘*" },
        { "SUBTRACT", "小键盘-" },
        { "ADD", "小键盘+" },
        { "DECIMAL", "小键盘." },
        
        // OEM 键
        { "OEM_1", ";" },
        { "OEM_2", "?" },
        { "OEM_3", "~" },
        { "OEM_4", "[" },
        { "OEM_5", "\\" },
        { "OEM_6", "]" },
        { "OEM_COMMA", "," },
        { "OEM_PERIOD", "." },
        { "OEM_PLUS", "=" },
        { "OEM_MINUS", "-" },
        
        // 功能键
        { "F1", "F1" },
        { "F2", "F2" },
        { "F3", "F3" },
        { "F4", "F4" },
        { "F5", "F5" },
        { "F6", "F6" },
        { "F7", "F7" },
        { "F8", "F8" },
        { "F9", "F9" },
        { "F10", "F10" },
        { "F11", "F11" },
        { "F12", "F12" },
        
        // 特殊符号键
        { "OEM_TILDE", "`" },
        { "OEM_PLUS", "=" },
        { "OEM_MINUS", "-" },
        
        // 修饰键
        { "SHIFT", "Shift" },
        { "CONTROL", "Ctrl" },
        { "ALT", "Alt" }
    };

    public KeyMappingService()
    {
        // 添加数字键映射
        for (int i = 0; i <= 9; i++)
        {
            _keyDisplayNames.Add($"NUM{i}", i.ToString());
            _keyDisplayNames.Add($"NUMPAD{i}", $"小键盘{i}");
        }
    }

    public string GetDisplayName(string keyCode)
    {
        return _keyDisplayNames.TryGetValue(keyCode, out var displayName) 
            ? displayName 
            : keyCode;
    }
}