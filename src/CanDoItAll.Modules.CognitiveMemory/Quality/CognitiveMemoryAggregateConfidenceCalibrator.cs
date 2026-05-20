namespace CanDoItAll.Modules.CognitiveMemory;

public interface ICognitiveMemoryAggregateConfidenceCalibrator
{
    CognitiveMemoryAggregateConfidenceCalibration Calibrate(CognitiveMemoryAggregateConfidenceCalibrationRequest request);
}

public sealed class CognitiveMemoryAggregateConfidenceCalibrator : ICognitiveMemoryAggregateConfidenceCalibrator
{
    public CognitiveMemoryAggregateConfidenceCalibration Calibrate(CognitiveMemoryAggregateConfidenceCalibrationRequest request)
    {
        var sourceBreadthScore = Math.Clamp(request.DistinctSourceItemCount / 6d, 0, 1) * 0.08;
        var claimAgreementScore = Math.Clamp((request.StrongestClaimSourceMemoryCount - 1) * 0.04, 0, 0.12);
        var claimPenalty = Math.Clamp((request.ClaimCount - 1) * 0.015, 0, 0.06);
        var issuePenalty = Math.Clamp(request.ValidationIssueCount * 0.05, 0, 0.2);
        var score = Math.Round(
            Math.Clamp(0.68 + sourceBreadthScore + claimAgreementScore - claimPenalty - issuePenalty, 0.55, 0.92),
            3,
            MidpointRounding.AwayFromZero);
        var bucket = score >= 0.88 &&
                     request.DistinctSourceItemCount >= 6 &&
                     request.StrongestClaimSourceMemoryCount >= 3 &&
                     request.ClaimCount <= 2
            ? CognitiveMemoryScoreProjectionBucket.StrongAccept
            : CognitiveMemoryScoreProjectionBucket.WeakAccept;
        var stabilityState = bucket == CognitiveMemoryScoreProjectionBucket.StrongAccept
            ? CognitiveMemoryStabilityState.Active
            : CognitiveMemoryStabilityState.Experimental;
        return new CognitiveMemoryAggregateConfidenceCalibration(score, bucket, stabilityState);
    }
}

public sealed record CognitiveMemoryAggregateConfidenceCalibrationRequest(
    int ValidationIssueCount,
    int ClaimCount,
    int DistinctSourceItemCount,
    int StrongestClaimSourceMemoryCount);

public sealed record CognitiveMemoryAggregateConfidenceCalibration(
    double Score,
    CognitiveMemoryScoreProjectionBucket Bucket,
    CognitiveMemoryStabilityState StabilityState);
