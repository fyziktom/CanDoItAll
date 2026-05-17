using CanDoItAll.Modules.CognitiveMemory;

namespace CanDoItAll.Tests.Support.CognitiveMemory;

public sealed class FakeCognitiveMemoryScoreGeometryDriver : ICognitiveMemoryScoreGeometryDriver
{
    private readonly CognitiveMemoryScoreGeometryDriver driver = new(new CognitiveMemoryScoreSpaceRegistry());

    public ValueTask<CognitiveMemoryScoreEvaluationTrace> EvaluateAsync(
        CognitiveMemoryScoreEvaluationRequest request,
        CancellationToken cancellationToken = default)
        => driver.EvaluateAsync(request, cancellationToken);
}

public static class CognitiveMemoryScoreGeometryFixtures
{
    public static CognitiveMemoryScoreEvaluationRequest DockerProductionCandidateAgainstTestBoundary(
        Guid projectId,
        Guid ownerId)
    {
        var schemaVersion = CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion;
        var normalizationProfile = CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile;
        var algorithmVersion = CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion;
        var calculatedAtUtc = DateTimeOffset.UnixEpoch;
        var vector = new CognitiveMemoryScoreVectorSnapshot(
            CognitiveMemoryScoreSpaceKind.RecallCandidate,
            schemaVersion,
            normalizationProfile,
            [
                Component(CognitiveMemoryScoreDimensionKind.SemanticSimilarity, 0.96),
                Component(CognitiveMemoryScoreDimensionKind.ContextFit, 0.92),
                Component(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.9),
                Component(CognitiveMemoryScoreDimensionKind.ContextSeparation, 0.91),
                Component(CognitiveMemoryScoreDimensionKind.LexicalMatch, 0.9),
                Component(CognitiveMemoryScoreDimensionKind.GraphProximity, 0.82),
                Component(CognitiveMemoryScoreDimensionKind.WorkspaceFocusFit, 0.88),
                Component(CognitiveMemoryScoreDimensionKind.MemoryActivation, 0.78),
                Component(CognitiveMemoryScoreDimensionKind.EvidenceSupport, 0.9),
                Component(CognitiveMemoryScoreDimensionKind.MetadataFit, 0.86),
                Component(CognitiveMemoryScoreDimensionKind.TemporalRecency, 0.72),
                Component(CognitiveMemoryScoreDimensionKind.HumanValidation, 0.92),
                Component(CognitiveMemoryScoreDimensionKind.ContradictionPressure, 0.02),
                Component(CognitiveMemoryScoreDimensionKind.StalenessPressure, 0.05),
                Component(CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, 0),
                Component(CognitiveMemoryScoreDimensionKind.RedactionPressure, 0)
            ],
            algorithmVersion,
            calculatedAtUtc,
            CognitiveMemoryHash.FromUtf8("docker-production-candidate-vs-test-boundary"));

        var inhibitShape = new CognitiveMemoryScoreShapeSnapshot(
            CognitiveMemoryScoreShapeKind.ThresholdEnvelope,
            CognitiveMemoryScoreSpaceKind.RecallCandidate,
            schemaVersion,
            [
                new CognitiveMemoryScoreShapeComponent(
                    CognitiveMemoryScoreDimensionKind.SemanticSimilarity,
                    center: 0.95,
                    lowerBound: 0.9,
                    upperBound: null,
                    weight: 1),
                new CognitiveMemoryScoreShapeComponent(
                    CognitiveMemoryScoreDimensionKind.ContextSeparation,
                    center: 0.9,
                    lowerBound: 0.85,
                    upperBound: null,
                    weight: 1.4)
            ],
            radius: null,
            CognitiveMemoryScoreProjectionBucket.Inhibit,
            "High semantic similarity is inhibited because Docker production and test contexts are separated.",
            [],
            algorithmVersion);

        return new CognitiveMemoryScoreEvaluationRequest(
            projectId,
            CognitiveMemoryScoreOwnerKind.MemoryRecord,
            ownerId,
            CognitiveMemoryScoreSpaceKind.RecallCandidate,
            schemaVersion,
            [vector],
            [inhibitShape]);
    }

    public static CognitiveMemoryScoreComponent Component(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double value,
        double confidence = 1)
        => new(
            dimensionKind,
            value,
            confidence,
            [
                new CognitiveMemoryScoreEvidenceRef(
                    CognitiveMemoryScoreEvidenceKind.SourceItem,
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    confidence,
                    DateTimeOffset.UnixEpoch)
            ]);
}
