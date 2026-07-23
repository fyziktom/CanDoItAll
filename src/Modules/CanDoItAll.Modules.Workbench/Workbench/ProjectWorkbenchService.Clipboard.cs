using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectWorkbenchService
{
    public async Task<ProjectStructureClipboardCopyResult> CopySubtreesAsync(
        Guid projectId,
        IReadOnlyCollection<string> sourceRootNodeKeys,
        string targetParentNodeKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetParentNodeKey);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await using var mutationScope =
            await ProjectStructureSerializableMutationScope.BeginAsync(
                dbContext,
                ProjectStructureSerializableMutationScope.ForProject(projectId),
                cancellationToken);

        var normalizedTargetNodeKey = ProjectWorkbenchGraphConventions.NormalizeEditableParentNodeKey(
            projectId,
            targetParentNodeKey);
        var assembly = await projectStructureAssemblyService.LoadAsync(dbContext, projectId, cancellationToken);
        ProjectStructureEditableForestResolver.ValidateTarget(
            projectId,
            normalizedTargetNodeKey,
            assembly.Nodes);

        var editableNodes = await dbContext.Set<ProjectObjectRecord>()
            .AsNoTracking()
            .Where(node => node.ProjectId == projectId && !node.IsSystemManaged)
            .ToListAsync(cancellationToken);
        var forest = ProjectStructureEditableForestResolver.Resolve(
            projectId,
            editableNodes,
            sourceRootNodeKeys);
        foreach (var rootNodeKey in forest.RootNodeKeys)
        {
            EnsureCanonicalTaskResourceChildAllowed(
                normalizedTargetNodeKey,
                forest.NodesByKey[rootNodeKey].ObjectType,
                assembly.Nodes,
                allowCanonicalTaskResourceChild: false);
        }

        foreach (var resourceChild in forest.Nodes.Where(node =>
                     ProjectStructureTaskResourceGraphPolicy.IsResourceChildType(
                         node.ObjectType) &&
                     node.ParentNodeKey is not null &&
                     forest.NodesByKey.TryGetValue(
                         node.ParentNodeKey,
                         out var copiedParent) &&
                     ProjectStructureCanonicalTaskMutationPolicy.IsTask(
                         copiedParent.ObjectType,
                         copiedParent.ObjectSubtype)))
        {
            ProjectStructureCanonicalTaskMutationPolicy
                .EnsureGenericResourceAttachmentAllowed(
                    forest.NodesByKey[resourceChild.ParentNodeKey!].ObjectType,
                    forest.NodesByKey[resourceChild.ParentNodeKey!].ObjectSubtype);
        }

        var copiedAtUtc = clock.GetUtcNow();
        var objectIdMap = forest.Nodes.ToDictionary(node => node.Id, _ => Guid.NewGuid());
        var nodeKeyMap = forest.Nodes.ToDictionary(
            node => node.NodeKey,
            _ => $"custom:{Guid.NewGuid():N}",
            StringComparer.Ordinal);
        var nodeReferenceIdMap = BuildNodeReferenceIdMap(nodeKeyMap);
        var clonesBySourceNodeKey = forest.Nodes.ToDictionary(
            node => node.NodeKey,
            node => CloneNode(
                node,
                objectIdMap[node.Id],
                nodeKeyMap[node.NodeKey],
                ResolveCopiedParentNodeKey(node, forest.RootNodeKeySet, nodeKeyMap, normalizedTargetNodeKey),
                nodeKeyMap,
                copiedAtUtc),
            StringComparer.Ordinal);

        PlaceCopiedForest(
            assembly.Nodes,
            forest,
            clonesBySourceNodeKey,
            normalizedTargetNodeKey);

        await dbContext.Set<ProjectObjectRecord>().AddRangeAsync(clonesBySourceNodeKey.Values, cancellationToken);
        var sourceObjectIds = objectIdMap.Keys.ToArray();
        var sourceBindings = await dbContext.Set<ProjectNodeBindingRecord>()
            .AsNoTracking()
            .Where(binding => sourceObjectIds.Contains(binding.ProjectObjectId))
            .ToListAsync(cancellationToken);
        if (sourceBindings.Count > 0)
        {
            await dbContext.Set<ProjectNodeBindingRecord>().AddRangeAsync(
                sourceBindings.Select(binding => new ProjectNodeBindingRecord
                {
                    Id = Guid.NewGuid(),
                    ProjectObjectId = objectIdMap[binding.ProjectObjectId],
                    Route = binding.Route,
                    ExternalArtifactKind = binding.ExternalArtifactKind,
                    ExternalArtifactId = binding.ExternalArtifactId,
                    MediaRelativePath = binding.MediaRelativePath,
                    MediaContentType = binding.MediaContentType,
                    MediaOriginalFileName = binding.MediaOriginalFileName,
                    StorageObjectReferenceJson = binding.StorageObjectReferenceJson,
                    CreatedAtUtc = copiedAtUtc,
                    UpdatedAtUtc = copiedAtUtc
                }),
                cancellationToken);
        }

        var sourceReferences = await dbContext.Set<ProjectNodeReferenceRecord>()
            .AsNoTracking()
            .Where(reference =>
                sourceObjectIds.Contains(reference.ProjectObjectId) &&
                reference.ReferenceKind != ProjectNodeReferenceKinds.WorkItemAssigneeParticipant)
            .ToListAsync(cancellationToken);
        if (sourceReferences.Count > 0)
        {
            await dbContext.Set<ProjectNodeReferenceRecord>().AddRangeAsync(
                sourceReferences.Select(reference => new ProjectNodeReferenceRecord
                {
                    Id = Guid.NewGuid(),
                    ProjectObjectId = objectIdMap[reference.ProjectObjectId],
                    ReferenceKind = reference.ReferenceKind,
                    ReferenceId = RemapNodeReferenceId(
                        reference.ReferenceKind,
                        reference.ReferenceId,
                        nodeReferenceIdMap),
                    OrderIndex = reference.OrderIndex,
                    CreatedAtUtc = copiedAtUtc
                }),
                cancellationToken);
        }

        var copiedNodeKeys = forest.Nodes
            .Select(node => node.NodeKey)
            .ToHashSet(StringComparer.Ordinal);
        var internalUserLinks = await dbContext.Set<ProjectObjectLinkRecord>()
            .AsNoTracking()
            .Where(link =>
                link.ProjectId == projectId &&
                !link.IsSystemManaged &&
                link.LinkKind != ProjectObjectLinkKind.Contains &&
                link.LinkKind != ProjectObjectLinkKind.BelongsTo &&
                copiedNodeKeys.Contains(link.SourceNodeKey) &&
                copiedNodeKeys.Contains(link.TargetNodeKey))
            .ToListAsync(cancellationToken);
        if (internalUserLinks.Count > 0)
        {
            await dbContext.Set<ProjectObjectLinkRecord>().AddRangeAsync(
                internalUserLinks.Select(link => new ProjectObjectLinkRecord
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    SourceNodeKey = nodeKeyMap[link.SourceNodeKey],
                    TargetNodeKey = nodeKeyMap[link.TargetNodeKey],
                    LinkKind = link.LinkKind,
                    IsSystemManaged = false,
                    CreatedAtUtc = copiedAtUtc
                }),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);

        return new ProjectStructureClipboardCopyResult(
            forest.RootNodeKeys.Select(nodeKey => nodeKeyMap[nodeKey]).ToList(),
            nodeKeyMap);
    }

    private static ProjectObjectRecord CloneNode(
        ProjectObjectRecord source,
        Guid id,
        string nodeKey,
        string parentNodeKey,
        IReadOnlyDictionary<string, string> nodeKeyMap,
        DateTimeOffset copiedAtUtc)
    {
        var copyState = ProjectObjectClipboardCopyPolicy.Prepare(source, nodeKeyMap);

        return new ProjectObjectRecord
        {
            Id = id,
            ProjectId = source.ProjectId,
            NodeKey = nodeKey,
            ObjectType = source.ObjectType,
            Title = source.Title,
            Subtitle = source.Subtitle,
            Status = copyState.Status,
            Notes = source.Notes,
            ObjectSubtype = source.ObjectSubtype,
            ProgressMode = copyState.ProgressMode,
            ProgressPercent = copyState.ProgressPercent,
            MarkersJson = copyState.MarkersJson,
            Priority = source.Priority,
            MetadataJson = copyState.MetadataJson,
            ParentNodeKey = parentNodeKey,
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            StartUtc = source.StartUtc,
            EndUtc = source.EndUtc,
            DurationSeconds = source.DurationSeconds,
            IsSystemManaged = false,
            CreatedAtUtc = copiedAtUtc,
            UpdatedAtUtc = copiedAtUtc
        };
    }

    private static string ResolveCopiedParentNodeKey(
        ProjectObjectRecord source,
        IReadOnlySet<string> rootNodeKeys,
        IReadOnlyDictionary<string, string> nodeKeyMap,
        string targetParentNodeKey)
    {
        if (rootNodeKeys.Contains(source.NodeKey))
        {
            return targetParentNodeKey;
        }

        if (source.ParentNodeKey is not null && nodeKeyMap.TryGetValue(source.ParentNodeKey, out var copiedParentNodeKey))
        {
            return copiedParentNodeKey;
        }

        throw new InvalidOperationException(
            $"Editable descendant '{source.NodeKey}' does not have a copied parent in the selected forest.");
    }

    private static IReadOnlyDictionary<Guid, Guid> BuildNodeReferenceIdMap(
        IReadOnlyDictionary<string, string> nodeKeyMap)
    {
        var result = new Dictionary<Guid, Guid>();
        foreach (var (sourceNodeKey, copiedNodeKey) in nodeKeyMap)
        {
            if (TryParseCustomNodeKey(sourceNodeKey, out var sourceNodeId) &&
                TryParseCustomNodeKey(copiedNodeKey, out var copiedNodeId))
            {
                result.Add(sourceNodeId, copiedNodeId);
            }
        }

        return result;
    }

    private static string RemapNodeReferenceId(
        string referenceKind,
        string referenceId,
        IReadOnlyDictionary<Guid, Guid> nodeReferenceIdMap)
    {
        return ProjectObjectClipboardCopyPolicy.IsInternalNodeReferenceKind(referenceKind) &&
            Guid.TryParse(referenceId, out var referencedNodeId) &&
            nodeReferenceIdMap.TryGetValue(referencedNodeId, out var copiedNodeId)
                ? copiedNodeId.ToString("D")
                : referenceId;
    }

    private static bool TryParseCustomNodeKey(string nodeKey, out Guid nodeId)
    {
        const string customNodeKeyPrefix = "custom:";
        nodeId = Guid.Empty;
        return nodeKey.StartsWith(customNodeKeyPrefix, StringComparison.Ordinal) &&
            Guid.TryParse(nodeKey[customNodeKeyPrefix.Length..], out nodeId);
    }

    private static void PlaceCopiedForest(
        IReadOnlyList<ProjectObjectRecord> assembledNodes,
        ProjectStructureEditableForest forest,
        IReadOnlyDictionary<string, ProjectObjectRecord> clonesBySourceNodeKey,
        string targetParentNodeKey)
    {
        var placementSession = new ProjectStructureAutomaticPlacementSession(assembledNodes);

        foreach (var sourceRootNodeKey in forest.RootNodeKeys)
        {
            var sourceRoot = forest.NodesByKey[sourceRootNodeKey];
            var copiedRoot = clonesBySourceNodeKey[sourceRootNodeKey];
            var targetPosition = placementSession.Resolve(new ProjectStructureAutomaticPlacementRequest(
                targetParentNodeKey,
                copiedRoot.ObjectType,
                copiedRoot.Title,
                copiedRoot.Subtitle,
                copiedRoot.Notes,
                (sourceRoot.PositionX, sourceRoot.PositionY)));
            var deltaX = targetPosition.X - copiedRoot.PositionX;
            var deltaY = targetPosition.Y - copiedRoot.PositionY;

            foreach (var sourceNode in forest.Trees[sourceRootNodeKey])
            {
                var copiedNode = clonesBySourceNodeKey[sourceNode.NodeKey];
                copiedNode.PositionX += deltaX;
                copiedNode.PositionY += deltaY;
                placementSession.Add(copiedNode);
            }
        }
    }
}

