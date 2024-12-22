using System;
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
using WpfApp.Services;


namespace WpfApp
{
    public partial class MainWindow : Window
    {
        private readonly LogManager _logger = LogManager.Instance;
        private readonly MainViewModel _viewModel;
        private bool _isClosing;

        public MainWindow()
        {
            // 在InitializeComponent之前设置DataContext
            _viewModel = new MainViewModel(App.DDDriver, this);
            DataContext = _viewModel;
            // 加载窗口尺寸
            Width = _viewModel.Config.UI.MainWindow.DefaultWidth;
            Height = _viewModel.Config.UI.MainWindow.DefaultHeight;
            InitializeComponent();

        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _logger.LogInitialization("App", 
                $"窗口源初始化 - 实际尺寸: {Width}x{Height}");
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_isClosing) return;
            _isClosing = true;

            try 
            {
                _logger.LogDebug("MainWindow", "开始清理窗口资源...");
                // 先清理ViewModel
                _viewModel.Cleanup();
                _logger.LogDebug("MainWindow", "窗口资源清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError("MainWindow", "窗口关闭异常", ex);
            }
            
            base.OnClosed(e);
        }
    }
}