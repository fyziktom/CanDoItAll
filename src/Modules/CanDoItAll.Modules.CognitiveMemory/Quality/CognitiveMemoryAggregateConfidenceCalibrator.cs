namespace CanDoItAll.Modules.CognitiveMemory;

public interface ICognitiveMemoryAggregateConfidenceCalibrator
{
    CognitiveMemoryAggregateConfidenceCalibration Calibrate(CognitiveMemoryAggregateConfidenceCalibrationRequest request);
}

public sealed class CognitiveMemoryAggregateConfidenceCalibrator : ICognitiveMemoryAggregateConfidenceCalibrator
{
    public CognitiveMemoryAggregateConfidenceCalibration Calibrate(CognitiveMemoryAggregateConfidenceCalibrationRequest request)
    {
        var validatedClaimCount = request.ValidatedClaimCount <= 0 ? request.ClaimCount : request.ValidatedClaimCount;
        var sourceMapCount = request.SourceMapCount <= 0 ? request.DistinctSourceItemCount : request.SourceMapCount;
        var sourceBreadthScore = Math.Clamp(request.DistinctSourceItemCount / 6d, 0, 1) * 0.08;
        var claimAgreementScore = Math.Clamp((request.StrongestClaimSourceMemoryCount - 1) * 0.04, 0, 0.12);
        var claimPenalty = Math.Clamp((request.ClaimCount - 1) * 0.015, 0, 0.06);
        var issuePenalty = Math.Clamp(request.ValidationIssueCount * 0.05, 0, 0.2);
        var validationDepthPenalty = validatedClaimCount < request.ClaimCount
            ? 0.12
            : Math.Clamp(Math.Max(0, request.ClaimCount * 2 - sourceMapCount) * 0.02, 0, 0.08);
        var complexityPenalty = Math.Clamp(request.OperatorBearingClaimCount * 0.025 + request.ClaimComplexityScore * 0.006, 0, 0.14);
        var score = Math.Round(
            Math.Clamp(0.68 + sourceBreadthScore + claimAgreementScore - claimPenalty - issuePenalty - validationDepthPenalty - complexityPenalty, 0.55, 0.92),
            3,
            MidpointRounding.AwayFromZero);
        var bucket = score >= 0.88 &&
                     request.DistinctSourceItemCount >= 6 &&
                     request.StrongestClaimSourceMemoryCount >= 3 &&
                     request.ClaimCount <= 2 &&
                     request.ValidationIssueCount == 0 &&
                     validationDepthPenalty == 0 &&
                     complexityPenalty <= 0.02
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
    int StrongestClaimSourceMemoryCount,
    int ValidatedClaimCount = 0,
    int SourceMapCount = 0,
    int OperatorBearingClaimCount = 0,
    int ClaimComplexityScore = 0);

public sealed record CognitiveMemoryAggregateConfidenceCalibration(
    double Score,
    CognitiveMemoryScoreProjectionBucket Bucket,
    CognitiveMemoryStabilityState StabilityState);
