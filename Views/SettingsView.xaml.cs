using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WpfApp.Services;
using System.Diagnostics;
using System.Linq;

namespace WpfApp.Views
{
    public partial class SettingsView : Page
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly string _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            ".lykeys");
        private const int MAX_BACKUP_FILES = 5; // 最大保留的备份文件数量

        public SettingsView()
        {
            InitializeComponent();
            CleanupOldBackups(); // 在初始化时清理旧的备份文件
        }

        private void CleanupOldBackups()
        {
            try
            {
                var backupFiles = Directory.GetFiles(_configDir, "AppConfig_backup_*.json")
                    .OrderByDescending(f => f)
                    .Skip(MAX_BACKUP_FILES)
                    .ToList();

                foreach (var file in backupFiles)
                {
                    try
                    {
                        File.Delete(file);
                        _logger.Debug($"已删除旧的备份文件: {file}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"删除备份文件失败: {file}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("清理备份文件失败", ex);
            }
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
                    string appConfigPath = Path.Combine(_configDir, "AppConfig.json");
                    
                    // 确保目录存在
                    Directory.CreateDirectory(_configDir);

                    // 备份当前配置
                    if (File.Exists(appConfigPath))
                    {
                        string backupPath = Path.Combine(
                            _configDir, 
                            $"AppConfig_backup_{DateTime.Now:yyyyMMddHHmmss}.json");
                        File.Copy(appConfigPath, backupPath);
                        
                        // 清理旧的备份文件
                        CleanupOldBackups();
                    }

                    // 导入新配置
                    File.WriteAllText(appConfigPath, configContent);
                    
                    var result = System.Windows.MessageBox.Show("配置导入成功，需要重启程序才能生效。是否立即重启？", 
                        "重启提示", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Question);
                        
                    if (result == MessageBoxResult.Yes)
                    {
                        // 获取当前程序路径
                        string appPath = Process.GetCurrentProcess().MainModule.FileName;
                        
                        // 启动新的进程
                        ProcessStartInfo startInfo = new ProcessStartInfo(appPath);
                        startInfo.UseShellExecute = true; // 使用操作系统shell启动
                        startInfo.Verb = "runas"; // 以管理员权限运行
                        
                        Process.Start(startInfo);
                        
                        // 关闭当前程序
                        System.Windows.Application.Current.Shutdown();
                    }
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
                    string appConfigPath = Path.Combine(_configDir, "AppConfig.json");
                    if (!File.Exists(appConfigPath))
                    {
                        throw new FileNotFoundException("配置文件不存在", appConfigPath);
                    }
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