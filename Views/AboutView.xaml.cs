using System;
using System.IO;
using System.Windows.Controls;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Threading;
using System.Diagnostics;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using Markdig;
using System.Text.RegularExpressions;
using System.Windows.Data;
using System.Windows.Markup;
using WpfApp.Services;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Windows.Navigation;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using WpfApp.Services.Cache;

namespace WpfApp.Views
{
    /// <summary>
    /// AboutView.xaml 的交互逻辑
    /// </summary>
    public partial class AboutView : Page, IDisposable
    {
        private readonly ViewModels.AboutViewModel _viewModel;
        private bool _disposedValue;
        private CancellationTokenSource _cts;
        private bool _isLoading;
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly MarkdownCacheService _markdownCache = MarkdownCacheService.Instance;

        public AboutView()
        {
            InitializeComponent();
            _viewModel = new ViewModels.AboutViewModel();
            DataContext = _viewModel;
            _cts = new CancellationTokenSource();

                // 注册页面加载和卸载事件
                Loaded += AboutView_Loaded;
                Unloaded += AboutView_Unloaded;
        }

        private void AboutView_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isLoading && !_disposedValue)
            {
                _isLoading = true;
                _logger.Debug("AboutView页面加载事件触发");

                try
                {
                    // 检查缓存中是否有内容
                    if (_markdownCache.HasContent())
                    {
                        _logger.Debug("检测到缓存中存在Markdown内容，准备恢复显示");
                        var (_, document) = _markdownCache.GetMarkdownContent();
                        
                        // 确保在UI线程上执行
                        Dispatcher.InvokeAsync(() =>
                        {
                            if (!_disposedValue)
                            {
                                DisplayMarkdownDocument(document);
                                _isLoading = false;
                                _logger.Debug("从缓存恢复Markdown内容完成");
                            }
                        });
                }
                else
                {
                        _logger.Debug("缓存中无内容，开始加载Markdown内容");
                        LoadMarkdownContentAsync(_cts.Token).ContinueWith(task =>
                    {
                        _isLoading = false;
                        if (task.IsFaulted)
                        {
                                _logger.Error("加载Markdown内容失败", task.Exception);
                            Dispatcher.InvokeAsync(() =>
                            {
                                if (!_disposedValue)
                                {
                                        if (LoadingIndicator != null)
                                            LoadingIndicator.Visibility = Visibility.Collapsed;
                                        if (ErrorMessage != null)
                                            ErrorMessage.Visibility = Visibility.Visible;
                                        if (ErrorDetails != null)
                                            ErrorDetails.Text = task.Exception?.InnerException?.Message ?? "加载失败";
                                }
                            });
                        }
                    }, TaskScheduler.Current);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("页面加载失败", ex);
                    _isLoading = false;
                    if (!_disposedValue)
                    {
                        if (LoadingIndicator != null)
                            LoadingIndicator.Visibility = Visibility.Collapsed;
                        if (ErrorMessage != null)
                            ErrorMessage.Visibility = Visibility.Visible;
                        if (ErrorDetails != null)
                            ErrorDetails.Text = ex.Message;
                    }
                }
            }
        }

