using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using WpfApp.Services;
using WpfApp.ViewModels;

namespace WpfApp
{
    public partial class App : Application
    {
        private readonly LogManager _logger = LogManager.Instance;
        public static DDDriverService DDDriver { get; private set; } = new DDDriverService();
        public static ConfigService ConfigService { get; private set; } = new ConfigService();
        public static AudioService AudioService { get; private set; } = new AudioService();
        private bool _isShuttingDown;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppConfigService.Initialize();  // 确保配置只加载一次

            try
            {
                _logger.LogDebug("App", $"日志系统初始化完成, 配置: " +
                    $"Level={AppConfigService.Config.Logging.LogLevel}, " +
                    $"MaxSize={AppConfigService.Config.Logging.FileSettings.MaxFileSize/1024/1024}MB");
                
                _logger.LogInitialization("App", "应用程序启动...");
                
                // 初始化驱动服务
                DDDriver = new DDDriverService();
                
                // 获取当前程序路径
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                string dllPath = Path.Combine(currentDir, "dd", Environment.Is64BitProcess ? "ddx64.dll" : "ddx32.dll");
                
                _logger.LogInitialization("App", $"使用驱动: {dllPath}");

                // 检查文件是否存在
                if (!File.Exists(dllPath))
                {
                    _logger.LogError("App", $"找不到驱动文件{dllPath}");
                    MessageBox.Show($"找不到驱动文件：{dllPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }

                // 加载驱动
                _logger.LogInitialization("App", "开始加载驱动...");
                if (!DDDriver.LoadDllFile(dllPath))
                {
                    _logger.LogError("App", "驱动加载失败");
                    Shutdown();
                    return;
                }

                // 在创建主窗口之前初始化 AudioService
                AudioService = new AudioService();
                
                // 创建并显示主窗口
                _logger.LogInitialization("App", "创建主窗口...");
                var mainWindow = new MainWindow();
                mainWindow.Show();
                _logger.LogInitialization("App", "主窗口已显示");

                // 创建 HotkeyService 并设置到 DDDriver
                var hotkeyService = new HotkeyService(mainWindow, DDDriver);
                

                // 注册应用程序退出事件
                Exit += OnApplicationExit;
            }
            catch (Exception ex)
            {
                _logger.LogError("App", "应用程序启动异常", ex);
                MessageBox.Show($"程序启动异常：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;

            try
            {
                _logger.LogInitialization("App", "开始释放应用程序资源...");
                DDDriver.Dispose();
                AudioService.Dispose();
                _logger.LogInitialization("App", "应用程序资源已释放");
            }
            catch (Exception ex)
            {
                _logger.LogError("App", "应用程序退出异常", ex);
            }
        }
    }
}
