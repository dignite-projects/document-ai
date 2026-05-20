using Dignite.Paperbase.Ocr;
using Volo.Abp.DependencyInjection;

namespace Dignite.Paperbase.TextExtraction.OcrProfiles;

public class DefaultOcrProfileResolver : IOcrProfileResolver, ITransientDependency
{
    public virtual OcrProfileResolution Resolve(OcrProfileResolutionContext context)
    {
        var requested = OcrProfileCodes.Normalize(context.RequestedProfileCode);
        var signals = OcrProfileSignalMapper.FromProbe(context.ProbeResult);

        if (requested != OcrProfileCodes.Auto)
        {
            return Build(
                requested,
                requested,
                $"Explicit OCR profile '{requested}' requested.",
                signals,
                OcrQualityDiagnosisCodes.None);
        }

        if (context.ProbeResult == null)
        {
            return Build(
                requested,
                OcrProfileCodes.General,
                "Auto OCR profile fell back to general because the provider did not return a probe.",
                signals,
                OcrQualityDiagnosisCodes.NoProbe);
        }

        if (signals.LowScanQualityScore is >= 0.60)
        {
            return Build(
                requested,
                OcrProfileCodes.LowQualityScan,
                "Auto OCR profile resolved to low-quality-scan from probe scan-quality signals.",
                signals,
                OcrQualityDiagnosisCodes.LowQualityScan);
        }

        if (signals.TableMarkerCount >= 3 && signals.TableMarkerCount >= signals.FormLikeLineCount)
        {
            return Build(
                requested,
                OcrProfileCodes.TableHeavy,
                "Auto OCR profile resolved to table-heavy from probe table structure signals.",
                signals,
                OcrQualityDiagnosisCodes.TableStructure);
        }

        if (signals.FormLikeLineCount >= 3)
        {
            return Build(
                requested,
                OcrProfileCodes.FormKeyValue,
                "Auto OCR profile resolved to form-key-value from probe label/value signals.",
                signals,
                OcrQualityDiagnosisCodes.FormKeyValue);
        }

        if (signals.Confidence is < 0.55)
        {
            return Build(
                requested,
                OcrProfileCodes.HighAccuracy,
                "Auto OCR profile resolved to high-accuracy from low probe confidence.",
                signals,
                OcrQualityDiagnosisCodes.LowConfidence);
        }

        return Build(
            requested,
            OcrProfileCodes.General,
            "Auto OCR profile resolved to general; probe did not show a stronger technical signal.",
            signals,
            OcrQualityDiagnosisCodes.None);
    }

    private static OcrProfileResolution Build(
        string requested,
        string effective,
        string reason,
        OcrQualitySignals signals,
        string diagnosisCode)
    {
        return new OcrProfileResolution
        {
            RequestedProfileCode = requested,
            EffectiveProfileCode = effective,
            Reason = reason,
            FeatureSnapshot = OcrProfileSignalMapper.ToSnapshot(signals, qualityScore: signals.Confidence ?? 0, diagnosisCode)
        };
    }
}
