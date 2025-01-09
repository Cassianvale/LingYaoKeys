using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp.Services.Models
{
    public class KeyConfig : INotifyPropertyChanged
    {
        private bool _isRapidFire;
        private int _rapidFireDelay = 100;
        private bool _isHighlighted;
        private bool _isDisabled;

        public LyKeysCode KeyCode { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 40;
        public double Height { get; set; } = 40;

        public bool IsRapidFire
        {
            get => _isRapidFire;
            set
            {
                if (_isRapidFire != value)
                {
                    _isRapidFire = value;
                    OnPropertyChanged();
                }
            }
        }

        public int RapidFireDelay
        {
            get => _rapidFireDelay;
            set
            {
                if (_rapidFireDelay != value)
                {
                    _rapidFireDelay = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                if (_isHighlighted != value)
                {
                    _isHighlighted = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsDisabled
        {
            get => _isDisabled;
            set
            {
                if (_isDisabled != value)
                {
                    _isDisabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class KeyboardLayoutConfig
    {
        public List<KeyConfig> StandardKeys { get; set; } = new List<KeyConfig>();
        public List<KeyConfig> FunctionKeys { get; set; } = new List<KeyConfig>();
        public List<KeyConfig> NumpadKeys { get; set; } = new List<KeyConfig>();
        public List<KeyConfig> NavigationKeys { get; set; } = new List<KeyConfig>();
        public List<KeyConfig> MouseButtons { get; set; } = new List<KeyConfig>();

        private const double DEFAULT_KEY_WIDTH = 40;
        private const double DEFAULT_KEY_HEIGHT = 40;
        private const double KEY_SPACING = 2;

        public KeyboardLayoutConfig()
        {
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            InitializeStandardKeys();
            InitializeFunctionKeys();
            InitializeNumpadKeys();
            InitializeNavigationKeys();
            InitializeMouseButtons();
        }

        private void InitializeStandardKeys()
        {
            double currentX = 0;
            double currentY = 0;

            // 第一行 - 数字键行
            AddStandardKey(LyKeysCode.VK_OEM_3, "`", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_1, "1", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_2, "2", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_3, "3", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_4, "4", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_5, "5", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_6, "6", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_7, "7", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_8, "8", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_9, "9", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_0, "0", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_OEM_MINUS, "-", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_OEM_PLUS, "=", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_BACK, "Backspace", currentX, currentY, DEFAULT_KEY_WIDTH * 2, DEFAULT_KEY_HEIGHT);

            // 第二行
            currentX = DEFAULT_KEY_WIDTH * 1.5;
            currentY += DEFAULT_KEY_HEIGHT + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_TAB, "Tab", 0, currentY, DEFAULT_KEY_WIDTH * 1.5, DEFAULT_KEY_HEIGHT);
            AddStandardKey(LyKeysCode.VK_Q, "Q", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_W, "W", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_E, "E", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_R, "R", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_T, "T", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_Y, "Y", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_U, "U", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_I, "I", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_O, "O", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_P, "P", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_OEM_4, "[", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_OEM_6, "]", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_OEM_5, "\\", currentX, currentY, DEFAULT_KEY_WIDTH * 1.5, DEFAULT_KEY_HEIGHT);

            // 第三行
            currentX = DEFAULT_KEY_WIDTH * 1.75;
            currentY += DEFAULT_KEY_HEIGHT + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_CAPITAL, "Caps", 0, currentY, DEFAULT_KEY_WIDTH * 1.75, DEFAULT_KEY_HEIGHT);
            AddStandardKey(LyKeysCode.VK_A, "A", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_S, "S", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_D, "D", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_F, "F", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_G, "G", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_H, "H", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_J, "J", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_K, "K", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_L, "L", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_OEM_1, ";", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_OEM_7, "'", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_RETURN, "Enter", currentX, currentY, DEFAULT_KEY_WIDTH * 2.25, DEFAULT_KEY_HEIGHT);

            // 第四行
            currentX = DEFAULT_KEY_WIDTH * 2.25;
            currentY += DEFAULT_KEY_HEIGHT + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_LSHIFT, "Shift", 0, currentY, DEFAULT_KEY_WIDTH * 2.25, DEFAULT_KEY_HEIGHT);
            AddStandardKey(LyKeysCode.VK_Z, "Z", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_X, "X", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_C, "C", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_V, "V", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_B, "B", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_N, "N", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_M, "M", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_OEM_COMMA, ",", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_OEM_PERIOD, ".", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_OEM_2, "/", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_RSHIFT, "Shift", currentX, currentY, DEFAULT_KEY_WIDTH * 2.75, DEFAULT_KEY_HEIGHT);

            // 第五行
            currentX = DEFAULT_KEY_WIDTH * 1.5;
            currentY += DEFAULT_KEY_HEIGHT + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_LCONTROL, "Ctrl", 0, currentY, DEFAULT_KEY_WIDTH * 1.5, DEFAULT_KEY_HEIGHT);
            AddStandardKey(LyKeysCode.VK_LWIN, "Win", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_LMENU, "Alt", currentX, currentY, DEFAULT_KEY_WIDTH * 1.5, DEFAULT_KEY_HEIGHT); currentX += DEFAULT_KEY_WIDTH * 1.5 + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_SPACE, "Space", currentX, currentY, DEFAULT_KEY_WIDTH * 6.5, DEFAULT_KEY_HEIGHT); currentX += DEFAULT_KEY_WIDTH * 6.5 + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_RMENU, "Alt", currentX, currentY, DEFAULT_KEY_WIDTH * 1.5, DEFAULT_KEY_HEIGHT); currentX += DEFAULT_KEY_WIDTH * 1.5 + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_RWIN, "Win", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_APPS, "Menu", currentX, currentY); currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddStandardKey(LyKeysCode.VK_RCONTROL, "Ctrl", currentX, currentY, DEFAULT_KEY_WIDTH * 1.5, DEFAULT_KEY_HEIGHT);
        }

        private void InitializeFunctionKeys()
        {
            double currentX = 0;
            double currentY = 0;
            double functionKeySpacing = 15;

            // F1-F12 功能键,分四组排列
            for (int i = 0; i < 12; i++)
            {
                // 每4个键后增加间距
                if (i > 0 && i % 4 == 0)
                {
                    currentX += functionKeySpacing;
                }

                AddFunctionKey((LyKeysCode)(0x70 + i), $"F{i + 1}", currentX, currentY);
                currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            }
        }

        private void InitializeNumpadKeys()
        {
            double currentX = 0;
            double currentY = 0;

            // 第一行
            AddNumpadKey(LyKeysCode.VK_NUMLOCK, "Num", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_DIVIDE, "/", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_MULTIPLY, "*", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_SUBTRACT, "-", currentX, currentY);

            // 第二行
            currentX = 0;
            currentY += DEFAULT_KEY_HEIGHT + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_NUMPAD7, "7", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_NUMPAD8, "8", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_NUMPAD9, "9", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_ADD, "+", currentX, currentY, DEFAULT_KEY_WIDTH, DEFAULT_KEY_HEIGHT * 2 + KEY_SPACING);

            // 第三行
            currentX = 0;
            currentY += DEFAULT_KEY_HEIGHT + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_NUMPAD4, "4", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_NUMPAD5, "5", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_NUMPAD6, "6", currentX, currentY);

            // 第四行
            currentX = 0;
            currentY += DEFAULT_KEY_HEIGHT + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_NUMPAD1, "1", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_NUMPAD2, "2", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_NUMPAD3, "3", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_RETURN, "Enter", currentX, currentY, DEFAULT_KEY_WIDTH, DEFAULT_KEY_HEIGHT * 2 + KEY_SPACING);

            // 第五行
            currentX = 0;
            currentY += DEFAULT_KEY_HEIGHT + KEY_SPACING;
            AddNumpadKey(LyKeysCode.VK_NUMPAD0, "0", currentX, currentY, DEFAULT_KEY_WIDTH * 2 + KEY_SPACING, DEFAULT_KEY_HEIGHT);
            currentX += DEFAULT_KEY_WIDTH * 2 + KEY_SPACING * 2;
            AddNumpadKey(LyKeysCode.VK_DECIMAL, ".", currentX, currentY);
        }

        private void InitializeNavigationKeys()
        {
            double currentX = 0;
            double currentY = 0;

            // 第一行
            AddNavigationKey(LyKeysCode.VK_INSERT, "Ins", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNavigationKey(LyKeysCode.VK_HOME, "Home", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNavigationKey(LyKeysCode.VK_PRIOR, "PgUp", currentX, currentY);

            // 第二行
            currentX = 0;
            currentY += DEFAULT_KEY_HEIGHT + KEY_SPACING;
            AddNavigationKey(LyKeysCode.VK_DELETE, "Del", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNavigationKey(LyKeysCode.VK_END, "End", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNavigationKey(LyKeysCode.VK_NEXT, "PgDn", currentX, currentY);

            // 方向键
            currentX = DEFAULT_KEY_WIDTH + KEY_SPACING;
            currentY += DEFAULT_KEY_HEIGHT + KEY_SPACING * 2;
            AddNavigationKey(LyKeysCode.VK_UP, "↑", currentX, currentY);

            currentX = 0;
            currentY += DEFAULT_KEY_HEIGHT + KEY_SPACING;
            AddNavigationKey(LyKeysCode.VK_LEFT, "←", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNavigationKey(LyKeysCode.VK_DOWN, "↓", currentX, currentY);
            currentX += DEFAULT_KEY_WIDTH + KEY_SPACING;
            AddNavigationKey(LyKeysCode.VK_RIGHT, "→", currentX, currentY);
        }

        private void InitializeMouseButtons()
        {
            double currentX = 0;
            double currentY = 0;
            double mouseButtonWidth = 35;
            double mouseButtonHeight = 35;

            // 鼠标按键布局
            AddMouseButton(LyKeysCode.VK_LBUTTON, "左键", currentX + mouseButtonWidth, currentY);
            AddMouseButton(LyKeysCode.VK_RBUTTON, "右键", currentX + mouseButtonWidth, currentY + mouseButtonHeight * 2);
            AddMouseButton(LyKeysCode.VK_MBUTTON, "中键", currentX + mouseButtonWidth, currentY + mouseButtonHeight);
            AddMouseButton(LyKeysCode.VK_XBUTTON1, "侧键1", currentX, currentY + mouseButtonHeight);
            AddMouseButton(LyKeysCode.VK_XBUTTON2, "侧键2", currentX + mouseButtonWidth * 2, currentY + mouseButtonHeight);

            // 添加滚轮 - 使用特殊的滚轮事件
            currentY += mouseButtonHeight * 3 + KEY_SPACING;
            // 滚轮向上和向下使用中键的键码,但显示不同的文本
            AddMouseButton(LyKeysCode.VK_MBUTTON, "滚轮↑", currentX + mouseButtonWidth, currentY);
            currentY += mouseButtonHeight + KEY_SPACING;
            AddMouseButton(LyKeysCode.VK_MBUTTON, "滚轮↓", currentX + mouseButtonWidth, currentY);
        }

        private void AddStandardKey(LyKeysCode keyCode, string displayName, double x, double y, double width = DEFAULT_KEY_WIDTH, double height = DEFAULT_KEY_HEIGHT)
        {
            StandardKeys.Add(new KeyConfig
            {
                KeyCode = keyCode,
                DisplayName = displayName,
                Description = GetKeyDescription(keyCode),
                X = x,
                Y = y,
                Width = width,
                Height = height
            });
        }

        private void AddFunctionKey(LyKeysCode keyCode, string displayName, double x, double y, double width = DEFAULT_KEY_WIDTH, double height = DEFAULT_KEY_HEIGHT)
        {
            FunctionKeys.Add(new KeyConfig
            {
                KeyCode = keyCode,
                DisplayName = displayName,
                Description = GetKeyDescription(keyCode),
                X = x,
                Y = y,
                Width = width,
                Height = height
            });
        }

        private void AddNumpadKey(LyKeysCode keyCode, string displayName, double x, double y, double width = DEFAULT_KEY_WIDTH, double height = DEFAULT_KEY_HEIGHT)
        {
            NumpadKeys.Add(new KeyConfig
            {
                KeyCode = keyCode,
                DisplayName = displayName,
                Description = GetKeyDescription(keyCode),
                X = x,
                Y = y,
                Width = width,
                Height = height
            });
        }

        private void AddNavigationKey(LyKeysCode keyCode, string displayName, double x, double y, double width = DEFAULT_KEY_WIDTH, double height = DEFAULT_KEY_HEIGHT)
        {
            NavigationKeys.Add(new KeyConfig
            {
                KeyCode = keyCode,
                DisplayName = displayName,
                Description = GetKeyDescription(keyCode),
                X = x,
                Y = y,
                Width = width,
                Height = height
            });
        }

        private void AddMouseButton(LyKeysCode keyCode, string displayName, double x, double y, double width = 35, double height = 35)
        {
            MouseButtons.Add(new KeyConfig
            {
                KeyCode = keyCode,
                DisplayName = displayName,
                Description = GetKeyDescription(keyCode),
                X = x,
                Y = y,
                Width = width,
                Height = height
            });
        }

        private string GetKeyDescription(LyKeysCode keyCode)
        {
            // 使用LyKeysService中的GetKeyDescription方法获取按键描述
            return keyCode.ToString();
        }
    }
} 