internal sealed record ProjectObjectClipboardCopyState(
    string Status,
    string ProgressMode,
    int ProgressPercent,
    string MarkersJson,
    string MetadataJson);

internal static class ProjectObjectClipboardCopyPolicy
{
    private const string WorkflowReadyStatus = "Ready";
    private const string WorkflowReadyProgressMode = "progress";

    private static readonly IReadOnlySet<string> InternalNodeReferenceKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        ProjectNodeReferenceKinds.MeetingParticipant,
        ProjectNodeReferenceKinds.RecordingMeetingNode,
        ProjectNodeReferenceKinds.RecordingTranscriptNode,
        ProjectNodeReferenceKinds.TranscriptRecordingNode,
        ProjectNodeReferenceKinds.ParticipantParentParticipant,
        ProjectNodeReferenceKinds.WorkItemRepositoryResource,
        ProjectNodeReferenceKinds.RepositoryResource,
        ProjectNodeReferenceKinds.EnvironmentRepositoryResource,
        ProjectNodeReferenceKinds.InfrastructureSecretReference
    };

    private static readonly IReadOnlySet<string> WorkflowOwnedMarkerIcons = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "alert",
        "pause",
        "stop"
    };

    public static ProjectObjectClipboardCopyState Prepare(
        ProjectObjectRecord source,
        IReadOnlyDictionary<string, string> nodeKeyMap)
    {
        var metadata = ProjectObjectMetadataSerializer.Parse(source.MetadataJson);
        metadata.WorkflowProjectWrite = null;
        if (metadata.DeferredCompletion is { } deferredCompletion)
        {
            if (deferredCompletion.State != ProjectStructureDeferredNodeCompletionState.Completed)
            {
                throw new InvalidOperationException(
                    $"Node '{source.NodeKey}' has deferred completion state '{deferredCompletion.State}' and cannot be copied before completion.");
            }

            metadata.DeferredCompletion = null;
        }

        if (metadata.WorkItem is not null)
        {
            metadata.WorkItem.AssigneePartyDisplayName = string.Empty;
        }

        if (metadata.Workflow is { } workflow)
        {
            ResetWorkflowRuntimeState(workflow);
            workflow.InputSettings ??= ProjectStructureWorkflowInputSettings.Default();
            RemapWorkflowInputSettings(workflow.InputSettings, nodeKeyMap);
        }

        var isCanonicalTask = ProjectStructureCanonicalTaskMutationPolicy.IsTask(
            source.ObjectType,
            source.ObjectSubtype);
        var metadataJson = ProjectObjectMetadataSerializer.Serialize(metadata);
        metadataJson = ProjectStructureCanonicalTaskCreationPolicy.NormalizeMetadataJson(
            source.ObjectType,
            source.ObjectSubtype,
            metadataJson,
            ProjectObjectTaskPricingInitialization.ClearAuthoritativePricing);
        var isWorkflowNode = source.ObjectType is ProjectObjectType.WorkflowDefinition or ProjectObjectType.WorkflowRun;
        return new ProjectObjectClipboardCopyState(
            isWorkflowNode ? WorkflowReadyStatus : isCanonicalTask ? "Draft" : source.Status,
            isWorkflowNode ? WorkflowReadyProgressMode : isCanonicalTask ? "progress" : source.ProgressMode,
            isWorkflowNode || isCanonicalTask ? 0 : source.ProgressPercent,
            isWorkflowNode ? FilterWorkflowOwnedMarkers(source.MarkersJson) : source.MarkersJson,
            metadataJson);
    }

    public static bool IsInternalNodeReferenceKind(string referenceKind)
    {
        return InternalNodeReferenceKinds.Contains(referenceKind);
    }

    private static void ResetWorkflowRuntimeState(ProjectWorkflowNodeMetadata workflow)
    {
        workflow.LastRunId = null;
        workflow.LastRunState = null;
        workflow.LastRunSummary = string.Empty;
        workflow.LastCreatedNodeIds = [];
        workflow.LastCreatedAssetIds = [];
        workflow.LastCreatedFilePaths = [];
        workflow.LastStepIndex = 0;
        workflow.LastStepCount = 0;
        workflow.LastStartedAtUtc = null;
        workflow.LastUpdatedAtUtc = null;
    }

    private static void RemapWorkflowInputSettings(
        ProjectStructureWorkflowInputSettings inputSettings,
        IReadOnlyDictionary<string, string> nodeKeyMap)
    {
        inputSettings.SelectedNodeIds = inputSettings.SelectedNodeIds
            .Select(nodeId => nodeKeyMap.GetValueOrDefault(nodeId, nodeId))
            .ToList();
        inputSettings.AdditionalSources = inputSettings.AdditionalSources
            .Select(source =>
                source.Kind == ProjectStructureWorkflowInputSourceKind.SelectedNode &&
                nodeKeyMap.TryGetValue(source.Value, out var copiedNodeKey)
                    ? source with { Value = copiedNodeKey }
                    : source)
            .ToList();
    }

    private static string FilterWorkflowOwnedMarkers(string markersJson)
    {
        return ProjectNodeMarkerState.Serialize(
            ProjectNodeMarkerState.Parse(markersJson)
                .Where(marker => !WorkflowOwnedMarkerIcons.Contains(marker.Icon)));
    }
}

