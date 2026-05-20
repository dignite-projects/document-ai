using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Paperbase.Abstractions.TextExtraction;
using Dignite.Paperbase.Ocr;
using Dignite.Paperbase.TextExtraction.OcrProfiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dignite.Paperbase.TextExtraction;

public class DefaultTextExtractor : ITextExtractor, ITransientDependency
{
    private readonly IOcrProvider _ocrProvider;
    private readonly IOcrProbeProvider? _ocrProbeProvider;
    private readonly IMarkdownTextProvider _markdownProvider;
    private readonly PaperbaseOcrOptions _ocrOptions;
    private readonly IOcrProfileResolver _profileResolver;
    private readonly IOcrQualityScorer _qualityScorer;

    public ILogger<DefaultTextExtractor> Logger { get; set; } = NullLogger<DefaultTextExtractor>.Instance;

    public DefaultTextExtractor(
        IOcrProvider ocrProvider,
        IEnumerable<IOcrProbeProvider> ocrProbeProviders,
        IMarkdownTextProvider markdownProvider,
        IOptions<PaperbaseOcrOptions> ocrOptions,
        IOcrProfileResolver profileResolver,
        IOcrQualityScorer qualityScorer)
    {
        _ocrProvider = ocrProvider;
        _ocrProbeProvider = ocrProvider as IOcrProbeProvider ?? ocrProbeProviders.FirstOrDefault();
        _markdownProvider = markdownProvider;
        _ocrOptions = ocrOptions.Value;
        _profileResolver = profileResolver;
        _qualityScorer = qualityScorer;
    }

    public virtual async Task<TextExtractionResult> ExtractAsync(
        Stream fileStream,
        TextExtractionContext context,
        CancellationToken cancellationToken = default)
    {
        if (IsImageFormat(context.FileExtension))
        {
            return await ExtractByOcrAsync(fileStream, context, cancellationToken);
        }

        // 用单一 MemoryStream 横跨 Markdown Provider + 可能的 OCR 回退两次读取：
        // 输入流来自 blob 存储可能不可 seek，且 ElBruno 内部 PdfPig/OpenXml 等
        // 解析器要求 seekable stream，故必须缓冲。
        // 已知限制：超大文件（GB 级扫描 PDF）会全量驻留内存，需要时改为临时文件路径。
        using var seekable = new MemoryStream();
        await fileStream.CopyToAsync(seekable, cancellationToken);
        seekable.Position = 0;

        var md = await _markdownProvider.ExtractAsync(seekable, context, cancellationToken);

        if (!HasMeaningfulText(md.Markdown) && IsPdfExtension(context.FileExtension))
        {
            Logger.LogDebug("Markdown provider produced no meaningful text for PDF; falling back to OCR.");
            seekable.Position = 0;
            return await ExtractByOcrAsync(seekable, context, cancellationToken);
        }

        Logger.LogDebug("Markdown extraction completed using {Provider}", _markdownProvider.GetType().Name);
        return md;
    }

