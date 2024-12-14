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

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 暂时注释掉驱动初始化

            // if (!App.InitializeDriver())
            // {
            //     Application.Current.Shutdown();
            // }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel mainViewModel)
            {
                System.Diagnostics.Debug.WriteLine("窗口关闭时保存配置");
                mainViewModel.SaveConfig();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Cleanup();
            }
            base.OnClosed(e);
        }

        private void Frame_Navigated(object sender, NavigationEventArgs e)
        {

        }
    }
}