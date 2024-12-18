using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using WpfApp.Models;
namespace WpfApp.Services
{
    public class LogManager
    {
        private static readonly Lazy<LogManager> _instance = new(() => new LogManager());
        private readonly object _lockObject = new();
        private string _currentLogFile = string.Empty;
        private StreamWriter? _logWriter;
        private long _currentFileSize;
        private readonly object _sizeLock = new object();
        private AppConfig _config;
        private bool _initialized;

        public static LogManager Instance => _instance.Value;

        private LogManager()
        {
            // 初始化默认配置
            _config = new AppConfig 
            { 
                Logging = new LoggingConfig 
                { 
                    Enabled = true,
                    LogLevel = "Debug",
                    FileSettings = new FileSettings(),
                    Categories = new LogCategories()
                }
            };
            
            InitializeLogManager();
        }

        private void InitializeLogManager()
        {
            try 
            {
                // 只创建目录,不创建文件
                string logDirectory = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, 
                    _config.Logging.FileSettings.Directory);
                    
                Directory.CreateDirectory(logDirectory);

                // 延迟加载配置
                Task.Run(() => 
                {
                    try
                    {
                        Thread.Sleep(100);
                        UpdateConfig();
                        _initialized = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"更新日志配置失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化日志管理器失败: {ex.Message}");
            }
        }

        private void UpdateConfig()
        {
            try 
            {
                var newConfig = AppConfigService.Config;
                if (newConfig?.Logging != null)
                {
                    _config = newConfig;
                }
                
                string newLogDirectory = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    _config.Logging.FileSettings.Directory);
                    
                if (!Directory.Exists(newLogDirectory))
                {
                    Directory.CreateDirectory(newLogDirectory);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新配置失败: {ex.Message}");
            }
        }

        private void EnsureLogWriterInitialized()
        {
            if (_logWriter != null) return;
            
            lock (_lockObject)
            {
                if (_logWriter != null) return;
                
                try
                {
                    if (!_config.Logging.Enabled) return;
                    
                    string logDirectory = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        _config.Logging.FileSettings.Directory);
                        
                    _currentLogFile = GetNewLogFilePath(logDirectory);
                    _logWriter = new StreamWriter(_currentLogFile, true, Encoding.UTF8)
                    {
                        AutoFlush = true
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"初始化日志写入器失败: {ex.Message}");
                }
            }
        }

        private bool ShouldLog(LogLevel level, string category)
        {
            try
            {
                // 检查日志级别
                var configLevel = Enum.Parse<LogLevel>(_config.Logging.LogLevel);
                if (level < configLevel) return false;

                // 检查分类开关
                return category switch
                {
                    "KeyOperation" => _config.Logging.Categories.KeyOperation,
                    "Performance" => _config.Logging.Categories.Performance,
                    "Driver" => _config.Logging.Categories.Driver,
                    "Config" => _config.Logging.Categories.Config,
                    _ => true
                };
            }
            catch
            {
                return true; // 配置解析失败时允许所有日志
            }
        }

        // 记录按键操作
        public void LogKeyOperation(DDKeyCode keyCode, bool isKeyDown, int returnValue)
        {
            var message = $"[按键操作] {keyCode} ({(int)keyCode}) | 状态: {(isKeyDown ? "按下" : "释放")} | 返回值: {returnValue}";
            WriteLog(LogLevel.Debug, "KeyOperation", message);
        }

        // 记录序列事件
        public void LogSequenceEvent(string eventType, string details)
        {
            var message = $"[序列事件] {eventType} | {details}";
            WriteLog(LogLevel.Info, "Sequence", message);
        }

        // 记录性能指标
        public void LogPerformanceMetrics(PerformanceMetrics metrics)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[性能指标] {DateTime.Now:HH:mm:ss.fff}");
            sb.AppendLine($"├─ 按键时间: {metrics.AverageKeyPressTime:F2}ms");
            sb.AppendLine($"├─ 按键间隔: {metrics.AverageKeyInterval:F2}ms");
            sb.AppendLine($"├─ 执行时间: {metrics.TotalExecutionTime.TotalSeconds:F2}s");
            sb.AppendLine($"├─ 按键次数: {metrics.TotalKeyPresses}");
            sb.AppendLine($"└─ 当前序列: {metrics.CurrentSequence}");
            
            WriteLog(LogLevel.Info, "Performance", sb.ToString());
        }

