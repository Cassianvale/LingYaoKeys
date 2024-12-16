using System.Collections.Generic;

namespace WpfApp.Services
{
    public static class KeyCodeMapping
    {
        // 使用惰性初始化避免静态构造函数异常
        private static readonly Lazy<Dictionary<int, DDKeyCode>> _virtualToDDKeyMap = 
            new Lazy<Dictionary<int, DDKeyCode>>(() => InitializeKeyMap());

        public static Dictionary<int, DDKeyCode> VirtualToDDKeyMap => _virtualToDDKeyMap.Value;

        private static Dictionary<int, DDKeyCode> InitializeKeyMap()
        {
            try
            {
                var map = new Dictionary<int, DDKeyCode>
                {
                    // 数字键 (主键盘)
                    { 0x31, DDKeyCode.NUM_1 }, // 1
                    { 0x32, DDKeyCode.NUM_2 }, // 2
                    { 0x33, DDKeyCode.NUM_3 }, // 3
                    { 0x34, DDKeyCode.NUM_4 }, // 4
                    { 0x35, DDKeyCode.NUM_5 }, // 5
                    { 0x36, DDKeyCode.NUM_6 }, // 6
                    { 0x37, DDKeyCode.NUM_7 }, // 7
                    { 0x38, DDKeyCode.NUM_8 }, // 8
                    { 0x39, DDKeyCode.NUM_9 }, // 9
                    { 0x30, DDKeyCode.NUM_0 }, // 0

                    // 数字键盘
                    { 0x60, DDKeyCode.NUMPAD_0 }, // 小键盘 0
                    { 0x61, DDKeyCode.NUMPAD_1 }, // 小键盘 1
                    { 0x62, DDKeyCode.NUMPAD_2 }, // 小键盘 2
                    { 0x63, DDKeyCode.NUMPAD_3 }, // 小键盘 3
                    { 0x64, DDKeyCode.NUMPAD_4 }, // 小键盘 4
                    { 0x65, DDKeyCode.NUMPAD_5 }, // 小键盘 5
                    { 0x66, DDKeyCode.NUMPAD_6 }, // 小键盘 6
                    { 0x67, DDKeyCode.NUMPAD_7 }, // 小键盘 7
                    { 0x68, DDKeyCode.NUMPAD_8 }, // 小键盘 8
                    { 0x69, DDKeyCode.NUMPAD_9 }, // 小键盘 9

                    // 功能键
                    { 0x70, DDKeyCode.F1 },  // F1
                    { 0x71, DDKeyCode.F2 },  // F2
                    { 0x72, DDKeyCode.F3 },  // F3
                    { 0x73, DDKeyCode.F4 },  // F4
                    { 0x74, DDKeyCode.F5 },  // F5
                    { 0x75, DDKeyCode.F6 },  // F6
                    { 0x76, DDKeyCode.F7 },  // F7
                    { 0x77, DDKeyCode.F8 },  // F8
                    { 0x78, DDKeyCode.F9 },  // F9
                    { 0x79, DDKeyCode.F10 }, // F10
                    { 0x7A, DDKeyCode.F11 }, // F11
                    { 0x7B, DDKeyCode.F12 }, // F12

                    // 其他常用键
                    { 0x0D, DDKeyCode.ENTER },     // Enter
                    { 0x1B, DDKeyCode.ESC },       // Esc
                    { 0x08, DDKeyCode.BACKSPACE },      // Backspace
                    { 0x09, DDKeyCode.TAB },       // Tab
                    { 0x20, DDKeyCode.SPACE },     // Space
                    { 0x2E, DDKeyCode.DELETE },    // Delete
                    { 0x24, DDKeyCode.HOME },      // Home
                    { 0x23, DDKeyCode.END },       // End
                    { 0x21, DDKeyCode.PAGE_UP },     // Page Up
                    { 0x22, DDKeyCode.PAGE_DOWN },      // Page Down

                    // 修饰键
                    { 0xA0, DDKeyCode.LEFT_SHIFT },   // Left Shift
                    { 0xA1, DDKeyCode.RIGHT_SHIFT },  // Right Shift
                    { 0xA2, DDKeyCode.LEFT_CTRL },    // Left Control
                    { 0xA3, DDKeyCode.RIGHT_CTRL },   // Right Control
                    { 0xA4, DDKeyCode.LEFT_ALT },     // Left Alt
                    { 0xA5, DDKeyCode.RIGHT_ALT },    // Right Alt
                    { 0x5B, DDKeyCode.LEFT_WIN },     // Left Windows
                };

                System.Diagnostics.Debug.WriteLine("键码映射表初始化完成");
                return map;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化键码映射表时发生异常: {ex}");
                throw;
            }
        }

        public static DDKeyCode GetDDKeyCode(int virtualKeyCode, DDDriverService ddDriver)
        {
            try
            {
                // 1. 先尝试从映射表中获取
                if (VirtualToDDKeyMap.TryGetValue(virtualKeyCode, out DDKeyCode ddKeyCode))
                {
                    return ddKeyCode;
                }

                // 2. 如果是数字键盘，使用特殊处理
                if (virtualKeyCode >= 0x60 && virtualKeyCode <= 0x69)
                {
                    int numValue = virtualKeyCode - 0x60;
                    return (DDKeyCode)(200 + numValue); // NUM_0 到 NUM_9 的枚举值从200开始
                }

                // 3. 尝试使用驱动的转换功能
                var ddCode = ddDriver.ConvertVirtualKeyToDDCode(virtualKeyCode);
                if (ddCode.HasValue && ddCode.Value > 0)
                {
                    return (DDKeyCode)ddCode.Value;
                }

                System.Diagnostics.Debug.WriteLine($"无法转换虚拟键码: 0x{virtualKeyCode:X2}");
                return DDKeyCode.None;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"转换虚拟键码时发生异常: {ex}");
                return DDKeyCode.None;
            }
        }
    }
} 