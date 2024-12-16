using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfApp.ViewModels;

namespace WpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            DataContext = new MainViewModel(App.DDDriver, this);
        }

        // 初始化驱动
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 只需要验证驱动状态
            if (!App.DDDriver.ValidateDriver())
            {
                MessageBox.Show("驱动状态异常，程序将退出！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }
            
            System.Diagnostics.Debug.WriteLine("驱动状态正常");
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.Cleanup();
            }
        }

    }
}