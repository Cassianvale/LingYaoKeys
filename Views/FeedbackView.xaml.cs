using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfApp.Views
{
    public partial class FeedbackView : Page, IDisposable
    {
        private bool _disposedValue;
        private readonly ViewModels.FeedbackViewModel _viewModel;

        public FeedbackView()
        {
            InitializeComponent();
            _viewModel = DataContext as ViewModels.FeedbackViewModel;
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
                e.Handled = true;
                
                if (sender is TextBox textBox)
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