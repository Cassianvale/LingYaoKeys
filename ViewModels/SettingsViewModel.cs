using System;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using WpfApp.Services;
using WpfApp.Services.Config;
using WpfApp.Services.Utils;

namespace WpfApp.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly UpdateService? _updateService;
        private readonly ConfigService _configService;
        private bool _isCheckingUpdate;
        private string _updateStatus = "检查更新";

        public string UpdateStatus
        {
            get => _updateStatus;
            set => SetProperty(ref _updateStatus, value);
        }

        public ICommand CheckUpdateCommand { get; }
        public ICommand ImportConfigCommand { get; }
        public ICommand ExportConfigCommand { get; }

        public SettingsViewModel(IConfiguration configuration)
        {
            _configService = new ConfigService();
            try
            {
                _updateService = new UpdateService(configuration);
            }
            catch (Exception ex)
            {
                _logger.Warning($"初始化更新服务失败: {ex.Message}");
                _updateService = null;
            }
            
            CheckUpdateCommand = new RelayCommand(async () => await CheckForUpdateAsync(), () => !_isCheckingUpdate);
            ImportConfigCommand = new RelayCommand(ImportConfig);
            ExportConfigCommand = new RelayCommand(ExportConfig);
        }

        private async Task CheckForUpdateAsync()
        {
            if (_updateService == null)
            {
                System.Windows.MessageBox.Show("更新服务未初始化，无法检查更新", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                UpdateStatus = "服务未初始化";
                return;
            }

            try
            {
                _isCheckingUpdate = true;
                UpdateStatus = "正在检查...";

                var updateInfo = await _updateService.CheckForUpdateAsync();
                if (updateInfo != null)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"发现新版本：{updateInfo.LatestVersion}\n当前版本：{updateInfo.CurrentVersion}\n\n更新内容：\n{updateInfo.ReleaseNotes}\n\n是否立即更新？",
                        "发现新版本",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        _updateService.OpenDownloadPage(updateInfo.DownloadUrl);
                    }
                    UpdateStatus = "有新版本";
                }
                else
                {
                    System.Windows.MessageBox.Show("当前已是最新版本", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateStatus = "已是最新";
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("无法连接到更新服务器"))
            {
                _logger.Error("检查更新失败：网络连接问题", ex);
                System.Windows.MessageBox.Show("无法连接到更新服务器，请检查网络连接", "网络错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                UpdateStatus = "网络错误";
            }
            catch (Exception ex)
            {
                _logger.Error("检查更新失败", ex);
                System.Windows.MessageBox.Show($"检查更新失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus = "检查失败";
            }
            finally
            {
                _isCheckingUpdate = false;
            }
        }

        private void ImportConfig()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择配置文件",
                    Filter = "JSON 文件 (*.json)|*.json",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                if (dialog.ShowDialog() == true)
                {
                    _configService.ImportConfig(dialog.FileName);
                    System.Windows.MessageBox.Show("配置导入成功，需要重启程序才能生效。是否立即重启？", 
                        "重启提示", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Question);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("导入配置失败", ex);
                System.Windows.MessageBox.Show($"导入配置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportConfig()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "保存配置文件",
                    Filter = "JSON 文件 (*.json)|*.json",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    FileName = $"AppConfig_{DateTime.Now:yyyyMMdd}.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    _configService.ExportConfig(dialog.FileName);
                    System.Windows.MessageBox.Show("配置导出成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("导出配置失败", ex);
                System.Windows.MessageBox.Show($"导出配置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 