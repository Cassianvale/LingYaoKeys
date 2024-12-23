using System;
using System.IO;
using System.Text;
using WpfApp.Services;
using Markdig;
using Microsoft.Web.WebView2.Core;
using System.Windows.Input;
using System.Diagnostics;
using WpfApp.Commands;

namespace WpfApp.ViewModels
{
    public class AboutViewModel : ViewModelBase
    {
        private readonly LogManager _logger = LogManager.Instance;
        private string _readmeContent = string.Empty;
        private string _htmlContent = string.Empty;
        private const string GITHUB_URL = "https://github.com/LingYaoKe/jx3wpftools";
        private ICommand? _openGitHubCommand;

        public string HtmlContent
        {
            get => _htmlContent;
            private set => SetProperty(ref _htmlContent, value);
        }

        public ICommand OpenGitHubCommand => _openGitHubCommand ??= new RelayCommand(OpenGitHub);

        public AboutViewModel()
        {
            LoadReadmeContent();
        }

        private void LoadReadmeContent()
        {
            try
            {
                string markdown;
                // 从嵌入式资源读取README.md
                using (var stream = GetType().Assembly.GetManifestResourceStream("WpfApp.README.md"))
                {
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        markdown = reader.ReadToEnd();
                        _logger.LogDebug("About", $"成功读取README文件，内容长度: {markdown.Length}");
                    }
                    else
                    {
                        _logger.LogWarning("About", "未找到嵌入式README.md资源");
                        markdown = "# 灵曜按键\n\n欢迎使用灵曜按键！";
                    }
                }

                // 转换Markdown为HTML
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .UseEmojiAndSmiley()
                    .Build();
                
                string html = Markdown.ToHtml(markdown, pipeline);

                // 添加CSS样式和编码声明
                html = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='utf-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1'>
                        <style>
                            :root {{
                                color-scheme: light dark;
                            }}
                            body {{
                                font-family: 'Microsoft YaHei UI', system-ui, sans-serif;
                                line-height: 1.6;
                                color: #333;
                                max-width: 100%;
                                margin: 0;
                                padding: 0 10px;
                                -webkit-font-smoothing: antialiased;
                            }}
                            h1, h2, h3, h4, h5, h6 {{
                                margin-top: 24px;
                                margin-bottom: 16px;
                                font-weight: 600;
                                line-height: 1.25;
                            }}
                            h1 {{ font-size: 2em; margin-top: 0; }}
                            h2 {{ font-size: 1.5em; }}
                            p {{ margin-bottom: 16px; }}
                            ul, ol {{ padding-left: 2em; }}
                            li {{ margin: 0.25em 0; }}
                            code {{
                                background-color: #f6f8fa;
                                padding: 0.2em 0.4em;
                                border-radius: 3px;
                                font-family: 'Cascadia Code', Consolas, monospace;
                            }}
                            pre code {{
                                display: block;
                                padding: 16px;
                                overflow-x: auto;
                            }}
                            @media (prefers-color-scheme: dark) {{
                                body {{ 
                                    color: #e4e4e4;
                                    background: transparent;
                                }}
                                code {{
                                    background-color: #2d2d2d;
                                }}
                            }}
                        </style>
                    </head>
                    <body>
                        {html}
                    </body>
                    </html>";

                HtmlContent = html;
            }
            catch (Exception ex)
            {
                _logger.LogError("About", "加载README.md内容失败", ex);
                HtmlContent = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='utf-8'>
                        <style>
                            body {{ font-family: 'Microsoft YaHei UI', sans-serif; padding: 20px; }}
                        </style>
                    </head>
                    <body>
                        <h1>加载失败</h1>
                        <p>错误信息: {ex.Message}</p>
                    </body>
                    </html>";
            }
        }

        private string GetProjectRootPath()
        {
            try
            {
                // 使用 AppContext.BaseDirectory 替代 Assembly.Location
                string? currentDir = AppContext.BaseDirectory;
                while (!string.IsNullOrEmpty(currentDir))
                {
                    _logger.LogDebug("About", $"正在检查目录: {currentDir}");
                    
                    // 检查是否存在 WpfApp.csproj 或 README.md
                    if (File.Exists(Path.Combine(currentDir, "WpfApp.csproj")) || 
                        File.Exists(Path.Combine(currentDir, "README.md")))
                    {
                        _logger.LogDebug("About", $"找到项目根目录: {currentDir}");
                        return currentDir;
                    }
                    
                    // 向上一级目录查找
                    currentDir = Path.GetDirectoryName(currentDir);
                }

                // 如果找不到，尝试使用开发环境下的目录
                string devPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\"));
                if (File.Exists(Path.Combine(devPath, "README.md")))
                {
                    _logger.LogDebug("About", $"使用开发环境目录: {devPath}");
                    return devPath;
                }

                // 如果都找不到，返回当前目录
                _logger.LogWarning("About", "未找到项目根目录，使用当前目录");
                return AppContext.BaseDirectory;
            }
            catch (Exception ex)
            {
                _logger.LogError("About", "获取项目根目录失败", ex);
                return AppContext.BaseDirectory;
            }
        }

        public void HandleWebViewError(CoreWebView2WebErrorStatus status)
        {
            _logger.LogError("About", $"WebView2导航错误: {status}");
            LoadErrorContent($"加载失败: {status}");
        }

        public void HandleWebViewError(Exception ex)
        {
            _logger.LogError("About", "WebView2初始化错误", ex);
            LoadErrorContent($"初始化失败: {ex.Message}");
        }

        private void LoadErrorContent(string message)
        {
            HtmlContent = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{ 
                            font-family: 'Microsoft YaHei UI', sans-serif; 
                            padding: 20px;
                            color: #e4e4e4;
                        }}
                    </style>
                </head>
                <body>
                    <h1>加载失败</h1>
                    <p>{message}</p>
                </body>
                </html>";
        }

        private void OpenGitHub()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = GITHUB_URL,
                    UseShellExecute = true
                };
                Process.Start(psi);
                _logger.LogDebug("About", "成功打开GitHub仓库链接");
            }
            catch (Exception ex)
            {
                _logger.LogError("About", "打开GitHub仓库链接失败", ex);
                // 可以考虑添加一个错误提示
            }
        }
    }
} 