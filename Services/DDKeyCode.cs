using System.ComponentModel;

// 提供DDKeyCode枚举
namespace WpfApp.Services
{
    public enum DDKeyCode
    {
        None = 0,   // 添加None值作为默认值

        // 功能键区
        ESC = 100,
        F1 = 101,
        F2 = 102,
        F3 = 103,
        F4 = 104,
        F5 = 105,
        F6 = 106,
        F7 = 107,
        F8 = 108,
        F9 = 109,
        F10 = 110,
        F11 = 111,
        F12 = 112,

        // 数字键区
        TILDE = 200,
        NUM_1 = 201,
        NUM_2 = 202,
        NUM_3 = 203,
        NUM_4 = 204,
        NUM_5 = 205,
        NUM_6 = 206,
        NUM_7 = 207,
        NUM_8 = 208,
        NUM_9 = 209,
        NUM_0 = 210,
        MINUS = 211,
        EQUALS = 212,
        BACKSLASH = 213,
        BACKSPACE = 214,

        // 字母键区
        TAB = 300,
        Q = 301,
        W = 302,
        E = 303,
        R = 304,
        T = 305,
        Y = 306,
        U = 307,
        I = 308,
        O = 309,
        P = 310,
        LEFT_BRACKET = 311,
        RIGHT_BRACKET = 312,
        ENTER = 313,

        CAPS_LOCK = 400,
        A = 401,
        S = 402,
        D = 403,
        F = 404,
        G = 405,
        H = 406,
        J = 407,
        K = 408,
        L = 409,
        SEMICOLON = 410,
        QUOTE = 411,

        LEFT_SHIFT = 500,
        Z = 501,
        X = 502,
        C = 503,
        V = 504,
        B = 505,
        N = 506,
        M = 507,
        COMMA = 508,
        PERIOD = 509,
        SLASH = 510,
        RIGHT_SHIFT = 511,

        // 控制键区
        LEFT_CTRL = 600,
        LEFT_WIN = 601,
        LEFT_ALT = 602,
        SPACE = 603,
        RIGHT_ALT = 604,
        FN = 605,
        MENU = 606,
        RIGHT_CTRL = 607,

        // 编辑键区
        PRINT_SCREEN = 700,
        SCROLL_LOCK = 701,
        PAUSE = 702,
        INSERT = 703,
        HOME = 704,
        PAGE_UP = 705,
        DELETE = 706,
        END = 707,
        PAGE_DOWN = 708,
        ARROW_UP = 709,
        ARROW_LEFT = 710,
        ARROW_DOWN = 711,
        ARROW_RIGHT = 712,

        // 小键盘区
        NUMPAD_0 = 800,
        NUMPAD_1 = 801,
        NUMPAD_2 = 802,
        NUMPAD_3 = 803,
        NUMPAD_4 = 804,
        NUMPAD_5 = 805,
        NUMPAD_6 = 806,
        NUMPAD_7 = 807,
        NUMPAD_8 = 808,
        NUMPAD_9 = 809,
        NUM_LOCK = 810,
        NUMPAD_DIVIDE = 811,
        NUMPAD_MULTIPLY = 812,
        NUMPAD_MINUS = 813,
        NUMPAD_PLUS = 814,
        NUMPAD_ENTER = 815,
        NUMPAD_DECIMAL = 816,

        // 鼠标按键区
        LBUTTON = 1,
        RBUTTON = 4,
        MBUTTON = 16,
        XBUTTON1 = 64,
        XBUTTON2 = 256
    }
} 