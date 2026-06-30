using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CognitiveMemory;

namespace CanDoItAll.Tests.Support.CognitiveMemory;

public sealed class FakeCognitiveMemoryEmbeddingProvider(int dimensions = 8) : ICognitiveMemoryEmbeddingProvider
{
    public ValueTask<CognitiveMemoryEmbeddingResult> EmbedAsync(
        CognitiveMemoryEmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (dimensions <= 0)
        {
            throw new InvalidOperationException("Fake embedding dimensions must be positive.");
        }

        var tracker = new CognitiveMemoryBudgetTracker(request.Budget, DateTimeOffset.UnixEpoch);
        var decision = tracker.TryAccept(Encoding.UTF8.GetByteCount(request.Input), DateTimeOffset.UnixEpoch, cancellationToken);
        if (!decision.Accepted)
        {
            throw new InvalidOperationException($"Fake embedding request exceeded the {decision.Limit} budget.");
        }

        var hash = CognitiveMemoryHash.FromUtf8($"{request.EmbeddingProfileId}:{request.Input}");
        var hashBytes = Convert.FromHexString(hash.Value);
        var values = new float[dimensions];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = hashBytes[index % hashBytes.Length] / 255f;
        }

        return ValueTask.FromResult(new CognitiveMemoryEmbeddingResult(
            request.EmbeddingProfileId,
            hash,
            new CognitiveMemoryVector(values),
            $"fake-embedding:{dimensions}"));
    }
}

public sealed class FakeCognitiveMemoryVectorStore : ICognitiveMemoryVectorStore
{
    private readonly Dictionary<CognitiveMemoryProjectionProfileId, IReadOnlyList<CognitiveMemoryVectorSearchHit>> hitsByProfile = [];

    public void SetHits(
        CognitiveMemoryProjectionProfileId projectionProfileId,
        IReadOnlyList<CognitiveMemoryVectorSearchHit> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);
        hitsByProfile[projectionProfileId] = hits;
    }

    public ValueTask<CognitiveMemoryVectorSearchResult> SearchAsync(
        CognitiveMemoryVectorSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!hitsByProfile.TryGetValue(request.ProjectionProfileId, out var hits))
        {
            throw new InvalidOperationException($"No fake vector hits were registered for profile '{request.ProjectionProfileId}'.");
        }

        var pageHits = hits
            .OrderBy(hit => hit.ProviderDistance)
            .Take(request.Page.Take)
            .ToList();

        return ValueTask.FromResult(new CognitiveMemoryVectorSearchResult(
            request.ProjectionProfileId,
            pageHits,
            $"fake-vector-store:{pageHits.Count}/{hits.Count}"));
    }
}

public sealed class FakeProjectStructureSourceSnapshotProvider : IProjectStructureSourceSnapshotProvider
{
    public const string ProviderVersion = "fake-project-structure-v1";

    private readonly Dictionary<Guid, IReadOnlyList<MemorySourceItem>> itemsByProject = [];

    public void SetItems(Guid projectId, IReadOnlyList<MemorySourceItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        itemsByProject[projectId] = items;
    }

