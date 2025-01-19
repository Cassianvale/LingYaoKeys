using System;
using System.Windows;
using Microsoft.Win32;

namespace LyKeys
{
    public partial class MainWindow : Window
    {
        private readonly DriverService _driverService;

        public MainWindow(DriverService driverService)
        {
            InitializeComponent();
            _driverService = driverService ?? throw new ArgumentNullException(nameof(driverService));
            _driverService.StatusChanged += DriverService_StatusChanged;
            
            UpdateButtonStates();
        }

        private void DriverService_StatusChanged(object sender, string message)
        {
            // 确保在UI线程上更新界面
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                LogTextBox.ScrollToEnd();
                UpdateButtonStates();
            });
        }

        private void UpdateButtonStates()
        {
            bool hasDriverPath = !string.IsNullOrEmpty(SysPathText.Text);
            bool hasDllPath = !string.IsNullOrEmpty(DllPathText.Text);
            
            InstallButton.IsEnabled = !_driverService.IsDriverInstalled && hasDriverPath && hasDllPath;
            UninstallButton.IsEnabled = _driverService.IsDriverInstalled;
            
            SelectSysButton.IsEnabled = !_driverService.IsDriverInstalled;
            SelectDllButton.IsEnabled = !_driverService.IsDriverInstalled;

            if (hasDriverPath && hasDllPath)
            {
                UpdateDriverPaths();
            }
        }

        private void SelectSysButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "驱动文件|*.sys|所有文件|*.*",
                Title = "选择驱动文件"
            };

            if (dialog.ShowDialog() == true)
            {
                SysPathText.Text = dialog.FileName;
                UpdateButtonStates();
            }
        }

        private void SelectDllButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "DLL文件|*.dll|所有文件|*.*",
                Title = "选择DLL文件"
            };

            if (dialog.ShowDialog() == true)
            {
                DllPathText.Text = dialog.FileName;
                UpdateButtonStates();
            }
        }

        private void UpdateDriverPaths()
        {
            if (string.IsNullOrEmpty(SysPathText.Text) || string.IsNullOrEmpty(DllPathText.Text))
            {
                return;
            }

            try
            {
                _driverService.SetDriverPaths(SysPathText.Text, DllPathText.Text);
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show($"设置驱动路径失败：{ex.Message}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InstallButton.IsEnabled = false;
                await _driverService.InitializeDriverAsync();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("需要管理员权限才能安装驱动。\n请以管理员身份运行程序。", 
                    "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"安装驱动时发生错误：{ex.Message}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                UpdateButtonStates();
            }
        }

        private async void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UninstallButton.IsEnabled = false;
                await _driverService.UninstallDriverAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"卸载驱动时发生错误：{ex.Message}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                UpdateButtonStates();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _driverService.Dispose();
            base.OnClosed(e);
        }
    }
}
