using Dignite.Paperbase.Abstractions.TextExtraction;

namespace Dignite.Paperbase.TextExtraction.OcrProfiles;

public class OcrProfileResolution
{
    public string RequestedProfileCode { get; set; } = default!;
    public string EffectiveProfileCode { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public OcrQualitySignalSnapshot FeatureSnapshot { get; set; } = new();
}
