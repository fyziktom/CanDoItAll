using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using System.Text.Json;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureAgentService(
    ProjectsService projectsService,
    ProjectWorkbenchService projectWorkbenchService,
    ProjectStructureLeaseService leaseService,
    ProjectStructureChecklistService checklistService,
    ProjectStructureImportService importService,
    IProjectStructureRuntimeLauncher runtimeLauncher,
    IProjectStructureLocalFileOpener localFileOpener)
{
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
