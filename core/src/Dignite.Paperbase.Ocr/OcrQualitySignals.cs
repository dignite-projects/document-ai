namespace Dignite.Paperbase.Ocr;

/// <summary>
/// Provider-neutral OCR quality diagnostics. Providers may leave fields at their
/// defaults; the orchestrator can enrich them from probe/full OCR Markdown.
/// </summary>
public class OcrQualitySignals
{
    public double? Confidence { get; set; }
    public int PageCount { get; set; }
    public int MarkdownLength { get; set; }
    public bool HasMeaningfulText { get; set; }
    public int TableMarkerCount { get; set; }
    public int FormLikeLineCount { get; set; }
    public double? LowScanQualityScore { get; set; }
    public double? StructuralComplexityScore { get; set; }
}
