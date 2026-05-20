using System;
using Dignite.Paperbase.Ocr;
using Volo.Abp.DependencyInjection;

namespace Dignite.Paperbase.TextExtraction.OcrProfiles;

public class DefaultOcrQualityScorer : IOcrQualityScorer, ITransientDependency
{
    private const double LowConfidenceThreshold = 0.72;
    private const double BetterScoreMargin = 0.03;

    public virtual OcrQualityAssessment Score(
        OcrResult result,
        OcrProfileResolution resolution,
        OcrProbeResult? probeResult)
    {
        var signals = OcrProfileSignalMapper.FromOcrResult(result);
        var probeSignals = OcrProfileSignalMapper.FromProbe(probeResult);

        signals.TableMarkerCount = Math.Max(signals.TableMarkerCount, probeSignals.TableMarkerCount);
        signals.FormLikeLineCount = Math.Max(signals.FormLikeLineCount, probeSignals.FormLikeLineCount);
        signals.LowScanQualityScore = MaxNullable(signals.LowScanQualityScore, probeSignals.LowScanQualityScore);
        signals.StructuralComplexityScore = MaxNullable(signals.StructuralComplexityScore, probeSignals.StructuralComplexityScore);

        var confidenceScore = signals.Confidence ?? 0;
        var textScore = signals.HasMeaningfulText ? 0.20 : 0;
        var lengthScore = Math.Clamp(signals.MarkdownLength / 1200d, 0, 0.10);
        var score = Math.Clamp((confidenceScore * 0.70) + textScore + lengthScore, 0, 1);

        var diagnosis = Diagnose(signals);
        var retryProfile = ResolveRetryProfile(diagnosis, resolution.EffectiveProfileCode);
        var isLowQuality = !signals.HasMeaningfulText || confidenceScore < LowConfidenceThreshold;

        return new OcrQualityAssessment
        {
            Score = score,
            IsLowQuality = isLowQuality,
            DiagnosisCode = diagnosis,
            TargetedRetryProfileCode = isLowQuality ? retryProfile : null,
            Reason = BuildReason(isLowQuality, diagnosis, retryProfile),
            Signals = OcrProfileSignalMapper.ToSnapshot(signals, score, diagnosis, retryProfile)
        };
    }

    public virtual bool IsBetter(OcrQualityAssessment candidate, OcrQualityAssessment current)
    {
        if (candidate.Signals.HasMeaningfulText != current.Signals.HasMeaningfulText)
        {
            return candidate.Signals.HasMeaningfulText;
        }

        if (candidate.Score >= current.Score + BetterScoreMargin)
        {
            return true;
        }

        if (candidate.Score + BetterScoreMargin < current.Score)
        {
            return false;
        }

        return (candidate.Signals.Confidence ?? 0) > (current.Signals.Confidence ?? 0);
    }

    private static string Diagnose(OcrQualitySignals signals)
    {
        if (!signals.HasMeaningfulText)
        {
            return OcrQualityDiagnosisCodes.EmptyText;
        }

        if (signals.LowScanQualityScore is >= 0.60)
        {
            return OcrQualityDiagnosisCodes.LowQualityScan;
        }

        if (signals.TableMarkerCount >= 3 && signals.TableMarkerCount >= signals.FormLikeLineCount)
        {
            return OcrQualityDiagnosisCodes.TableStructure;
        }

        if (signals.FormLikeLineCount >= 3)
        {
            return OcrQualityDiagnosisCodes.FormKeyValue;
        }

        if (signals.Confidence is < LowConfidenceThreshold)
        {
            return OcrQualityDiagnosisCodes.LowConfidence;
        }

        return OcrQualityDiagnosisCodes.None;
    }

    private static string? ResolveRetryProfile(string diagnosis, string currentProfile)
    {
        var target = diagnosis switch
        {
            OcrQualityDiagnosisCodes.LowQualityScan => OcrProfileCodes.LowQualityScan,
            OcrQualityDiagnosisCodes.TableStructure => OcrProfileCodes.TableHeavy,
            OcrQualityDiagnosisCodes.FormKeyValue => OcrProfileCodes.FormKeyValue,
            OcrQualityDiagnosisCodes.EmptyText => OcrProfileCodes.HighAccuracy,
            OcrQualityDiagnosisCodes.LowConfidence => OcrProfileCodes.HighAccuracy,
            _ => null
        };

        return target == currentProfile ? null : target;
    }

    private static string BuildReason(bool isLowQuality, string diagnosis, string? retryProfile)
    {
        if (!isLowQuality)
        {
            return "OCR quality accepted; no automatic retry needed.";
        }

        return retryProfile == null
            ? $"OCR quality is low ({diagnosis}) but no different targeted retry profile is available."
            : $"OCR quality is low ({diagnosis}); retry once with {retryProfile}.";
    }

    private static double? MaxNullable(double? left, double? right)
    {
        if (!left.HasValue)
        {
            return right;
        }

        if (!right.HasValue)
        {
            return left;
        }

        return Math.Max(left.Value, right.Value);
    }
}