internal sealed record ProjectStructureEditableForest(
    IReadOnlyList<string> RootNodeKeys,
    IReadOnlySet<string> RootNodeKeySet,
    IReadOnlyList<ProjectObjectRecord> Nodes,
    IReadOnlyDictionary<string, ProjectObjectRecord> NodesByKey,
    IReadOnlyDictionary<string, IReadOnlyList<ProjectObjectRecord>> Trees);

internal static class ProjectStructureEditableForestResolver
{
    public static ProjectStructureEditableForest Resolve(
        Guid projectId,
        IReadOnlyCollection<ProjectObjectRecord> editableNodes,
        IReadOnlyCollection<string> requestedRootNodeKeys)
    {
        ArgumentNullException.ThrowIfNull(requestedRootNodeKeys);
        if (requestedRootNodeKeys.Count == 0)
        {
            throw new ArgumentException("At least one source node is required.", nameof(requestedRootNodeKeys));
        }

        var normalizedRequestedNodeKeys = new List<string>(requestedRootNodeKeys.Count);
        var requestedNodeKeySet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requestedNodeKey in requestedRootNodeKeys)
        {
            if (string.IsNullOrWhiteSpace(requestedNodeKey))
            {
                throw new ArgumentException("Source node keys cannot be blank.", nameof(requestedRootNodeKeys));
            }

            var normalizedNodeKey = requestedNodeKey.Trim();
            if (requestedNodeKeySet.Add(normalizedNodeKey))
            {
                normalizedRequestedNodeKeys.Add(normalizedNodeKey);
            }
        }