    public Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ProjectStructureSourceSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!itemsByProject.TryGetValue(request.ProjectId, out var items))
        {
            throw new InvalidOperationException($"No fake project-structure source items were registered for project '{request.ProjectId:D}'.");
        }

        var snapshotAnchor = MemorySourceSnapshotHasher.Compute(items.Select(item => item.ContentHash).ToArray());
        var page = MemorySourceSnapshotPage.Apply(
            items,
            request.Cursor,
            request.Take,
            MemorySourceKind.WorkbenchProjectStructure,
            request.ProjectId,
            ProviderVersion,
            out var nextCursor,
            out var hasMore,
            snapshotAnchor);

        var manifest = new MemorySourceSnapshotManifest(
            MemorySourceSnapshotId.Create(MemorySourceKind.WorkbenchProjectStructure, request.ProjectId, snapshotAnchor),
            MemorySourceKind.WorkbenchProjectStructure,
            request.ProjectId,
            DateTimeOffset.UnixEpoch,
            items.Count,
            nextCursor,
            hasMore,
            hasMore ? MemorySourceSnapshotPageStatus.PageReturned : MemorySourceSnapshotPageStatus.EndOfSource,
            MemorySourceSnapshotHashScope.FullSnapshot,
            ProviderVersion);

        return Task.FromResult(new MemorySourceSnapshot(manifest, page));
    }

    public static MemorySourceItem CreateNode(
        Guid projectId,
        string sourceEntityId,
        string title,
        string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(content);

        var itemId = MemorySourceItemId.Create(
            MemorySourceKind.WorkbenchProjectStructure,
            projectId,
            MemorySourceEntityKind.ProjectNode,
            sourceEntityId);
        var contentHash = MemorySourceSnapshotHasher.Compute(sourceEntityId, title, content);

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.WorkbenchProjectStructure,
            MemorySourceEntityKind.ProjectNode,
            title,
            content,
            contentHash,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            new MemorySourceProvenance(
                MemorySourceKind.WorkbenchProjectStructure,
                projectId,
                MemorySourceEntityKind.ProjectNode,
                sourceEntityId,
                $"/projects/{projectId:D}/structure/{sourceEntityId}"),
            new MemorySourcePermissionContext(
                MemorySourceAccessMode.ReadOnly,
                MemorySourceSensitivity.Internal,
                ContainsSensitivePayload: false,
                RedactionPolicy: "fake-no-redaction",
                AllowedFutureUsageSummary: "Deterministic fake source snapshot tests."),
            null,
            [],
            [],
            null,
            new Dictionary<string, string>
            {
                ["fake"] = "true"
            })
        {
            HashPolicy = MemorySourceHashPolicy.PublicRedactedContent
        };
    }
}

public sealed class FakeProcessRuntimeEvidenceSourceProvider : IProcessRuntimeEvidenceSourceProvider
{
    public const string ProviderVersion = "fake-process-runtime-v1";

    private readonly Dictionary<Guid, IReadOnlyList<MemorySourceItem>> itemsByScope = [];

    public void SetItems(Guid scopeId, IReadOnlyList<MemorySourceItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        itemsByScope[scopeId] = items;
    }

    public Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ProcessRuntimeEvidenceSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scopeId = request.ProcessRunId ?? Guid.Empty;
        if (!itemsByScope.TryGetValue(scopeId, out var items))
        {
            throw new InvalidOperationException($"No fake process-runtime source items were registered for scope '{scopeId:D}'.");
        }

        var snapshotAnchor = MemorySourceSnapshotHasher.Compute(items.Select(item => item.ContentHash).ToArray());
        var page = MemorySourceSnapshotPage.Apply(
            items,
            request.Cursor,
            request.Take,
            MemorySourceKind.ProcessRuntime,
            scopeId,
            ProviderVersion,
            out var nextCursor,
            out var hasMore,
            snapshotAnchor);

        var manifest = new MemorySourceSnapshotManifest(
            MemorySourceSnapshotId.Create(MemorySourceKind.ProcessRuntime, scopeId, snapshotAnchor),
            MemorySourceKind.ProcessRuntime,
            scopeId,
            DateTimeOffset.UnixEpoch,
            items.Count,
            nextCursor,
            hasMore,
            hasMore ? MemorySourceSnapshotPageStatus.PageReturned : MemorySourceSnapshotPageStatus.EndOfSource,
            MemorySourceSnapshotHashScope.FullSnapshot,
            ProviderVersion);

        return Task.FromResult(new MemorySourceSnapshot(manifest, page));
    }

    public static MemorySourceItem CreateRun(Guid processRunId, string title, string content)
        => CreateItem(
            MemorySourceKind.ProcessRuntime,
            processRunId,
            MemorySourceEntityKind.ProcessRun,
            processRunId.ToString("D"),
            title,
            content);

    private static MemorySourceItem CreateItem(
        MemorySourceKind sourceKind,
        Guid scopeId,
        MemorySourceEntityKind entityKind,
        string sourceEntityId,
        string title,
        string content)
    {
        var itemId = MemorySourceItemId.Create(sourceKind, scopeId, entityKind, sourceEntityId);
        var contentHash = MemorySourceSnapshotHasher.Compute(sourceEntityId, title, content);
        return new MemorySourceItem(
            itemId,
            sourceKind,
            entityKind,
            title,
            content,
            contentHash,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            new MemorySourceProvenance(sourceKind, scopeId, entityKind, sourceEntityId, $"/fake/{sourceKind}/{sourceEntityId}"),
            new MemorySourcePermissionContext(
                MemorySourceAccessMode.ReadOnly,
                MemorySourceSensitivity.Internal,
                ContainsSensitivePayload: false,
                RedactionPolicy: "fake-no-redaction",
                AllowedFutureUsageSummary: "Deterministic fake process source snapshot tests."),
            null,
            [],
            [],
            null,
            new Dictionary<string, string>
            {
                ["fake"] = "true"
            })
        {
            HashPolicy = MemorySourceHashPolicy.PublicRedactedContent
        };
    }
}

