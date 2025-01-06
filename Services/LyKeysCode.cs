using System.ComponentModel;

namespace WpfApp.Services
{
    public enum LyKeysCode
    {
        // 鼠标按键
        [Description("Left")]
        VK_LBUTTON = 0x01,
        [Description("Right")]
        VK_RBUTTON = 0x02,
        [Description("Cancel")]
        VK_CANCEL = 0x03,
        [Description("Middle")]
        VK_MBUTTON = 0x04,
        [Description("XButton1")]
        VK_XBUTTON1 = 0x05,
        [Description("XButton2")]
        VK_XBUTTON2 = 0x06,

        // 控制键
        [Description("Backspace")]
        VK_BACK = 0x08,
        [Description("Tab")]
        VK_TAB = 0x09,
        [Description("Clear")]
        VK_CLEAR = 0x0C,
        [Description("Enter")]
        VK_RETURN = 0x0D,
        [Description("Shift")]
        VK_SHIFT = 0x10,
        [Description("Ctrl")]
        VK_CONTROL = 0x11,
        [Description("Alt")]
        VK_MENU = 0x12,
        [Description("Pause")]
        VK_PAUSE = 0x13,
        [Description("CapsLock")]
        VK_CAPITAL = 0x14,
        [Description("Esc")]
        VK_ESCAPE = 0x1B,
        [Description("Space")]
        VK_SPACE = 0x20,

        // 导航键
        [Description("PageUp")]
        VK_PRIOR = 0x21,
        [Description("PageDown")]
        VK_NEXT = 0x22,
        [Description("End")]
        VK_END = 0x23,
        [Description("Home")]
        VK_HOME = 0x24,
        [Description("Left")]
        VK_LEFT = 0x25,
        [Description("Up")]
        VK_UP = 0x26,
        [Description("Right")]
        VK_RIGHT = 0x27,
        [Description("Down")]
        VK_DOWN = 0x28,
        [Description("Select")]
        VK_SELECT = 0x29,
        [Description("Print")]
        VK_PRINT = 0x2A,
        [Description("Execute")]
        VK_EXECUTE = 0x2B,
        [Description("Snapshot")]
        VK_SNAPSHOT = 0x2C,
        [Description("Insert")]
        VK_INSERT = 0x2D,
        [Description("Delete")]
        VK_DELETE = 0x2E,
        [Description("Help")]
        VK_HELP = 0x2F,

        // 数字键
        [Description("0")]
        VK_0 = 0x30,
        [Description("1")]
        VK_1 = 0x31,
        [Description("2")]
        VK_2 = 0x32,
        [Description("3")]
        VK_3 = 0x33,
        [Description("4")]
        VK_4 = 0x34,
        [Description("5")]
        VK_5 = 0x35,
        [Description("6")]
        VK_6 = 0x36,
        [Description("7")]
        VK_7 = 0x37,
        [Description("8")]
        VK_8 = 0x38,
        [Description("9")]
        VK_9 = 0x39,

        // 字母键
        [Description("A")]
        VK_A = 0x41,
        [Description("B")]
        VK_B = 0x42,
        [Description("C")]
        VK_C = 0x43,
        [Description("D")]
        VK_D = 0x44,
        [Description("E")]
        VK_E = 0x45,
        [Description("F")]
        VK_F = 0x46,
        [Description("G")]
        VK_G = 0x47,
        [Description("H")]
        VK_H = 0x48,
        [Description("I")]
        VK_I = 0x49,
        [Description("J")]
        VK_J = 0x4A,
        [Description("K")]
        VK_K = 0x4B,
        [Description("L")]
        VK_L = 0x4C,
        [Description("M")]
        VK_M = 0x4D,
        [Description("N")]
        VK_N = 0x4E,
        [Description("O")]
        VK_O = 0x4F,
        [Description("P")]
        VK_P = 0x50,
        [Description("Q")]
        VK_Q = 0x51,
        [Description("R")]
        VK_R = 0x52,
        [Description("S")]
        VK_S = 0x53,
        [Description("T")]
        VK_T = 0x54,
        [Description("U")]
        VK_U = 0x55,
        [Description("V")]
        VK_V = 0x56,
        [Description("W")]
        VK_W = 0x57,
        [Description("X")]
        VK_X = 0x58,
        [Description("Y")]
        VK_Y = 0x59,
        [Description("Z")]
        VK_Z = 0x5A,

        // 功能键
        [Description("F1")]
        VK_F1 = 0x70,
        [Description("F2")]
        VK_F2 = 0x71,
        [Description("F3")]
        VK_F3 = 0x72,
        [Description("F4")]
        VK_F4 = 0x73,
        [Description("F5")]
        VK_F5 = 0x74,
        [Description("F6")]
        VK_F6 = 0x75,
        [Description("F7")]
        VK_F7 = 0x76,
        [Description("F8")]
        VK_F8 = 0x77,
        [Description("F9")]
        VK_F9 = 0x78,
        [Description("F10")]
        VK_F10 = 0x79,
        [Description("F11")]
        VK_F11 = 0x7A,
        [Description("F12")]
        VK_F12 = 0x7B,

