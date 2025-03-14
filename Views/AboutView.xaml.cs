using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Documents;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using WpfApp.Services.Cache;
using WpfApp.Services.Utils;
using System.Windows.Controls;

namespace WpfApp.Views;

/// <summary>
/// AboutView.xaml 的交互逻辑
/// </summary>
public partial class AboutView : Page
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
        // 修改：每次页面加载时，重置状态，确保能够正确加载内容
        _isLoading = false;
        _logger.Debug("AboutView页面加载事件触发 - 重置状态");

        if (!_disposedValue)
        {
            _isLoading = true;
            _logger.Debug("AboutView开始加载内容");

            try
            {
                // 确保视图元素是可见的
                if (DocumentViewer != null)
                    DocumentViewer.Visibility = Visibility.Collapsed;
                
                if (LoadingIndicator != null)
                    LoadingIndicator.Visibility = Visibility.Visible;
                
                if (ErrorMessage != null)
                    ErrorMessage.Visibility = Visibility.Collapsed;

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
        // 修改：页面卸载时不真正释放资源，仅重置状态
        _isLoading = false;
        
        // 取消当前加载任务，但不释放资源
        _cts?.Cancel();
        
        _logger.Debug("AboutView页面卸载 - 重置状态但不释放资源");
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
                if (!_disposedValue) DisplayMarkdownDocument(document);
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
            
            // 获取RichTextBox中的FlowDocument
            if (MainDocument != null && MainDocument.Document != null)
            {
                // 清理现有内容
                MainDocument.Document.Blocks.Clear();

                // 转换Markdown为FlowDocument
                ConvertMarkdownToFlowDocument(document, MainDocument.Document);

                // 显示文档
                if (DocumentViewer != null)
                    DocumentViewer.Visibility = Visibility.Visible;
                if (LoadingIndicator != null)
                    LoadingIndicator.Visibility = Visibility.Collapsed;

                _logger.Debug("Markdown文档显示完成");
            }
            else
            {
                _logger.Error("MainDocument或其Document为null");
                if (ErrorMessage != null)
                    ErrorMessage.Visibility = Visibility.Visible;
                if (ErrorDetails != null)
                    ErrorDetails.Text = "无法加载文档显示控件";
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
                switch (block)
                {
                    case HeadingBlock heading:
                        var headingPara = new Paragraph
                        {
                            FontSize = 24 - (heading.Level - 1) * 2,
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
                            if (item is ListItemBlock listItem)
                            {
                                var listItemPara = new Paragraph();
                                foreach (var itemBlock in listItem)
                                    if (itemBlock is ParagraphBlock itemPara)
                                        AddInlineContent(itemPara.Inline, listItemPara.Inlines);

                                wpfList.ListItems.Add(new ListItem(listItemPara));
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
                            if (quoteBlock is ParagraphBlock quoteParagraph)
                                AddInlineContent(quoteParagraph.Inline, quotePara.Inlines);

                        flowDocument.Blocks.Add(quotePara);
                        break;
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
                            span.FontWeight = FontWeights.Bold;
                        else
                            span.FontStyle = FontStyles.Italic;
                        AddInlineContent(emphasis, span.Inlines);
                        inlines.Add(span);
                        break;

                    case LinkInline link:
                        try
                        {
                            var linkUrl = link.Url;
                            // 检查URL是否是相对路径
                            if (!linkUrl.StartsWith("http://") && !linkUrl.StartsWith("https://"))
                                linkUrl = $"https://{linkUrl}";

                            if (Uri.TryCreate(linkUrl, UriKind.Absolute, out var uri))
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
                        if (inline is ContainerInline container2) AddInlineContent(container2, inlines);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"处理内联内容失败: {ex.Message}", ex);
                // 如果处理某个内联元素失败，添加原始文本
                if (inline is LiteralInline literal) inlines.Add(new Run(literal.Content.ToString()));
            }
    }

    // 资源释放时要重新初始化状态，以便页面可以重用
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
                try
                {
                    _cts?.Cancel();
                    
                    // 重新创建取消令牌源，以便下次使用
                    _cts?.Dispose();
                    _cts = new CancellationTokenSource();
                    
                    // 重置加载状态
                    _isLoading = false;

                    // 清理事件订阅
                    Loaded -= AboutView_Loaded;
                    Unloaded -= AboutView_Unloaded;

                    // 清理文档内容
                    if (MainDocument != null && MainDocument.Document != null) 
                        MainDocument.Document.Blocks.Clear();
                }
                catch (Exception ex)
                {
                    _logger.Error("资源清理失败", ex);
                }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}