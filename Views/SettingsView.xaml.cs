using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WpfApp.Services;

namespace WpfApp.Views
{
    public partial class SettingsView : Page
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;

        public SettingsView()
        {
            InitializeComponent();
        }

        private void ImportConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择配置文件",
                    Filter = "JSON 文件 (*.json)|*.json",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    DereferenceLinks = true,
                    CheckFileExists = true,
                    CheckPathExists = true,
                    ValidateNames = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string configContent = File.ReadAllText(openFileDialog.FileName);
                    string appConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppConfig.json");
                    
                    // 备份当前配置
                    if (File.Exists(appConfigPath))
                    {
                        string backupPath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory, 
                            $"AppConfig_backup_{DateTime.Now:yyyyMMddHHmmss}.json");
                        File.Copy(appConfigPath, backupPath);
                    }

                    // 导入新配置
                    File.WriteAllText(appConfigPath, configContent);
                    System.Windows.MessageBox.Show("配置导入成功，重启程序后生效", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("导入配置文件失败", ex);
                System.Windows.MessageBox.Show($"导入配置文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "保存配置文件",
                    Filter = "JSON 文件 (*.json)|*.json",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    FileName = $"AppConfig_{DateTime.Now:yyyyMMdd}.json",
                    DereferenceLinks = true,
                    ValidateNames = true,
                    OverwritePrompt = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string appConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppConfig.json");
                    File.Copy(appConfigPath, saveFileDialog.FileName, true);
                    System.Windows.MessageBox.Show("配置导出成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("导出配置文件失败", ex);
                System.Windows.MessageBox.Show($"导出配置文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 