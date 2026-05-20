using Dignite.Paperbase.Abstractions.TextExtraction;
using Dignite.Paperbase.Ocr;

namespace Dignite.Paperbase.TextExtraction.OcrProfiles;

public class OcrProfileResolutionContext
{
    public TextExtractionContext TextExtractionContext { get; set; } = default!;
    public string RequestedProfileCode { get; set; } = OcrProfileCodes.Auto;
    public OcrProbeResult? ProbeResult { get; set; }
}
