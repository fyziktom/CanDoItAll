using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.Modules.Workbench.ProjectStructure;

internal sealed class ProjectStructureInvocationSnapshotReadContext
{
    internal ProjectStructureInvocationSnapshotReadContext(
        AgentRuntimeToolProviderPurpose purpose,
        AgentRuntimeContextIntent contextIntent,
        ImmutableArray<AgentChatContextAttachmentEnvelope> exactAttachments,
        ImmutableArray<AgentChatContextAttachmentEnvelope> advertisedKindAttachments)
    {
        Purpose = purpose;
        ContextIntent = contextIntent;
        ExactAttachments = exactAttachments;
        AdvertisedKindAttachments = advertisedKindAttachments;
    }

    public AgentRuntimeToolProviderPurpose Purpose { get; }

    public AgentRuntimeContextIntent ContextIntent { get; }

    public ImmutableArray<AgentChatContextAttachmentEnvelope> ExactAttachments { get; }

    public ImmutableArray<AgentChatContextAttachmentEnvelope> AdvertisedKindAttachments { get; }

    public static ProjectStructureInvocationSnapshotReadContext Capture(
        AgentRuntimeToolProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ProjectStructureInvocationSnapshotReadContext(
            context.Purpose,
            context.ContextIntent,
            context.GetAttachments<ProjectStructureInvocationSnapshot>(),
            context.Attachments
                .Where(static attachment => string.Equals(
                    attachment.Kind.Value,
                    ProjectStructureInvocationSnapshotMapper.AttachmentKindValue,
                    StringComparison.Ordinal))
                .ToImmutableArray());
    }
}

internal sealed record ProjectStructureReadDispatchResult(
    ProjectStructureReadResponse Response,
    ProjectStructureReadSource Source);

internal static class ProjectStructureInvocationSnapshotReadDispatcher
{
    private const string SnapshotCoverageGuidance =
        "The invocation snapshot contains hierarchy, classification, status, progress, priority, schedule, project relationships, links, and selection from the held UI surface. Notes, metadata, assets, layout, routes, action capabilities, storage references, and file contents are omitted. Set request.source to CanonicalCurrent when those facts are required.";

    public static ProjectStructureReadSource ResolveEffectiveSource(
        ProjectStructureReadSource requestedSource,
        AgentRuntimeToolProviderPurpose purpose,
        AgentRuntimeContextIntent contextIntent)
    {
        ArgumentNullException.ThrowIfNull(contextIntent);
        if (!Enum.IsDefined(requestedSource))
        {
            throw Failure(
                400,
                "ProjectStructureReadSourceInvalid",
                $"Project-structure read source '{requestedSource}' is undefined.");
        }

        return requestedSource switch
        {
            ProjectStructureReadSource.ContextDefault
                when IsInteractiveProjectStructureContext(purpose, contextIntent)
                => ProjectStructureReadSource.InvocationSnapshot,
            ProjectStructureReadSource.ContextDefault
                => ProjectStructureReadSource.CanonicalCurrent,
            ProjectStructureReadSource.InvocationSnapshot
                when IsInteractiveProjectStructureContext(purpose, contextIntent)
                => ProjectStructureReadSource.InvocationSnapshot,
            ProjectStructureReadSource.InvocationSnapshot
                => throw Failure(
                    409,
                    "ProjectStructureInvocationSnapshotContextIneligible",
                    "InvocationSnapshot is available only to interactive project-structure chat. Governed-process and non-project contexts must use CanonicalCurrent."),
            ProjectStructureReadSource.CanonicalCurrent
                => ProjectStructureReadSource.CanonicalCurrent,
            _ => throw Failure(
                400,
                "ProjectStructureReadSourceInvalid",
                $"Project-structure read source '{requestedSource}' is undefined.")
        };
    }

    public static async Task<ProjectStructureReadDispatchResult> ReadAsync(
        ProjectStructureInvocationSnapshotReadContext context,
        DatabaseProfileGeneration currentDatabaseProfileGeneration,
        DateTimeOffset nowUtc,
        Guid projectId,
        ProjectStructureReadRequest request,
        Func<ProjectStructureReadRequest, CancellationToken, Task<ProjectStructureReadResponse>> canonicalRead,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(canonicalRead);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveSource = ResolveEffectiveSource(
            request.Source,
            context.Purpose,
            context.ContextIntent);
        if (effectiveSource == ProjectStructureReadSource.CanonicalCurrent)
        {
            var canonicalResponse = await canonicalRead(
                request with { Source = ProjectStructureReadSource.CanonicalCurrent },
                cancellationToken);
            return new ProjectStructureReadDispatchResult(
                canonicalResponse,
                ProjectStructureReadSource.CanonicalCurrent);
        }

        var snapshot = ResolveSnapshot(
            context,
            currentDatabaseProfileGeneration,
            nowUtc.ToUniversalTime(),
            projectId);
        EnsureCoverage(snapshot, request);
        return new ProjectStructureReadDispatchResult(
            Project(snapshot, request),
            ProjectStructureReadSource.InvocationSnapshot);
    }

