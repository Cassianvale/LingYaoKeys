using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

// 键盘布局配置
namespace WpfApp.Services.Models
{
    public class KeyboardLayoutKey : INotifyPropertyChanged
    {
        private bool _isRapidFire;
        private int _rapidFireDelay = 10;
        private bool _isDisabled;
        private int _pressTime = 5;


        public LyKeysCode KeyCode { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }

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
                if (_rapidFireDelay != value && value >= 1)
                {
                    _rapidFireDelay = value;
                    OnPropertyChanged();
                    // 只有在连发模式下才额外通知IsRapidFire变更
                    if (IsRapidFire)
                    {
                        OnPropertyChanged(nameof(IsRapidFire));
                    }
                }
            }
        }

        public int PressTime
        {
            get => _pressTime;
            set
            {
                if (_pressTime != value && value >= 1)
                {
                    _pressTime = value;
                    OnPropertyChanged();
                    // 只有在连发模式下才额外通知IsRapidFire变更
                    if (IsRapidFire)
                    {
                        OnPropertyChanged(nameof(IsRapidFire));
                    }
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
        private readonly LyKeysService _lyKeysService;

        public List<KeyboardLayoutKey> StandardKeys { get; set; } = new List<KeyboardLayoutKey>();
        public List<KeyboardLayoutKey> FunctionKeys { get; set; } = new List<KeyboardLayoutKey>();
        public List<KeyboardLayoutKey> NumpadKeys { get; set; } = new List<KeyboardLayoutKey>();
        public List<KeyboardLayoutKey> NavigationKeys { get; set; } = new List<KeyboardLayoutKey>();
        public List<KeyboardLayoutKey> MouseButtons { get; set; } = new List<KeyboardLayoutKey>();

        public KeyboardLayoutConfig(LyKeysService lyKeysService)
        {
            _lyKeysService = lyKeysService;
            InitializeLayout();
        }

        public void InitializeLayout()
        {
            InitializeStandardKeys();
            InitializeFunctionKeys();
            InitializeNumpadKeys();
            InitializeNavigationKeys();
            InitializeMouseButtons();
        }

        private void InitializeStandardKeys()
        {
            // 第一行 - 数字键行
            AddStandardKey(LyKeysCode.VK_OEM_3, "`");
            AddStandardKey(LyKeysCode.VK_1, "1");
            AddStandardKey(LyKeysCode.VK_2, "2");
            AddStandardKey(LyKeysCode.VK_3, "3");
            AddStandardKey(LyKeysCode.VK_4, "4");
            AddStandardKey(LyKeysCode.VK_5, "5");
            AddStandardKey(LyKeysCode.VK_6, "6");
            AddStandardKey(LyKeysCode.VK_7, "7");
            AddStandardKey(LyKeysCode.VK_8, "8");
            AddStandardKey(LyKeysCode.VK_9, "9");
            AddStandardKey(LyKeysCode.VK_0, "0");
            AddStandardKey(LyKeysCode.VK_OEM_MINUS, "-");
            AddStandardKey(LyKeysCode.VK_OEM_PLUS, "=");
            AddStandardKey(LyKeysCode.VK_BACK, "Backspace");

            // 第二行
            AddStandardKey(LyKeysCode.VK_TAB, "Tab");
            AddStandardKey(LyKeysCode.VK_Q, "Q");
            AddStandardKey(LyKeysCode.VK_W, "W");
            AddStandardKey(LyKeysCode.VK_E, "E");
            AddStandardKey(LyKeysCode.VK_R, "R");
            AddStandardKey(LyKeysCode.VK_T, "T");
            AddStandardKey(LyKeysCode.VK_Y, "Y");
            AddStandardKey(LyKeysCode.VK_U, "U");
            AddStandardKey(LyKeysCode.VK_I, "I");
            AddStandardKey(LyKeysCode.VK_O, "O");
            AddStandardKey(LyKeysCode.VK_P, "P");
            AddStandardKey(LyKeysCode.VK_OEM_4, "[");
            AddStandardKey(LyKeysCode.VK_OEM_6, "]");
            AddStandardKey(LyKeysCode.VK_OEM_5, "\\");

            // 第三行
            AddStandardKey(LyKeysCode.VK_CAPITAL, "Caps");
            AddStandardKey(LyKeysCode.VK_A, "A");
            AddStandardKey(LyKeysCode.VK_S, "S");
            AddStandardKey(LyKeysCode.VK_D, "D");
            AddStandardKey(LyKeysCode.VK_F, "F");
            AddStandardKey(LyKeysCode.VK_G, "G");
            AddStandardKey(LyKeysCode.VK_H, "H");
            AddStandardKey(LyKeysCode.VK_J, "J");
            AddStandardKey(LyKeysCode.VK_K, "K");
            AddStandardKey(LyKeysCode.VK_L, "L");
            AddStandardKey(LyKeysCode.VK_OEM_1, ";");
            AddStandardKey(LyKeysCode.VK_OEM_7, "'");
            AddStandardKey(LyKeysCode.VK_RETURN, "Enter");

            // 第四行
            AddStandardKey(LyKeysCode.VK_LSHIFT, "Shift");
            AddStandardKey(LyKeysCode.VK_Z, "Z");
            AddStandardKey(LyKeysCode.VK_X, "X");
            AddStandardKey(LyKeysCode.VK_C, "C");
            AddStandardKey(LyKeysCode.VK_V, "V");
            AddStandardKey(LyKeysCode.VK_B, "B");
            AddStandardKey(LyKeysCode.VK_N, "N");
            AddStandardKey(LyKeysCode.VK_M, "M");
            AddStandardKey(LyKeysCode.VK_OEM_COMMA, ",");
            AddStandardKey(LyKeysCode.VK_OEM_PERIOD, ".");
            AddStandardKey(LyKeysCode.VK_OEM_2, "/");
            AddStandardKey(LyKeysCode.VK_RSHIFT, "Shift");

            // 第五行
            AddStandardKey(LyKeysCode.VK_LCONTROL, "Ctrl");
            AddStandardKey(LyKeysCode.VK_LWIN, "Win");
            AddStandardKey(LyKeysCode.VK_LMENU, "Alt");
            AddStandardKey(LyKeysCode.VK_SPACE, "Space");
            AddStandardKey(LyKeysCode.VK_RMENU, "Alt");
            AddStandardKey(LyKeysCode.VK_RWIN, "Win");
            AddStandardKey(LyKeysCode.VK_APPS, "Menu");
            AddStandardKey(LyKeysCode.VK_RCONTROL, "Ctrl");
        }

        private void InitializeFunctionKeys()
        {
            AddFunctionKey(LyKeysCode.VK_ESCAPE, "Esc");
            AddFunctionKey(LyKeysCode.VK_F1, "F1");
            AddFunctionKey(LyKeysCode.VK_F2, "F2");
            AddFunctionKey(LyKeysCode.VK_F3, "F3");
            AddFunctionKey(LyKeysCode.VK_F4, "F4");
            AddFunctionKey(LyKeysCode.VK_F5, "F5");
            AddFunctionKey(LyKeysCode.VK_F6, "F6");
            AddFunctionKey(LyKeysCode.VK_F7, "F7");
            AddFunctionKey(LyKeysCode.VK_F8, "F8");
            AddFunctionKey(LyKeysCode.VK_F9, "F9");
            AddFunctionKey(LyKeysCode.VK_F10, "F10");
            AddFunctionKey(LyKeysCode.VK_F11, "F11");
            AddFunctionKey(LyKeysCode.VK_F12, "F12");
        }

        private void InitializeNumpadKeys()
        {
            AddNumpadKey(LyKeysCode.VK_NUMLOCK, "Num");
            AddNumpadKey(LyKeysCode.VK_DIVIDE, "/");
            AddNumpadKey(LyKeysCode.VK_MULTIPLY, "*");
            AddNumpadKey(LyKeysCode.VK_SUBTRACT, "-");
            AddNumpadKey(LyKeysCode.VK_NUMPAD7, "7");
            AddNumpadKey(LyKeysCode.VK_NUMPAD8, "8");
            AddNumpadKey(LyKeysCode.VK_NUMPAD9, "9");
            AddNumpadKey(LyKeysCode.VK_ADD, "+");
            AddNumpadKey(LyKeysCode.VK_NUMPAD4, "4");
            AddNumpadKey(LyKeysCode.VK_NUMPAD5, "5");
            AddNumpadKey(LyKeysCode.VK_NUMPAD6, "6");
            AddNumpadKey(LyKeysCode.VK_NUMPAD1, "1");
            AddNumpadKey(LyKeysCode.VK_NUMPAD2, "2");
            AddNumpadKey(LyKeysCode.VK_NUMPAD3, "3");
            AddNumpadKey(LyKeysCode.VK_RETURN, "Enter");
            AddNumpadKey(LyKeysCode.VK_NUMPAD0, "0");
            AddNumpadKey(LyKeysCode.VK_DECIMAL, ".");
        }

        private void InitializeNavigationKeys()
        {
            AddNavigationKey(LyKeysCode.VK_SNAPSHOT, "PrtSc");
            AddNavigationKey(LyKeysCode.VK_SCROLL, "ScrLk");
            AddNavigationKey(LyKeysCode.VK_PAUSE, "Pause");
            AddNavigationKey(LyKeysCode.VK_INSERT, "Ins");
            AddNavigationKey(LyKeysCode.VK_HOME, "Home");
            AddNavigationKey(LyKeysCode.VK_PRIOR, "PgUp");
            AddNavigationKey(LyKeysCode.VK_DELETE, "Del");
            AddNavigationKey(LyKeysCode.VK_END, "End");
            AddNavigationKey(LyKeysCode.VK_NEXT, "PgDn");
            AddNavigationKey(LyKeysCode.VK_UP, "↑");
            AddNavigationKey(LyKeysCode.VK_LEFT, "←");
            AddNavigationKey(LyKeysCode.VK_DOWN, "↓");
            AddNavigationKey(LyKeysCode.VK_RIGHT, "→");
        }

        private void InitializeMouseButtons()
        {
            AddMouseButton(LyKeysCode.VK_XBUTTON1, "侧键1");
            AddMouseButton(LyKeysCode.VK_XBUTTON2, "侧键2");
            AddMouseButton(LyKeysCode.VK_MBUTTON, "滚轮↑");
            AddMouseButton(LyKeysCode.VK_LBUTTON, "左键");
            AddMouseButton(LyKeysCode.VK_MBUTTON, "中键");
            AddMouseButton(LyKeysCode.VK_RBUTTON, "右键");
            AddMouseButton(LyKeysCode.VK_MBUTTON, "滚轮↓");
        }

        private void AddStandardKey(LyKeysCode keyCode, string displayName)
        {
            StandardKeys.Add(new KeyboardLayoutKey
            {
                KeyCode = keyCode,
                DisplayName = displayName,
                Description = _lyKeysService.GetKeyDescription(keyCode)
            });
        }

        private void AddFunctionKey(LyKeysCode keyCode, string displayName)
        {
            FunctionKeys.Add(new KeyboardLayoutKey
            {
                KeyCode = keyCode,
                DisplayName = displayName,
                Description = _lyKeysService.GetKeyDescription(keyCode)
            });
        }

        private void AddNumpadKey(LyKeysCode keyCode, string displayName)
        {
            NumpadKeys.Add(new KeyboardLayoutKey
            {
                KeyCode = keyCode,
                DisplayName = displayName,
                Description = _lyKeysService.GetKeyDescription(keyCode)
            });
        }

        private void AddNavigationKey(LyKeysCode keyCode, string displayName)
        {
            NavigationKeys.Add(new KeyboardLayoutKey
            {
                KeyCode = keyCode,
                DisplayName = displayName,
                Description = _lyKeysService.GetKeyDescription(keyCode)
            });
        }

        private void AddMouseButton(LyKeysCode keyCode, string displayName)
        {
            MouseButtons.Add(new KeyboardLayoutKey
            {
                KeyCode = keyCode,
                DisplayName = displayName,
                Description = _lyKeysService.GetKeyDescription(keyCode)
            });
        }
    }
} 