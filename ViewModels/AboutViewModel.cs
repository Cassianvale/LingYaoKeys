using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using WpfApp.Services.Utils;
using WpfApp.Services.Config;

namespace WpfApp.ViewModels;

public class AboutViewModel : ViewModelBase
{
    private readonly SerilogManager _logger = SerilogManager.Instance;
    private readonly string _githubUrl = AppConfigService.Config.AppInfo.GitHubUrl;
    private readonly string _websiteUrl = "https://cassianvale.github.io/LingYaoKeys/";
    private ICommand? _openGitHubCommand;
    private ICommand? _openWebsiteCommand;

    public ICommand OpenGitHubCommand => _openGitHubCommand ??= new RelayCommand(OpenGitHub);
    public ICommand OpenWebsiteCommand => _openWebsiteCommand ??= new RelayCommand(OpenWebsite);

    private void OpenWebsite()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _websiteUrl,
                UseShellExecute = true
            };
            Process.Start(psi);
            _logger.Debug("成功打开官网链接");
        }
        catch (Exception ex)
        {
            _logger.Error("打开官网链接失败", ex);
            System.Windows.MessageBox.Show(
                "无法打开官网链接，请检查网络连接后重试。",
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
}