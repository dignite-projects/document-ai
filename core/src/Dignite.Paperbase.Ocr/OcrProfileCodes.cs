namespace Dignite.Paperbase.Ocr;

public static class OcrProfileCodes
{
    public const string Auto = "auto";
    public const string General = "general";
    public const string TableHeavy = "table-heavy";
    public const string FormKeyValue = "form-key-value";
    public const string LowQualityScan = "low-quality-scan";
    public const string HighAccuracy = "high-accuracy";

    public static bool IsKnown(string? profileCode)
        => !string.IsNullOrWhiteSpace(profileCode)
            && IsKnownNormalized(profileCode.Trim().ToLowerInvariant());

    public static string Normalize(string? profileCode)
    {
        if (string.IsNullOrWhiteSpace(profileCode))
        {
            return Auto;
        }

        var normalized = profileCode.Trim().ToLowerInvariant();
        return IsKnownNormalized(normalized) ? normalized : Auto;
    }

    private static bool IsKnownNormalized(string profileCode)
        => profileCode is Auto
            or General
            or TableHeavy
            or FormKeyValue
            or LowQualityScan
            or HighAccuracy;
}
