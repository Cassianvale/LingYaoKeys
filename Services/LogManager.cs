using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Linq;
using WpfApp.Models;
using Newtonsoft.Json;
using System.Collections.Generic;


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
        private volatile bool _isInitialized;
        private readonly object _initLock = new object();
        private string _baseDirectory;
        private const string LOG_FOLDER_NAME = "logs";

        public static LogManager Instance => _instance.Value;

        private LogManager()
        {
            // 初始化默认配置
            _config = new AppConfig 
            { 
                Logging = new LoggingConfig 
                { 
                    Enabled = false,
                    LogLevel = "Debug",
                    FileSettings = new LogFileSettings(),
                    Categories = new LogCategories(),
                    ExcludedTags = new List<string> { "ControlStyles" }
                }
            };
            
            // 默认使用用户目录下的.lingyao
            _baseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".lingyao"
            );
            
            InitializeLogManager();
        }

        private void InitializeLogManager()
        {
            if (!_isInitialized)
            {
                lock (_initLock)
                {
                    if (!_isInitialized)
                    {
                        _isInitialized = true;
                    }
                }
            }
        }

        private void UpdateConfig()
        {
            lock (_lockObject)
            {
                if (!_isInitialized) return;

                var config = AppConfigService.Config;
                if (config?.Logging != null)
                {
                    _config = config;
                }
                
                string logDirectory = Path.Combine(_baseDirectory, LOG_FOLDER_NAME);
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // 初始化时清理旧日志
                CleanupOldLogFiles(logDirectory);
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

                    // 如果当前日期的日志文件已存在，就继续使用
                    string logDirectory = Path.Combine(_baseDirectory, LOG_FOLDER_NAME);
                    string currentDatePrefix = DateTime.Now.ToString("yyyyMMdd");
                    var existingLogFile = Directory.GetFiles(logDirectory, $"LingYaoKeys_{currentDatePrefix}*.log")
                        .OrderByDescending(f => f)
                        .FirstOrDefault();

                    if (existingLogFile != null)
                    {
                        _currentLogFile = existingLogFile;
                        _logWriter = new StreamWriter(_currentLogFile, true, Encoding.UTF8)
                        {
                            AutoFlush = true
                        };
                        _currentFileSize = new FileInfo(_currentLogFile).Length;
                    }
                    else
                    {
                        _currentLogFile = GetNewLogFilePath(logDirectory);
                        _logWriter = new StreamWriter(_currentLogFile, true, Encoding.UTF8)
                        {
                            AutoFlush = true
                        };
                        _currentFileSize = 0;
                    }

                    Debug.WriteLine($"使用日志文件: {_currentLogFile}");
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
                // 首先检查是否在排除列表中
                if (_config.Logging.ExcludedTags.Contains(category))
                {
                    return false;
                }

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
                return !_config.Logging.ExcludedTags.Contains(category); // 配置解析失败时仍然检查排除标签
            }
        }

        // 记录按键操作
        // public void LogKeyOperation(DDKeyCode keyCode, bool isKeyDown, int returnValue)
        // {
        //     var message = $"[按键操作] {keyCode} ({(int)keyCode}) | 状态: {(isKeyDown ? "按下" : "释放")} | 返回值: {returnValue}";
        //     WriteLog(LogLevel.Debug, "KeyOperation", message);
        // }

        // 记录序列事件
        public void LogSequenceEvent(string eventType, string details)
        {
            var message = $"[序列事件] {eventType} | {details}";
            WriteLog(LogLevel.Info, "Sequence", message);
        }

        // 记录性能指标
        // public void LogPerformanceMetrics(PerformanceMetrics metrics)
        // {
        //     var sb = new StringBuilder();
        //     sb.AppendLine($"[性能指标] {DateTime.Now:HH:mm:ss.fff}");
        //     sb.AppendLine($"├─ 按键时间: {metrics.AverageKeyPressTime:F2}ms");
        //     sb.AppendLine($"├─ 按键间隔: {metrics.AverageKeyInterval:F2}ms");
        //     sb.AppendLine($"├─ 执行时间: {metrics.TotalExecutionTime.TotalSeconds:F2}s");
        //     sb.AppendLine($"├─ 按键次数: {metrics.TotalKeyPresses}");
        //     sb.AppendLine($"└─ 当前序列: {metrics.CurrentSequence}");
        //     WriteLog(LogLevel.Info, "Performance", sb.ToString());
        // }

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
            if (!_isInitialized)
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
                try
                {
                    // 检查是否需要按日期滚动
                    if (_currentLogFile != null && ShouldRollByDate())
                    {
                        RollLogFile();
                        return;
                    }

                    // 检查文件大小
                    long maxSizeInBytes = _config.Logging.FileSettings.MaxFileSize * 1024 * 1024;
                    if (_currentFileSize >= maxSizeInBytes)
                    {
                        RollLogFile();
                    }

                    // 清理过期的日志文件
                    CleanupOldLogFiles();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"检查日志文件大小时发生错误: {ex.Message}");
                }
            }
        }

        private bool ShouldRollByDate()
        {
            if (string.IsNullOrEmpty(_currentLogFile)) return false;

            var currentDate = DateTime.Now.Date;
            var fileDate = File.GetCreationTime(_currentLogFile).Date;

            return _config.Logging.FileSettings.RollingInterval.ToLower() == "day" 
                && currentDate > fileDate;
        }

        private void RollLogFile()
        {
            _logWriter?.Dispose();
            string logDirectory = Path.GetDirectoryName(_currentLogFile)!;
            _currentLogFile = GetNewLogFilePath(logDirectory);
            EnsureLogWriterInitialized();
            _currentFileSize = 0;
            
            // 检查并删除超出数量的旧文件
            EnforceFileCountLimit(logDirectory);
        }

        private void CleanupOldLogFiles()
        {
            try
            {
                string logDirectory = Path.GetDirectoryName(_currentLogFile)!;
                var retainDate = DateTime.Now.AddDays(-_config.Logging.FileSettings.RetainDays);
                
                var files = Directory.GetFiles(logDirectory, "LingYaoKeys_*.log")
                    .Select(f => new FileInfo(f))
                    .Where(f => f.CreationTime < retainDate);

                foreach (var file in files)
                {
                    if (file.FullName != _currentLogFile)
                    {
                        File.Delete(file.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清理旧日志文件失败: {ex.Message}");
            }
        }

        private void EnforceFileCountLimit(string logDirectory)
        {
            try
            {
                var files = Directory.GetFiles(logDirectory, "LingYaoKeys_*.log")
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
                var files = Directory.GetFiles(directory, "LingYaoKeys_*.log");
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
            // 取消订阅配置变更事件
            AppConfigService.ConfigChanged -= OnConfigChanged;
            
            _logWriter?.Dispose();
            
            // 在程序退出时清理日志文件
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
            string fileName = $"LingYaoKeys_{timestamp}.log";
            return Path.Combine(logDirectory, fileName);
        }

        public void SetBaseDirectory(string path)
        {
            lock (_lockObject)
            {
                _baseDirectory = path;
                string logDirectory = Path.Combine(_baseDirectory, LOG_FOLDER_NAME);
                
                if (_isInitialized && _config.Logging.Enabled)
                {
                    Directory.CreateDirectory(logDirectory);
                    CleanupOldLogFiles(logDirectory);
                }
                
                // 重新初始化日志写入器
                if (_isInitialized)
                {
                    _logWriter?.Dispose();
                    _logWriter = null;
                    _currentLogFile = string.Empty;
                    _currentFileSize = 0;
                    
                    if (_config.Logging.Enabled)
                    {
                        EnsureLogWriterInitialized();
                    }
                }
            }
        }

        private void OnConfigChanged(object? sender, AppConfig newConfig)
        {
            lock (_lockObject)
            {
                bool loggingStateChanged = _config.Logging.Enabled != newConfig.Logging.Enabled;
                bool logLevelChanged = _config.Logging.LogLevel != newConfig.Logging.LogLevel;
                bool fileSettingsChanged = !JsonConvert.SerializeObject(_config.Logging.FileSettings)
                    .Equals(JsonConvert.SerializeObject(newConfig.Logging.FileSettings));

                _config = newConfig;

                // 只有在日志配置实质性变化时才重新初始化
                if (loggingStateChanged || logLevelChanged || fileSettingsChanged)
                {
                    _logWriter?.Dispose();
                    _logWriter = null;
                    _currentLogFile = string.Empty;
                    _currentFileSize = 0;
                    
                    if (_config.Logging.Enabled)
                    {
                        string logDirectory = Path.Combine(_baseDirectory, "logs");
                        Directory.CreateDirectory(logDirectory);
                        EnsureLogWriterInitialized();
                    }
                }
            }
        }

        // 新增：延迟订阅方法
        public void InitializeConfigSubscription()
        {
            // 订阅配置变更事件
            AppConfigService.ConfigChanged += OnConfigChanged;
            // 立即更新一次配置
            UpdateConfig();
        }

        private void CleanupOldLogFiles(string logDirectory)
        {
            try
            {
                // 按保留天数清理
                var retainDate = DateTime.Now.AddDays(-_config.Logging.FileSettings.RetainDays);
                var oldFiles = Directory.GetFiles(logDirectory, "LingYaoKeys_*.log")
                    .Select(f => new FileInfo(f))
                    .Where(f => f.CreationTime < retainDate);

                foreach (var file in oldFiles)
                {
                    if (file.FullName != _currentLogFile)
                    {
                        File.Delete(file.FullName);
                        Debug.WriteLine($"删除过期日志文件: {file.Name}");
                    }
                }

                // 按文件数量清理
                var excessFiles = Directory.GetFiles(logDirectory, "LingYaoKeys_*.log")
                    .OrderByDescending(f => new FileInfo(f).CreationTime)
                    .Skip(_config.Logging.FileSettings.MaxFileCount);

                foreach (var file in excessFiles)
                {
                    if (file != _currentLogFile)
                    {
                        File.Delete(file);
                        Debug.WriteLine($"删除超出数量限制的日志文件: {Path.GetFileName(file)}");
                    }
                }

                // 清理空文件
                var emptyFiles = Directory.GetFiles(logDirectory, "LingYaoKeys_*.log")
                    .Select(f => new FileInfo(f))
                    .Where(f => f.Length == 0 && f.FullName != _currentLogFile);

                foreach (var file in emptyFiles)
                {
                    File.Delete(file.FullName);
                    Debug.WriteLine($"删除空日志文件: {file.Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清理日志文件失败: {ex.Message}");
            }
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