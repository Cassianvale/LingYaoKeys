using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfApp.Views
{
    public partial class FeedbackView : Page
    {
        public FeedbackView()
        {
            InitializeComponent();
        }

        private void FeedbackTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                // 允许Ctrl+Enter插入换行
                return;
            }
            else if (e.Key == Key.Enter)
            {
                // 阻止默认的Enter键行为
                e.Handled = true;
                
                // 在光标位置插入换行
                if (sender is TextBox textBox)
                {
                    int caretIndex = textBox.CaretIndex;
                    textBox.Text = textBox.Text.Insert(caretIndex, Environment.NewLine);
                    textBox.CaretIndex = caretIndex + Environment.NewLine.Length;
                }
            }
        }
    }
} 