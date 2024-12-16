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
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            // 在InitializeComponent之前设置DataContext
            _viewModel = new MainViewModel(App.DDDriver, this);
            DataContext = _viewModel;
            
            InitializeComponent();

            // 添加调试信息
            System.Diagnostics.Debug.WriteLine($"MainWindow初始化 - 配置尺寸: {_viewModel.Config.UI.MainWindow.DefaultWidth}x{_viewModel.Config.UI.MainWindow.DefaultHeight}");

            // 窗口加载完成后再次检查尺寸
            Loaded += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow加载完成 - 实际尺寸: {Width}x{Height}");
            };

            // 确保窗口尺寸正确
            Width = _viewModel.Config.UI.MainWindow.DefaultWidth;
            Height = _viewModel.Config.UI.MainWindow.DefaultHeight;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            System.Diagnostics.Debug.WriteLine($"MainWindow源初始化 - 实际尺寸: {Width}x{Height}");
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.Cleanup();
            base.OnClosed(e);
        }
    }
}