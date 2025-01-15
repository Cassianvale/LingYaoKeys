using System;
using System.IO;
using System.Text;
using System.Windows;
using WpfApp.Services;
using Markdig;
using Microsoft.Web.WebView2.Core;
using System.Windows.Input;
using System.Diagnostics;
using WpfApp.Services.Utils;
using WpfApp.Views;
using WpfApp.Services.Models;
using WpfApp.Services.Config;

namespace WpfApp.ViewModels
{
    public class AboutViewModel : ViewModelBase
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private string _htmlContent = string.Empty;
        private readonly string _githubUrl = AppConfigService.Config.AppInfo.GitHubUrl;
        private ICommand? _openGitHubCommand;
        private ICommand? _showQRCodeCommand;
        private CoreWebView2? _webView;

        public string HtmlContent
        {
            get => _htmlContent;
            private set => SetProperty(ref _htmlContent, value);
        }

        public ICommand OpenGitHubCommand => _openGitHubCommand ??= new RelayCommand(OpenGitHub);
        public ICommand ShowQRCodeCommand => _showQRCodeCommand ??= new RelayCommand(ShowQRCode);

        public AboutViewModel()
        {
            // 构造函数中不再直接加载内容
        }

        public void Initialize(CoreWebView2 webView)
        {
            _webView = webView;
            // 初始化时预加载HTML头部
            LoadHtmlHeader();
            // 然后开始加载内容
            LoadReadmeContent();
        }