    protected virtual async Task<TextExtractionResult> ExtractByOcrAsync(
        Stream fileStream,
        TextExtractionContext ctx,
        CancellationToken cancellationToken)
    {
        using var seekable = new MemoryStream();
        await fileStream.CopyToAsync(seekable, cancellationToken);

        var languageHints = ctx.LanguageHints?.Count > 0
            ? ctx.LanguageHints
            : (IList<string>)_ocrOptions.DefaultLanguageHints;
        var requestedProfile = OcrProfileCodes.Normalize(ctx.OcrProfileCode ?? _ocrOptions.DefaultOcrProfileCode);

        var probeResult = requestedProfile == OcrProfileCodes.Auto
            ? await TryProbeAsync(seekable, ctx, languageHints, requestedProfile, cancellationToken)
            : null;

        var resolution = _profileResolver.Resolve(new OcrProfileResolutionContext
        {
            TextExtractionContext = ctx,
            RequestedProfileCode = requestedProfile,
            ProbeResult = probeResult
        });

        var initialResult = await RecognizeAsync(seekable, ctx, languageHints, resolution.EffectiveProfileCode);
        var initialAssessment = _qualityScorer.Score(initialResult, resolution, probeResult);

        var finalResult = initialResult;
        var finalAssessment = initialAssessment;
        var finalProfile = resolution.EffectiveProfileCode;
        var retryAttempted = false;
        var retrySelected = false;
        var reason = resolution.Reason;

        if (initialAssessment.IsLowQuality &&
            !string.IsNullOrWhiteSpace(initialAssessment.TargetedRetryProfileCode))
        {
            retryAttempted = true;
            var retryProfile = initialAssessment.TargetedRetryProfileCode!;
            var retryResult = await RecognizeAsync(seekable, ctx, languageHints, retryProfile);
            var retryResolution = new OcrProfileResolution
            {
                RequestedProfileCode = resolution.RequestedProfileCode,
                EffectiveProfileCode = retryProfile,
                Reason = $"Automatic retry with targeted OCR profile '{retryProfile}'.",
                FeatureSnapshot = resolution.FeatureSnapshot
            };
            var retryAssessment = _qualityScorer.Score(retryResult, retryResolution, probeResult);

            if (_qualityScorer.IsBetter(retryAssessment, initialAssessment))
            {
                finalResult = retryResult;
                finalAssessment = retryAssessment;
                finalProfile = retryProfile;
                retrySelected = true;
                reason = $"{resolution.Reason} Automatic retry selected '{retryProfile}' because it scored better.";
            }
            else
            {
                reason = $"{resolution.Reason} Automatic retry with '{retryProfile}' was discarded because the first full OCR result scored better.";
            }
        }

        finalAssessment.Signals.AutomaticRetryAttempted = retryAttempted;
        finalAssessment.Signals.AutomaticRetrySelected = retrySelected;

        var providerName = finalResult.ProviderName ?? _ocrProvider.GetType().Name;

        Logger.LogDebug(
            "OCR extraction completed using {Provider} with requested profile {RequestedProfile} and effective profile {EffectiveProfile}.",
            providerName,
            resolution.RequestedProfileCode,
            finalProfile);

        return new TextExtractionResult
        {
            Markdown = finalResult.Markdown,
            Confidence = finalResult.Confidence,
            DetectedLanguage = finalResult.DetectedLanguage,
            PageCount = finalResult.PageCount,
            UsedOcr = true,
            OcrMetadata = new OcrExtractionMetadata
            {
                RequestedProfileCode = resolution.RequestedProfileCode,
                EffectiveProfileCode = finalProfile,
                ResolutionReason = reason,
                ProviderName = providerName,
                ProviderModelName = finalResult.ProviderModelName,
                ProviderVersion = finalResult.ProviderVersion,
                QualitySignals = finalAssessment.Signals
            }
        };
    }

    private async Task<OcrProbeResult?> TryProbeAsync(
        Stream seekable,
        TextExtractionContext ctx,
        IList<string> languageHints,
        string requestedProfile,
        CancellationToken cancellationToken)
    {
        if (_ocrProbeProvider == null)
        {
            return null;
        }

        try
        {
            seekable.Position = 0;
            return await _ocrProbeProvider.ProbeAsync(
                seekable,
                new OcrProbeOptions
                {
                    ContentType = ctx.ContentType ?? string.Empty,
                    LanguageHints = languageHints,
                    RequestedOcrProfileCode = requestedProfile,
                    MaxPages = _ocrOptions.ProbeMaxPages
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "OCR probe failed; falling back to general OCR profile resolution.");
            return null;
        }
    }

    private async Task<OcrResult> RecognizeAsync(
        Stream seekable,
        TextExtractionContext ctx,
        IList<string> languageHints,
        string profileCode)
    {
        seekable.Position = 0;
        return await _ocrProvider.RecognizeAsync(
            seekable,
            new OcrOptions
            {
                ContentType = ctx.ContentType ?? string.Empty,
                LanguageHints = languageHints,
                OcrProfileCode = profileCode
            });
    }

    protected virtual bool IsImageFormat(string? fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension)) return false;
        var ext = fileExtension.ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".tiff" or ".tif" or ".bmp" or ".webp" or ".gif";
    }

    protected virtual bool HasMeaningfulText(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return false;
        return markdown.Any(c => char.IsLetter(c) || char.IsDigit(c));
    }

    protected virtual bool IsPdfExtension(string? fileExtension)
        => string.Equals(fileExtension, ".pdf", StringComparison.OrdinalIgnoreCase);
}
