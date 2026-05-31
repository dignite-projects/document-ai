using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Paperbase.Abstractions.TextExtraction;
using Dignite.Paperbase.TextExtraction;
using ElBruno.MarkItDotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;

namespace Dignite.Paperbase.TextExtraction.ElBrunoMarkItDown;

[ExposeServices(typeof(IMarkdownTextProvider))]
public class ElBrunoMarkdownProvider : IMarkdownTextProvider, ITransientDependency
{
    public const string ProviderIdentifier = "ElBruno.MarkItDotNet";

    private readonly MarkdownService _markdownService;

    public ILogger<ElBrunoMarkdownProvider> Logger { get; set; } = NullLogger<ElBrunoMarkdownProvider>.Instance;

    public ElBrunoMarkdownProvider(MarkdownService markdownService)
    {
        _markdownService = markdownService;
    }

    public virtual async Task<TextExtractionResult> ExtractAsync(
        Stream fileStream,
        TextExtractionContext context,
        CancellationToken cancellationToken = default)
    {
        var conversion = await _markdownService.ConvertAsync(
            fileStream,
            context.FileExtension ?? string.Empty,
            cancellationToken);

        if (!conversion.Success)
        {
            Logger.LogDebug("ElBruno conversion failed for {Extension}: {Error}",
                context.FileExtension, conversion.ErrorMessage);
            // 仍自报 provider 身份（失败结果也记 provenance）。
            return new TextExtractionResult { ProviderName = ProviderIdentifier };
        }

        // 纯 text→Markdown，无空间模型 → NativePayload 留 null。
        return new TextExtractionResult
        {
            Markdown = conversion.Markdown ?? string.Empty,
            DetectedLanguage = null,
            UsedOcr = false,
            ProviderName = ProviderIdentifier,
            NativePayload = null
        };
    }
}