        private void AboutView_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private async Task LoadMarkdownContentAsync(CancellationToken cancellationToken)
        {
            try
            {
                string markdown;
                // 从嵌入式资源读取README.md
                using (var stream = GetType().Assembly.GetManifestResourceStream("WpfApp.README.md"))
                {
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        markdown = await reader.ReadToEndAsync();
                        _logger.Debug($"成功读取嵌入资源中的README文件，内容长度: {markdown.Length}");
                    }
                    else
                    {
                        _logger.Warning("未找到嵌入式README.md资源");
                        markdown = "# 灵曜按键\n\n欢迎使用灵曜按键！";
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // 缓存Markdown内容
                _markdownCache.SetMarkdownContent(markdown);
                var (_, document) = _markdownCache.GetMarkdownContent();

                await Dispatcher.InvokeAsync(() =>
                {
                    if (!_disposedValue)
                    {
                        DisplayMarkdownDocument(document);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error("加载Markdown内容失败", ex);
                throw;
            }
        }

        private void DisplayMarkdownDocument(MarkdownDocument document)
        {
            try
            {
                _logger.Debug("开始显示Markdown文档");
                // 清理现有内容
                if (MainDocument != null)
                {
                    MainDocument.Blocks.Clear();
                    
                    // 转换Markdown为FlowDocument
                    ConvertMarkdownToFlowDocument(document, MainDocument);

                    // 显示文档
                    if (DocumentViewer != null)
                        DocumentViewer.Visibility = Visibility.Visible;
                    if (LoadingIndicator != null)
                        LoadingIndicator.Visibility = Visibility.Collapsed;

                    _logger.Debug("Markdown文档显示完成");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("显示Markdown文档失败", ex);
                if (ErrorMessage != null)
                    ErrorMessage.Visibility = Visibility.Visible;
                if (ErrorDetails != null)
                    ErrorDetails.Text = ex.Message;
            }
        }

        private void ConvertMarkdownToFlowDocument(MarkdownDocument markdownDocument, FlowDocument flowDocument)
        {
            try
            {
                foreach (var block in markdownDocument)
                {
                    switch (block)
                    {
                        case HeadingBlock heading:
                            var headingPara = new Paragraph
                            {
                                FontSize = 24 - ((heading.Level - 1) * 2),
                                FontWeight = FontWeights.Bold,
                                Margin = new Thickness(0, 10, 0, 5)
                            };
                            AddInlineContent(heading.Inline, headingPara.Inlines);
                            flowDocument.Blocks.Add(headingPara);
                            break;

                        case ParagraphBlock para:
                            var paragraph = new Paragraph
                            {
                                Margin = new Thickness(0, 5, 0, 5)
                            };
                            AddInlineContent(para.Inline, paragraph.Inlines);
                            flowDocument.Blocks.Add(paragraph);
                            break;

                        case ListBlock list:
                            var wpfList = new List
                            {
                                Margin = new Thickness(0, 5, 0, 5),
                                MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc
                            };

                            foreach (var item in list)
                            {
                                if (item is ListItemBlock listItem)
                                {
                                    var listItemPara = new Paragraph();
                                    foreach (var itemBlock in listItem)
                                    {
                                        if (itemBlock is ParagraphBlock itemPara)
                                        {
                                            AddInlineContent(itemPara.Inline, listItemPara.Inlines);
                                        }
                                    }
                                    wpfList.ListItems.Add(new ListItem(listItemPara));
                                }
                            }
                            flowDocument.Blocks.Add(wpfList);
                            break;

                        case FencedCodeBlock codeBlock:
                            var codePara = new Paragraph
                            {
                                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                                FontFamily = new FontFamily("Consolas"),
                                Padding = new Thickness(10),
                                Margin = new Thickness(0, 5, 0, 5)
                            };
                            codePara.Inlines.Add(new Run(codeBlock.Lines.ToString()));
                            flowDocument.Blocks.Add(codePara);
                            break;

                        case QuoteBlock quote:
                            var quotePara = new Paragraph
                            {
                                Margin = new Thickness(20, 5, 0, 5),
                                BorderThickness = new Thickness(4, 0, 0, 0),
                                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                                Padding = new Thickness(10, 0, 0, 0)
                            };
                            foreach (var quoteBlock in quote)
                            {
                                if (quoteBlock is ParagraphBlock quoteParagraph)
                                {
                                    AddInlineContent(quoteParagraph.Inline, quotePara.Inlines);
                                }
                            }
                            flowDocument.Blocks.Add(quotePara);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("转换Markdown到FlowDocument失败", ex);
                var errorParagraph = new Paragraph(new Run($"内容转换失败: {ex.Message}"))
                {
                    Foreground = new SolidColorBrush(Colors.Red)
                };
                flowDocument.Blocks.Add(errorParagraph);
            }
        }

        private void AddInlineContent(ContainerInline container, InlineCollection inlines)
        {
            foreach (var inline in container)
            {
                try
                {
                    switch (inline)
                    {
                        case LiteralInline literal:
                            inlines.Add(new Run(literal.Content.ToString()));
                            break;

                        case EmphasisInline emphasis:
                            var span = new Span();
                            if (emphasis.DelimiterCount == 2)
                            {
                                span.FontWeight = FontWeights.Bold;
                            }
                            else
                            {
                                span.FontStyle = FontStyles.Italic;
                            }
                            AddInlineContent(emphasis, span.Inlines);
                            inlines.Add(span);
                            break;

                        case LinkInline link:
                            try
                            {
                                string linkUrl = link.Url;
                                // 检查URL是否是相对路径
                                if (!linkUrl.StartsWith("http://") && !linkUrl.StartsWith("https://"))
                                {
                                    linkUrl = $"https://{linkUrl}";
                                }

                                if (Uri.TryCreate(linkUrl, UriKind.Absolute, out Uri? uri))
                                {
                                    var hyperlink = new Hyperlink(new Run(link.Title ?? link.Url))
                                    {
                                        NavigateUri = uri,
                                        ToolTip = linkUrl
                                    };
                                    hyperlink.RequestNavigate += (s, e) =>
                                    {
                                        try
                                        {
                                            Process.Start(new ProcessStartInfo
                                            {
                                                FileName = e.Uri.AbsoluteUri,
                                                UseShellExecute = true
                                            });
                                            e.Handled = true;
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.Error($"打开链接失败: {ex.Message}", ex);
                                        }
                                    };
                                    inlines.Add(hyperlink);
                                }
                                else
                                {
                                    // 如果URL无效，就显示为普通文本
                                    inlines.Add(new Run(link.Title ?? link.Url));
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"处理链接失败: {ex.Message}", ex);
                                inlines.Add(new Run(link.Title ?? link.Url));
                            }
                            break;

                        case CodeInline code:
                            var codeSpan = new Span
                            {
                                FontFamily = new FontFamily("Consolas"),
                                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
                            };
                            codeSpan.Inlines.Add(new Run(code.Content));
                            inlines.Add(codeSpan);
                            break;

                        default:
                            if (inline is ContainerInline container2)
                            {
                                AddInlineContent(container2, inlines);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"处理内联内容失败: {ex.Message}", ex);
                    // 如果处理某个内联元素失败，添加原始文本
                    if (inline is LiteralInline literal)
                    {
                        inlines.Add(new Run(literal.Content.ToString()));
                    }
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        _cts?.Cancel();
                        _cts?.Dispose();
                        
                        // 清理事件订阅
                        Loaded -= AboutView_Loaded;
                        Unloaded -= AboutView_Unloaded;

                        // 清理文档内容
                        if (MainDocument != null)
                        {
                            MainDocument.Blocks.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("资源清理失败", ex);
                    }
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
} 