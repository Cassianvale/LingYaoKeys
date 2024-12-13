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

        public KeyMappingView()
        {
            InitializeComponent();
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
                case Key.OemPlus:
                    return "+";
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
    }
} 