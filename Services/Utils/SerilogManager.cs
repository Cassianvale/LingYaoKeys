using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using WpfApp.Services.Models;

namespace WpfApp.Services
{
    public class SerilogManager : ILogger, IDisposable
    {
        private static readonly Lazy<SerilogManager> _instance = new(() => new SerilogManager());
        private ILogger? _logger;
        private string _baseDirectory = string.Empty;
        private bool _disposed;
        private readonly object _lock = new();
        private bool _initialized;

        public static SerilogManager Instance => _instance.Value;

        public void Initialize(LoggingConfig loggingConfig)
        {
            if (_initialized) return;

            try
            {
                // 1. 设置日志级别
                var logLevel = loggingConfig.LogLevel.ToLower() switch
                {
                    "debug" => LogEventLevel.Debug,
                    "information" => LogEventLevel.Information,
                    "warning" => LogEventLevel.Warning,
                    "error" => LogEventLevel.Error,
                    _ => LogEventLevel.Information
                };

                // 2. 创建日志过滤器
                var logFilter = new LoggingLevelSwitch(logLevel);

                // 3. 配置日志输出
                var loggerConfig = new LoggerConfiguration()
                    .MinimumLevel.ControlledBy(logFilter)
                    .Enrich.WithThreadId()
                    .Filter.ByIncludingOnly(evt =>
                    {
                        // 获取源上下文和消息模板
                        var messageTemplate = evt.MessageTemplate.Text;
                        var sourceContext = evt.Properties.ContainsKey("SourceContext") 
                            ? evt.Properties["SourceContext"].ToString() 
                            : string.Empty;
                        var callerMember = evt.Properties.ContainsKey("CallerMember")
                            ? evt.Properties["CallerMember"].ToString()
                            : string.Empty;

                        // 1. 检查是否在排除的源上下文列表中
                        if (loggingConfig.ExcludedSources?.Any(source => 
                            sourceContext.Contains(source, StringComparison.OrdinalIgnoreCase)) == true)
                            return false;

                        // 2. 检查是否在排除的方法列表中
                        if (loggingConfig.ExcludedMethods?.Any(method => 
                            callerMember.Equals(method, StringComparison.OrdinalIgnoreCase)) == true)
                            return false;

                        // 3. 检查是否匹配排除的消息模式
                        if (loggingConfig.ExcludedPatterns?.Any(pattern =>
                        {
                            // 将通配符模式转换为正则表达式
                            var regex = new System.Text.RegularExpressions.Regex(
                                "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                                    .Replace("\\*", ".*") + "$",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            return regex.IsMatch(messageTemplate);
                        }) == true)
                            return false;

                        // 4. 检查是否在排除的标签列表中
                        if (loggingConfig.ExcludedTags?.Any(tag => 
                            messageTemplate.Contains($"[{tag}]", StringComparison.OrdinalIgnoreCase)) == true)
                            return false;

                        return true;
                    });

                // 4. 添加输出目标
                if (loggingConfig.Enabled)
                {
                    const string outputTemplate = 
                        "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}.{CallerMember}:{LineNumber}] {Message}{NewLine}{Exception}";

                    // Debug输出
                    loggerConfig = loggerConfig.WriteTo.Debug(outputTemplate: outputTemplate);

                    // 控制台输出
                    loggerConfig = loggerConfig.WriteTo.Console(outputTemplate: outputTemplate);

                    // 文件输出
                    if (!string.IsNullOrEmpty(_baseDirectory))
                    {
                        const string LOG_DIRECTORY = "logs";
                        var logPath = Path.Combine(_baseDirectory, LOG_DIRECTORY, "app.log");
                        var logDir = Path.GetDirectoryName(logPath);

                        // 确保日志目录存在
                        if (!string.IsNullOrEmpty(logDir))
                            Directory.CreateDirectory(logDir);

                        // 清理旧日志
                        if (loggingConfig.FileSettings.RetainDays > 0)
                        {
                            try
                            {
                                var cutoff = DateTime.Now.AddDays(-loggingConfig.FileSettings.RetainDays);
                                if (Directory.Exists(logDir))
                                {
                                    foreach (var file in Directory.GetFiles(logDir, "app*.log"))
                                    {
                                        if (File.GetLastWriteTime(file) < cutoff)
                                        {
                                            try
                                            {
                                                File.Delete(file);
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"删除旧日志文件失败: {ex.Message}");
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"清理旧日志文件失败: {ex.Message}");
                            }
                        }

                        // 设置文件输出
                        const string fileOutputTemplate = 
                            "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}.{CallerMember}:{LineNumber}] {Message}{NewLine}{Exception}";
                            
                        loggerConfig = loggerConfig.WriteTo.File(
                            logPath,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: loggingConfig.FileSettings.MaxFileCount,
                            fileSizeLimitBytes: loggingConfig.FileSettings.MaxFileSize * 1024 * 1024,
                            rollOnFileSizeLimit: true,
                            outputTemplate: fileOutputTemplate);
                    }
                }

                // 5. 创建日志实例
                _logger = loggerConfig.CreateLogger();
                _initialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化日志系统失败: {ex}");
                throw;
            }
        }

        public void SetBaseDirectory(string path)
        {
            _baseDirectory = path;
        }

        public void UpdateLoggerConfig(LoggingConfig loggingConfig)
        {
            lock (_lock)
            {
                if (_disposed) return;
                Initialize(loggingConfig);
            }
        }

        #region ILogger Implementation

        public void Write(LogEvent logEvent)
        {
            if (_disposed || _logger == null) return;
            _logger.Write(logEvent);
        }

        public bool BindMessageTemplate(string? messageTemplate, object?[]? propertyValues, out MessageTemplate? parsedTemplate, out IEnumerable<LogEventProperty>? boundProperties)
        {
            if (_logger is ILogger concreteLogger)
            {
                return concreteLogger.BindMessageTemplate(messageTemplate, propertyValues, out parsedTemplate, out boundProperties);
            }
            
            parsedTemplate = null;
            boundProperties = Array.Empty<LogEventProperty>();
            return false;
        }

        public bool BindProperty(string? propertyName, object? value, bool destructureObjects, out LogEventProperty? property)
        {
            if (_logger is ILogger concreteLogger)
            {
                return concreteLogger.BindProperty(propertyName, value, destructureObjects, out property);
            }
            
            property = null;
            return false;
        }

        #endregion

        #region Logging Methods

        public void Debug(string message, 
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (_disposed || _logger == null) return;
            _logger
                .ForContext("CallerMember", memberName)
                .ForContext("SourceContext", Path.GetFileNameWithoutExtension(sourceFilePath))
                .ForContext("LineNumber", sourceLineNumber)
                .Debug(message);
        }

        public void Info(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (_disposed || _logger == null) return;
            _logger
                .ForContext("CallerMember", memberName)
                .ForContext("SourceContext", Path.GetFileNameWithoutExtension(sourceFilePath))
                .ForContext("LineNumber", sourceLineNumber)
                .Information(message);
        }

        public void Warning(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (_disposed || _logger == null) return;
            _logger
                .ForContext("CallerMember", memberName)
                .ForContext("SourceContext", Path.GetFileNameWithoutExtension(sourceFilePath))
                .ForContext("LineNumber", sourceLineNumber)
                .Warning(message);
        }

        public void Error(string message, Exception? ex = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (_disposed || _logger == null) return;
            _logger
                .ForContext("CallerMember", memberName)
                .ForContext("SourceContext", Path.GetFileNameWithoutExtension(sourceFilePath))
                .ForContext("LineNumber", sourceLineNumber)
                .Error(ex, message);
        }

        public void SequenceEvent(string message, string? details = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (_disposed || _logger == null) return;
            _logger
                .ForContext("CallerMember", memberName)
                .ForContext("SourceContext", Path.GetFileNameWithoutExtension(sourceFilePath))
                .ForContext("LineNumber", sourceLineNumber)
                .Information(details == null ? $"[Sequence] {message}" : $"[Sequence] {message} - {details}");
        }

        public void DriverEvent(string message, string? details = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (_disposed || _logger == null) return;
            _logger
                .ForContext("CallerMember", memberName)
                .ForContext("SourceContext", Path.GetFileNameWithoutExtension(sourceFilePath))
                .ForContext("LineNumber", sourceLineNumber)
                .Information(details == null ? $"[Driver] {message}" : $"[Driver] {message} - {details}");
        }

        public void InitLog(string message, string? details = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (_disposed || _logger == null) return;
            _logger
                .ForContext("CallerMember", memberName)
                .ForContext("SourceContext", Path.GetFileNameWithoutExtension(sourceFilePath))
                .ForContext("LineNumber", sourceLineNumber)
                .Information(details == null ? $"[Init] {message}" : $"[Init] {message} - {details}");
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                lock (_lock)
                {
                    if (_logger is IDisposable disposableLogger)
                    {
                        disposableLogger.Dispose();
                    }
                    _logger = null;
                }
            }

            _disposed = true;
        }

        #endregion
    }
} 