        // 小键盘
        [Description("小键盘0")]
        VK_NUMPAD0 = 0x60,
        [Description("小键盘1")]
        VK_NUMPAD1 = 0x61,
        [Description("小键盘2")]
        VK_NUMPAD2 = 0x62,
        [Description("小键盘3")]
        VK_NUMPAD3 = 0x63,
        [Description("小键盘4")]
        VK_NUMPAD4 = 0x64,
        [Description("小键盘5")]
        VK_NUMPAD5 = 0x65,
        [Description("小键盘6")]
        VK_NUMPAD6 = 0x66,
        [Description("小键盘7")]
        VK_NUMPAD7 = 0x67,
        [Description("小键盘8")]
        VK_NUMPAD8 = 0x68,
        [Description("小键盘9")]
        VK_NUMPAD9 = 0x69,
        [Description("小键盘*")]
        VK_MULTIPLY = 0x6A,
        [Description("小键盘+")]
        VK_ADD = 0x6B,
        [Description("小键盘,")]
        VK_SEPARATOR = 0x6C,
        [Description("小键盘-")]
        VK_SUBTRACT = 0x6D,
        [Description("小键盘.")]
        VK_DECIMAL = 0x6E,
        [Description("小键盘/")]
        VK_DIVIDE = 0x6F,

        // 左右修饰键
        [Description("左Shift")]
        VK_LSHIFT = 0xA0,
        [Description("右Shift")]
        VK_RSHIFT = 0xA1,
        [Description("左Ctrl")]
        VK_LCONTROL = 0xA2,
        [Description("右Ctrl")]
        VK_RCONTROL = 0xA3,
        [Description("左Alt")]
        VK_LMENU = 0xA4,
        [Description("右Alt")]
        VK_RMENU = 0xA5,

        // Windows和应用程序键
        [Description("左Win")]
        VK_LWIN = 0x5B,
        [Description("右Win")]
        VK_RWIN = 0x5C,
        [Description("Apps")]
        VK_APPS = 0x5D,

        // 符号键
        [Description("+")]
        VK_OEM_PLUS = 0xBB,
        [Description(",")]
        VK_OEM_COMMA = 0xBC,
        [Description("-")]
        VK_OEM_MINUS = 0xBD,
        [Description(".")]
        VK_OEM_PERIOD = 0xBE,
        [Description("/")]
        VK_OEM_2 = 0xBF,
        [Description("`")]
        VK_OEM_3 = 0xC0,
        [Description("[")]
        VK_OEM_4 = 0xDB,
        [Description("\\")]
        VK_OEM_5 = 0xDC,
        [Description("]")]
        VK_OEM_6 = 0xDD,
        [Description("'")]
        VK_OEM_7 = 0xDE,
        [Description(";")]
        VK_OEM_1 = 0xBA,

        // 浏览器控制键
        [Description("浏览器后退")]
        VK_BROWSER_BACK = 0xA6,
        [Description("浏览器前进")]
        VK_BROWSER_FORWARD = 0xA7,
        [Description("浏览器刷新")]
        VK_BROWSER_REFRESH = 0xA8,
        [Description("浏览器停止")]
        VK_BROWSER_STOP = 0xA9,
        [Description("浏览器搜索")]
        VK_BROWSER_SEARCH = 0xAA,
        [Description("浏览器收藏")]
        VK_BROWSER_FAVORITES = 0xAB,
        [Description("浏览器主页")]
        VK_BROWSER_HOME = 0xAC,

        // 音量控制键
        [Description("静音")]
        VK_VOLUME_MUTE = 0xAD,
        [Description("音量-")]
        VK_VOLUME_DOWN = 0xAE,
        [Description("音量+")]
        VK_VOLUME_UP = 0xAF,

        // 媒体控制键
        [Description("下一曲")]
        VK_MEDIA_NEXT_TRACK = 0xB0,
        [Description("上一曲")]
        VK_MEDIA_PREV_TRACK = 0xB1,
        [Description("停止")]
        VK_MEDIA_STOP = 0xB2,
        [Description("播放/暂停")]
        VK_MEDIA_PLAY_PAUSE = 0xB3,

        // 锁定键
        [Description("NumLock")]
        VK_NUMLOCK = 0x90,
        [Description("ScrollLock")]
        VK_SCROLL = 0x91,

        // 特殊功能键
        [Description("Sleep")]
        VK_SLEEP = 0x5F,
        [Description("Process")]
        VK_PROCESSKEY = 0xE5
    }
} 