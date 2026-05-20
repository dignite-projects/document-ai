namespace Dignite.Paperbase.Abstractions.TextExtraction;

public class OcrExtractionMetadata
{
    public string? RequestedProfileCode { get; set; }
    public string? EffectiveProfileCode { get; set; }
    public string? ResolutionReason { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderModelName { get; set; }
    public string? ProviderVersion { get; set; }
    public OcrQualitySignalSnapshot? QualitySignals { get; set; }
}
