using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using WpfApp.Services;
using WpfApp.ViewModels;
using System.Runtime.InteropServices;

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

            try
            {
                System.Diagnostics.Debug.WriteLine("应用程序启动...");
                
                // 初始化驱动服务
                DDDriver = new DDDriverService();
                
                // 获取当前程序路径
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                string dllPath = System.IO.Path.Combine(currentDir, "dd", Environment.Is64BitProcess ? "ddx64.dll" : "ddx32.dll");
                
                System.Diagnostics.Debug.WriteLine($"使用驱动: {dllPath}");

                // 检查文件是否存在
                if (!System.IO.File.Exists(dllPath))
                {
                    System.Diagnostics.Debug.WriteLine($"驱动文件不存在: {dllPath}");
                    MessageBox.Show($"找不到驱动文件：{dllPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }

                // 加载驱动
                System.Diagnostics.Debug.WriteLine("开始加载驱动...");
                if (!DDDriver.LoadDllFile(dllPath))
                {
                    System.Diagnostics.Debug.WriteLine("驱动加载失败");
                    Shutdown();
                    return;
                }

                // 创建并显示主窗口
                System.Diagnostics.Debug.WriteLine("创建主窗口...");
                var mainWindow = new MainWindow();
                mainWindow.Show();
                System.Diagnostics.Debug.WriteLine("主窗口已显示");

                // 注册应用程序退出事件
                Exit += OnApplicationExit;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用程序启动异常: {ex}");
                MessageBox.Show($"程序启动异常：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
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
                // 清理驱动资源
                System.Diagnostics.Debug.WriteLine("开始释放驱动资源...");
                DDDriver.Dispose();
                System.Diagnostics.Debug.WriteLine("驱动资源已释放");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用程序退出时发生错误：{ex.Message}");
            }
        }
    }
}
