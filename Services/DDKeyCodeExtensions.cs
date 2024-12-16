using System.Collections.Generic;

namespace WpfApp.Services
{
    public static class DDKeyCodeExtensions
    {
        // 显示名称映射表
        private static readonly Dictionary<DDKeyCode, string> _displayNames = new()
        {
            // 数字键
            { DDKeyCode.NUM_0, "0" },
            { DDKeyCode.NUM_1, "1" },
            { DDKeyCode.NUM_2, "2" },
            { DDKeyCode.NUM_3, "3" },
            { DDKeyCode.NUM_4, "4" },
            { DDKeyCode.NUM_5, "5" },
            { DDKeyCode.NUM_6, "6" },
            { DDKeyCode.NUM_7, "7" },
            { DDKeyCode.NUM_8, "8" },
            { DDKeyCode.NUM_9, "9" },
            
            // 功能键
            { DDKeyCode.ESC, "Esc" },
            { DDKeyCode.F1, "F1" },
            { DDKeyCode.F2, "F2" },
            { DDKeyCode.F3, "F3" },
            { DDKeyCode.F4, "F4" },
            { DDKeyCode.F5, "F5" },
            { DDKeyCode.F6, "F6" },
            { DDKeyCode.F7, "F7" },
            { DDKeyCode.F8, "F8" },
            { DDKeyCode.F9, "F9" },
            { DDKeyCode.F10, "F10" },
            { DDKeyCode.F11, "F11" },
            { DDKeyCode.F12, "F12" },
            
            // 特殊键
            { DDKeyCode.BACKSPACE, "Backspace" },
            { DDKeyCode.TAB, "Tab" },
            { DDKeyCode.ENTER, "Enter" },
            { DDKeyCode.SPACE, "Space" },
            { DDKeyCode.CAPS_LOCK, "Caps Lock" },
            { DDKeyCode.PRINT_SCREEN, "Print Screen" },
            { DDKeyCode.SCROLL_LOCK, "Scroll Lock" },
            { DDKeyCode.PAUSE, "Pause" },
            { DDKeyCode.INSERT, "Insert" },
            { DDKeyCode.DELETE, "Delete" },
            { DDKeyCode.HOME, "Home" },
            { DDKeyCode.END, "End" },
            { DDKeyCode.PAGE_UP, "Page Up" },
            { DDKeyCode.PAGE_DOWN, "Page Down" },
            
            // 控制键
            { DDKeyCode.LEFT_CTRL, "Left Ctrl" },
            { DDKeyCode.RIGHT_CTRL, "Right Ctrl" },
            { DDKeyCode.LEFT_ALT, "Left Alt" },
            { DDKeyCode.RIGHT_ALT, "Right Alt" },
            { DDKeyCode.LEFT_SHIFT, "Left Shift" },
            { DDKeyCode.RIGHT_SHIFT, "Right Shift" },
            { DDKeyCode.LEFT_WIN, "Left Win" },
            { DDKeyCode.MENU, "Menu" },
            
            // 方向键
            { DDKeyCode.ARROW_UP, "↑" },
            { DDKeyCode.ARROW_DOWN, "↓" },
            { DDKeyCode.ARROW_LEFT, "←" },
            { DDKeyCode.ARROW_RIGHT, "→" },
            
            // 符号键
            { DDKeyCode.MINUS, "-" },
            { DDKeyCode.EQUALS, "=" },
            { DDKeyCode.LEFT_BRACKET, "[" },
            { DDKeyCode.RIGHT_BRACKET, "]" },
            { DDKeyCode.SEMICOLON, ";" },
            { DDKeyCode.QUOTE, "'" },
            { DDKeyCode.COMMA, "," },
            { DDKeyCode.PERIOD, "." },
            { DDKeyCode.SLASH, "/" },
            { DDKeyCode.BACKSLASH, "\\" },
            { DDKeyCode.TILDE, "~" },
            
            // 小键盘
            { DDKeyCode.NUMPAD_0, "Num 0" },
            { DDKeyCode.NUMPAD_1, "Num 1" },
            { DDKeyCode.NUMPAD_2, "Num 2" },
            { DDKeyCode.NUMPAD_3, "Num 3" },
            { DDKeyCode.NUMPAD_4, "Num 4" },
            { DDKeyCode.NUMPAD_5, "Num 5" },
            { DDKeyCode.NUMPAD_6, "Num 6" },
            { DDKeyCode.NUMPAD_7, "Num 7" },
            { DDKeyCode.NUMPAD_8, "Num 8" },
            { DDKeyCode.NUMPAD_9, "Num 9" },
            { DDKeyCode.NUM_LOCK, "Num Lock" },
            { DDKeyCode.NUMPAD_DIVIDE, "Num /" },
            { DDKeyCode.NUMPAD_MULTIPLY, "Num *" },
            { DDKeyCode.NUMPAD_MINUS, "Num -" },
            { DDKeyCode.NUMPAD_PLUS, "Num +" },
            { DDKeyCode.NUMPAD_ENTER, "Num Enter" },
            { DDKeyCode.NUMPAD_DECIMAL, "Num ." },
            
            // 鼠标按键
            { DDKeyCode.LBUTTON, "Left Click" },
            { DDKeyCode.RBUTTON, "Right Click" },
            { DDKeyCode.MBUTTON, "Middle Click" },
            { DDKeyCode.XBUTTON1, "Mouse X1" },
            { DDKeyCode.XBUTTON2, "Mouse X2" }
        };

