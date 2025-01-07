using System;
using System.IO;
using System.Threading.Tasks;
using WpfApp.Services;
using System.Reflection;
using System.Windows;
using Forms = System.Windows.Forms;

namespace WpfApp
{
    public partial class App : System.Windows.Application
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        public static LyKeysService LyKeysDriver { get; private set; }
        public static ConfigService ConfigService { get; private set; } = new ConfigService();
        public static AudioService AudioService { get; private set; } = new AudioService();
        private bool _isShuttingDown;
        private readonly string _userDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".lingyao"
        );

        public App()
        {
            // 设置高DPI模式
            if (Environment.OSVersion.Version >= new Version(6, 3))
            {
                try
                {
                    System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
                }
                catch (Exception ex)
                {
                    _logger?.Error("设置高DPI模式失败", ex);
                }
            }
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 预初始化WebView2环境
            _ = Services.WebView2Service.Instance.GetEnvironmentAsync();
            
            try
            {
                // 确保用户数据目录存在
                Directory.CreateDirectory(_userDataPath);

                // 1. 首先初始化配置服务
                AppConfigService.Initialize(_userDataPath);

                // 2. 然后初始化日志系统
                _logger.SetBaseDirectory(_userDataPath);
                _logger.Initialize(AppConfigService.Config.Logging);

                // 3. 设置配置变更监听
                AppConfigService.ConfigChanged += (sender, args) =>
                {
                    if (args.Section == "Logging")
                    {
                        _logger.UpdateLoggerConfig(args.Config.Logging);
                    }
                };

                // 注册全局异常处理
                AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                {
                    var ex = args.ExceptionObject as Exception;
                    _logger.Error("未处理的异常，程序发生致命错误", ex);
                };

                Current.DispatcherUnhandledException += (s, args) =>
                {
                    _logger.Error("UI线程异常，界面线程发生异常", args.Exception);
                    args.Handled = true;
                };

                TaskScheduler.UnobservedTaskException += (s, args) =>
                {
                    _logger.Error("任务异常, 异步任务发生异常", args.Exception);
                    args.SetObserved();
                };

                _logger.Debug($"日志系统初始化完成, 配置: Level={AppConfigService.Config.Logging.LogLevel}, MaxSize={AppConfigService.Config.Logging.FileSettings.MaxFileSize}MB");
                _logger.Debug("应用程序启动...");

                // 获取临时目录路径
                string tempPath = Path.Combine(Path.GetTempPath(), "LingYaoKeys");
                string driverPath = Path.Combine(tempPath, "Resource", "lykeysdll");
                string driverFile = Path.Combine(driverPath, "lykeys.sys");
                string dllFile = Path.Combine(driverPath, "lykeysdll.dll");
                string catFile = Path.Combine(driverPath, "lykeys.cat");

                try
                {
                    // 确保临时目录存在
                    Directory.CreateDirectory(driverPath);

                    // 从嵌入式资源提取驱动文件
                    ExtractEmbeddedResource("WpfApp.Resource.lykeysdll.lykeys.sys", driverFile);
                    ExtractEmbeddedResource("WpfApp.Resource.lykeysdll.lykeysdll.dll", dllFile);
                    ExtractEmbeddedResource("WpfApp.Resource.lykeysdll.lykeys.cat", catFile);

                    _logger.Debug($"驱动文件已提取到临时目录: {driverPath}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"提取驱动文件失败: {ex.Message}", ex);
                    System.Windows.MessageBox.Show("提取驱动文件失败，请确保程序完整性", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Current.Shutdown();
                    return;
                }

                if (!File.Exists(driverFile) || !File.Exists(dllFile))
                {
                    _logger.Error($"驱动文件丢失: {driverFile} 或 {dllFile}");
                    System.Windows.MessageBox.Show("驱动文件丢失，请确保程序完整性", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Current.Shutdown();
                    return;
                }

                _logger.Debug($"驱动文件目录: {driverPath}");
                _logger.Debug($"驱动文件: {driverFile}");
                _logger.Debug($"DLL文件: {dllFile}");
                _logger.Debug($"CAT文件: {catFile}");

                // 初始化驱动管理器
                try 
                {
                    // 初始化 LyKeys 服务
                    LyKeysDriver = new LyKeysService();
                    if (!await LyKeysDriver.InitializeAsync(driverFile))
                    {
                        _logger.Error("驱动加载失败，无法加载LyKeys驱动文件");
                        System.Windows.MessageBox.Show("驱动加载失败，请检查是否以管理员身份运行程序", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        Current.Shutdown();
                        return;
                    }
                    _logger.Debug("驱动初始化成功");
                }
                catch (Exception ex)
                {
                    _logger.Error($"驱动初始化失败: {ex.Message}", ex);
                    System.Windows.MessageBox.Show("驱动初始化失败，请检查是否以管理员身份运行程序", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Current.Shutdown();
                    return;
                }

                // 初始化音频服务
                AudioService = new AudioService();
                _logger.Debug("音频服务初始化完成");

                // 创建并显示主窗口
                _logger.Debug("创建主窗口...");
                var mainWindow = new MainWindow();
                mainWindow.Show();
                _logger.Debug("主窗口已显示");

                var hotkeyService = new HotkeyService(mainWindow, LyKeysDriver);
                _logger.Debug("热键服务初始化完成");

                // 注册应用程序退出事件
                Exit += OnApplicationExit;

                // 添加程序退出时的清理逻辑
                Current.Exit += (s, e) =>
                {
                    try
                    {
                        // 清理临时文件
                        if (Directory.Exists(tempPath))
                        {
                            Directory.Delete(tempPath, true);
                            _logger.Debug("临时文件已清理");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"清理临时文件失败: {ex.Message}", ex);
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.Error("启动失败, 应用程序启动过程中发生异常", ex);
                System.Windows.MessageBox.Show($"程序启动异常：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        /// <summary>
        /// 从嵌入式资源提取文件
        /// </summary>
        /// <param name="resourceName">资源名称</param>
        /// <param name="outputPath">输出路径</param>
        private void ExtractEmbeddedResource(string resourceName, string outputPath)
        {
            try
            {
                using (Stream? stream = GetType().Assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        throw new FileNotFoundException($"找不到嵌入式资源: {resourceName}");
                    }

                    using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"提取资源文件失败: {resourceName}", ex);
            }
        }

        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;

            try
            {
                _logger.Debug("开始释放应用程序资源...");
                
                // 确保所有服务都被释放
                if (LyKeysDriver != null)
                {
                    LyKeysDriver.Dispose();
                    LyKeysDriver = null;
                }
                
                if (AudioService != null)
                {
                    AudioService.Dispose();
                    AudioService = null;
                }

                // 清理配置服务
                ConfigService = null;
                
                // 最后释放日志服务
                _logger.Debug("应用程序资源已释放");
                _logger.Debug("=================================================");
                _logger.Dispose();
            }
            catch (Exception ex)
            {
                // 在这里我们只能尝试直接写入到调试输出，因为日志系统可能已经关闭
                System.Diagnostics.Debug.WriteLine($"应用程序退出异常: {ex.Message}");
            }
            finally
            {
                // 确保进程退出
                Environment.Exit(0);
            }
        }
    }
}
