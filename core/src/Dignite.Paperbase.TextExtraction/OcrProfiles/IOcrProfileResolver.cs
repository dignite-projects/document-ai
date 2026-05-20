namespace Dignite.Paperbase.TextExtraction.OcrProfiles;

public interface IOcrProfileResolver
{
    OcrProfileResolution Resolve(OcrProfileResolutionContext context);
}