public sealed class FakeWorkflowRuntimeEvidenceSourceProvider : IWorkflowRuntimeEvidenceSourceProvider
{
    public const string ProviderVersion = "fake-workflow-runtime-v1";

    private readonly Dictionary<Guid, IReadOnlyList<MemorySourceItem>> itemsByRun = [];

    public void SetItems(Guid runId, IReadOnlyList<MemorySourceItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        itemsByRun[runId] = items;
    }

    public Task<MemorySourceSnapshot> ReadSnapshotAsync(
        WorkflowRuntimeEvidenceSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runId = request.RunId?.Value ?? Guid.Empty;
        if (!itemsByRun.TryGetValue(runId, out var items))
        {
            throw new InvalidOperationException($"No fake workflow-runtime source items were registered for run '{runId:D}'.");
        }

        var snapshotAnchor = MemorySourceSnapshotHasher.Compute(items.Select(item => item.ContentHash).ToArray());
        var page = MemorySourceSnapshotPage.Apply(
            items,
            request.Cursor,
            request.Take,
            MemorySourceKind.WorkflowRuntime,
            runId,
            ProviderVersion,
            out var nextCursor,
            out var hasMore,
            snapshotAnchor);

        var manifest = new MemorySourceSnapshotManifest(
            MemorySourceSnapshotId.Create(MemorySourceKind.WorkflowRuntime, runId, snapshotAnchor),
            MemorySourceKind.WorkflowRuntime,
            runId,
            DateTimeOffset.UnixEpoch,
            items.Count,
            nextCursor,
            hasMore,
            hasMore ? MemorySourceSnapshotPageStatus.PageReturned : MemorySourceSnapshotPageStatus.EndOfSource,
            MemorySourceSnapshotHashScope.FullSnapshot,
            ProviderVersion);

        return Task.FromResult(new MemorySourceSnapshot(manifest, page));
    }

    public static MemorySourceItem CreateRun(Guid runId, string title, string content)
    {
        var itemId = MemorySourceItemId.Create(
            MemorySourceKind.WorkflowRuntime,
            runId,
            MemorySourceEntityKind.WorkflowRun,
            runId.ToString("D"));
        var contentHash = MemorySourceSnapshotHasher.Compute(runId.ToString("D"), title, content);
        return new MemorySourceItem(
            itemId,
            MemorySourceKind.WorkflowRuntime,
            MemorySourceEntityKind.WorkflowRun,
            title,
            content,
            contentHash,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            new MemorySourceProvenance(MemorySourceKind.WorkflowRuntime, runId, MemorySourceEntityKind.WorkflowRun, runId.ToString("D"), $"/fake/workflow/{runId:D}"),
            new MemorySourcePermissionContext(
                MemorySourceAccessMode.ReadOnly,
                MemorySourceSensitivity.Internal,
                ContainsSensitivePayload: false,
                RedactionPolicy: "fake-no-redaction",
                AllowedFutureUsageSummary: "Deterministic fake workflow source snapshot tests."),
            null,
            [],
            [],
            null,
            new Dictionary<string, string>
            {
                ["fake"] = "true"
            })
        {
            HashPolicy = MemorySourceHashPolicy.PublicRedactedContent
        };
    }
}

public static class CognitiveMemoryFakePolicyContexts
{
    public static CognitiveMemoryPolicyContext Project(
        Guid projectId,
        bool allowRestrictedContent = false)
        => new(
            projectId,
            "agent:fake",
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("fake-policy"),
            CognitiveMemoryRiskLevel.Low,
            allowRestrictedContent);
}