        private void LoadHtmlHeader()
        {
            if (_webView == null) return;

            string htmlHeader = $@"
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
                            font-family: -apple-system,BlinkMacSystemFont,Segoe UI,Helvetica,Arial,sans-serif,Apple Color Emoji,Segoe UI Emoji;
                            line-height: 1.6;
                            color: #24292f;
                            width: 100%;
                            max-width: 100%;
                            margin: 0 auto;
                            padding: 16px 24px;
                            word-wrap: break-word;
                            box-sizing: border-box;
                            -webkit-font-smoothing: antialiased;
                        }}
                        .markdown-body {{
                            width: 100%;
                            max-width: 1012px;
                            margin: 0 auto;
                        }}
                        h1, h2, h3, h4, h5, h6 {{
                            margin-top: 24px;
                            margin-bottom: 16px;
                            font-weight: 600;
                            line-height: 1.25;
                            padding-bottom: .3em;
                            border-bottom: 1px solid #eaecef;
                        }}
                        h1 {{ font-size: 2em; margin-top: 0; }}
                        h2 {{ font-size: 1.5em; }}
                        h3 {{ font-size: 1.25em; }}
                        p {{ margin-bottom: 16px; }}
                        a {{ color: #0969da; text-decoration: none; }}
                        a:hover {{ text-decoration: underline; }}
                        ul, ol {{ padding-left: 2em; margin-bottom: 16px; }}
                        li {{ margin: 0.25em 0; }}
                        li + li {{ margin-top: 0.25em; }}
                        code {{
                            padding: .2em .4em;
                            margin: 0;
                            font-size: 85%;
                            font-family: ui-monospace,SFMono-Regular,SF Mono,Menlo,Consolas,Liberation Mono,monospace;
                            background-color: rgba(175,184,193,0.2);
                            border-radius: 6px;
                        }}
                        pre {{
                            padding: 16px;
                            overflow: auto;
                            font-size: 85%;
                            line-height: 1.45;
                            background-color: #f6f8fa;
                            border-radius: 6px;
                            margin-bottom: 16px;
                        }}
                        pre code {{
                            padding: 0;
                            margin: 0;
                            word-break: normal;
                            white-space: pre;
                            background: transparent;
                            border: 0;
                            display: inline;
                            overflow: visible;
                            line-height: inherit;
                        }}
                        blockquote {{
                            padding: 0 1em;
                            color: #57606a;
                            border-left: .25em solid #d0d7de;
                            margin: 0 0 16px;
                        }}
                        table {{
                            border-spacing: 0;
                            border-collapse: collapse;
                            margin-bottom: 16px;
                            display: block;
                            width: max-content;
                            max-width: 100%;
                            overflow: auto;
                        }}
                        table th, table td {{
                            padding: 6px 13px;
                            border: 1px solid #d0d7de;
                        }}
                        table th {{
                            font-weight: 600;
                            background-color: #f6f8fa;
                        }}
                        img {{
                            max-width: 100%;
                            border-style: none;
                            box-sizing: content-box;
                            background-color: #ffffff;
                        }}
                        hr {{
                            height: .25em;
                            padding: 0;
                            margin: 24px 0;
                            background-color: #d0d7de;
                            border: 0;
                        }}
                        @media (prefers-color-scheme: dark) {{
                            body {{ 
                                color: #c9d1d9;
                                background: #0d1117;
                            }}
                            h1, h2, h3, h4, h5, h6 {{
                                border-bottom: 1px solid #21262d;
                            }}
                            a {{ color: #58a6ff; }}
                            code {{
                                background-color: rgba(110,118,129,0.4);
                            }}
                            pre {{
                                background-color: #161b22;
                            }}
                            blockquote {{
                                color: #8b949e;
                                border-left-color: #30363d;
                            }}
                            table th, table td {{
                                border-color: #30363d;
                            }}
                            table th {{
                                background-color: #161b22;
                            }}
                            hr {{
                                background-color: #30363d;
                            }}
                        }}
                        .content-container {{
                            opacity: 0;
                            transition: opacity 0.3s ease-in-out;
                        }}
                        .content-container.visible {{
                            opacity: 1;
                        }}
                        details {{
                            margin: 1em 0;
                        }}
                        details summary {{
                            cursor: pointer;
                            font-weight: 600;
                            padding: 8px 0;
                        }}
                        details summary:hover {{
                            color: #0969da;
                        }}
                        details[open] summary {{
                            margin-bottom: 12px;
                        }}
                        @media (prefers-color-scheme: dark) {{
                            details summary:hover {{
                                color: #58a6ff;
                            }}
                        }}
                    </style>
                    <script>
                        function appendContent(content) {{
                            const container = document.createElement('div');
                            container.className = 'content-container markdown-body';
                            container.innerHTML = content;
                            document.body.appendChild(container);
                            
                            // 为所有details元素添加动画效果
                            container.querySelectorAll('details').forEach(details => {{
                                details.addEventListener('toggle', event => {{
                                    if (details.open) {{
                                        const content = details.querySelector('div');
                                        if (content) {{
                                            content.style.maxHeight = content.scrollHeight + 'px';
                                        }}
                                    }}
                                }});
                            }});
                            
                            // 强制重排以触发动画
                            void container.offsetWidth;
                            container.classList.add('visible');
                        }}

                        // 在页面加载完成后初始化所有折叠块
                        document.addEventListener('DOMContentLoaded', () => {{
                            document.querySelectorAll('details').forEach(details => {{
                                details.addEventListener('toggle', event => {{
                                    if (details.open) {{
                                        const content = details.querySelector('div');
                                        if (content) {{
                                            content.style.maxHeight = content.scrollHeight + 'px';
                                        }}
                                    }}
                                }});
                            }});
                        }});
                    </script>
                </head>
                <body>
                </body>
                </html>";

            _webView.NavigateToString(htmlHeader);
        }

        private async void LoadReadmeContent()
        {
            if (_webView == null) return;

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
                        _logger.Debug($"成功读取嵌入资源中的README文件，内容长度: {markdown.Length}");
                    }
                    else
                    {
                        _logger.Warning("未找到嵌入式README.md资源");
                        markdown = "# 灵曜按键\n\n欢迎使用灵曜按键！";
                    }
                }

                // 配置Markdown转换器，添加GitHub风格扩展
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .UseEmojiAndSmiley()
                    .UseTaskLists()
                    .UseAutoLinks()
                    .UseGridTables()
                    .UsePipeTables()
                    .UseListExtras()
                    .UseFootnotes()
                    .UseFooters()
                    .UseCitations()
                    .UseCustomContainers()
                    .UseDefinitionLists()
                    .UseFigures()
                    .UseBootstrap()
                    .UseMediaLinks()
                    .UseDiagrams()
                    .UseAutoIdentifiers()
                    .UseGenericAttributes()
                    .Build();
                
                // 一次性转换整个文档
                string html = Markdown.ToHtml(markdown, pipeline);

                // 注入内容
                await _webView.ExecuteScriptAsync($"appendContent(`{html.Replace("`", "\\`")}`)");
            }
            catch (Exception ex)
            {
                _logger.Error("加载README.md内容失败", ex);
                await _webView.ExecuteScriptAsync(@"
                    appendContent(`
                        <h1>加载失败</h1>
                        <p>错误信息: " + ex.Message.Replace("`", "\\`") + @"</p>
                    `)");
            }
        }

        public void HandleWebViewError(CoreWebView2WebErrorStatus status)
        {
            _logger.Error($"WebView2导航错误: {status}");
            LoadErrorContent($"加载失败: {status}");
        }

        public void HandleWebViewError(Exception ex)
        {
            _logger.Error("WebView2初始化错误", ex);
            LoadErrorContent($"初始化失败: {ex.Message}");
        }

        private void LoadErrorContent(string message)
        {
            if (_webView != null)
            {
                _webView.ExecuteScriptAsync($@"
                    appendContent(`
                    <h1>加载失败</h1>
                        <p>{message.Replace("`", "\\`")}</p>
                    `)");
            }
        }

        private void OpenGitHub()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = _githubUrl,
                    UseShellExecute = true
                };
                Process.Start(psi);
                _logger.Debug("成功打开GitHub仓库链接");
            }
            catch (Exception ex)
            {
                _logger.Error("打开GitHub仓库链接失败", ex);
                // 可以考虑添加一个错误提示
            }
        }

        private void ShowQRCode()
        {
            try
            {
                _logger.Debug("开始执行ShowQRCode方法");
                
                var mainWindow = System.Windows.Application.Current.MainWindow;
                _logger.Debug($"MainWindow是否为null: {mainWindow == null}");
                
                if (mainWindow == null)
                {
                    _logger.Error("无法获取MainWindow实例");
                    System.Windows.MessageBox.Show(
                        "无法打开二维码页面，请尝试重启应用程序。",
                        "错误",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                _logger.Debug($"MainWindow类型: {mainWindow.GetType().FullName}");
                _logger.Debug($"MainWindow.DataContext是否为null: {mainWindow.DataContext == null}");
                
                if (mainWindow.DataContext == null)
                {
                    _logger.Error("MainWindow.DataContext为null");
                    System.Windows.MessageBox.Show(
                        "应用程序状态异常，请尝试重启应用程序。",
                        "错误",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                _logger.Debug($"MainWindow.DataContext类型: {mainWindow.DataContext.GetType().FullName}");

                if (mainWindow.DataContext is MainViewModel mainViewModel)
                {
                    _logger.Debug("成功获取MainViewModel，准备导航到QRCode页面");
                    mainViewModel.NavigateCommand.Execute("QRCode");
                    _logger.Debug("已执行导航命令");
                }
                else
                {
                    _logger.Error($"MainWindow.DataContext类型不正确: {mainWindow.DataContext.GetType().FullName}");
                    System.Windows.MessageBox.Show(
                        "应用程序状态异常，请尝试重启应用程序。",
                        "错误",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("显示二维码页面失败", ex);
                System.Windows.MessageBox.Show(
                    $"显示二维码页面时发生错误：{ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
} 