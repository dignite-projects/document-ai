using Dignite.Paperbase.Abstractions.TextExtraction;

namespace Dignite.Paperbase.TextExtraction.OcrProfiles;

public class OcrQualityAssessment
{
    public double Score { get; set; }
    public bool IsLowQuality { get; set; }
    public string DiagnosisCode { get; set; } = OcrQualityDiagnosisCodes.None;
    public string? TargetedRetryProfileCode { get; set; }
    public string Reason { get; set; } = string.Empty;
    public OcrQualitySignalSnapshot Signals { get; set; } = new();
}