        // 获取显示名称
        public static string ToDisplayName(this DDKeyCode keyCode)
        {
            // 检查是否在字典中
            if (_displayNames.TryGetValue(keyCode, out string? displayName))
            {
                return displayName;
            }

            // 处理字母键
            if (keyCode >= DDKeyCode.A && keyCode <= DDKeyCode.Z)
            {
                return keyCode.ToString();
            }

            // 默认返回枚举名称
            return keyCode.ToString();
        }

        // 检查是否是修饰键
        public static bool IsModifierKey(this DDKeyCode keyCode)
        {
            return keyCode switch
            {
                DDKeyCode.LEFT_CTRL => true,
                DDKeyCode.RIGHT_CTRL => true,
                DDKeyCode.LEFT_ALT => true,
                DDKeyCode.RIGHT_ALT => true,
                DDKeyCode.LEFT_SHIFT => true,
                DDKeyCode.RIGHT_SHIFT => true,
                DDKeyCode.LEFT_WIN => true,
                _ => false
            };
        }

        // 检查是否是功能键
        public static bool IsFunctionKey(this DDKeyCode keyCode)
        {
            return keyCode >= DDKeyCode.F1 && keyCode <= DDKeyCode.F12;
        }

        // 检查是否是数字键
        public static bool IsNumberKey(this DDKeyCode keyCode)
        {
            return keyCode >= DDKeyCode.NUM_0 && keyCode <= DDKeyCode.NUM_9;
        }

        // 检查是否是字母键
        public static bool IsLetterKey(this DDKeyCode keyCode)
        {
            return keyCode >= DDKeyCode.A && keyCode <= DDKeyCode.Z;
        }

        // 检查是否是小键盘键
        public static bool IsNumPadKey(this DDKeyCode keyCode)
        {
            return keyCode >= DDKeyCode.NUMPAD_0 && keyCode <= DDKeyCode.NUMPAD_DECIMAL;
        }

        // 检查是否是鼠标按键
        public static bool IsMouseButton(this DDKeyCode keyCode)
        {
            return keyCode switch
            {
                DDKeyCode.LBUTTON => true,
                DDKeyCode.RBUTTON => true,
                DDKeyCode.MBUTTON => true,
                DDKeyCode.XBUTTON1 => true,
                DDKeyCode.XBUTTON2 => true,
                _ => false
            };
        }

        // 获取键码类别描述
        public static string GetKeyCategory(this DDKeyCode keyCode)
        {
            if (IsModifierKey(keyCode)) return "修饰键";
            if (IsFunctionKey(keyCode)) return "功能键";
            if (IsNumberKey(keyCode)) return "数字键";
            if (IsLetterKey(keyCode)) return "字母键";
            if (IsNumPadKey(keyCode)) return "小键盘";
            if (IsMouseButton(keyCode)) return "鼠标按键";
            return "其他键";
        }
    }
} 