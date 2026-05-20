namespace Dignite.Paperbase.Ocr;

public class OcrProbeResult
{
    /// <summary>
    /// Probe Markdown/text sample for diagnostics only. The orchestrator must never
    /// persist this as final Document.Markdown.
    /// </summary>
    public string Markdown { get; set; } = string.Empty;

    public double? Confidence { get; set; }
    public string? DetectedLanguage { get; set; }
    public int PageCount { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderModelName { get; set; }
    public string? ProviderVersion { get; set; }
    public OcrQualitySignals? QualitySignals { get; set; }
}