    private static bool IsInteractiveProjectStructureContext(
        AgentRuntimeToolProviderPurpose purpose,
        AgentRuntimeContextIntent contextIntent)
    {
        return purpose == AgentRuntimeToolProviderPurpose.InteractiveChat &&
               !contextIntent.IsGovernedProcessStep &&
               string.Equals(
                   contextIntent.SourceKind,
                   ProjectStructureAgentChatContextBuilder.SourceKind,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectStructureInvocationSnapshot ResolveSnapshot(
        ProjectStructureInvocationSnapshotReadContext context,
        DatabaseProfileGeneration currentDatabaseProfileGeneration,
        DateTimeOffset nowUtc,
        Guid projectId)
    {
        if (context.ExactAttachments.Length == 0)
        {
            if (context.AdvertisedKindAttachments.Length > 0)
            {
                throw Failure(
                    409,
                    "ProjectStructureInvocationSnapshotTypeMismatch",
                    "The project-structure invocation attachment kind was published with an unexpected payload type.");
            }

            throw Failure(
                409,
                "ProjectStructureInvocationSnapshotUnavailable",
                "No project-structure invocation snapshot was captured for this agent invocation. Reopen the chat from a ready project-structure surface or use CanonicalCurrent.");
        }

        if (context.ExactAttachments.Length != 1)
        {
            throw Failure(
                409,
                "ProjectStructureInvocationSnapshotAmbiguous",
                $"The invocation contains {context.ExactAttachments.Length} project-structure snapshots; exactly one is required.");
        }

        var envelope = context.ExactAttachments[0];
        if (!envelope.TryGetAttachment<ProjectStructureInvocationSnapshot>(out var snapshot))
        {
            throw Failure(
                409,
                "ProjectStructureInvocationSnapshotTypeMismatch",
                "The project-structure invocation snapshot payload type does not match its envelope.");
        }

        if (!string.Equals(
                envelope.Kind.Value,
                ProjectStructureInvocationSnapshotMapper.AttachmentKindValue,
                StringComparison.Ordinal))
        {
            throw Failure(
                409,
                "ProjectStructureInvocationSnapshotKindMismatch",
                $"The project-structure snapshot attachment kind '{envelope.Kind.Value}' is not supported.");
        }

        if (!Guid.TryParse(context.ContextIntent.SourceId, out var contextProjectId) ||
            contextProjectId == Guid.Empty ||
            !string.Equals(
                envelope.Source.Kind.Value,
                ProjectStructureAgentChatContextBuilder.SourceKind,
                StringComparison.Ordinal) ||
            !string.Equals(
                envelope.Source.Id.Value,
                context.ContextIntent.SourceId,
                StringComparison.OrdinalIgnoreCase) ||
            envelope.WorkspaceScope is not
            {
                Kind: WorkspaceScopeKind.Project
            } workspaceScope ||
            !Guid.TryParse(workspaceScope.Key, out var workspaceProjectId) ||
            workspaceProjectId != contextProjectId)
        {
            throw Failure(
                409,
                "ProjectStructureInvocationSnapshotScopeMismatch",
                "The project-structure snapshot scope does not match the active runtime context.");
        }

        if (projectId == Guid.Empty ||
            snapshot.ProjectId != projectId ||
            contextProjectId != projectId)
        {
            throw Failure(
                409,
                "ProjectStructureInvocationSnapshotProjectMismatch",
                $"The requested project '{projectId:D}' does not match the captured project-structure invocation snapshot.");
        }

        var freshness = envelope.ResolveFreshness(
            currentDatabaseProfileGeneration,
            nowUtc);
        if (freshness == AgentChatContextAttachmentFreshness.ProfileMismatch)
        {
            throw Failure(
                409,
                "ProjectStructureInvocationSnapshotProfileMismatch",
                "The active database profile generation changed after the project-structure snapshot was captured. Reopen the chat from the current surface or use CanonicalCurrent.");
        }

        if (freshness == AgentChatContextAttachmentFreshness.Expired)
        {
            throw Failure(
                409,
                "ProjectStructureInvocationSnapshotExpired",
                "The held project-structure snapshot exceeded its freshness lifetime. Refresh the surface or use CanonicalCurrent.");
        }

        if (freshness == AgentChatContextAttachmentFreshness.NotYetValid)
        {
            throw Failure(
                409,
                "ProjectStructureInvocationSnapshotNotYetValid",
                "The project-structure snapshot capture time is later than the current execution time. Refresh the surface or use CanonicalCurrent.");
        }

        var contentFingerprint =
            ProjectStructureInvocationSnapshotMapper.ComputeContentFingerprint(snapshot);
        if (envelope.ContentFingerprint != contentFingerprint)
        {
            throw Failure(
                409,
                "ProjectStructureInvocationSnapshotContentMismatch",
                "The project-structure snapshot content fingerprint does not match its captured payload.");
        }

        var coverageFingerprint =
            ProjectStructureInvocationSnapshotMapper.ComputeCoverageFingerprint(snapshot);
        if (envelope.CoverageFingerprint != coverageFingerprint)
        {
            throw Failure(
                409,
                "ProjectStructureInvocationSnapshotCoverageMismatch",
                "The project-structure snapshot coverage fingerprint does not match its captured payload.");
        }

        var freshnessFingerprint =
            ProjectStructureInvocationSnapshotMapper.ComputeFreshnessFingerprint(
                contentFingerprint,
                coverageFingerprint,
                envelope.DatabaseProfileGeneration);
        if (envelope.FreshnessFingerprint != freshnessFingerprint)
        {
            throw Failure(
                409,
                "ProjectStructureInvocationSnapshotFreshnessMismatch",
                "The project-structure snapshot freshness fingerprint does not match its captured payload.");
        }

        return snapshot;
    }

    private static void EnsureCoverage(
        ProjectStructureInvocationSnapshot snapshot,
        ProjectStructureReadRequest request)
    {
        var unsupportedFields = new List<string>();
        if (request.IncludeNotes)
        {
            unsupportedFields.Add("notes");
        }

        if (request.IncludeMetadata)
        {
            unsupportedFields.Add("metadata");
        }

        if (request.IncludeAssets)
        {
            unsupportedFields.Add("assets");
        }

        if (request.IncludeLayout)
        {
            unsupportedFields.Add("layout");
        }

        if (unsupportedFields.Count > 0)
        {
            throw CoverageFailure(
                $"The invocation snapshot intentionally omits {string.Join(", ", unsupportedFields)}.");
        }

        if (request.IncludeLinks && !snapshot.Coverage.HasCompleteLinks)
        {
            throw CoverageFailure(
                "The held surface does not provide complete link coverage for this invocation snapshot.");
        }

        if (!snapshot.Coverage.HasCompletePriorityDerivation)
        {
            throw CoverageFailure(
                "The held surface does not provide complete priority derivation for the captured nodes.");
        }

        var requestedNodeIds = NormalizeIds(request.NodeIds);
        var requestedSubtreeRootIds = NormalizeIds(request.SubtreeRootIds);
        var isExactNodeRead =
            requestedNodeIds.Count > 0 &&
            requestedSubtreeRootIds.Count == 0;
        if (!snapshot.Coverage.HasCompleteHierarchy && !isExactNodeRead)
        {
            throw CoverageFailure(
                $"The held surface snapshot contains {snapshot.Coverage.CapturedNodeCount} of {snapshot.Coverage.SourceNodeCount} nodes, so an unscoped, filtered, or subtree read is not covered.");
        }

        if (!snapshot.Coverage.HasCompleteHierarchy && isExactNodeRead)
        {
            var capturedNodeIds = snapshot.Nodes
                .Select(static node => node.Id)
                .ToHashSet(StringComparer.Ordinal);
            var missingNodeIds = requestedNodeIds
                .Where(nodeId => !capturedNodeIds.Contains(nodeId))
                .ToArray();
            if (missingNodeIds.Length > 0)
            {
                throw CoverageFailure(
                    $"The partial invocation snapshot does not cover {missingNodeIds.Length} requested exact node id(s).");
            }
        }
    }

    private static ProjectStructureReadResponse Project(
        ProjectStructureInvocationSnapshot snapshot,
        ProjectStructureReadRequest request)
    {
        var includedNodeIds = ResolveIncludedNodeIds(snapshot.Nodes, request);
        var selectedNodes = snapshot.Nodes
            .Where(node => includedNodeIds is null || includedNodeIds.Contains(node.Id))
            .Where(node =>
                request.ObjectTypes is null ||
                request.ObjectTypes.Count == 0 ||
                request.ObjectTypes.Contains(node.ObjectType))
            .Where(node =>
                request.ProjectRoles is null ||
                request.ProjectRoles.Count == 0 ||
                request.ProjectRoles.Contains(node.ProjectRole))
            .Where(node =>
                request.Statuses is null ||
                request.Statuses.Count == 0 ||
                request.Statuses.Contains(node.Status, StringComparer.OrdinalIgnoreCase))
            .Where(node => !request.OnlyUnfinished || !node.IsFinished)
            .Where(node =>
                !request.MaxPriority.HasValue ||
                node.EffectivePriority > 0 &&
                node.EffectivePriority <= request.MaxPriority.Value)
            .OrderBy(static node => node.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var warnings = new List<string>
        {
            SnapshotCoverageGuidance
        };

        if (request.Take.HasValue && selectedNodes.Count > request.Take.Value)
        {
            selectedNodes = selectedNodes
                .Take(Math.Max(1, request.Take.Value))
                .ToList();
            warnings.Add($"Structure result truncated to {request.Take.Value} nodes.");
        }

        var selectedNodeIds = selectedNodes
            .Select(static node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        var links = request.IncludeLinks
            ? snapshot.Links
                .Where(link =>
                    selectedNodeIds.Contains(link.SourceId) &&
                    selectedNodeIds.Contains(link.TargetId))
                .Select(static link => new ProjectStructureLinkSummary(
                    link.SourceId,
                    link.TargetId,
                    link.Kind,
                    link.IsUserAuthored))
                .ToList()
            : [];
        var nodes = selectedNodes
            .Select(static node => new ProjectStructureNodeSummary(
                node.Id,
                node.ParentId,
                node.ObjectType,
                node.ObjectSubtype,
                node.Title,
                Subtitle: string.Empty,
                node.Status,
                Notes: null,
                Route: string.Empty,
                node.ArtifactKind,
                node.ArtifactId,
                MediaRelativePath: null,
                MediaContentType: null,
                MediaOriginalFileName: null,
                Badges: [],
                node.ProgressMode,
                node.ProgressPercent,
                MarkerIcon: string.Empty,
                MarkerTone: string.Empty,
                MarkerLabel: string.Empty,
                node.Priority,
                node.EffectivePriority,
                node.StartUtc,
                node.EndUtc,
                MetadataJson: null,
                node.ProjectRole,
                node.RelatedProjectId,
                node.ParentProjectCount,
                X: null,
                Y: null,
                node.DurationSeconds,
                ActionCapabilities: null))
            .ToList();

        return new ProjectStructureReadResponse(
            snapshot.ProjectId,
            snapshot.ProjectName,
            nodes,
            links,
            warnings);
    }

    private static HashSet<string>? ResolveIncludedNodeIds(
        IReadOnlyList<ProjectStructureInvocationSnapshotNode> nodes,
        ProjectStructureReadRequest request)
    {
        var selectedNodeIds = NormalizeIds(request.NodeIds);
        var subtreeRootIds = NormalizeIds(request.SubtreeRootIds);
        if (subtreeRootIds.Count > 0)
        {
            var childIdsByParentId = nodes
                .Where(static node => node.ParentId is not null)
                .GroupBy(static node => node.ParentId!, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    static group => group.Select(static node => node.Id).ToArray(),
                    StringComparer.Ordinal);
            foreach (var rootId in subtreeRootIds)
            {
                ExpandSubtree(rootId, childIdsByParentId, selectedNodeIds);
            }
        }

        return selectedNodeIds.Count == 0 ? null : selectedNodeIds;
    }

    private static HashSet<string> NormalizeIds(IReadOnlyList<string>? values)
    {
        return values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
    }

    private static void ExpandSubtree(
        string rootId,
        IReadOnlyDictionary<string, string[]> childIdsByParentId,
        ISet<string> selectedNodeIds)
    {
        var pendingNodeIds = new Queue<string>();
        pendingNodeIds.Enqueue(rootId);
        while (pendingNodeIds.TryDequeue(out var nodeId))
        {
            if (!selectedNodeIds.Add(nodeId) ||
                !childIdsByParentId.TryGetValue(nodeId, out var childIds))
            {
                continue;
            }

            foreach (var childId in childIds)
            {
                pendingNodeIds.Enqueue(childId);
            }
        }
    }

    private static ProjectStructureAgentException CoverageFailure(string reason)
    {
        return Failure(
            409,
            "ProjectStructureInvocationSnapshotCoverageInsufficient",
            $"{reason} {SnapshotCoverageGuidance}");
    }

    private static ProjectStructureAgentException Failure(
        int statusCode,
        string errorCode,
        string message)
    {
        return new ProjectStructureAgentException(statusCode, errorCode, message);
    }
}
