using Dignite.Paperbase.Ocr;

namespace Dignite.Paperbase.TextExtraction.OcrProfiles;

public interface IOcrQualityScorer
{
    OcrQualityAssessment Score(
        OcrResult result,
        OcrProfileResolution resolution,
        OcrProbeResult? probeResult);

    bool IsBetter(OcrQualityAssessment candidate, OcrQualityAssessment current);
}
