using System.Windows;
using System.Windows.Controls;
using WPFProgressBar = System.Windows.Controls.ProgressBar;

namespace WpfApp.Views
{
    /// <summary>
    /// SplashWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SplashWindow : Window
    {
        private TextBlock _statusText;
        private WPFProgressBar _progressBar;

        public SplashWindow()
        {
            InitializeComponent();
            _statusText = (TextBlock)FindName("StatusText");
            _progressBar = (WPFProgressBar)FindName("ProgressBar");
        }

        public void UpdateProgress(string message, int percentage)
        {
            if (_statusText == null || _progressBar == null) return;

            Dispatcher.Invoke(() =>
            {
                _statusText.Text = message;
                _progressBar.Value = percentage;
            });
        }
    }
} 