using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace WpfApp.Services
{
    public class LogManager
    {
        private static readonly Lazy<LogManager> _instance = new(() => new LogManager());
        private readonly object _lockObject = new();
        private readonly string _logFilePath;
        private StreamWriter? _logWriter;

        public static LogManager Instance => _instance.Value;

        private LogManager()
        {
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logDirectory);
            _logFilePath = Path.Combine(logDirectory, $"DDDriver_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            InitializeLogWriter();
        }

        // 初始化日志写入器
        private void InitializeLogWriter()
        {
            try
            {
                _logWriter = new StreamWriter(_logFilePath, true, Encoding.UTF8)
                {
                    AutoFlush = true
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化日志写入器失败: {ex.Message}");
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
            var threadId = Thread.CurrentThread.ManagedThreadId;
            
            var formattedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{threadId:D2}] [{category}] {message}";
            
            lock (_lockObject)
            {
                try
                {
                    Debug.WriteLine(formattedMessage);
                    _logWriter?.WriteLine(formattedMessage);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"写入日志失败: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            _logWriter?.Dispose();
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