        // 记录错误
        public void LogError(string source, string message, Exception? ex = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[错误] {source}");
            sb.AppendLine($"├─ 消息: {message}");
            if (ex != null)
            {
                sb.AppendLine($"├─ 异常: {ex.GetType().Name}");
                sb.AppendLine($"└─ 详情: {ex.Message}");
            }
            
            WriteLog(LogLevel.Error, source, sb.ToString());
        }

        // 记录驱动事件
        public void LogDriverEvent(string eventType, string details)
        {
            var message = $"[驱动事件] {eventType} | {details}";
            WriteLog(LogLevel.Info, "Driver", message);
        }

        // 记录调试信息
        public void LogDebug(string category, string message)
        {
            WriteLog(LogLevel.Debug, category, message);
        }

        // 记录警告信息
        public void LogWarning(string source, string message)
        {
            WriteLog(LogLevel.Warning, source, message);
        }

        // 记录初始化信息
        public void LogInitialization(string status, string details)
        {
            var message = $"[初始化] {status} | {details}";
            WriteLog(LogLevel.Info, "Initialization", message);
        }

        // 写入日志
        private void WriteLog(LogLevel level, string category, string message)
        {
            if (!_initialized)
            {
                Debug.WriteLine(FormatLogMessage(level, category, message));
                return;
            }

            try
            {
                if (!_config.Logging.Enabled) return;
                if (!ShouldLog(level, category)) return;

                var formattedMessage = FormatLogMessage(level, category, message);
                
                lock (_lockObject)
                {
                    try
                    {
                        Debug.WriteLine(formattedMessage);
                        
                        // 确保写入器已初始化
                        EnsureLogWriterInitialized();
                        
                        if (_logWriter != null)
                        {
                            _logWriter.WriteLine(formattedMessage);
                            _currentFileSize += Encoding.UTF8.GetByteCount(formattedMessage + Environment.NewLine);
                            CheckLogFileSize();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"写入日志失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"日志处理失败: {ex.Message}");
            }
        }

        private void CheckLogFileSize()
        {
            if (!_config.Logging.Enabled) return;

            lock (_sizeLock)
            {
                // 将MB转换为字节进行比较
                long maxSizeInBytes = _config.Logging.FileSettings.MaxFileSize * 1024 * 1024;
                if (_currentFileSize >= maxSizeInBytes)
                {
                    _logWriter?.Dispose();
                    string logDirectory = Path.GetDirectoryName(_currentLogFile)!;
                    _currentLogFile = GetNewLogFilePath(logDirectory);
                    EnsureLogWriterInitialized();
                    _currentFileSize = 0;
                    
                    // 检查并删除超出数量的旧文件
                    EnforceFileCountLimit(logDirectory);
                }
            }
        }

        private void EnforceFileCountLimit(string logDirectory)
        {
            try
            {
                var files = Directory.GetFiles(logDirectory, "DDDriver_*.log")
                                   .OrderByDescending(f => new FileInfo(f).CreationTime)
                                   .Skip(_config.Logging.FileSettings.MaxFileCount);
                                   
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"强制执行文件数量限制失败: {ex.Message}");
            }
        }

        // 清理空日志文件
        private void CleanupEmptyLogs(string directory)
        {
            try
            {
                var files = Directory.GetFiles(directory, "DDDriver_*.log");
                foreach (var file in files)
                {
                    var fi = new FileInfo(file);
                    if (fi.Length == 0 && fi.FullName != _currentLogFile)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清理空日志文件失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _logWriter?.Dispose();
            
            // 在程序退出时清理空日志文件
            if (!string.IsNullOrEmpty(_currentLogFile))
            {
                string? directory = Path.GetDirectoryName(_currentLogFile);
                if (directory != null)
                {
                    CleanupEmptyLogs(directory);
                }
            }
        }

        private string FormatLogMessage(LogLevel level, string category, string message)
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{threadId:D2}] [{category}] {message}";
        }

        private string GetNewLogFilePath(string logDirectory)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"DDDriver_{timestamp}.log";
            return Path.Combine(logDirectory, fileName);
        }
    }

    public class PerformanceMetrics
    {
        public double AverageKeyPressTime { get; set; }
        public double AverageKeyInterval { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public int TotalKeyPresses { get; set; }
        public string CurrentSequence { get; set; } = string.Empty;
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
} 