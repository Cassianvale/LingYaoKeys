using System.Collections.Generic;

namespace WpfApp.Services
{
    public static class KeyCodeMapping
    {
        // 修改映射表的值类型为DDKeyCode
        private static readonly Dictionary<int, DDKeyCode> _virtualToDDKeyMap = new()
        {
            // 字母键 (VK 0x41-0x5A)
            {0x41, DDKeyCode.A},
            {0x42, DDKeyCode.B},
            {0x43, DDKeyCode.C},
            {0x44, DDKeyCode.D},
            {0x45, DDKeyCode.E},
            {0x46, DDKeyCode.F},
            {0x47, DDKeyCode.G},
            {0x48, DDKeyCode.H},
            {0x49, DDKeyCode.I},
            {0x4A, DDKeyCode.J},
            {0x4B, DDKeyCode.K},
            {0x4C, DDKeyCode.L},
            {0x4D, DDKeyCode.M},
            {0x4E, DDKeyCode.N},
            {0x4F, DDKeyCode.O},
            {0x50, DDKeyCode.P},
            {0x51, DDKeyCode.Q},
            {0x52, DDKeyCode.R},
            {0x53, DDKeyCode.S},
            {0x54, DDKeyCode.T},
            {0x55, DDKeyCode.U},
            {0x56, DDKeyCode.V},
            {0x57, DDKeyCode.W},
            {0x58, DDKeyCode.X},
            {0x59, DDKeyCode.Y},
            {0x5A, DDKeyCode.Z},
        };

        public static IReadOnlyDictionary<int, DDKeyCode> VirtualToDDKeyMap => _virtualToDDKeyMap;

        // 修改返回类型
        public static DDKeyCode GetDDKeyCode(int virtualKeyCode, DDDriverService ddService)
        {
            if (VirtualToDDKeyMap.TryGetValue(virtualKeyCode, out DDKeyCode ddCode))
            {
                return ddCode;
            }

            // 如果在映射表中找不到，尝试使用驱动转换
            var result = ddService.ConvertVirtualKeyToDDCode(virtualKeyCode);
            if (result.HasValue)
            {
                // 缓存结果
                CachedResults[virtualKeyCode] = (DDKeyCode)result.Value;
                return (DDKeyCode)result.Value;
            }

            return DDKeyCode.None;
        }

        // 缓存查询结果
        private static readonly Dictionary<int, DDKeyCode> CachedResults = new();

        // 清理缓存
        public static void ClearCache()
        {
            CachedResults.Clear();
        }

        // 获取虚拟键码的描述
        public static string GetVirtualKeyDescription(int virtualKeyCode)
        {
            if (VirtualToDDKeyMap.TryGetValue(virtualKeyCode, out DDKeyCode ddCode))
            {
                return ddCode.ToDisplayName();
            }
            return $"VK 0x{virtualKeyCode:X2}";
        }

        // 检查是否是修饰键
        public static bool IsModifierKey(int virtualKeyCode)
        {
            return virtualKeyCode switch
            {
                0x10 => true, // VK_SHIFT
                0x11 => true, // VK_CONTROL
                0x12 => true, // VK_MENU (Alt)
                0x5B => true, // VK_LWIN
                _ => false
            };
        }

        static KeyCodeMapping()
        {
            // 添加数字键盘的映射
            _virtualToDDKeyMap.Add(0x60, DDKeyCode.NUM_0);
            _virtualToDDKeyMap.Add(0x61, DDKeyCode.NUM_1);
            _virtualToDDKeyMap.Add(0x62, DDKeyCode.NUM_2);
            _virtualToDDKeyMap.Add(0x63, DDKeyCode.NUM_3);
            _virtualToDDKeyMap.Add(0x64, DDKeyCode.NUM_4);
            _virtualToDDKeyMap.Add(0x65, DDKeyCode.NUM_5);
            _virtualToDDKeyMap.Add(0x66, DDKeyCode.NUM_6);
            _virtualToDDKeyMap.Add(0x67, DDKeyCode.NUM_7);
            _virtualToDDKeyMap.Add(0x68, DDKeyCode.NUM_8);
            _virtualToDDKeyMap.Add(0x69, DDKeyCode.NUM_9);
        }
    }
} 