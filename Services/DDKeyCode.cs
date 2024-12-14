using System.ComponentModel;

// 提供DDKeyCode枚举
namespace WpfApp.Services
{
    public enum DDKeyCode
    {
        // 功能键区
        [Description("ESC")] ESC = 100,
        [Description("F1")] F1 = 101,
        [Description("F2")] F2 = 102,
        [Description("F3")] F3 = 103,
        [Description("F4")] F4 = 104,
        [Description("F5")] F5 = 105,
        [Description("F6")] F6 = 106,
        [Description("F7")] F7 = 107,
        [Description("F8")] F8 = 108,
        [Description("F9")] F9 = 109,
        [Description("F10")] F10 = 110,
        [Description("F11")] F11 = 111,
        [Description("F12")] F12 = 112,

        // 数字键区
        [Description("~")] TILDE = 200,
        [Description("1")] NUM_1 = 201,
        [Description("2")] NUM_2 = 202,
        [Description("3")] NUM_3 = 203,
        [Description("4")] NUM_4 = 204,
        [Description("5")] NUM_5 = 205,
        [Description("6")] NUM_6 = 206,
        [Description("7")] NUM_7 = 207,
        [Description("8")] NUM_8 = 208,
        [Description("9")] NUM_9 = 209,
        [Description("0")] NUM_0 = 210,
        [Description("-")] MINUS = 211,
        [Description("=")] EQUALS = 212,
        [Description("\\")] BACKSLASH = 213,
        [Description("Backspace")] BACKSPACE = 214,

        // 字母键区第一行
        [Description("Tab")] TAB = 300,
        [Description("Q")] Q = 301,
        [Description("W")] W = 302,
        [Description("E")] E = 303,
        [Description("R")] R = 304,
        [Description("T")] T = 305,
        [Description("Y")] Y = 306,
        [Description("U")] U = 307,
        [Description("I")] I = 308,
        [Description("O")] O = 309,
        [Description("P")] P = 310,
        [Description("[")] LEFT_BRACKET = 311,
        [Description("]")] RIGHT_BRACKET = 312,
        [Description("Enter")] ENTER = 313,

        // 字母键区第二行
        [Description("Caps Lock")] CAPS_LOCK = 400,
        [Description("A")] A = 401,
        [Description("S")] S = 402,
        [Description("D")] D = 403,
        [Description("F")] F = 404,
        [Description("G")] G = 405,
        [Description("H")] H = 406,
        [Description("J")] J = 407,
        [Description("K")] K = 408,
        [Description("L")] L = 409,
        [Description(";")] SEMICOLON = 410,
        [Description("'")] QUOTE = 411,

        // 字母键区第三行
        [Description("LShift")] LEFT_SHIFT = 500,
        [Description("Z")] Z = 501,
        [Description("X")] X = 502,
        [Description("C")] C = 503,
        [Description("V")] V = 504,
        [Description("B")] B = 505,
        [Description("N")] N = 506,
        [Description("M")] M = 507,
        [Description(",")] COMMA = 508,
        [Description(".")] PERIOD = 509,
        [Description("/")] SLASH = 510,
        [Description("RShift")] RIGHT_SHIFT = 511,

        // 控制键区
        [Description("LCtrl")] LEFT_CTRL = 600,
        [Description("LWin")] LEFT_WIN = 601,
        [Description("LAlt")] LEFT_ALT = 602,
        [Description("Space")] SPACE = 603,
        [Description("RAlt")] RIGHT_ALT = 604,
        [Description("Fn")] FN = 605,
        [Description("Menu")] MENU = 606,
        [Description("RCtrl")] RIGHT_CTRL = 607,

        // 编辑键区
        [Description("Print Screen")] PRINT_SCREEN = 700,
        [Description("Scroll Lock")] SCROLL_LOCK = 701,
        [Description("Pause")] PAUSE = 702,
        [Description("Insert")] INSERT = 703,
        [Description("Home")] HOME = 704,
        [Description("Page Up")] PAGE_UP = 705,
        [Description("Delete")] DELETE = 706,
        [Description("End")] END = 707,
        [Description("Page Down")] PAGE_DOWN = 708,
        [Description("↑")] ARROW_UP = 709,
        [Description("←")] ARROW_LEFT = 710,
        [Description("↓")] ARROW_DOWN = 711,
        [Description("→")] ARROW_RIGHT = 712,
        
        // 小键盘区
        [Description("小键盘0")] NUMPAD_0 = 800,
        [Description("小键盘1")] NUMPAD_1 = 801,
        [Description("小键盘2")] NUMPAD_2 = 802,
        [Description("小键盘3")] NUMPAD_3 = 803,
        [Description("小键盘4")] NUMPAD_4 = 804,
        [Description("小键盘5")] NUMPAD_5 = 805,
        [Description("小键盘6")] NUMPAD_6 = 806,
        [Description("小键盘7")] NUMPAD_7 = 807,
        [Description("小键盘8")] NUMPAD_8 = 808,
        [Description("小键盘9")] NUMPAD_9 = 809,
        [Description("Num Lock")] NUM_LOCK = 810,
        [Description("小键盘/")] NUMPAD_DIVIDE = 811,
        [Description("小键盘*")] NUMPAD_MULTIPLY = 812,
        [Description("小键盘-")] NUMPAD_MINUS = 813,
        [Description("小键盘+")] NUMPAD_PLUS = 814,
        [Description("小键盘Enter")] NUMPAD_ENTER = 815,
        [Description("小键盘.")] NUMPAD_DECIMAL = 816,

        // 鼠标按键区
        [Description("鼠标左键")] LBUTTON = 1,
        [Description("鼠标右键")] RBUTTON = 4,
        [Description("鼠标中键")] MBUTTON = 16,
        [Description("鼠标前侧键")] XBUTTON1 = 64,    // 4键侧键
        [Description("鼠标后侧键")] XBUTTON2 = 256,   // 5键侧键

    }
} 