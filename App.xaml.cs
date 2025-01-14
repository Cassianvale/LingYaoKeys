using System;
using System.IO;
using System.Threading.Tasks;
using WpfApp.Services;
using WpfApp.Services.Config;
using System.Reflection;
using System.Windows;
using Forms = System.Windows.Forms;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace WpfApp
{
    public partial class App : System.Windows.Application
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        public static LyKeysService LyKeysDriver { get; private set; }
        public static ConfigService ConfigService { get; private set; }
        public static AudioService AudioService { get; private set; }
        private bool _isShuttingDown;
        private readonly string _userDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".lykeys"
        );

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);
        private static EventHandler _handler;

        private enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        public static IConfiguration Configuration { get; private set; }

        // 清理级别
        // Normal 级别：基本资源清理（适用于正常应用退出）
        // Complete 级别：完整清理（包括驱动服务，适用于进程强制终止）
        private enum CleanupLevel
        {
            Normal,     // 普通清理
            Complete    // 完整清理（包括驱动服务）
        }

        public App()
        {
            // 注册进程退出事件处理
            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);

            // 注册应用程序域未处理异常处理程序
            AppDomain.CurrentDomain.ProcessExit += (s, e) => CleanupServices();
            
            // 注册任务调度器未观察到的异常处理程序
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                CleanupServices();
                e.SetObserved();
            };

            // 注册应用程序退出事件
            this.Exit += OnApplicationExit;
        }

        private bool Handler(CtrlType sig)
        {
            switch (sig)
            {
                case CtrlType.CTRL_BREAK_EVENT:
                case CtrlType.CTRL_C_EVENT:
                case CtrlType.CTRL_LOGOFF_EVENT:
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                    CleanupServices();
                    return false;
                default:
                    return false;
            }
        }

        private void Cleanup(CleanupLevel level = CleanupLevel.Normal)
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;

            try
            {
                _logger.Debug($"开始清理服务... 清理级别: {level}");

                // 清理 WebView2 环境
                try
                {
                    var webView2Service = Services.WebView2Service.Instance;
                    webView2Service.Dispose();
                    _logger.Debug("WebView2 服务已清理");
                }
                catch (Exception ex)
                {
                    _logger.Error("清理 WebView2 服务失败", ex);
                }

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

                // 如果是完整清理，则尝试停止和删除驱动服务
                if (level == CleanupLevel.Complete)
                {
                    try
                    {
                        using (Process p = new Process())
                        {
                            p.StartInfo.FileName = "sc.exe";
                            p.StartInfo.Arguments = "stop lykeys";
                            p.StartInfo.UseShellExecute = false;
                            p.StartInfo.CreateNoWindow = true;
                            p.Start();
                            p.WaitForExit(100);
                        }

                        using (Process p = new Process())
                        {
                            p.StartInfo.FileName = "sc.exe";
                            p.StartInfo.Arguments = "delete lykeys";
                            p.StartInfo.UseShellExecute = false;
                            p.StartInfo.CreateNoWindow = true;
                            p.Start();
                            p.WaitForExit(100);
                        }
                        _logger.Debug("驱动服务已停止并删除");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("清理驱动服务失败", ex);
                    }
                }

                // 最后释放日志服务
                _logger.Debug("服务清理完成");
                _logger.Debug("=================================================");
                _logger.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清理服务异常: {ex.Message}");
            }
            finally
            {
                Environment.Exit(0);
            }
        }

        private void CleanupServices()
        {
            Cleanup(CleanupLevel.Complete);
        }

        private async Task<bool> CheckServiceExistsAsync(string serviceName)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = $"query {serviceName}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.Error($"检查服务状态失败: {ex.Message}", ex);
                return false;
            }
        }

        private async Task<bool> StopServiceAsync(string serviceName)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = $"stop {serviceName}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.Error($"停止服务失败: {ex.Message}", ex);
                return false;
            }
        }

        private async Task<bool> DeleteServiceAsync(string serviceName)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = $"delete {serviceName}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.Error($"删除服务失败: {ex.Message}", ex);
                return false;
            }
        }

        private async Task CleanupExistingServiceAsync()
        {
            const string serviceName = "lykeys";
            
            try
            {
                if (await CheckServiceExistsAsync(serviceName))
                {
                    _logger.Debug("检测到已存在的lykeys服务，开始清理...");
                    
                    // 尝试停止服务
                    if (await StopServiceAsync(serviceName))
                    {
                        _logger.Debug("成功停止lykeys服务");
                        // 等待服务完全停止
                        await Task.Delay(1000);
                    }
                    
                    // 尝试删除服务
                    if (await DeleteServiceAsync(serviceName))
                    {
                        _logger.Debug("成功删除lykeys服务");
                        // 等待服务完全删除
                        await Task.Delay(1000);
                    }

                    // 尝试结束所有相关进程
                    try
                    {
                        var processes = Process.GetProcessesByName("lykeys");
                        foreach (var proc in processes)
                        {
                            try
                            {
                                proc.Kill();
                                proc.WaitForExit(1000);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"结束进程失败: {ex.Message}", ex);
                            }
                            finally
                            {
                                proc.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"清理相关进程失败: {ex.Message}", ex);
                    }

                    // 等待一段时间，确保所有资源都被释放
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("清理已存在的服务失败", ex);
                throw;
            }
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 在应用程序启动前设置高 DPI 模式
                if (Environment.OSVersion.Version >= new Version(6, 3))
                {
                    Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
                    _logger.Debug("已设置高DPI模式");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("设置高DPI模式失败", ex);
            }

            // 初始化配置
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{GetEnvironment()}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();

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
                _logger.Initialize(AppConfigService.Config.Debug);

                // 3. 设置配置变更监听
                AppConfigService.ConfigChanged += (sender, args) =>
                {
                    if (args.Section == "Debug")
                    {
                        _logger.UpdateLoggerConfig(args.Config.Debug);
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

                _logger.Debug($"日志系统初始化完成, 配置: Level={AppConfigService.Config.Debug.LogLevel}, MaxSize={AppConfigService.Config.Debug.FileSettings.MaxFileSize}MB");
                _logger.Debug("应用程序启动...");

                // 获取用户目录路径
                string driverPath = Path.Combine(_userDataPath, "Resource", "lykeysdll");
                string driverFile = Path.Combine(driverPath, "lykeys.sys");
                string dllFile = Path.Combine(driverPath, "lykeysdll.dll");
                string catFile = Path.Combine(driverPath, "lykeys.cat");

                // 新增：清理已存在的服务
                try
                {
                    await CleanupExistingServiceAsync();
                }
                catch (Exception ex)
                {
                    _logger.Error("清理已存在的服务失败", ex);
                    System.Windows.MessageBox.Show("清理已存在的服务失败，请手动停止并删除lykeys服务后重试", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Current.Shutdown();
                    return;
                }

                try
                {
                    // 确保驱动目录存在
                    Directory.CreateDirectory(driverPath);

                    // 检查驱动文件是否存在且完整
                    bool needExtractFiles = !File.Exists(driverFile) || 
                                          !File.Exists(dllFile) || 
                                          !File.Exists(catFile);

                    if (needExtractFiles)
                    {
                        _logger.Debug("驱动文件不存在或不完整，开始提取...");
                        // 从嵌入式资源提取驱动文件
                        ExtractEmbeddedResource("WpfApp.Resource.lykeysdll.lykeys.sys", driverFile);
                        ExtractEmbeddedResource("WpfApp.Resource.lykeysdll.lykeysdll.dll", dllFile);
                        ExtractEmbeddedResource("WpfApp.Resource.lykeysdll.lykeys.cat", catFile);
                        _logger.Debug($"驱动文件已提取到用户数据目录: {driverPath}");
                    }
                    else
                    {
                        _logger.Debug("驱动文件已存在且完整，跳过提取步骤");
                    }
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
                _logger.Debug("初始化音频服务");
                AudioService = new AudioService();

                // 创建并显示主窗口
                _logger.Debug("创建主窗口...");
                var mainWindow = new MainWindow();
                mainWindow.Show();

                // 初始化热键服务
                _logger.Debug("初始化热键服务");
                var hotkeyService = new HotkeyService(mainWindow, LyKeysDriver);

                // 注册应用程序退出事件
                Exit += OnApplicationExit;

                // 添加程序退出时的清理逻辑
                Current.Exit += (s, e) =>
                {
                    try
                    {
                        _logger.Debug("程序正常退出");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"程序退出时发生错误: {ex.Message}", ex);
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

                    // 使用 FileShare.Delete 允许其他进程删除文件
                    using (FileStream fileStream = new FileStream(
                        outputPath, 
                        FileMode.Create, 
                        FileAccess.Write, 
                        FileShare.Delete))
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
            Cleanup(CleanupLevel.Normal);
        }

        private string GetEnvironment()
        {
            #if DEBUG
                return "Development";
            #else
                return "Production";
            #endif
        }
    }
}
