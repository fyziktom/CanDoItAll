using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

internal static class CognitiveMemoryProfessorAnchorTransitionAudit
{
    private const string AlgorithmVersion = "professor-anchor-lifecycle-v2-events";
    private const string ScoreSchemaVersion = "professor-anchor-transition-v1";
    private const string NormalizationProfile = "professor-anchor-transition";
    private const string PolicyProfileId = "system:professor-anchor-lifecycle";

    public static void AddTransition(
        AppDbContext dbContext,
        CognitiveMemoryCuratorCapturedImprovementRecord capture,
        CognitiveMemoryProfessorAnchorState previousState,
        CognitiveMemoryProfessorAnchorState nextState,
        DateTimeOffset observedAtUtc,
        string reason,
        bool manualReviewConfirmed = false,
        Guid? derivedMemoryRecordId = null)
    {
        if (previousState == nextState)
        {
            return;
        }

        var traceId = Guid.NewGuid();
        var ownerMemoryRecordId = derivedMemoryRecordId ??
                                  capture.AssimilatedMemoryRecordId ??
                                  capture.AppliedMemoryRecordId;
        dbContext.Add(new CognitiveMemoryScoreEvaluationTraceRecord
        {
            Id = traceId,
            ProjectId = capture.ProjectId,
            OwnerKind = ownerMemoryRecordId is null
                ? CognitiveMemoryScoreOwnerKind.Unknown
                : CognitiveMemoryScoreOwnerKind.MemoryRecord,
            OwnerId = ownerMemoryRecordId,
            SpaceKind = CognitiveMemoryScoreSpaceKind.SalienceSignal,
            SchemaVersion = ScoreSchemaVersion,
            NormalizationProfile = NormalizationProfile,
            AlgorithmVersion = AlgorithmVersion,
            InputHash = CognitiveMemoryHash.FromUtf8($"{capture.Id:D}:{previousState}:{nextState}:{observedAtUtc:O}:{reason}").Value,
            ScalarProjectionKind = CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            ProjectionBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
            DisplayScore = 1,
            MatchedShapeCount = 1,
            TracePayloadJson = "{}",
            CalculatedAtUtc = observedAtUtc,
            CreatedAtUtc = observedAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        });
        dbContext.Add(new CognitiveMemorySignalRecord
        {
            ProjectId = capture.ProjectId,
            SignalKind = CognitiveMemorySignalKind.ProfessorAnchorLifecycleTransition,
            SourceKind = CognitiveMemorySignalSourceKind.ProfessorAnchorLifecycle,
            ActorKind = CognitiveMemoryActorKind.System,
            ActorId = string.IsNullOrWhiteSpace(capture.ActorId)
                ? "system:cognitive-memory"
                : capture.ActorId,
            PolicyProfileId = PolicyProfileId,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            RequiresReview = false,
            MemoryRecordId = ownerMemoryRecordId,
            SourceItemId = capture.SourceItemId,
            SignalScoreEvaluationTraceId = traceId,
            ScoreSchemaVersion = ScoreSchemaVersion,
            NormalizationProfileId = NormalizationProfile,
            AlgorithmVersion = AlgorithmVersion,
            ComponentCount = 1,
            MatchedShapeCount = 1,
            DisplayMagnitudeProjection = 1,
            Summary = $"Professor anchor transition {previousState} -> {nextState}: {reason}",
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, string>
                {
                    ["captureId"] = capture.Id.ToString("D"),
                    ["previousState"] = previousState.ToString(),
                    ["nextState"] = nextState.ToString(),
                    ["derivedMemoryRecordId"] = derivedMemoryRecordId?.ToString("D") ?? string.Empty,
                    ["manualReviewConfirmed"] = manualReviewConfirmed.ToString()
                },
                CognitiveMemoryAdvancedJson.Options),
            ObservedAtUtc = observedAtUtc,
            CreatedAtUtc = observedAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        });
    }
}
