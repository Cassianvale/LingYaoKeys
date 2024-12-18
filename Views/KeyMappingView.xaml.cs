using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Input;
using System.Text;
using System.Windows;
using WpfApp.ViewModels;
using WpfApp.Services;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

// 提供按键映射视图
namespace WpfApp.Views
{
    public partial class KeyMappingView : Page
    {   
        private readonly LogManager _logger = LogManager.Instance;
        private const string KEY_ERROR = "无法识别按键，请检查输入法是否关闭";
        private bool isErrorShown = false;

        private KeyMappingViewModel ViewModel => (KeyMappingViewModel)DataContext;

        public KeyMappingView()
        {
            InitializeComponent();
        }

        private void KeyInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            
            e.Handled = true;
            
            // 处理IME输入
            if (e.Key == Key.ImeProcessed && e.SystemKey == Key.None)
            {
                if (!isErrorShown)
                {
                    ShowError(textBox);
                }
                return;
            }

            // 获取实际按键
            var key = e.Key == Key.ImeProcessed ? e.SystemKey : e.Key;
            
            // 过滤系统按键和空按键
            if (key == Key.System || key == Key.None)
            {
                return;
            }
            
            // 转换并设置按键
            if (TryConvertToDDKeyCode(key, out DDKeyCode ddKeyCode))
            {
                ViewModel?.SetCurrentKey(ddKeyCode);
                isErrorShown = false;
            }
            else if (!isErrorShown)
            {
                ShowError(textBox);
            }
        }

        // 将WPF的Key映射到DDKeyCode
        private bool TryConvertToDDKeyCode(Key key, out DDKeyCode ddKeyCode)
        {
            // 将WPF的Key映射到DDKeyCode
            ddKeyCode = key switch
            {
                // 字母键
                Key.A => DDKeyCode.A,
                Key.B => DDKeyCode.B,
                Key.C => DDKeyCode.C,
                Key.D => DDKeyCode.D,
                Key.E => DDKeyCode.E,
                Key.F => DDKeyCode.F,
                Key.G => DDKeyCode.G,
                Key.H => DDKeyCode.H,
                Key.I => DDKeyCode.I,
                Key.J => DDKeyCode.J,
                Key.K => DDKeyCode.K,
                Key.L => DDKeyCode.L,
                Key.M => DDKeyCode.M,
                Key.N => DDKeyCode.N,
                Key.O => DDKeyCode.O,
                Key.P => DDKeyCode.P,
                Key.Q => DDKeyCode.Q,
                Key.R => DDKeyCode.R,
                Key.S => DDKeyCode.S,
                Key.T => DDKeyCode.T,
                Key.U => DDKeyCode.U,
                Key.V => DDKeyCode.V,
                Key.W => DDKeyCode.W,
                Key.X => DDKeyCode.X,
                Key.Y => DDKeyCode.Y,
                Key.Z => DDKeyCode.Z,

                // 数字键
                Key.D0 => DDKeyCode.NUM_0,
                Key.D1 => DDKeyCode.NUM_1,
                Key.D2 => DDKeyCode.NUM_2,
                Key.D3 => DDKeyCode.NUM_3,
                Key.D4 => DDKeyCode.NUM_4,
                Key.D5 => DDKeyCode.NUM_5,
                Key.D6 => DDKeyCode.NUM_6,
                Key.D7 => DDKeyCode.NUM_7,
                Key.D8 => DDKeyCode.NUM_8,
                Key.D9 => DDKeyCode.NUM_9,

                // 功能键
                Key.F1 => DDKeyCode.F1,
                Key.F2 => DDKeyCode.F2,
                Key.F3 => DDKeyCode.F3,
                Key.F4 => DDKeyCode.F4,
                Key.F5 => DDKeyCode.F5,
                Key.F6 => DDKeyCode.F6,
                Key.F7 => DDKeyCode.F7,
                Key.F8 => DDKeyCode.F8,
                Key.F9 => DDKeyCode.F9,
                Key.F10 => DDKeyCode.F10,
                Key.F11 => DDKeyCode.F11,
                Key.F12 => DDKeyCode.F12,

                // 特殊键
                Key.Escape => DDKeyCode.ESC,
                Key.Tab => DDKeyCode.TAB,
                Key.CapsLock => DDKeyCode.CAPS_LOCK,
                Key.LeftShift => DDKeyCode.LEFT_SHIFT,
                Key.RightShift => DDKeyCode.RIGHT_SHIFT,
                Key.LeftCtrl => DDKeyCode.LEFT_CTRL,
                Key.RightCtrl => DDKeyCode.RIGHT_CTRL,
                Key.LeftAlt => DDKeyCode.LEFT_ALT,
                Key.RightAlt => DDKeyCode.RIGHT_ALT,
                Key.Space => DDKeyCode.SPACE,
                Key.Enter => DDKeyCode.ENTER,
                Key.Back => DDKeyCode.BACKSPACE,

                // 符号键
                Key.OemTilde => DDKeyCode.TILDE,
                Key.OemMinus => DDKeyCode.MINUS,
                Key.OemPlus => DDKeyCode.EQUALS,
                Key.OemOpenBrackets => DDKeyCode.LEFT_BRACKET,
                Key.OemCloseBrackets => DDKeyCode.RIGHT_BRACKET,
                Key.OemSemicolon => DDKeyCode.SEMICOLON,
                Key.OemQuotes => DDKeyCode.QUOTE,
                Key.OemComma => DDKeyCode.COMMA,
                Key.OemPeriod => DDKeyCode.PERIOD,
                Key.OemQuestion => DDKeyCode.SLASH,
                Key.OemBackslash => DDKeyCode.BACKSLASH,

                _ => DDKeyCode.ESC
            };

            // 只要按键被正确映射就返回true
            return key == Key.Escape || ddKeyCode != DDKeyCode.ESC;
        }

