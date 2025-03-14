using Markdig;
using Markdig.Syntax;
using WpfApp.Services.Utils;

namespace WpfApp.Services.Cache;

public class MarkdownCacheService
{
    private static readonly Lazy<MarkdownCacheService> _instance = new(() => new MarkdownCacheService());

    public static MarkdownCacheService Instance => _instance.Value;

    private string _cachedMarkdown;
    private MarkdownDocument _cachedDocument;
    private readonly MarkdownPipeline _pipeline;
    private readonly object _lock = new();
    private readonly SerilogManager _logger = SerilogManager.Instance;

    private MarkdownCacheService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public void SetMarkdownContent(string markdown)
    {
        lock (_lock)
        {
            try
            {
                _cachedMarkdown = markdown;
                _cachedDocument = Markdown.Parse(markdown, _pipeline);
                _logger.Debug("Markdown内容已缓存");
            }
            catch (Exception ex)
            {
                _logger.Error("缓存Markdown内容失败", ex);
                throw;
            }
        }
    }

    public (string RawMarkdown, MarkdownDocument ParsedDocument) GetMarkdownContent()
    {
        lock (_lock)
        {
            return (_cachedMarkdown, _cachedDocument);
        }
    }

    public bool HasContent()
    {
        lock (_lock)
        {
            return !string.IsNullOrEmpty(_cachedMarkdown) && _cachedDocument != null;
        }
    }
    
}