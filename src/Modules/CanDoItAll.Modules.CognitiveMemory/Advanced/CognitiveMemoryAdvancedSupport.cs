using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

internal static class CognitiveMemoryDistributedWorkerRecordExtensions
{
    public static void CreatedOrUpdated(
        this CognitiveMemoryDistributedWorkerRecord worker,
        string machineName,
        IReadOnlyList<CognitiveMemoryDistributedJobKind> capabilities,
        DateTimeOffset now)
    {
        worker.MachineName = CognitiveMemoryGuard.EnsureText(machineName, nameof(machineName));
        worker.Status = CognitiveMemoryDistributedWorkerStatus.Active;
        worker.CapabilitiesJson = JsonSerializer.Serialize(capabilities, CognitiveMemoryAdvancedJson.Options);
        worker.LastSeenAtUtc = now;
    }
}

internal static class CognitiveMemoryAdvancedScoring
{
    public static CognitiveMemoryScoreComponent Component(CognitiveMemoryScoreDimensionKind kind, double value, double confidence = 1)
        => new(kind, Math.Clamp(value, 0, 1), Math.Clamp(confidence, 0, 1));

    public static async Task<CognitiveMemoryScoreEvaluationTrace> EvaluateAndPersistAsync(
        AppDbContext dbContext,
        ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
        Guid? projectId,
        CognitiveMemoryScoreOwnerKind ownerKind,
        Guid? ownerId,
        CognitiveMemoryScoreSpaceKind spaceKind,
        IReadOnlyList<CognitiveMemoryScoreComponent> components,
        CognitiveMemoryScoreProjectionBucket bucket,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var vector = new CognitiveMemoryScoreVectorSnapshot(
            spaceKind,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile,
            components,
            CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion,
            now,
            CognitiveMemoryHash.FromUtf8($"{projectId:D}|{ownerKind}|{ownerId:D}|{spaceKind}|{string.Join('|', components.Select(item => $"{item.DimensionKind}:{item.NormalizedValue:0.000}"))}"));
        var shape = new CognitiveMemoryScoreShapeSnapshot(
            CognitiveMemoryScoreShapeKind.ThresholdEnvelope,
            spaceKind,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            components.Select(component => new CognitiveMemoryScoreShapeComponent(
                    component.DimensionKind,
                    component.NormalizedValue,
                    0,
                    1,
                    1))
                .ToArray(),
            radius: null,
            bucket,
            $"{spaceKind} evaluated by cognitive-memory advanced service.",
            [],
            CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion);
        var trace = await scoreGeometryDriver.EvaluateAsync(
            new CognitiveMemoryScoreEvaluationRequest(
                projectId,
                ownerKind,
                ownerId,
                spaceKind,
                CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
                [vector],
                [shape]),
            cancellationToken);
        await CognitiveMemoryScoreTracePersistence.AddIfMissingAsync(dbContext, trace, now, cancellationToken);
        return trace;
    }
}

internal static class CognitiveMemoryAdvancedJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
