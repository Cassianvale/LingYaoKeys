using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using WpfApp.Services;

namespace WpfApp
{
    public partial class App : Application
    {
        private const string DD_FOLDER = "dd";
        private const string DD_X64 = "ddx64.dll";
        private const string DD_X86 = "ddx32.dll";

        public static DDDriverService DDDriver { get; private set; } = new DDDriverService();
        public static ConfigService ConfigService { get; private set; } = new ConfigService();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 只创建和显示主窗口，驱动初始化移到窗口Loaded事件中
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        // 修改为public以便MainWindow可以调用
        public static bool InitializeDriver()
        {
            // 检查系统架构
            string dllName = GetDriverFileName();
            if (string.IsNullOrEmpty(dllName))
            {
                MessageBox.Show("不支持的处理器架构", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // 构建驱动路径
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dllPath = Path.Combine(baseDir, DD_FOLDER, dllName);

            // 检查驱动文件
            try 
            {
                CheckDriverFile(dllPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"驱动文件检查失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // 同步加载驱动
            if (!DDDriver.LoadDllFile(dllPath))
            {
                return false;
            }

            // 修改事件注册方式
            if (Application.Current is App app)
            {
                app.Exit += app.OnApplicationExit;
            }
            
            return true;
        }

        private static string GetDriverFileName()
        {
            return IntPtr.Size switch
            {
                8 => DD_X64,  // 64位系统
                4 => DD_X86,  // 32位系统
                _ => string.Empty
            };
        }

        private static void CheckDriverFile(string fullPath)
        {
            string ddFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DD_FOLDER);
            
            if (!Directory.Exists(ddFolder))
            {
                throw new DirectoryNotFoundException($"找不到驱动目录：{ddFolder}");
            }

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"找不到驱动文件：{fullPath}");
            }
        }

        private void ShowError(string message, Exception ex)
        {
            string fullMessage = $"{message}\n\n详细信息：{ex.Message}";
            
            if (ex.InnerException != null)
            {
                fullMessage += $"\n\n内部错误：{ex.InnerException.Message}";
            }

            MessageBox.Show(
                fullMessage,
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            try
            {
                // 清理资源
                DDDriver.Dispose();
            }
            catch (Exception ex)
            {
                // 记录错误但不显示，因为应用程序正在退出
                System.Diagnostics.Debug.WriteLine($"应用程序退出时发生错误：{ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            // 可以添加其他退出时的清理代码
        }
    }
}
