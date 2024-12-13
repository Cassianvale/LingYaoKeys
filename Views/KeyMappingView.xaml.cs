using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Input;
using System.Text;
using System.Windows;

namespace WpfApp.Views
{
    public partial class KeyMappingView : Page
    {
        private const string KEY_ERROR = "无法识别按键，请检查输入法是否关闭";
        private bool isErrorShown = false;
        private readonly KeyMappingService _keyMappingService;

        public KeyMappingView()
        {
            InitializeComponent();
            _keyMappingService = new KeyMappingService();
        }

        private void KeyInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var key = e.Key;
                
                // 如果是IME处理的按键但没有对应的SystemKey
                if (key == Key.ImeProcessed && e.SystemKey == Key.None)
                {
                    ShowError(textBox);
                    e.Handled = true;
                    return;
                }

                // 如果之前显示过错误信息，现在有正常按键，则清除错误
                if (isErrorShown && textBox.Text == KEY_ERROR)
                {
                    textBox.Text = string.Empty;
                    isErrorShown = false;
                }

                // 使用SystemKey如果可用
                if (key == Key.ImeProcessed)
                {
                    key = e.SystemKey;
                }

                // 忽略单独按下的修饰键和特殊键
                if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                    key == Key.LeftAlt || key == Key.RightAlt ||
                    key == Key.LeftShift || key == Key.RightShift ||
                    key == Key.System || key == Key.None)
                {
                    e.Handled = true;
                    return;
                }

                // 构建按键文本
                StringBuilder keyText = new StringBuilder();
                var modifiers = Keyboard.Modifiers;

                if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    keyText.Append("Ctrl + ");
                if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                    keyText.Append("Alt + ");
                if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    keyText.Append("Shift + ");

                string keyName = GetDisplayKeyName(key);
                keyText.Append(keyName);

                textBox.Text = keyText.ToString().TrimEnd(' ', '+');
            }

            e.Handled = true;
        }

        private void KeyInputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.Text = string.Empty;
                isErrorShown = false;
            }
        }

        private void KeyInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrEmpty(textBox.Text))
            {
                textBox.Text = string.Empty;
                isErrorShown = false;
            }
        }

        private string GetDisplayKeyName(Key key)
        {
            switch (key)
            {
                // 特殊符号键
                case Key.OemPlus:
                    return "=";
                case Key.OemMinus:
                    return "-";
                case Key.OemQuestion:
                    return "?";
                case Key.OemPeriod:
                    return ".";
                case Key.OemComma:
                    return ",";
                case Key.OemSemicolon:
                    return ";";
                case Key.OemQuotes:
                    return "'";
                case Key.OemOpenBrackets:
                    return "[";
                case Key.OemCloseBrackets:
                    return "]";
                case Key.OemBackslash:
                    return "\\";
                case Key.OemTilde:
                    return "`";
                    
                // 功能键
                case Key.F1:
                case Key.F2:
                case Key.F3:
                case Key.F4:
                case Key.F5:
                case Key.F6:
                case Key.F7:
                case Key.F8:
                case Key.F9:
                case Key.F10:
                case Key.F11:
                case Key.F12:
                    return key.ToString();
                    
                // 数字键
                case Key.D0:
                case Key.D1:
                case Key.D2:
                case Key.D3:
                case Key.D4:
                case Key.D5:
                case Key.D6:
                case Key.D7:
                case Key.D8:
                case Key.D9:
                    return key.ToString().Replace("D", "");
                    
                // 修饰键
                case Key.LeftShift:
                case Key.RightShift:
                    return "Shift";
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    return "Ctrl";
                case Key.LeftAlt:
                case Key.RightAlt:
                    return "Alt";
                    
                default:
                    return key.ToString();
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void ShowError(TextBox textBox)
        {
            textBox.Text = KEY_ERROR;
            isErrorShown = true;
        }

        // 添加热键输入框的事件处理
        private void HotkeyInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var key = e.Key;
                
                // 如果是IME处理的按键但没有对应的SystemKey
                if (key == Key.ImeProcessed && e.SystemKey == Key.None)
                {
                    ShowError(textBox);
                    e.Handled = true;
                    return;
                }

                // 如果之前显示过错误信息，现在有正常按键，则清除错误
                if (isErrorShown && textBox.Text == KEY_ERROR)
                {
                    textBox.Text = string.Empty;
                    isErrorShown = false;
                }

                // 使用SystemKey如果可用
                if (key == Key.ImeProcessed)
                {
                    key = e.SystemKey;
                }

                // 只过滤System和None键
                if (key == Key.System || key == Key.None)
                {
                    e.Handled = true;
                    return;
                }

                // 构建按键文本
                StringBuilder keyText = new StringBuilder();
                
                // 如果按键本身不是修饰键，才添加组合键
                if (key != Key.LeftCtrl && key != Key.RightCtrl &&
                    key != Key.LeftAlt && key != Key.RightAlt &&
                    key != Key.LeftShift && key != Key.RightShift)
                {
                    var modifiers = Keyboard.Modifiers;
                    if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        keyText.Append("Ctrl + ");
                    if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                        keyText.Append("Alt + ");
                    if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                        keyText.Append("Shift + ");
                }

                string keyName = GetDisplayKeyName(key);
                keyText.Append(keyName);

                textBox.Text = keyText.ToString().TrimEnd(' ', '+');
            }

            e.Handled = true;
        }

        private void HotkeyInputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.Text = string.Empty;
                isErrorShown = false;
            }
        }

        private void HotkeyInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrEmpty(textBox.Text))
            {
                textBox.Text = string.Empty;
                isErrorShown = false;
            }
        }

        private void HotkeyInputBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string keyName = e.ChangedButton switch
                {
                    MouseButton.Middle => "MBUTTON",
                    MouseButton.XButton1 => "XBUTTON1",
                    MouseButton.XButton2 => "XBUTTON2",
                    _ => string.Empty
                };

                if (!string.IsNullOrEmpty(keyName))
                {
                    textBox.Text = _keyMappingService.GetDisplayName(keyName);
                    e.Handled = true;
                }
            }
        }

        private void HotkeyInputBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string keyName = e.Delta > 0 ? "MWHEELU" : "MWHEELD";
                textBox.Text = _keyMappingService.GetDisplayName(keyName);
                e.Handled = true;
            }
        }
    }
} 