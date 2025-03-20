using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using WpfApp.Services.Utils;

namespace WpfApp.Views;

/// <summary>
/// AboutView.xaml 的交互逻辑
/// </summary>
public partial class AboutView : Page
{
    private readonly ViewModels.AboutViewModel _viewModel;
    private readonly SerilogManager _logger = SerilogManager.Instance;

    public AboutView()
    {
        InitializeComponent();
        _viewModel = new ViewModels.AboutViewModel();
        DataContext = _viewModel;
        _logger.Debug("AboutView页面已初始化");
    }
}