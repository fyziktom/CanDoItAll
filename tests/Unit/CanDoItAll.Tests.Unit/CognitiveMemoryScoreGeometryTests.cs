using System.Text.Json;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.Tests.Support.CognitiveMemory;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryScoreGeometryTests
{
    [Fact]
    public async Task ScoreSpaceRegistry_ExposesEveryInitialScoreSpaceWithTypedDimensions()
    {
        var registry = new CognitiveMemoryScoreSpaceRegistry();
        var expectedKinds = Enum.GetValues<CognitiveMemoryScoreSpaceKind>()
            .Where(kind => kind != CognitiveMemoryScoreSpaceKind.Unknown)
            .ToList();

        foreach (var kind in expectedKinds)
        {
            var definition = await registry.GetDefinitionAsync(kind, CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion);

            Assert.Equal(kind, definition.Kind);
            Assert.NotEmpty(definition.Dimensions);
            Assert.DoesNotContain(definition.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.Unknown);
            Assert.Equal(
                definition.Dimensions.Count,
                definition.Dimensions.Select(dimension => dimension.Kind).Distinct().Count());
        }
    }

    [Fact]
    public async Task ScoreGeometryDriver_ReportsMissingRequiredDimensionsWithoutNeutralDefaults()
    {
        var driver = new FakeCognitiveMemoryScoreGeometryDriver();
        var schemaVersion = CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion;
        var vector = new CognitiveMemoryScoreVectorSnapshot(
            CognitiveMemoryScoreSpaceKind.RecallCandidate,
            schemaVersion,
            CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile,
            [
                CognitiveMemoryScoreGeometryFixtures.Component(CognitiveMemoryScoreDimensionKind.SemanticSimilarity, 0.94),
                CognitiveMemoryScoreGeometryFixtures.Component(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.88)
            ],
            CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion,
            DateTimeOffset.UnixEpoch,
            CognitiveMemoryHash.FromUtf8("missing-context-fit"));

        var trace = await driver.EvaluateAsync(new CognitiveMemoryScoreEvaluationRequest(
            Guid.NewGuid(),
            CognitiveMemoryScoreOwnerKind.MemoryRecord,
            Guid.NewGuid(),
            CognitiveMemoryScoreSpaceKind.RecallCandidate,
            schemaVersion,
            [vector],
            []));

        var missing = Assert.Single(trace.MissingRequiredDimensions);
        var projection = Assert.IsType<CognitiveMemoryScoreScalarProjection>(trace.ScalarProjection);
        Assert.Equal(CognitiveMemoryScoreDimensionKind.ContextFit, missing.DimensionKind);
        Assert.Equal(CognitiveMemoryScoreProjectionBucket.Reject, projection.Bucket);
        Assert.Null(projection.DisplayScore);
    }

    [Fact]
    public async Task ScoreGeometryDriver_InhibitsDockerContextBoundaryDespiteHighSemanticSimilarity()
    {
        var driver = new FakeCognitiveMemoryScoreGeometryDriver();
        var request = CognitiveMemoryScoreGeometryFixtures.DockerProductionCandidateAgainstTestBoundary(
            Guid.NewGuid(),
            Guid.NewGuid());

        var trace = await driver.EvaluateAsync(request);

        var projection = Assert.IsType<CognitiveMemoryScoreScalarProjection>(trace.ScalarProjection);
        Assert.Equal(CognitiveMemoryScoreProjectionBucket.Inhibit, projection.Bucket);
        Assert.True(projection.DisplayScore > 0.8);
        var matchedShape = Assert.Single(trace.MatchedShapes);
        Assert.Contains("Docker production and test contexts", matchedShape.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScoreGeometryDriver_IsDeterministicForFixedVectorAndShapeInputs()
    {
        var driver = new FakeCognitiveMemoryScoreGeometryDriver();
        var request = CognitiveMemoryScoreGeometryFixtures.DockerProductionCandidateAgainstTestBoundary(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"));

        var first = await driver.EvaluateAsync(request);
        var second = await driver.EvaluateAsync(request);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.ScalarProjection, second.ScalarProjection);
        Assert.Equal(first.DecisionExplanation, second.DecisionExplanation);
    }

    [Fact]
    public async Task ScoreEvaluationTrace_RoundTripsThroughSourceGeneratedJsonContext()
    {
        var driver = new FakeCognitiveMemoryScoreGeometryDriver();
        var request = CognitiveMemoryScoreGeometryFixtures.DockerProductionCandidateAgainstTestBoundary(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"));
        var trace = await driver.EvaluateAsync(request);

        var json = JsonSerializer.Serialize(
            trace,
            CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryScoreEvaluationTrace);
        var roundTrip = JsonSerializer.Deserialize(
            json,
            CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryScoreEvaluationTrace);

        Assert.NotNull(roundTrip);
        Assert.Equal(trace.Id, roundTrip.Id);
        Assert.Equal(CognitiveMemoryScoreProjectionBucket.Inhibit, roundTrip.ScalarProjection?.Bucket);
        Assert.Single(roundTrip.MatchedShapes);
    }

    [Fact]
    public void CognitiveMemoryContracts_DoNotIntroduceScalarOnlyBehaviorScoringSurfaces()
    {
        var offenders = typeof(CognitiveMemoryScoreEvaluationTrace).Assembly.GetTypes()
            .Where(type => string.Equals(type.Namespace, typeof(CognitiveMemoryScoreEvaluationTrace).Namespace, StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties().Select(property => new { Type = type, Property = property }))
            .Where(item =>
                string.Equals(item.Property.Name, "FinalScore", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Property.Name, "ScoreBreakdown", StringComparison.OrdinalIgnoreCase) ||
                IsDictionaryStringDouble(item.Property.PropertyType) ||
                IsScalarOnlyPriority(item.Type, item.Property))
            .Select(item => $"{item.Type.Name}.{item.Property.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    private static bool IsDictionaryStringDouble(Type propertyType)
    {
        if (!propertyType.IsGenericType)
        {
            return false;
        }

        var genericType = propertyType.GetGenericTypeDefinition();
        if (genericType != typeof(Dictionary<,>) &&
            genericType != typeof(IReadOnlyDictionary<,>))
        {
            return false;
        }

        var arguments = propertyType.GetGenericArguments();
        return arguments[0] == typeof(string) && arguments[1] == typeof(double);
    }

    private static bool IsScalarOnlyPriority(Type type, System.Reflection.PropertyInfo property)
        => string.Equals(property.Name, "Priority", StringComparison.OrdinalIgnoreCase) &&
           type != typeof(CognitiveMemoryScoreScalarProjection) &&
           (property.PropertyType == typeof(double) ||
            property.PropertyType == typeof(double?) ||
            property.PropertyType == typeof(int) ||
            property.PropertyType == typeof(int?));
}
