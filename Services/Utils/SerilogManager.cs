using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WpfApp.Services.Utils;
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

        public void Initialize(DebugConfig debugConfig)
        {
            if (_initialized) return;

            try
            {
                // 如果调试模式未启用且日志未启用，则不初始化日志系统
                if (!debugConfig.IsDebugMode && !debugConfig.EnableLogging)
                {
                    return;
                }

                // 如果是调试模式，显示控制台窗口
                if (debugConfig.IsDebugMode)
                {
                    ConsoleManager.Show();
                }

                // 1. 设置日志级别
                var logLevel = debugConfig.LogLevel.ToLower() switch
                {
                    "debug" => LogEventLevel.Debug,
                    "information" => LogEventLevel.Information,
                    "warning" => LogEventLevel.Warning,
                    "error" => LogEventLevel.Error,
                    _ => LogEventLevel.Debug // 默认使用 Debug 级别
                };

                // 2. 创建日志过滤器
                var logFilter = new LoggingLevelSwitch(logLevel);

                // 3. 配置日志输出
                var loggerConfig = new LoggerConfiguration()
                    .MinimumLevel.ControlledBy(logFilter)
                    .Enrich.WithThreadId()
                    .Enrich.FromLogContext()
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

                        // 如果是调试模式，不过滤任何日志
                        if (debugConfig.IsDebugMode)
                            return true;

                        // 1. 检查是否在排除的源上下文列表中
                        if (debugConfig.ExcludedSources?.Any(source => 
                            sourceContext.Contains(source, StringComparison.OrdinalIgnoreCase)) == true)
                            return false;

                        // 2. 检查是否在排除的方法列表中
                        if (debugConfig.ExcludedMethods?.Any(method => 
                            callerMember.Equals(method, StringComparison.OrdinalIgnoreCase)) == true)
                            return false;

                        // 3. 检查是否匹配排除的消息模式
                        if (debugConfig.ExcludedPatterns?.Any(pattern =>
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
                        if (debugConfig.ExcludedTags?.Any(tag => 
                            messageTemplate.Contains($"[{tag}]", StringComparison.OrdinalIgnoreCase)) == true)
                            return false;

                        return true;
                    });

                // 4. 添加输出目标
                const string outputTemplate = 
                    "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] [{ThreadId}] [{SourceContext}.{CallerMember}:{LineNumber}] {Message}{NewLine}{Exception}";

                // 调试模式下始终输出到控制台
                if (debugConfig.IsDebugMode)
                {
                    loggerConfig = loggerConfig
                        .WriteTo.Console(
                            outputTemplate: outputTemplate,
                            theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code,
                            restrictedToMinimumLevel: LogEventLevel.Debug
                        );
                    
                    // 输出一条测试日志
                    Console.WriteLine("Serilog 控制台输出已初始化");
                }

                // 如果启用了日志记录，添加其他输出目标
                if (debugConfig.EnableLogging)
                {
                    // Debug输出
                    loggerConfig = loggerConfig.WriteTo.Debug(
                        outputTemplate: outputTemplate,
                        restrictedToMinimumLevel: LogEventLevel.Debug
                    );

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
                        if (debugConfig.FileSettings.RetainDays > 0)
                        {
                            try
                            {
                                var cutoff = DateTime.Now.AddDays(-debugConfig.FileSettings.RetainDays);
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
                            retainedFileCountLimit: debugConfig.FileSettings.MaxFileCount,
                            fileSizeLimitBytes: debugConfig.FileSettings.MaxFileSize * 1024 * 1024,
                            rollOnFileSizeLimit: true,
                            outputTemplate: fileOutputTemplate);
                    }
                }

                // 5. 创建日志实例
                _logger = loggerConfig.CreateLogger();
                _initialized = true;

                // 输出初始化成功日志
                if (debugConfig.IsDebugMode)
                {
                    _logger.Debug("Serilog 日志系统初始化成功");
                    _logger.Debug($"日志级别: {logLevel}, 调试模式: {debugConfig.IsDebugMode}, 日志记录: {debugConfig.EnableLogging}");
                }
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

        public void UpdateLoggerConfig(DebugConfig debugConfig)
        {
            lock (_lock)
            {
                if (_disposed) return;
                Initialize(debugConfig);
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