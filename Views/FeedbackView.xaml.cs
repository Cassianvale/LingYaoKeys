using System.Windows.Input;

namespace WpfApp.Views
{
    public partial class FeedbackView
    {
        private bool _disposedValue;
        private readonly ViewModels.FeedbackViewModel _viewModel;

        public FeedbackView()
        {
            InitializeComponent();
            _viewModel = (ViewModels.FeedbackViewModel)DataContext;
        }

        private void FeedbackTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                // 允许Ctrl+Enter插入换行
                return;
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                
                if (sender is System.Windows.Controls.TextBox textBox)
                {
                    int caretIndex = textBox.CaretIndex;
                    textBox.Text = textBox.Text.Insert(caretIndex, Environment.NewLine);
                    textBox.CaretIndex = caretIndex + Environment.NewLine.Length;
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // 释放托管资源
                    if (_viewModel is IDisposable disposableViewModel)
                    {
                        disposableViewModel.Dispose();
                    }
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
} 