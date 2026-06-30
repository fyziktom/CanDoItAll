using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

internal static class CognitiveMemoryScoreTracePersistence
{
    public static async Task AddIfMissingAsync(
        AppDbContext dbContext,
        CognitiveMemoryScoreEvaluationTrace trace,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(trace);

        var traceId = trace.Id.Value;
        var exists = await dbContext.Set<CognitiveMemoryScoreEvaluationTraceRecord>()
            .AnyAsync(item => item.Id == traceId, cancellationToken);
        if (exists)
        {
            return;
        }

        var scalarProjection = trace.ScalarProjection;
        var primaryVector = trace.InputVectors.FirstOrDefault(vector =>
            vector.SpaceKind == trace.SpaceKind &&
            vector.SchemaVersion == trace.SchemaVersion);
        var inputHash = primaryVector?.InputHash ?? CognitiveMemoryHash.FromUtf8(trace.Id.Value.ToString("D"));

        dbContext.Add(new CognitiveMemoryScoreEvaluationTraceRecord
        {
            Id = traceId,
            ProjectId = trace.ProjectId,
            OwnerKind = trace.OwnerKind,
            OwnerId = trace.OwnerId,
            SpaceKind = trace.SpaceKind,
            SchemaVersion = trace.SchemaVersion.Value,
            NormalizationProfile = primaryVector?.NormalizationProfileId.Value ?? CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile.Value,
            AlgorithmVersion = trace.AlgorithmVersion.Value,
            InputHashAlgorithm = inputHash.Algorithm,
            InputHash = inputHash.Value,
            ScalarProjectionKind = scalarProjection?.ProjectionKind ?? CognitiveMemoryScoreScalarProjectionKind.None,
            ProjectionBucket = scalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            DisplayScore = scalarProjection?.DisplayScore,
            MissingRequiredDimensionCount = trace.MissingRequiredDimensions.Count,
            MatchedShapeCount = trace.MatchedShapes.Count,
            TracePayloadJson = JsonSerializer.Serialize(
                trace,
                CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryScoreEvaluationTrace),
            CalculatedAtUtc = trace.CalculatedAtUtc,
            CreatedAtUtc = createdAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        });

        foreach (var vector in trace.InputVectors)
        {
            foreach (var component in vector.Components)
            {
                var evidence = component.EvidenceRefs.FirstOrDefault();
                dbContext.Add(new CognitiveMemoryScoreComponentRecord
                {
                    ScoreEvaluationTraceId = traceId,
                    ProjectId = trace.ProjectId,
                    OwnerKind = trace.OwnerKind,
                    OwnerId = trace.OwnerId,
                    SpaceKind = vector.SpaceKind,
                    SchemaVersion = vector.SchemaVersion.Value,
                    DimensionKind = component.DimensionKind,
                    NormalizedValue = component.NormalizedValue,
                    Confidence = component.Confidence,
                    EvidenceKind = evidence?.EvidenceKind ?? CognitiveMemoryScoreEvidenceKind.Unknown,
                    EvidenceId = evidence?.EvidenceId,
                    EvidenceConfidence = evidence?.Confidence,
                    CalculatedAtUtc = vector.CalculatedAtUtc,
                    AlgorithmVersion = vector.AlgorithmVersion.Value,
                    ComponentPayloadJson = JsonSerializer.Serialize(
                        component,
                        CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryScoreComponent)
                });
            }
        }
    }
}
