using Dignite.Paperbase.Abstractions.TextExtraction;
using Dignite.Paperbase.Ocr;

namespace Dignite.Paperbase.TextExtraction.OcrProfiles;

public static class OcrProfileSignalMapper
{
    public static OcrQualitySignals FromProbe(OcrProbeResult? probeResult)
    {
        if (probeResult == null)
        {
            return new OcrQualitySignals();
        }

        return Merge(
            probeResult.QualitySignals,
            OcrQualitySignalBuilder.FromMarkdown(probeResult.Markdown, probeResult.Confidence, probeResult.PageCount));
    }

    public static OcrQualitySignals FromOcrResult(OcrResult result)
    {
        return Merge(
            result.QualitySignals,
            OcrQualitySignalBuilder.FromMarkdown(result.Markdown, result.Confidence, result.PageCount));
    }

    public static OcrQualitySignalSnapshot ToSnapshot(
        OcrQualitySignals signals,
        double qualityScore,
        string diagnosisCode,
        string? retryProfileCode = null)
    {
        return new OcrQualitySignalSnapshot
        {
            Confidence = signals.Confidence,
            QualityScore = qualityScore,
            PageCount = signals.PageCount,
            MarkdownLength = signals.MarkdownLength,
            HasMeaningfulText = signals.HasMeaningfulText,
            TableMarkerCount = signals.TableMarkerCount,
            FormLikeLineCount = signals.FormLikeLineCount,
            DiagnosisCode = diagnosisCode,
            RetryProfileCode = retryProfileCode
        };
    }

    private static OcrQualitySignals Merge(OcrQualitySignals? primary, OcrQualitySignals fallback)
    {
        if (primary == null)
        {
            return fallback;
        }

        return new OcrQualitySignals
        {
            Confidence = primary.Confidence ?? fallback.Confidence,
            PageCount = primary.PageCount > 0 ? primary.PageCount : fallback.PageCount,
            MarkdownLength = primary.MarkdownLength > 0 ? primary.MarkdownLength : fallback.MarkdownLength,
            HasMeaningfulText = primary.HasMeaningfulText || fallback.HasMeaningfulText,
            TableMarkerCount = primary.TableMarkerCount > 0 ? primary.TableMarkerCount : fallback.TableMarkerCount,
            FormLikeLineCount = primary.FormLikeLineCount > 0 ? primary.FormLikeLineCount : fallback.FormLikeLineCount,
            LowScanQualityScore = primary.LowScanQualityScore ?? fallback.LowScanQualityScore,
            StructuralComplexityScore = primary.StructuralComplexityScore ?? fallback.StructuralComplexityScore
        };
    }
}
