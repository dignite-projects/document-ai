namespace Dignite.Paperbase.Abstractions.TextExtraction;

public class OcrQualitySignalSnapshot
{
    public double? Confidence { get; set; }
    public double QualityScore { get; set; }
    public int PageCount { get; set; }
    public int MarkdownLength { get; set; }
    public bool HasMeaningfulText { get; set; }
    public int TableMarkerCount { get; set; }
    public int FormLikeLineCount { get; set; }
    public string? DiagnosisCode { get; set; }
    public string? RetryProfileCode { get; set; }
    public bool AutomaticRetryAttempted { get; set; }
    public bool AutomaticRetrySelected { get; set; }
}