        var editableNodesByKey = editableNodes.ToDictionary(node => node.NodeKey, StringComparer.Ordinal);
        foreach (var requestedNodeKey in normalizedRequestedNodeKeys)
        {
            if (!editableNodesByKey.ContainsKey(requestedNodeKey))
            {
                throw new InvalidOperationException(
                    $"Source node '{requestedNodeKey}' is not a persisted editable node in project '{projectId}'.");
            }
        }

        var rootNodeKeys = normalizedRequestedNodeKeys
            .Where(nodeKey => !HasSelectedAncestor(
                editableNodesByKey[nodeKey],
                editableNodesByKey,
                requestedNodeKeySet))
            .ToList();
        var childrenByParentNodeKey = editableNodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentNodeKey))
            .GroupBy(node => node.ParentNodeKey!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(node => node.NodeKey, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);
        var trees = new Dictionary<string, IReadOnlyList<ProjectObjectRecord>>(StringComparer.Ordinal);
        var forestNodes = new List<ProjectObjectRecord>();
        var forestNodeKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rootNodeKey in rootNodeKeys)
        {
            var tree = ResolveTree(rootNodeKey, editableNodesByKey, childrenByParentNodeKey);
            trees.Add(rootNodeKey, tree);
            foreach (var node in tree)
            {
                if (!forestNodeKeys.Add(node.NodeKey))
                {
                    throw new InvalidOperationException(
                        $"Editable node '{node.NodeKey}' belongs to more than one selected subtree.");
                }

                forestNodes.Add(node);
            }
        }

        return new ProjectStructureEditableForest(
            rootNodeKeys,
            rootNodeKeys.ToHashSet(StringComparer.Ordinal),
            forestNodes,
            forestNodes.ToDictionary(node => node.NodeKey, StringComparer.Ordinal),
            trees);
    }

    public static void ValidateTarget(
        Guid projectId,
        string targetParentNodeKey,
        IReadOnlyCollection<ProjectObjectRecord> assembledNodes)
    {
        if (string.Equals(
            targetParentNodeKey,
            ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId),
            StringComparison.Ordinal))
        {
            return;
        }

        if (ProjectWorkbenchGraphConventions.TryResolveProjectHierarchyNode(
                targetParentNodeKey,
                out _,
                out _))
        {
            throw new InvalidOperationException(
                $"Target parent node '{targetParentNodeKey}' is a projected project node and cannot receive editable children in project '{projectId}'.");
        }

        if (!assembledNodes.Any(node => string.Equals(node.NodeKey, targetParentNodeKey, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Target parent node '{targetParentNodeKey}' was not found in project '{projectId}'.");
        }
    }

    private static bool HasSelectedAncestor(
        ProjectObjectRecord node,
        IReadOnlyDictionary<string, ProjectObjectRecord> editableNodesByKey,
        IReadOnlySet<string> requestedNodeKeys)
    {
        var visitedNodeKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            node.NodeKey
        };
        var parentNodeKey = node.ParentNodeKey;

        while (!string.IsNullOrWhiteSpace(parentNodeKey) && editableNodesByKey.TryGetValue(parentNodeKey, out var parent))
        {
            if (!visitedNodeKeys.Add(parent.NodeKey))
            {
                throw new InvalidOperationException("The persisted project structure contains a hierarchy cycle.");
            }

            if (requestedNodeKeys.Contains(parent.NodeKey))
            {
                return true;
            }

            parentNodeKey = parent.ParentNodeKey;
        }

        return false;
    }

    private static IReadOnlyList<ProjectObjectRecord> ResolveTree(
        string rootNodeKey,
        IReadOnlyDictionary<string, ProjectObjectRecord> editableNodesByKey,
        IReadOnlyDictionary<string, List<ProjectObjectRecord>> childrenByParentNodeKey)
    {
        var result = new List<ProjectObjectRecord>();
        var visitedNodeKeys = new HashSet<string>(StringComparer.Ordinal);
        var pendingNodes = new Stack<ProjectObjectRecord>();
        pendingNodes.Push(editableNodesByKey[rootNodeKey]);

        while (pendingNodes.Count > 0)
        {
            var node = pendingNodes.Pop();
            if (!visitedNodeKeys.Add(node.NodeKey))
            {
                throw new InvalidOperationException("The persisted project structure contains a hierarchy cycle.");
            }

            result.Add(node);
            if (!childrenByParentNodeKey.TryGetValue(node.NodeKey, out var children))
            {
                continue;
            }

            for (var index = children.Count - 1; index >= 0; index--)
            {
                pendingNodes.Push(children[index]);
            }
        }

        return result;
    }
}
