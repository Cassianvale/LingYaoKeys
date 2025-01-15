using System;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using WpfApp.Services;
using WpfApp.Services.Config;
using WpfApp.Services.Utils;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace WpfApp.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly UpdateService? _updateService;
        private readonly ConfigService _configService;
        private bool _isCheckingUpdate;
        private string _updateStatus = "检查更新";
        private string _debugModeStatus = "调试模式关闭";

        public string UpdateStatus
        {
            get => _updateStatus;
            set => SetProperty(ref _updateStatus, value);
        }

        public string DebugModeStatus
        {
            get => _debugModeStatus;
            set => SetProperty(ref _debugModeStatus, value);
        }

        public ICommand CheckUpdateCommand { get; }
        public ICommand ImportConfigCommand { get; }
        public ICommand ExportConfigCommand { get; }
        public ICommand ToggleDebugModeCommand { get; }

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
            ToggleDebugModeCommand = new RelayCommand(ToggleDebugMode);

            // 初始化调试模式状态
            UpdateDebugModeStatus();
        }

        private void UpdateDebugModeStatus()
        {
            var config = AppConfigService.Config;
            _debugModeStatus = config.Debug.IsDebugMode ? "🟢 调试模式：已开启" : "⭕ 调试模式：已关闭";
        }

        private void ToggleDebugMode()
        {
            try
            {
                var currentDebugMode = AppConfigService.Config.Debug.IsDebugMode;
                
                AppConfigService.UpdateConfig(config =>
                {
                    config.Debug.IsDebugMode = !currentDebugMode;
                    config.Debug.UpdateDebugState();
                });

                // 更新控制台显示状态
                if (AppConfigService.Config.Debug.IsDebugMode)
                {
                    ConsoleManager.Show();
                }
                else
                {
                    ConsoleManager.Hide();
                }

                UpdateDebugModeStatus();

                var result = MessageBox.Show(
                    "调试模式设置已更改，需要重启程序才能生效。是否立即重启？",
                    "重启提示",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    RestartApplication();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("切换调试模式失败", ex);
                MessageBox.Show($"切换调试模式失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestartApplication()
        {
            try
            {
                string appPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName 
                    ?? throw new InvalidOperationException("无法获取应用程序路径");

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = appPath,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                System.Diagnostics.Process.Start(startInfo);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _logger.Error("重启应用程序失败", ex);
                MessageBox.Show($"重启应用程序失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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