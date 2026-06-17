using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using System.Net;
using System.Text.Json;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureAgentService(
    ProjectsService projectsService,
    ProjectWorkbenchService projectWorkbenchService,
    ProjectStructureLeaseService leaseService,
    ProjectStructureChecklistService checklistService,
    ProjectStructureImportService importService,
    IProjectStructureRuntimeLauncher runtimeLauncher,
    IProjectStructureLocalFileOpener localFileOpener,
    IWorkspacePathAccessGuard pathAccessGuard,
    IHttpClientFactory httpClientFactory,
    ProjectStructureSourceWorkspacePathResolver sourceWorkspacePathResolver,
    ProjectStructureProcessNodeService processNodeService,
    ProjectStructureWorkflowNodeService workflowNodeService)
{
    private const long MaxExternalAssetSourceBytes = 25L * 1024L * 1024L;

    private static readonly ProjectStructureReadRequest FullNodeReadRequest = new(
        IncludeLinks: true,
        IncludeLayout: true,
        IncludeMetadata: true,
        IncludeNotes: true,
        IncludeAssets: true);

    public Task<IReadOnlyList<ProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        return projectsService.ListAsync(cancellationToken);
    }

    public Task<ProjectHierarchySnapshot> GetHierarchyAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return projectsService.GetHierarchyAsync(projectId, cancellationToken);
    }

    public async Task<ProjectSummary> SaveProjectAsync(
        Guid? projectId,
        ProjectStructureProjectSaveRequest request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectRequest(request);

        var editor = new ProjectEditorModel
        {
            Id = projectId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Objective = request.Objective?.Trim() ?? string.Empty,
            CurrentPhase = request.CurrentPhase?.Trim() ?? string.Empty,
            Status = request.Status,
            TargetDateUtc = request.TargetDateUtc
        };

        if (projectId.HasValue)
        {
            return await leaseService.RunWithProjectMutationLeaseAsync(
                projectId.Value,
                request.LeaseToken,
                agent,
                "save-project",
                async cancellationToken =>
                {
                    var saveResult = await projectsService.SaveAsync(editor, cancellationToken);
                    return await ResolveSavedProjectAsync(saveResult, cancellationToken);
                },
                cancellationToken);
        }

        var createResult = await projectsService.SaveAsync(editor, cancellationToken);
        return await ResolveSavedProjectAsync(createResult, cancellationToken);
    }

    public async Task ChangeSubprojectAsync(
        Guid parentProjectId,
        ProjectStructureSubprojectChangeRequest request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        if (request.ChildProjectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "ChildProjectRequired", "A child project id is required.");
        }

        await leaseService.RunWithProjectMutationLeaseAsync(
            request.ChildProjectId,
            request.LeaseToken,
            agent,
            "change-subproject-link",
            async cancellationToken =>
            {
                Result result;
                if (request.CurrentParentProjectId.HasValue)
                {
                    result = await projectsService.ReconnectSubprojectAsync(
                        request.ChildProjectId,
                        request.CurrentParentProjectId.Value,
                        parentProjectId,
                        cancellationToken);
                }
                else
                {
                    result = await projectsService.AddSubprojectAsync(parentProjectId, request.ChildProjectId, cancellationToken);
                }

                ThrowIfFailure(result);
                return 0;
            },
            cancellationToken);
    }

    public async Task<ProjectStructureReadResponse> GetStructureAsync(
        Guid projectId,
        ProjectStructureReadRequest request,
        CancellationToken cancellationToken = default)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var effectivePriorities = ProjectStructureChecklistRules.BuildEffectivePriorityMap(surface.Nodes);
        var includedNodeIds = ResolveIncludedNodeIds(surface.Nodes, request);
        var selectedNodes = surface.Nodes
            .Where(node => includedNodeIds is null || includedNodeIds.Contains(node.Id))
            .Where(node => request.ObjectTypes is null || request.ObjectTypes.Count == 0 || request.ObjectTypes.Contains(node.ObjectType))
            .Where(node => request.ProjectRoles is null || request.ProjectRoles.Count == 0 || request.ProjectRoles.Contains(node.ProjectRole))
            .Where(node => request.Statuses is null || request.Statuses.Count == 0 || request.Statuses.Contains(node.Status, StringComparer.OrdinalIgnoreCase))
            .Where(node => !request.OnlyUnfinished || !ProjectStructureChecklistRules.IsFinished(node))
            .Where(node => !request.MaxPriority.HasValue || effectivePriorities.GetValueOrDefault(node.Id) > 0 && effectivePriorities[node.Id] <= request.MaxPriority.Value)
            .OrderBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var warnings = new List<string>();

        if (request.Take.HasValue && selectedNodes.Count > request.Take.Value)
        {
            selectedNodes = selectedNodes.Take(Math.Max(1, request.Take.Value)).ToList();
            warnings.Add($"Structure result truncated to {request.Take.Value} nodes.");
        }

        var selectedIds = selectedNodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var links = request.IncludeLinks
            ? surface.Links
                .Where(link => selectedIds.Contains(link.SourceId) && selectedIds.Contains(link.TargetId))
                .Select(link => new ProjectStructureLinkSummary(link.SourceId, link.TargetId, link.Kind, link.IsUserAuthored))
                .ToList()
            : [];

        var mappedNodes = selectedNodes
            .Select(node => MapNodeSummary(node, effectivePriorities.GetValueOrDefault(node.Id), request))
            .ToList();

        return new ProjectStructureReadResponse(surface.ProjectId, surface.ProjectName, mappedNodes, links, warnings);
    }

    public Task<ProjectStructureNodeCatalogResponse> GetNodeCatalogAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProjectStructureCanvasCatalog.BuildAgentNodeCatalog());
    }

    public async Task<ProjectStructureNodeSummary> CreateNodeAsync(
        Guid projectId,
        ProjectStructureNodeCreateInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        EnsureValidMediaPayload(request.Media);

        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "create-structure-node",
            async cancellationToken =>
            {
                var createdNode = await projectWorkbenchService.CreateObjectAsync(
                    projectId,
                    new ProjectObjectCreateRequest(
                        request.ObjectType,
                        request.Title,
                        request.Subtitle,
                        request.Notes,
                        request.ParentNodeKey,
                        request.X,
                        request.Y,
                        request.StartUtc,
                        request.EndUtc,
                        ProjectStructureRequestedNodeKindParser.NormalizeSubtypeForType(request.ObjectType, request.ObjectSubtype),
                        request.Media,
                        request.MetadataJson,
                        request.DurationSeconds),
                    cancellationToken);
                return MapNodeSummary(createdNode, createdNode.Priority, FullNodeReadRequest);
            },
            cancellationToken);
    }

    public async Task<ProjectStructureNodeSummary> UpdateNodeAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureNodeEditInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "update-structure-node",
            async cancellationToken =>
            {
                var existingNode = await GetNodeAsync(projectId, nodeId, cancellationToken);
                var targetObjectType = request.ObjectType ?? existingNode.ObjectType;
                var targetObjectSubtype = request.ObjectSubtype is not null
                    ? ProjectStructureRequestedNodeKindParser.NormalizeSubtypeForType(targetObjectType, request.ObjectSubtype) ?? string.Empty
                    : request.ObjectType.HasValue && request.ObjectType.Value != existingNode.ObjectType
                        ? string.Empty
                        : existingNode.ObjectSubtype;
                var metadataJson = string.IsNullOrWhiteSpace(request.MetadataJson)
                    ? string.IsNullOrWhiteSpace(existingNode.MetadataJson) ? "{}" : existingNode.MetadataJson
                    : request.MetadataJson;

                ProjectStructureNode? updatedNode;
                var requiresReclassification = targetObjectType != existingNode.ObjectType ||
                    !string.Equals(targetObjectSubtype, existingNode.ObjectSubtype, StringComparison.OrdinalIgnoreCase);
                if (requiresReclassification)
                {
                    updatedNode = await projectWorkbenchService.ReclassifyObjectAsync(
                        projectId,
                        nodeId,
                        new ProjectObjectReclassificationRequest(
                            targetObjectType,
                            targetObjectSubtype,
                            request.Title,
                            request.Subtitle,
                            request.Notes,
                            metadataJson),
                        cancellationToken);
                    if (updatedNode is null)
                    {
                        throw new ProjectStructureAgentException(
                            400,
                            "NodeReclassificationUnavailable",
                            $"Node '{nodeId}' cannot be reclassified from '{existingNode.ObjectType}:{existingNode.ObjectSubtype}' to '{targetObjectType}:{targetObjectSubtype}'.");
                    }

                    if (request.StartUtc.HasValue ||
                        request.EndUtc.HasValue ||
                        request.DurationSeconds.HasValue)
                    {
                        updatedNode = await projectWorkbenchService.UpdateObjectAsync(
                            projectId,
                            nodeId,
                            new ProjectObjectEditRequest(
                                updatedNode.Title,
                                updatedNode.Subtitle,
                                updatedNode.Notes,
                                request.StartUtc,
                                request.EndUtc,
                                metadataJson,
                                request.DurationSeconds),
                            cancellationToken);
                    }
                }
                else
                {
                    updatedNode = await projectWorkbenchService.UpdateObjectAsync(
                        projectId,
                        nodeId,
                        new ProjectObjectEditRequest(
                            request.Title,
                            request.Subtitle,
                            request.Notes,
                            request.StartUtc,
                            request.EndUtc,
                            metadataJson,
                            request.DurationSeconds),
                        cancellationToken);
                }

                return MapRequiredNode(updatedNode, nodeId);
            },
            cancellationToken);
    }

    public async Task<ProjectStructureNodeSummary> UpdateNodeTypeAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureNodeTypeInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        var existingNode = await GetNodeAsync(projectId, nodeId, cancellationToken);
        return await UpdateNodeAsync(
            projectId,
            nodeId,
            new ProjectStructureNodeEditInput(
                existingNode.Title,
                existingNode.Subtitle,
                existingNode.Notes,
                request.ObjectType,
                request.ObjectSubtype,
                existingNode.StartUtc,
                existingNode.EndUtc,
                existingNode.MetadataJson,
                request.LeaseToken,
                existingNode.DurationSeconds),
            agent,
            cancellationToken);
    }

    public async Task<ProjectStructureNodeSummary> UpdateNodeMetadataAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureNodeMetadataInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "update-node-metadata",
            async cancellationToken =>
            {
                var updatedNode = await projectWorkbenchService.UpdateObjectMetadataAsync(
                    projectId,
                    nodeId,
                    request.MetadataJson,
                    request.Notes,
                    request.Status,
                    cancellationToken: cancellationToken);
                return MapRequiredNode(updatedNode, nodeId);
            },
            cancellationToken);
    }

    public async Task<int> UpdateNodeStatusesAsync(
        Guid projectId,
        ProjectStructureStatusBatchInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "update-node-statuses",
            cancellationToken => projectWorkbenchService.UpdateObjectStatusesAsync(projectId, request.NodeIds, request.Status, cancellationToken),
            cancellationToken);
    }

    public async Task<int> UpdateNodeProgressAsync(
        Guid projectId,
        ProjectStructureProgressBatchInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "update-node-progress",
            cancellationToken => projectWorkbenchService.UpdateObjectProgressAsync(projectId, request.NodeIds, request.ProgressMode, request.ProgressPercent, cancellationToken),
            cancellationToken);
    }

    public async Task<int> UpdateNodeMarkerAsync(
        Guid projectId,
        ProjectStructureMarkerBatchInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "update-node-marker",
            cancellationToken => projectWorkbenchService.UpdateObjectMarkerAsync(projectId, request.NodeIds, request.MarkerIcon, request.MarkerTone, request.MarkerLabel, cancellationToken),
            cancellationToken);
    }

    public async Task<int> UpdateNodePriorityAsync(
        Guid projectId,
        ProjectStructurePriorityBatchInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "update-node-priority",
            cancellationToken => projectWorkbenchService.UpdateObjectPriorityAsync(projectId, request.NodeIds, request.Priority, cancellationToken),
            cancellationToken);
    }

    public async Task<int> ChangeNodeMarkerAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureMarkerInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "change-node-marker",
            cancellationToken => request.Mode switch
            {
                ProjectStructureMarkerMutationMode.Add => projectWorkbenchService.AddObjectMarkerAsync(
                    projectId,
                    [nodeId],
                    request.MarkerIcon,
                    request.MarkerTone,
                    request.MarkerLabel,
                    cancellationToken),
                ProjectStructureMarkerMutationMode.Toggle => projectWorkbenchService.ToggleObjectMarkerAsync(
                    projectId,
                    [nodeId],
                    request.MarkerIcon,
                    request.MarkerTone,
                    request.MarkerLabel,
                    cancellationToken),
                ProjectStructureMarkerMutationMode.Remove => projectWorkbenchService.RemoveObjectMarkerAsync(
                    projectId,
                    [nodeId],
                    request.MarkerIcon,
                    request.MarkerTone,
                    request.MarkerLabel,
                    cancellationToken),
                ProjectStructureMarkerMutationMode.Clear => projectWorkbenchService.ClearObjectMarkersAsync(
                    projectId,
                    [nodeId],
                    cancellationToken),
                _ => projectWorkbenchService.UpdateObjectMarkerAsync(
                    projectId,
                    [nodeId],
                    request.MarkerIcon,
                    request.MarkerTone,
                    request.MarkerLabel,
                    cancellationToken)
            },
            cancellationToken);
    }

    public async Task MoveNodeAsync(
        Guid projectId,
        ProjectStructureNodeMoveInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "move-structure-node",
            async cancellationToken =>
            {
                await projectWorkbenchService.MoveObjectAsync(projectId, request.NodeId, request.X, request.Y, cancellationToken);
                return 0;
            },
            cancellationToken);
    }

    public async Task<ProjectStructureSubtreeRecompositionResult> RecomposeNodeAsync(
        Guid projectId,
        ProjectStructureNodeRecomposeInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "recompose-structure-node",
            async cancellationToken =>
            {
                var result = await projectWorkbenchService.RecomposeSubtreeAsync(projectId, request.RootNodeId, cancellationToken);
                if (result is null)
                {
                    throw new ProjectStructureAgentException(
                        400,
                        "RecompositionUnavailable",
                        $"Node '{request.RootNodeId}' could not be recomposed because it has no descendants or does not exist.");
                }

                return result;
            },
            cancellationToken);
    }

    public async Task<ProjectStructureNodeSummary> ReparentNodeAsync(
        Guid projectId,
        ProjectStructureNodeReparentInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "reparent-structure-node",
            async cancellationToken =>
            {
                var updatedNode = await projectWorkbenchService.ReparentObjectAsync(projectId, request.NodeId, request.ParentNodeKey, cancellationToken);
                return MapRequiredNode(updatedNode, request.NodeId);
            },
            cancellationToken);
    }

    public async Task<ProjectStructureLinkChangeResult> LinkNodesAsync(
        Guid projectId,
        ProjectStructureLinkInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ValidateLinkInput(request);
        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "link-structure-nodes",
            async cancellationToken =>
            {
                await projectWorkbenchService.LinkObjectsAsync(
                    projectId,
                    request.SourceNodeId,
                    request.TargetNodeId,
                    request.Kind,
                    cancellationToken);
                return new ProjectStructureLinkChangeResult(
                    true,
                    new ProjectStructureLinkSummary(request.SourceNodeId, request.TargetNodeId, request.Kind, true));
            },
            cancellationToken);
    }

    public async Task<ProjectStructureLinkChangeResult> UnlinkNodesAsync(
        Guid projectId,
        ProjectStructureLinkInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ValidateLinkInput(request);
        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "unlink-structure-nodes",
            async cancellationToken =>
            {
                var changed = await projectWorkbenchService.UnlinkObjectsAsync(
                    projectId,
                    request.SourceNodeId,
                    request.TargetNodeId,
                    request.Kind,
                    cancellationToken);
                return new ProjectStructureLinkChangeResult(
                    changed,
                    new ProjectStructureLinkSummary(request.SourceNodeId, request.TargetNodeId, request.Kind, true));
            },
            cancellationToken);
    }

    public Task<ProjectStructureLinkChangeResult> LinkProcessDefinitionAsync(
        Guid projectId,
        string sourceNodeId,
        ProjectStructureProcessDefinitionLinkInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        if (request.ProcessDefinitionId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "ProcessDefinitionRequired", "A process definition id is required.");
        }

        return LinkNodesAsync(
            projectId,
            new ProjectStructureLinkInput(
                sourceNodeId,
                ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(request.ProcessDefinitionId),
                ProjectObjectLinkKind.Uses,
                request.LeaseToken),
            agent,
            cancellationToken);
    }

    public async Task<ProjectStructureSubprojectTransferResult> MoveDescendantsToProjectAsync(
        Guid sourceProjectId,
        string sourceNodeId,
        ProjectStructureSubtreeTransferInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        if (request.TargetProjectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "TargetProjectRequired", "A target project id is required.");
        }

        return await leaseService.RunWithProjectMutationLeaseAsync(
            sourceProjectId,
            request.LeaseToken,
            agent,
            "move-descendants-to-project",
            async cancellationToken =>
            {
                var result = await projectWorkbenchService.MoveDescendantsToProjectAsync(
                    sourceProjectId,
                    sourceNodeId,
                    request.TargetProjectId,
                    cancellationToken);
                if (result is null)
                {
                    throw new ProjectStructureAgentException(
                        404,
                        "NodeNotFound",
                        $"Node '{sourceNodeId}' was not found or has no descendants to transfer.");
                }

                return result;
            },
            cancellationToken);
    }

    public async Task<ProjectStructureNodesToSubprojectResult> MoveNodesToNewSubprojectAsync(
        Guid sourceProjectId,
        ProjectStructureNodesToSubprojectInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        var requestedNodeIds = NormalizeNodeIds(request.NodeIds);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ProjectStructureAgentException(400, "SubprojectNameRequired", "A subproject name is required.");
        }

        if (requestedNodeIds.Count == 0)
        {
            throw new ProjectStructureAgentException(400, "SelectedNodesRequired", "At least one selected project-structure node id is required.");
        }

        return await leaseService.RunWithProjectMutationLeaseAsync(
            sourceProjectId,
            request.LeaseToken,
            agent,
            "move-selected-nodes-to-new-subproject",
            async cancellationToken =>
            {
                var warnings = new List<string>();
                var sourceSurface = await projectWorkbenchService.GetStructureAsync(sourceProjectId, cancellationToken);
                var sourceNodeIds = sourceSurface.Nodes
                    .Where(node => node.ObjectType != ProjectObjectType.ProjectRoot)
                    .Select(node => node.Id)
                    .ToHashSet(StringComparer.Ordinal);
                var existingRequestedNodeIds = requestedNodeIds
                    .Where(sourceNodeIds.Contains)
                    .ToList();
                var missingNodeIds = requestedNodeIds
                    .Where(nodeId => !sourceNodeIds.Contains(nodeId))
                    .ToList();

                if (missingNodeIds.Count > 0)
                {
                    warnings.Add($"Ignored {missingNodeIds.Count} selected node id(s) that were not found in the source project.");
                }

                if (existingRequestedNodeIds.Count == 0)
                {
                    throw new ProjectStructureAgentException(
                        404,
                        "SelectedNodesNotFound",
                        "None of the selected project-structure node ids were found in the source project.",
                        new { requestedNodeIds });
                }

                var createResult = await projectsService.SaveAsync(new ProjectEditorModel
                {
                    Name = request.Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(request.Description)
                        ? $"Extracted from {sourceSurface.ProjectName}."
                        : request.Description.Trim(),
                    Objective = string.IsNullOrWhiteSpace(request.Objective)
                        ? $"Own selected project-structure nodes from {sourceSurface.ProjectName}."
                        : request.Objective.Trim(),
                    CurrentPhase = string.IsNullOrWhiteSpace(request.CurrentPhase)
                        ? "Execution"
                        : request.CurrentPhase.Trim(),
                    Status = request.Status
                }, cancellationToken);
                var targetProject = await ResolveSavedProjectAsync(createResult, cancellationToken);

                ThrowIfFailure(await projectsService.AddSubprojectAsync(sourceProjectId, targetProject.Id, cancellationToken));

                var transfer = await projectWorkbenchService.MoveNodesToProjectAsync(
                    sourceProjectId,
                    existingRequestedNodeIds,
                    targetProject.Id,
                    request.IncludeDescendants,
                    cancellationToken);
                if (transfer is null)
                {
                    throw new ProjectStructureAgentException(
                        400,
                        "SelectedNodesTransferUnavailable",
                        "The selected nodes could not be moved to the new subproject.",
                        new { sourceProjectId, targetProject.Id, existingRequestedNodeIds });
                }

                var targetSurface = await projectWorkbenchService.GetStructureAsync(targetProject.Id, cancellationToken);
                var movedNodeIds = targetSurface.Nodes
                    .Where(node => node.ObjectType != ProjectObjectType.ProjectRoot)
                    .Select(node => node.Id)
                    .OrderBy(nodeId => nodeId, StringComparer.Ordinal)
                    .ToList();

                if (transfer.MovedNodeCount != movedNodeIds.Count)
                {
                    warnings.Add($"Moved {transfer.MovedNodeCount} node(s), but {movedNodeIds.Count} node id(s) were found in the new subproject.");
                }

                return new ProjectStructureNodesToSubprojectResult(
                    sourceProjectId,
                    targetProject.Id,
                    targetProject.Name,
                    requestedNodeIds,
                    movedNodeIds,
                    transfer.MovedNodeCount,
                    transfer.MovedRootCount,
                    warnings);
            },
            cancellationToken);
    }

    public async Task<ArtifactReference> ExecuteNodeCommandAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureNodeCommandInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "execute-node-command",
            async cancellationToken =>
            {
                var artifact = await projectWorkbenchService.ExecuteNodeCommandAsync(
                    projectId,
                    nodeId,
                    request.CommandKind,
                    cancellationToken);
                if (artifact is null)
                {
                    throw new ProjectStructureAgentException(
                        400,
                        "NodeCommandUnavailable",
                        $"Command '{request.CommandKind}' is not available for node '{nodeId}'.");
                }

                return artifact;
            },
            cancellationToken);
    }

    public Task<ProjectStructureProcessNodeStartResult> StartProcessNodeAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureProcessNodeStartInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return processNodeService.StartAsync(projectId, nodeId, request, agent, cancellationToken);
    }

    public Task<ProjectStructureProcessSubprocessLaunchResult> StartProcessSubprocessAsync(
        Guid projectId,
        string parentProcessRunId,
        string parentProcessStepId,
        ProjectStructureProcessSubprocessLaunchInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return processNodeService.StartSubprocessAsync(
            projectId,
            parentProcessRunId,
            parentProcessStepId,
            request,
            agent,
            cancellationToken);
    }

    public Task<ProjectStructureWorkflowNodeCreateResult> CreateWorkflowNodeAsync(
        Guid projectId,
        string parentNodeId,
        ProjectStructureWorkflowNodeCreateInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return workflowNodeService.CreateAsync(projectId, parentNodeId, request, agent, cancellationToken);
    }

    public Task<ProjectStructureWorkflowAddOptionsResult> GetWorkflowAddOptionsAsync(
        Guid projectId,
        string parentNodeId,
        ProjectStructureWorkflowAddOptionsInput request,
        CancellationToken cancellationToken = default)
    {
        return workflowNodeService.GetAddOptionsAsync(projectId, parentNodeId, request, cancellationToken);
    }

    public Task<ProjectStructureWorkflowNodeStartResult> StartWorkflowNodeAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureWorkflowNodeStartInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return workflowNodeService.StartAsync(projectId, nodeId, request, agent, cancellationToken);
    }

    public Task<ProjectStructureWorkflowRunStatus> GetWorkflowNodeStatusAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        return workflowNodeService.GetStatusAsync(projectId, nodeId, cancellationToken);
    }

    public Task<int> DeleteNodeAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureNodeDeleteInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "delete-structure-node",
            cancellationToken => projectWorkbenchService.DeleteObjectAsync(projectId, nodeId, cancellationToken),
            cancellationToken);
    }

    public async Task<ProjectStructureNodeSummary> CreateApprovalRequestAsync(
        Guid projectId,
        ProjectStructureApprovalRequestCreateInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ProjectStructureAgentException(400, "ApprovalTitleRequired", "Approval request title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RequestedOperation))
        {
            throw new ProjectStructureAgentException(400, "ApprovalOperationRequired", "Approval requests must describe the blocked operation.");
        }

        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "create-approval-request",
            async cancellationToken =>
            {
                var metadataJson = string.IsNullOrWhiteSpace(request.MetadataJson)
                    ? JsonSerializer.Serialize(new
                    {
                        approvalRequest = new
                        {
                            requestedOperation = request.RequestedOperation.Trim(),
                            estimatedMinutes = request.EstimatedMinutes,
                            agentId = agent.AgentId,
                            agentName = agent.AgentName,
                            machineName = agent.MachineName,
                            requestedAtUtc = DateTimeOffset.UtcNow
                        }
                    })
                    : request.MetadataJson;

                var createdNode = await projectWorkbenchService.CreateObjectAsync(
                    projectId,
                    new ProjectObjectCreateRequest(
                        ProjectObjectType.Decision,
                        request.Title,
                        request.Subtitle,
                        request.Notes,
                        string.IsNullOrWhiteSpace(request.ParentNodeKey) ? $"project:{projectId}" : request.ParentNodeKey,
                        null,
                        null,
                        null,
                        null,
                        "approval-request",
                        null,
                        metadataJson,
                        null),
                    cancellationToken);

                return MapNodeSummary(createdNode, createdNode.Priority, FullNodeReadRequest);
            },
            cancellationToken);
    }

    public Task<ProjectStructureChecklistResponse> GetChecklistAsync(
        Guid projectId,
        ProjectStructureChecklistRequest request,
        CancellationToken cancellationToken = default)
    {
        return checklistService.GetChecklistAsync(projectId, request, cancellationToken);
    }

    public async Task<ProjectStructureDependencyResponse> GetDependenciesAsync(
        Guid projectId,
        ProjectStructureDependencyQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var warnings = new List<string>();
        var dependencyAnalysis = ProjectStructureDependencyAnalyzer.Build(surface, request.DefaultDurationSeconds ?? 3600);
        var selectedNodeIds = request.NodeIds?
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(nodeId => nodeId.Trim())
            .ToHashSet(StringComparer.Ordinal);

        var items = dependencyAnalysis.Nodes
            .Where(item => selectedNodeIds is null || selectedNodeIds.Contains(item.Node.Id))
            .Where(item => request.IncludeFinished || !item.IsFinished)
            .OrderBy(item => item.Node.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (request.Take.HasValue && items.Count > request.Take.Value)
        {
            items = items.Take(Math.Max(1, request.Take.Value)).ToList();
            warnings.Add($"Dependency result truncated to {request.Take.Value} nodes.");
        }

        return new ProjectStructureDependencyResponse(
            surface.ProjectId,
            surface.ProjectName,
            dependencyAnalysis.DefaultDurationSeconds,
            items.Select(MapDependencyItem).ToList(),
            warnings);
    }

    public async Task<ProjectStructureAssetDescriptor> GetAssetAsync(Guid projectId, string nodeId, CancellationToken cancellationToken = default)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var node = surface.Nodes.FirstOrDefault(item => string.Equals(item.Id, nodeId, StringComparison.Ordinal));
        if (node is null)
        {
            throw new ProjectStructureAgentException(404, "NodeNotFound", $"Node '{nodeId}' was not found.");
        }

        return MapAsset(node, projectId);
    }

    public async Task<ProjectStructureAssetContentDescriptor> GetAssetContentAsync(Guid projectId, string nodeId, CancellationToken cancellationToken = default)
    {
        var asset = await GetAssetAsync(projectId, nodeId, cancellationToken);
        var resolution = pathAccessGuard.ResolveManagedFilePath(asset.MediaRelativePath);
        if (!resolution.IsSuccess)
        {
            throw new ProjectStructureAgentException(
                400,
                "AssetPathInvalid",
                resolution.Message,
                new { asset.MediaRelativePath });
        }

        if (!File.Exists(resolution.FullPath))
        {
            throw new ProjectStructureAgentException(
                404,
                "AssetContentNotFound",
                $"Asset content for node '{nodeId}' was not found.",
                new { asset.MediaRelativePath });
        }

        var bytes = await File.ReadAllBytesAsync(resolution.FullPath, cancellationToken);
        return new ProjectStructureAssetContentDescriptor(
            asset,
            bytes.LongLength,
            Convert.ToBase64String(bytes));
    }

    public async Task<ProjectStructureNodeSummary> CreateAssetAsync(
        Guid projectId,
        ProjectStructureAssetCreateInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        if (request.ObjectType is not (ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset))
        {
            throw new ProjectStructureAgentException(400, "AssetTypeRequired", "Asset nodes must be File, ImageAsset, or VideoAsset.");
        }

        var media = await ResolveAssetCreateMediaAsync(request, cancellationToken);

        return await CreateNodeAsync(
            projectId,
            new ProjectStructureNodeCreateInput(
                request.ObjectType,
                request.Title,
                request.Subtitle,
                request.Notes,
                request.ParentNodeKey,
                ObjectSubtype: request.ObjectSubtype,
                Media: media,
                MetadataJson: request.MetadataJson,
                LeaseToken: request.LeaseToken),
            agent,
            cancellationToken);
    }

    public async Task<ProjectStructureAssetDescriptor> CreateAssetRevisionAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureAssetRevisionRequest request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        EnsureValidMediaPayload(request.Media);

        return await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "create-asset-revision",
            async cancellationToken =>
            {
                var originalAsset = await GetAssetAsync(projectId, nodeId, cancellationToken);
                var originalNode = await GetNodeAsync(projectId, nodeId, cancellationToken);

                var createdRevision = await projectWorkbenchService.CreateObjectAsync(
                    projectId,
                    new ProjectObjectCreateRequest(
                        originalNode.ObjectType,
                        request.Title,
                        request.Subtitle,
                        request.Notes,
                        nodeId,
                        null,
                        null,
                        null,
                        null,
                        string.IsNullOrWhiteSpace(request.ObjectSubtype) ? originalNode.ObjectSubtype : request.ObjectSubtype,
                        request.Media,
                        string.IsNullOrWhiteSpace(request.MetadataJson) ? originalAsset.MetadataJson : request.MetadataJson,
                        originalNode.DurationSeconds),
                    cancellationToken);

                await projectWorkbenchService.LinkObjectsAsync(projectId, createdRevision.Id, nodeId, ProjectObjectLinkKind.DerivedFrom, cancellationToken);

                return MapAsset(createdRevision, projectId) with
                {
                    RevisionParentNodeId = nodeId
                };
            },
            cancellationToken);
    }

    public Task<ProjectStructureImportResult> ImportAsync(
        ProjectStructureImportRequest request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return leaseService.RunWithProjectMutationLeaseAsync(
            request.ProjectId,
            request.LeaseToken,
            agent,
            "import-project-structure",
            cancellationToken => importService.ImportAsync(request, cancellationToken),
            cancellationToken);
    }

    private async Task<ProjectSummary> ResolveSavedProjectAsync(Result<Guid> result, CancellationToken cancellationToken)
    {
        ThrowIfFailure(result);
        var projectId = result.Value;
        if (projectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(500, "ProjectIdMissing", "The project save operation completed without a readable id.");
        }

        var projects = await projectsService.ListAsync(cancellationToken);
        var summary = projects.FirstOrDefault(project => project.Id == projectId);
        if (summary is null)
        {
            throw new ProjectStructureAgentException(404, "ProjectNotFound", $"Project '{projectId}' was saved but could not be read back.");
        }

        return summary;
    }

    private async Task<ProjectStructureNode> GetNodeAsync(Guid projectId, string nodeId, CancellationToken cancellationToken)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var node = surface.Nodes.FirstOrDefault(item => string.Equals(item.Id, nodeId, StringComparison.Ordinal));
        if (node is null)
        {
            throw new ProjectStructureAgentException(404, "NodeNotFound", $"Node '{nodeId}' was not found.");
        }

        return node;
    }

    private ProjectStructureNodeSummary MapRequiredNode(ProjectStructureNode? node, string nodeId)
    {
        if (node is null)
        {
            throw new ProjectStructureAgentException(404, "NodeNotFound", $"Node '{nodeId}' was not found.");
        }

        return MapNodeSummary(node, node.Priority, FullNodeReadRequest);
    }

    private static ProjectStructureAssetDescriptor MapAsset(ProjectStructureNode node, Guid projectId)
    {
        if (node.ObjectType is not (ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset) ||
            string.IsNullOrWhiteSpace(node.MediaRelativePath))
        {
            throw new ProjectStructureAgentException(400, "AssetRequired", $"Node '{node.Id}' is not a managed asset node.");
        }

        return new ProjectStructureAssetDescriptor(
            projectId,
            node.Id,
            node.ObjectType,
            node.ObjectSubtype,
            node.Title,
            node.Subtitle,
            node.Route,
            node.MediaRelativePath,
            node.MediaContentType,
            node.MediaOriginalFileName,
            string.IsNullOrWhiteSpace(node.MetadataJson) ? "{}" : node.MetadataJson,
            true,
            node.Id);
    }

    private static void EnsureValidMediaPayload(ProjectObjectMediaPayload? media)
    {
        if (media is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(media.FileName))
        {
            throw new ProjectStructureAgentException(400, "FileNameRequired", "Uploaded media requires a file name.");
        }

        if (string.IsNullOrWhiteSpace(media.Base64Data))
        {
            throw new ProjectStructureAgentException(400, "MediaPayloadRequired", "Uploaded media requires base64 content.");
        }

        try
        {
            _ = Convert.FromBase64String(media.Base64Data.Trim());
        }
        catch (FormatException ex)
        {
            throw new ProjectStructureAgentException(400, "InvalidBase64Payload", "Uploaded media content was not valid base64.", ex.Message);
        }
    }

    private async Task<ProjectObjectMediaPayload> ResolveAssetCreateMediaAsync(
        ProjectStructureAssetCreateInput request,
        CancellationToken cancellationToken)
    {
        if (request.Media is not null)
        {
            EnsureValidMediaPayload(request.Media);
            return request.Media;
        }

        if (!string.IsNullOrWhiteSpace(request.SourceWorkspacePath))
        {
            if (TryResolveHttpSourceUri(request.SourceWorkspacePath, out var workspacePathSourceUri))
            {
                return await ResolveExternalSourceMediaAsync(request, workspacePathSourceUri, cancellationToken);
            }

            return await ResolveWorkspaceSourceMediaAsync(request, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            return await ResolveExternalSourceMediaAsync(
                request,
                ResolveExternalSourceUri(request.SourceUrl),
                cancellationToken);
        }

        throw new ProjectStructureAgentException(
            400,
            "MediaSourceRequired",
            "Asset creation requires a media payload, a source workspace path, or an external source URL.");
    }

    private async Task<ProjectObjectMediaPayload> ResolveWorkspaceSourceMediaAsync(
        ProjectStructureAssetCreateInput request,
        CancellationToken cancellationToken)
    {
        var resolution = sourceWorkspacePathResolver.ResolveExistingFile(request.SourceWorkspacePath!);
        var bytes = await File.ReadAllBytesAsync(resolution.FullPath, cancellationToken);
        var fileName = ResolveSourceAssetFileName(request.SourceFileName, resolution.FullPath);
        var contentType = ResolveSourceAssetContentType(request.SourceContentType, fileName);
        return new ProjectObjectMediaPayload(
            fileName,
            contentType,
            Convert.ToBase64String(bytes));
    }

    private async Task<ProjectObjectMediaPayload> ResolveExternalSourceMediaAsync(
        ProjectStructureAssetCreateInput request,
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        ValidateExternalSourceUri(sourceUri);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        var httpClient = httpClientFactory.CreateClient("ProjectStructureExternalAssetSource");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProjectStructureAgentException(
                504,
                "SourceUrlTimeout",
                $"External asset source '{sourceUri}' did not respond before the download timeout.",
                ex.Message);
        }
        catch (HttpRequestException ex)
        {
            throw new ProjectStructureAgentException(
                502,
                "SourceUrlDownloadFailed",
                $"External asset source '{sourceUri}' could not be downloaded.",
                ex.Message);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ProjectStructureAgentException(
                    502,
                    "SourceUrlDownloadFailed",
                    $"External asset source '{sourceUri}' returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var declaredLength = response.Content.Headers.ContentLength;
            if (declaredLength is > MaxExternalAssetSourceBytes)
            {
                throw new ProjectStructureAgentException(
                    413,
                    "SourceUrlTooLarge",
                    $"External asset source '{sourceUri}' is larger than the {MaxExternalAssetSourceBytes} byte limit.");
            }

            var bytes = await ReadExternalSourceBytesAsync(response.Content, sourceUri, timeout.Token);
            var fileName = ResolveSourceAssetFileName(request.SourceFileName, sourceUri);
            var contentType = ResolveSourceAssetContentType(
                string.IsNullOrWhiteSpace(request.SourceContentType)
                    ? response.Content.Headers.ContentType?.MediaType
                    : request.SourceContentType,
                fileName);

            return new ProjectObjectMediaPayload(
                fileName,
                contentType,
                Convert.ToBase64String(bytes));
        }
    }

    private static async Task<byte[]> ReadExternalSourceBytesAsync(
        HttpContent content,
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        long totalBytes = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalBytes += read;
            if (totalBytes > MaxExternalAssetSourceBytes)
            {
                throw new ProjectStructureAgentException(
                    413,
                    "SourceUrlTooLarge",
                    $"External asset source '{sourceUri}' is larger than the {MaxExternalAssetSourceBytes} byte limit.");
            }

            memory.Write(buffer, 0, read);
        }

        if (memory.Length == 0)
        {
            throw new ProjectStructureAgentException(
                400,
                "SourceUrlEmpty",
                $"External asset source '{sourceUri}' returned no content.");
        }

        return memory.ToArray();
    }

    private static Uri ResolveExternalSourceUri(string? sourceUrl)
    {
        if (!TryResolveHttpSourceUri(sourceUrl, out var uri))
        {
            throw new ProjectStructureAgentException(
                400,
                "SourceUrlInvalid",
                "External asset source URLs must be absolute http or https URLs.");
        }

        return uri;
    }

    private static bool TryResolveHttpSourceUri(string? sourceUrl, out Uri uri)
    {
        uri = null!;
        return !string.IsNullOrWhiteSpace(sourceUrl) &&
               Uri.TryCreate(sourceUrl.Trim(), UriKind.Absolute, out uri!) &&
               uri.Scheme is "http" or "https";
    }

    private static void ValidateExternalSourceUri(Uri sourceUri)
    {
        if (!string.IsNullOrWhiteSpace(sourceUri.UserInfo))
        {
            throw new ProjectStructureAgentException(
                400,
                "SourceUrlNotAllowed",
                "External asset source URLs must not contain embedded credentials.");
        }

        if (sourceUri.IsLoopback ||
            sourceUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            sourceUri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(sourceUri.Host, out var address) && IsBlockedSourceAddress(address)))
        {
            throw new ProjectStructureAgentException(
                400,
                "SourceUrlNotAllowed",
                "External asset source URLs must point to public http or https hosts.");
        }
    }

    private static bool IsBlockedSourceAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                   bytes[0] == 127 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 169 && bytes[1] == 254) ||
                   bytes[0] == 0;
        }

        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
        {
            return true;
        }

        return address.Equals(IPAddress.IPv6Loopback) || address.Equals(IPAddress.IPv6None);
    }

    private static string ResolveSourceAssetFileName(string? requestedFileName, Uri sourceUri)
    {
        var pathFileName = Uri.UnescapeDataString(Path.GetFileName(sourceUri.AbsolutePath));
        var candidate = string.IsNullOrWhiteSpace(requestedFileName)
            ? pathFileName
            : Path.GetFileName(requestedFileName.Trim());
        return string.IsNullOrWhiteSpace(candidate)
            ? "project-asset.bin"
            : candidate;
    }

    private static string ResolveSourceAssetFileName(string? requestedFileName, string fullPath)
    {
        var candidate = string.IsNullOrWhiteSpace(requestedFileName)
            ? Path.GetFileName(fullPath)
            : Path.GetFileName(requestedFileName.Trim());
        return string.IsNullOrWhiteSpace(candidate)
            ? "project-asset.bin"
            : candidate;
    }

    private static string ResolveSourceAssetContentType(string? requestedContentType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(requestedContentType))
        {
            return requestedContentType.Trim();
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".json" => "application/json",
            ".md" => "text/markdown",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static void ValidateLinkInput(ProjectStructureLinkInput request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceNodeId))
        {
            throw new ProjectStructureAgentException(400, "SourceNodeRequired", "A source node id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.TargetNodeId))
        {
            throw new ProjectStructureAgentException(400, "TargetNodeRequired", "A target node id is required.");
        }
    }

    private static void ValidateProjectRequest(ProjectStructureProjectSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ProjectStructureAgentException(400, "ProjectNameRequired", "Project name is required.");
        }
    }

    private static void ThrowIfFailure(Result result)
    {
        if (result.IsSuccess)
        {
            return;
        }

        var message = string.Join(" ", result.Errors.Select(error => error.Message));
        throw new ProjectStructureAgentException(400, "ProjectStructureValidation", string.IsNullOrWhiteSpace(message) ? "The request could not be completed." : message);
    }

    private static IReadOnlyList<string> NormalizeNodeIds(IReadOnlyList<string>? nodeIds)
        => nodeIds?
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(nodeId => nodeId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList()
            ?? [];

    private static HashSet<string>? ResolveIncludedNodeIds(IReadOnlyList<ProjectStructureNode> nodes, ProjectStructureReadRequest request)
    {
        var selectedIds = new HashSet<string>(StringComparer.Ordinal);
        if (request.NodeIds is not null)
        {
            foreach (var nodeId in request.NodeIds.Where(nodeId => !string.IsNullOrWhiteSpace(nodeId)))
            {
                selectedIds.Add(nodeId.Trim());
            }
        }

        if (request.SubtreeRootIds is not null)
        {
            var childrenByParent = nodes
                .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
                .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => item.Id).ToList(),
                    StringComparer.Ordinal);

            foreach (var rootId in request.SubtreeRootIds.Where(rootId => !string.IsNullOrWhiteSpace(rootId)))
            {
                ExpandSubtree(rootId.Trim(), childrenByParent, selectedIds);
            }
        }

        return selectedIds.Count == 0 ? null : selectedIds;
    }

    private static void ExpandSubtree(
        string rootId,
        IReadOnlyDictionary<string, List<string>> childrenByParent,
        ISet<string> selectedIds)
    {
        var queue = new Queue<string>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!selectedIds.Add(currentId))
            {
                continue;
            }

            if (!childrenByParent.TryGetValue(currentId, out var childIds))
            {
                continue;
            }

            foreach (var childId in childIds)
            {
                queue.Enqueue(childId);
            }
        }
    }

    internal static ProjectStructureNodeSummary MapNodeSummaryForInternalUse(ProjectStructureNode node)
    {
        return new ProjectStructureNodeSummary(
            node.Id,
            node.ParentId,
            node.ObjectType,
            node.ObjectSubtype,
            node.Title,
            node.Subtitle,
            node.Status,
            node.Notes,
            node.Route,
            node.ArtifactKind,
            node.ArtifactId,
            node.MediaRelativePath,
            node.MediaContentType,
            node.MediaOriginalFileName,
            node.Badges,
            node.ProgressMode,
            node.ProgressPercent,
            node.MarkerIcon,
            node.MarkerTone,
            node.MarkerLabel,
            node.Priority,
            node.Priority,
            node.StartUtc,
            node.EndUtc,
            node.MetadataJson,
            node.ProjectRole,
            node.RelatedProjectId,
            node.ParentProjectCount,
            node.X,
            node.Y,
            node.DurationSeconds,
            null);
    }

    private ProjectStructureNodeSummary MapNodeSummary(
        ProjectStructureNode node,
        int effectivePriority,
        ProjectStructureReadRequest options)
    {
        return new ProjectStructureNodeSummary(
            node.Id,
            node.ParentId,
            node.ObjectType,
            node.ObjectSubtype,
            node.Title,
            node.Subtitle,
            node.Status,
            options.IncludeNotes ? node.Notes : null,
            node.Route,
            node.ArtifactKind,
            node.ArtifactId,
            options.IncludeAssets ? node.MediaRelativePath : null,
            options.IncludeAssets ? node.MediaContentType : null,
            options.IncludeAssets ? node.MediaOriginalFileName : null,
            node.Badges,
            node.ProgressMode,
            node.ProgressPercent,
            node.MarkerIcon,
            node.MarkerTone,
            node.MarkerLabel,
            node.Priority,
            effectivePriority,
            node.StartUtc,
            node.EndUtc,
            options.IncludeMetadata ? node.MetadataJson : null,
            node.ProjectRole,
            node.RelatedProjectId,
            node.ParentProjectCount,
            options.IncludeLayout ? node.X : null,
            options.IncludeLayout ? node.Y : null,
            node.DurationSeconds,
            ProjectStructureNodeActionCapabilityResolver.Resolve(node, runtimeLauncher, localFileOpener));
    }

    private static ProjectStructureDependencyItem MapDependencyItem(ProjectStructureDependencyNodeAnalysis analysis)
    {
        return new ProjectStructureDependencyItem(
            analysis.Node.Id,
            analysis.Node.ParentId,
            analysis.Node.ObjectType,
            analysis.Node.ObjectSubtype,
            analysis.Node.Title,
            analysis.Node.Status,
            analysis.Node.ProgressMode,
            analysis.Node.ProgressPercent,
            analysis.Node.MarkerLabel,
            analysis.Node.Priority,
            analysis.EffectivePriority,
            analysis.IsFinished,
            analysis.IsPausedOrStopped,
            analysis.CanExecute,
            analysis.DurationSeconds,
            analysis.EffectiveDurationSeconds,
            analysis.Node.StartUtc,
            analysis.Node.EndUtc,
            analysis.Node.Route,
            analysis.Prerequisites.Select(item => new ProjectStructureDependencyRelationSummary(
                item.NodeId,
                item.Title,
                item.Status,
                item.EffectivePriority,
                item.IsFinished,
                item.Reason)).ToList(),
            analysis.Dependents.Select(item => new ProjectStructureDependencyRelationSummary(
                item.NodeId,
                item.Title,
                item.Status,
                item.EffectivePriority,
                item.IsFinished,
                item.Reason)).ToList());
    }
}