        // 处理按键输入框获得焦点
        private void KeyInputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                isErrorShown = false;
            }
        }

        // 处理按键输入框失去焦点
        private void KeyInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrEmpty(textBox.Text))
            {
                isErrorShown = false;
            }
        }

        // 处理超链接请求导航
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        // 显示错误信息
        private void ShowError(TextBox? textBox)
        {
            if (textBox == null) return;
            
            isErrorShown = true;
            if (textBox.Name == "StartHotkeyInput")
            {
                ViewModel.StartHotkeyText = KEY_ERROR;
            }
            else if (textBox.Name == "StopHotkeyInput")
            {
                ViewModel.StopHotkeyText = KEY_ERROR;
            }
        }

        // 处理热键输入框获得焦点
        private void HotkeyInputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                isErrorShown = false;
            }
        }

        // 处理热键输入框失去焦点
        private void HotkeyInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrEmpty(textBox.Text))
            {
                isErrorShown = false;
            }
        }

        // 处理鼠标按键
        private void HotkeyInputBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                DDKeyCode keyCode = e.ChangedButton switch
                {
                    MouseButton.Middle => DDKeyCode.MBUTTON,
                    MouseButton.XButton1 => DDKeyCode.XBUTTON1,
                    MouseButton.XButton2 => DDKeyCode.XBUTTON2,
                    _ => DDKeyCode.ESC
                };

                if (keyCode != DDKeyCode.ESC)
                {
                    ViewModel.SetCurrentKey(keyCode);
                    e.Handled = true;
                }
            }
        }

        // 统一的热键处理方法
        private void HandleHotkeyInput(TextBox textBox, DDKeyCode keyCode, ModifierKeys modifiers, bool isStartHotkey)
        {
            if (textBox == null)
            {
                _logger.LogWarning("HotkeyInput", "HandleHotkeyInput: textBox is null");
                return;
            }

            // 只过滤修饰键
            if (IsModifierKey(keyCode))
            {
                return;
            }

            // 记录热键输入处理
            _logger.LogDebug("HotkeyInput", 
                $"处理热键输入 - keyCode: {keyCode}, 修饰键: {modifiers}, isStartHotkey: {isStartHotkey}");

            // 区分当前处理的是开始热键还是停止热键
            if (isStartHotkey)
            {
                ViewModel?.SetStartHotkey(keyCode, modifiers);
            }
            else
            {
                ViewModel?.SetStopHotkey(keyCode, modifiers);
            }
            isErrorShown = false;
        }

        // 判断是否为修饰键
        private bool IsModifierKey(DDKeyCode keyCode)
        {
            return keyCode == DDKeyCode.LEFT_CTRL 
                || keyCode == DDKeyCode.RIGHT_CTRL
                || keyCode == DDKeyCode.LEFT_ALT 
                || keyCode == DDKeyCode.RIGHT_ALT
                || keyCode == DDKeyCode.LEFT_SHIFT 
                || keyCode == DDKeyCode.RIGHT_SHIFT;
        }

        // 处理开始热键
        private void StartHotkeyInput_KeyDown(object sender, KeyEventArgs e)
        {
            _logger.LogDebug("HotkeyInput", "StartHotkeyInput_KeyDown 已触发");
            _logger.LogDebug("HotkeyInput", $"Key: {e.Key}, SystemKey: {e.SystemKey}, KeyStates: {e.KeyStates}");
            StartHotkeyInput_PreviewKeyDown(sender, e);
        }

        private void StartHotkeyInput_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _logger.LogDebug("HotkeyInput", "StartHotkeyInput_MouseDown 已触发");
            _logger.LogDebug("HotkeyInput", $"ChangedButton: {e.ChangedButton}");
            StartHotkeyInput_PreviewMouseDown(sender, e);
        }

        // 处理开始热键的鼠标释放
        private void StartHotkeyInput_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _logger.LogDebug("HotkeyInput", "StartHotkeyInput_PreviewMouseUp 已触发");
            _logger.LogDebug("HotkeyInput", $"ChangedButton: {e.ChangedButton}");
        }

        private void StartHotkeyInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            try 
            {
                e.Handled = true;
                
                if (e.Key == Key.ImeProcessed && e.SystemKey == Key.None)
                {
                    _logger.LogDebug("HotkeyInput", "检测到IME输入，显示错误");
                    if (!isErrorShown)
                    {
                        ShowError(textBox);
                    }
                    return;
                }

                var key = e.Key == Key.ImeProcessed ? e.SystemKey : e.Key;
                
                if (key == Key.System || key == Key.None)
                {
                    return;
                }

                if (TryConvertToDDKeyCode(key, out DDKeyCode ddKeyCode))
                {
                    HandleHotkeyInput(textBox, ddKeyCode, Keyboard.Modifiers, true);
                }
                else if (!isErrorShown)
                {
                    ShowError(textBox);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyInput", "StartHotkeyInput_PreviewKeyDown 处理异常", ex);
            }
        }

        // 处理停止热键
        private void StopHotkeyInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            
            try
            {
                e.Handled = true;
                
                if (e.Key == Key.ImeProcessed && e.SystemKey == Key.None)
                {
                    _logger.LogDebug("HotkeyInput", "检测到IME输入，显示错误");
                    if (!isErrorShown)
                    {
                        ShowError(textBox);
                    }
                    return;
                }

                var key = e.Key == Key.ImeProcessed ? e.SystemKey : e.Key;
                
                if (key == Key.System || key == Key.None)
                {
                    return;
                }

                // 获取当前修饰键状态
                var modifiers = Keyboard.Modifiers;
                
                if (TryConvertToDDKeyCode(key, out DDKeyCode ddKeyCode))
                {
                    if (IsModifierKey(ddKeyCode))
                    {
                        return;
                    }
                    HandleHotkeyInput(textBox, ddKeyCode, modifiers, false);
                }
                else if (!isErrorShown)
                {
                    ShowError(textBox);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyInput", "StopHotkeyInput_PreviewKeyDown 处理异常", ex);
            }
        }

        // 处理开始热键的鼠标点击
        private void StartHotkeyInput_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                DDKeyCode? keyCode = e.ChangedButton switch
                {
                    MouseButton.Middle => DDKeyCode.MBUTTON,
                    MouseButton.XButton1 => DDKeyCode.XBUTTON1,
                    MouseButton.XButton2 => DDKeyCode.XBUTTON2,
                    _ => null // 对于左键和右键不处理，让输入框正常获取焦点以接收键盘输入
                };

                if (keyCode.HasValue)
                {
                    HandleHotkeyInput(textBox, keyCode.Value, Keyboard.Modifiers, true);
                    e.Handled = true; // 阻止事件继续传播
                }
            }
        }

        // 处理停止热键的鼠标点击
        private void StopHotkeyInput_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                DDKeyCode? keyCode = e.ChangedButton switch
                {
                    MouseButton.Middle => DDKeyCode.MBUTTON,
                    MouseButton.XButton1 => DDKeyCode.XBUTTON1,
                    MouseButton.XButton2 => DDKeyCode.XBUTTON2,
                    _ => null // 对于左键和右键，不处理，让输入框正常获取焦点以接收键盘输入
                };

                if (keyCode.HasValue)
                {
                    HandleHotkeyInput(textBox, keyCode.Value, Keyboard.Modifiers, false);
                    e.Handled = true; // 阻止事件继续传播
                }
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        private void HandleStartHotkey(bool isKeyDown)
        {
            if (ViewModel == null)
            {
                _logger.LogWarning("HotkeyHandler", "ViewModel is null");
                return;
            }
            
            try
            {
                if (ViewModel.SelectedKeyMode == 0) // 顺序模式
                {
                    _logger.LogDebug("HotkeyHandler", $"顺序模式 - 按键{(isKeyDown ? "按下" : "释放")}");
                    if (isKeyDown) // 按下时启动
                    {
                        ViewModel.StartKeyMapping();
                    }
                }
                else // 按压模式
                {
                    _logger.LogDebug("HotkeyHandler", $"按压模式 - 按键{(isKeyDown ? "按下" : "释放")}");
                    if (isKeyDown)
                    {
                        ViewModel.StartKeyMapping();
                        ViewModel.SetHoldMode(true);
                    }
                    else
                    {
                        ViewModel.SetHoldMode(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyHandler", "处理开始热键异常", ex);
            }
        }

        private void HandleStopHotkey()
        {
            try
            {
                if (ViewModel == null)
                {
                    _logger.LogWarning("HotkeyHandler", "ViewModel is null");
                    return;
                }
                
                _logger.LogDebug("HotkeyHandler", "处理停止热键");
                ViewModel.StopKeyMapping();
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyHandler", "处理停止热键异常", ex);
            }
        }
    }
} 
