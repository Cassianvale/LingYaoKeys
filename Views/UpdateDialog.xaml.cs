using System.Windows;
using System.Windows.Input;

namespace WpfApp.Views
{
    public partial class UpdateDialog : Window
    {
        public string LatestVersion { get; set; }
        public string CurrentVersion { get; set; }
        public string DownloadUrl { get; set; }

        public UpdateDialog(string latestVersion, string currentVersion, string downloadUrl)
        {
            InitializeComponent();
            DataContext = this;
            
            LatestVersion = latestVersion;
            CurrentVersion = currentVersion;
            DownloadUrl = downloadUrl;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 