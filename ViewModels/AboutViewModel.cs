using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.Windows.Controls;
using WpfApp.Services.Utils;
using WpfApp.Services.Config;
using WpfApp.Views;

namespace WpfApp.ViewModels;

public class AboutViewModel : ViewModelBase
{
    private readonly SerilogManager _logger = SerilogManager.Instance;
    private readonly string _githubUrl = AppConfigService.Config.AppInfo.GitHubUrl;
    private ICommand? _openGitHubCommand;
    private ICommand? _showQRCodeCommand;
    private ICommand? _refreshCommand;

    public ICommand OpenGitHubCommand => _openGitHubCommand ??= new RelayCommand(OpenGitHub);
    public ICommand ShowQRCodeCommand => _showQRCodeCommand ??= new RelayCommand(ShowQRCode);
    public ICommand RefreshCommand => _refreshCommand ??= new RelayCommand(RefreshContent);

    private void RefreshContent()
    {
        try
        {
            _logger.Debug("开始刷新About页面内容");
            
            // 获取当前视图的控件
            var currentPage = System.Windows.Application.Current.MainWindow?.Content as Frame;
            if (currentPage?.Content is AboutView aboutView)
            {
                // 触发页面重新加载
                if (aboutView is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                
                // 重新触发Loaded事件
                var loadedEvent = new RoutedEventArgs(Page.LoadedEvent);
                aboutView.RaiseEvent(loadedEvent);
                
                _logger.Debug("已触发About页面重新加载");
            }
            else
            {
                _logger.Warning("无法获取About页面实例进行刷新");
                
                // 尝试从MainViewModel导航到About页面
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel.NavigateCommand.Execute("About");
                    _logger.Debug("已通过导航命令刷新About页面");
                }
                else
                {
                    _logger.Error("无法通过MainViewModel刷新页面");
                    System.Windows.MessageBox.Show(
                        "无法刷新页面内容，请尝试重新打开\"关于\"页面。",
                        "刷新失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("刷新About页面内容失败", ex);
            System.Windows.MessageBox.Show(
                $"刷新页面时发生错误：{ex.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenGitHub()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _githubUrl,
                UseShellExecute = true
            };
            Process.Start(psi);
            _logger.Debug("成功打开GitHub仓库链接");
        }
        catch (Exception ex)
        {
            _logger.Error("打开GitHub仓库链接失败", ex);
            System.Windows.MessageBox.Show(
                "无法打开GitHub链接，请检查网络连接后重试。",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ShowQRCode()
    {
        try
        {
            _logger.Debug("开始执行ShowQRCode方法");

            var mainWindow = System.Windows.Application.Current.MainWindow;
            _logger.Debug($"MainWindow是否为null: {mainWindow == null}");

            if (mainWindow == null)
            {
                _logger.Error("无法获取MainWindow实例");
                System.Windows.MessageBox.Show(
                    "无法打开二维码页面，请尝试重启应用程序。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            _logger.Debug($"MainWindow类型: {mainWindow.GetType().FullName}");
            _logger.Debug($"MainWindow.DataContext是否为null: {mainWindow.DataContext == null}");

            if (mainWindow.DataContext == null)
            {
                _logger.Error("MainWindow.DataContext为null");
                System.Windows.MessageBox.Show(
                    "应用程序状态异常，请尝试重启应用程序。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            _logger.Debug($"MainWindow.DataContext类型: {mainWindow.DataContext.GetType().FullName}");

            if (mainWindow.DataContext is MainViewModel mainViewModel)
            {
                _logger.Debug("成功获取MainViewModel，准备导航到QRCode页面");
                mainViewModel.NavigateCommand.Execute("QRCode");
                _logger.Debug("已执行导航命令");
            }
            else
            {
                _logger.Error($"MainWindow.DataContext类型不正确: {mainWindow.DataContext.GetType().FullName}");
                System.Windows.MessageBox.Show(
                    "应用程序状态异常，请尝试重启应用程序。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("显示二维码页面失败", ex);
            System.Windows.MessageBox.Show(
                $"显示二维码页面时发生错误：{ex.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}