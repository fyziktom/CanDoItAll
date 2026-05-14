using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureWorkflowNodeService(
    ProjectWorkbenchService projectWorkbenchService,
    ProjectsService projectsService,
    IWorkflowCatalogService workflowCatalogService,
    IWorkflowRuntimeManager workflowRuntimeManager,
    IWorkflowRunStore workflowRunStore,
    ProjectStructureLeaseService leaseService,
    ILogger<ProjectStructureWorkflowNodeService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = BuildJsonOptions();
    private static readonly ProjectStructureNodeStatePresentation NotStartedPresentation = new(
        "Ready",
        "progress",
        string.Empty,
        string.Empty,
        string.Empty);

    public async Task<ProjectStructureWorkflowAddOptionsResult> GetAddOptionsAsync(
        Guid projectId,
        string parentNodeId,
        ProjectStructureWorkflowAddOptionsInput request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parentNodeId))
        {
            throw new ProjectStructureAgentException(400, "ParentNodeRequired", "A parent project-structure node id is required.");
        }

        var project = (await projectsService.ListAsync(cancellationToken))
            .FirstOrDefault(item => item.Id == projectId);
        if (project is null)
        {
            throw new ProjectStructureAgentException(404, "ProjectNotFound", $"Project '{projectId:D}' was not found.");
        }

        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var nodesById = surface.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        if (!nodesById.TryGetValue(parentNodeId, out var parentNode))
        {
            throw new ProjectStructureAgentException(404, "ParentNodeNotFound", $"Parent node '{parentNodeId}' was not found.");
        }

        var inputSettings = ProjectStructureWorkflowInputSettingsNormalizer.Normalize(request.InputSettings);
        if (request.SelectedNodeIds is not null)
        {
            inputSettings.SelectedNodeIds = ProjectStructureWorkflowInputSettingsNormalizer.NormalizeNodeIds(request.SelectedNodeIds);
        }

        var definitions = await workflowCatalogService.ListDefinitionsAsync(cancellationToken);
        var options = definitions
            .OrderBy(item => item.Status == WorkflowLifecycleStatus.Active ? 0 : 1)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapDefinitionOption)
            .ToList();
        var selectedWorkflowId = ResolveSelectedWorkflowId(request.WorkflowId, options);
        var selectedVersionId = ResolveSelectedVersionId(selectedWorkflowId, request.VersionId, definitions);
        var preview = BuildPreview(project, parentNode, surface, nodesById, inputSettings);

        return new ProjectStructureWorkflowAddOptionsResult(
            projectId,
            ProjectStructureAgentService.MapNodeSummaryForInternalUse(parentNode),
            options,
            selectedWorkflowId,
            selectedVersionId,
            inputSettings,
            preview,
            options.Count == 0 ? ["No workflow definitions are available."] : []);
    }

    public Task<ProjectStructureWorkflowNodeCreateResult> CreateAsync(
        Guid projectId,
        string parentNodeId,
        ProjectStructureWorkflowNodeCreateInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "create-workflow-node",
            cancellationToken => CreateCoreAsync(projectId, parentNodeId, request, cancellationToken),
            cancellationToken);
    }

    public Task<ProjectStructureWorkflowNodeStartResult> StartAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureWorkflowNodeStartInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "start-workflow-node",
            cancellationToken => StartCoreAsync(projectId, nodeId, request, agent, cancellationToken),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectStructureWorkflowPreviewSimulationOption>> ListStartSimulationOptionsAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadNodeContextAsync(projectId, nodeId, cancellationToken);
        var workflowMetadata = ResolveWorkflowMetadata(context.Node);
        var detail = await LoadDefinitionAsync(workflowMetadata, cancellationToken);
        EnsureValidDefinition(detail);
        return ProjectStructureWorkflowPreviewSimulationSupport.Analyze(detail.Definition);
    }

    public async Task<ProjectStructureWorkflowRunStatus> GetStatusAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadNodeContextAsync(projectId, nodeId, cancellationToken);
        var workflowMetadata = ResolveWorkflowMetadata(context.Node);
        var detail = await LoadDefinitionAsync(workflowMetadata, cancellationToken);
        var run = workflowMetadata.LastRunId.HasValue
            ? await workflowRuntimeManager.GetRunAsync(workflowMetadata.LastRunId.Value, cancellationToken)
            : null;
        if (workflowMetadata.LastRunId.HasValue && run is null)
        {
            throw new ProjectStructureAgentException(
                404,
                "WorkflowRunNotFound",
                $"Workflow run '{workflowMetadata.LastRunId.Value}' linked from node '{nodeId}' was not found.");
        }

        var status = await BuildStatusAsync(detail.Definition, workflowMetadata, run, cancellationToken);
        await ApplyStatusAsync(projectId, nodeId, workflowMetadata, detail.Definition, status, run, cancellationToken);
        return status;
    }

    private async Task<ProjectStructureWorkflowNodeCreateResult> CreateCoreAsync(
        Guid projectId,
        string parentNodeId,
        ProjectStructureWorkflowNodeCreateInput request,
        CancellationToken cancellationToken)
    {
        if (request.WorkflowId.Value == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "WorkflowDefinitionRequired", "A workflow definition id is required.");
        }

        if (request.VersionId.HasValue && request.VersionId.Value.Value == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "WorkflowVersionInvalid", "Workflow version id cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(parentNodeId))
        {
            throw new ProjectStructureAgentException(400, "ParentNodeRequired", "A parent project-structure node id is required.");
        }

        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var parentNode = surface.Nodes.FirstOrDefault(node => string.Equals(node.Id, parentNodeId, StringComparison.Ordinal));
        if (parentNode is null)
        {
            throw new ProjectStructureAgentException(404, "ParentNodeNotFound", $"Parent node '{parentNodeId}' was not found.");
        }

        var detail = await workflowCatalogService.GetDefinitionAsync(
            request.WorkflowId,
            request.VersionId,
            cancellationToken);
        if (detail is null)
        {
            throw new ProjectStructureAgentException(
                404,
                "WorkflowDefinitionNotFound",
                $"Workflow definition '{request.WorkflowId}' was not found.");
        }
        EnsureActiveDefinition(detail.Definition);

        var inputSettings = ProjectStructureWorkflowInputSettingsNormalizer.Normalize(request.InputSettings);
        var metadata = new ProjectObjectMetadataEnvelope
        {
            Workflow = new ProjectWorkflowNodeMetadata
            {
                WorkflowId = detail.Definition.Id,
                WorkflowVersionId = detail.Definition.VersionId,
                WorkflowName = detail.Definition.Name,
                WorkflowDescription = detail.Definition.Description,
                InputSettings = inputSettings
            }
        };
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? detail.Definition.Name
            : request.Title.Trim();
        var subtitle = string.IsNullOrWhiteSpace(request.Subtitle)
            ? BuildSubtitle(detail.Definition)
            : request.Subtitle.Trim();
        var notes = string.IsNullOrWhiteSpace(request.Notes)
            ? BuildNotes(detail.Definition, parentNode)
            : request.Notes.Trim();

        var createdNode = await projectWorkbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkflowDefinition,
                title,
                subtitle,
                notes,
                parentNode.Id,
                request.X,
                request.Y,
                ObjectSubtype: string.Empty,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(metadata),
                ExternalBinding: new ProjectObjectExternalBindingRequest(
                    BuildWorkflowRoute(projectId, detail.Definition.Id),
                    "workflow-definition",
                    detail.Definition.Id.Value)),
            cancellationToken);

        return new ProjectStructureWorkflowNodeCreateResult(
            projectId,
            ProjectStructureAgentService.MapNodeSummaryForInternalUse(createdNode),
            detail.Definition.Id,
            detail.Definition.VersionId,
            []);
    }

    private async Task<ProjectStructureWorkflowNodeStartResult> StartCoreAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureWorkflowNodeStartInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ProjectStructureAgentException(400, "NodeRequired", "A project-structure node id is required.");
        }

        var context = await LoadNodeContextAsync(projectId, nodeId, cancellationToken);
        var existingNodeIds = context.Surface.Nodes
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        var workflowMetadata = ResolveWorkflowMetadata(context.Node);
        var detail = await LoadDefinitionAsync(workflowMetadata, cancellationToken);
        EnsureActiveDefinition(detail.Definition);
        EnsureValidDefinition(detail);

        workflowMetadata.WorkflowId = detail.Definition.Id;
        workflowMetadata.WorkflowVersionId = detail.Definition.VersionId;
        workflowMetadata.WorkflowName = detail.Definition.Name;
        workflowMetadata.WorkflowDescription = detail.Definition.Description;

        var inputSettings = ProjectStructureWorkflowInputSettingsNormalizer.Normalize(workflowMetadata.InputSettings);
        workflowMetadata.InputSettings = inputSettings;
        var simulationPlan = ProjectStructureWorkflowPreviewSimulationSupport.BuildPlan(
            detail.Definition,
            request.SimulatedNodeIds);
        var preview = BuildPreview(
            context.Project,
            context.ParentNode,
            context.Surface,
            context.NodesById,
            inputSettings,
            context.Node,
            agent,
            request.RequestedBy);
        var startingStatus = BuildStatus(
            detail.Definition,
            workflowMetadata,
            null,
            WorkflowRunState.Running,
            [],
            [],
            "Workflow run is starting.");
        await ApplyStatusAsync(projectId, nodeId, workflowMetadata, detail.Definition, startingStatus, null, cancellationToken);

        try
        {
            var run = await workflowRuntimeManager.StartAsync(
                detail.Definition,
                new WorkflowRunStartRequest(
                    detail.Definition.Id,
                    detail.Definition.VersionId,
                    preview.InputJson,
                    request.RequestedBackend,
                    SourceProcessRunId: null,
                    SourceProcessAssignmentId: null)
                {
                    PreviewSimulationPlan = simulationPlan
                },
                cancellationToken);
            var status = await BuildStatusAsync(detail.Definition, workflowMetadata, run, cancellationToken);
            var projection = await BuildResultProjectionAsync(projectId, existingNodeIds, cancellationToken);
            status = MergeResultProjection(status, projection);
            await ApplyStatusAsync(projectId, nodeId, workflowMetadata, detail.Definition, status, run, cancellationToken);

            return new ProjectStructureWorkflowNodeStartResult(
                projectId,
                nodeId,
                detail.Definition.Id,
                detail.Definition.VersionId,
                run.RunId,
                BuildWorkflowRunRoute(projectId, detail.Definition.Id, run.RunId),
                status,
                []);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            logger.LogWarning(
                exception,
                "Workflow run from project structure failed. ProjectId={ProjectId} NodeId={NodeId} WorkflowId={WorkflowId} WorkflowVersionId={WorkflowVersionId} RequestedBackend={RequestedBackend}",
                projectId,
                nodeId,
                detail.Definition.Id,
                detail.Definition.VersionId,
                request.RequestedBackend?.ToString() ?? detail.Definition.RuntimePolicy.PreferredBackend.ToString());
            var failedStatus = BuildStatus(
                detail.Definition,
                workflowMetadata,
                null,
                WorkflowRunState.Failed,
                [],
                [],
                exception.Message);
            await ApplyStatusAsync(projectId, nodeId, workflowMetadata, detail.Definition, failedStatus, null, cancellationToken);

            throw new ProjectStructureAgentException(
                400,
                "WorkflowRunStartFailed",
                exception.Message);
        }
    }

    private async Task<ProjectStructureWorkflowNodeContext> LoadNodeContextAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        var project = (await projectsService.ListAsync(cancellationToken))
            .FirstOrDefault(item => item.Id == projectId);
        if (project is null)
        {
            throw new ProjectStructureAgentException(404, "ProjectNotFound", $"Project '{projectId:D}' was not found.");
        }

        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var nodesById = surface.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        if (!nodesById.TryGetValue(nodeId, out var node))
        {
            throw new ProjectStructureAgentException(404, "NodeNotFound", $"Node '{nodeId}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(node.ParentId) || !nodesById.TryGetValue(node.ParentId, out var parentNode))
        {
            throw new ProjectStructureAgentException(
                400,
                "WorkflowParentNodeMissing",
                $"Workflow node '{nodeId}' must have an existing parent node to supply run input.");
        }

        return new ProjectStructureWorkflowNodeContext(project, surface, nodesById, node, parentNode);
    }

    private async Task<WorkflowDefinitionDetail> LoadDefinitionAsync(
        ProjectWorkflowNodeMetadata workflowMetadata,
        CancellationToken cancellationToken)
    {
        if (workflowMetadata.WorkflowId is not { } workflowId)
        {
            throw new ProjectStructureAgentException(
                400,
                "WorkflowDefinitionRequired",
                "Workflow node metadata is missing the workflow definition id.");
        }

        var detail = await workflowCatalogService.GetDefinitionAsync(
            workflowId,
            workflowMetadata.WorkflowVersionId,
            cancellationToken);
        if (detail is null)
        {
            throw new ProjectStructureAgentException(
                404,
                "WorkflowDefinitionNotFound",
                $"Workflow definition '{workflowId}' was not found.");
        }

        return detail;
    }

    private static ProjectWorkflowNodeMetadata ResolveWorkflowMetadata(ProjectStructureNode node)
    {
        if (node.ObjectType != ProjectObjectType.WorkflowDefinition)
        {
            throw new ProjectStructureAgentException(
                400,
                "WorkflowNodeRequired",
                $"Node '{node.Id}' is '{node.ObjectType}', but workflow start requires a workflow node.");
        }

        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        if (metadata.Workflow is null)
        {
            throw new ProjectStructureAgentException(
                400,
                "WorkflowMetadataMissing",
                $"Workflow node '{node.Id}' is missing workflow metadata.");
        }

        if (metadata.Workflow.WorkflowId is null)
        {
            throw new ProjectStructureAgentException(
                400,
                "WorkflowDefinitionRequired",
                $"Workflow node '{node.Id}' is missing the workflow definition id.");
        }

        return metadata.Workflow;
    }

    private static void EnsureValidDefinition(WorkflowDefinitionDetail detail)
    {
        if (detail.Validation.Succeeded)
        {
            return;
        }

        throw new ProjectStructureAgentException(
            400,
            "WorkflowDefinitionInvalid",
            $"Workflow definition '{detail.Definition.Name}' cannot start from project structure because validation failed.",
            detail.Validation.Issues);
    }

    private static ProjectStructureWorkflowDefinitionOption MapDefinitionOption(WorkflowCatalogItem definition)
    {
        var isSelectable = definition.Status == WorkflowLifecycleStatus.Active;
        return new ProjectStructureWorkflowDefinitionOption(
            definition.Id,
            definition.VersionId,
            definition.Name,
            definition.Description,
            definition.Status,
            definition.PreferredBackend,
            isSelectable,
            isSelectable ? string.Empty : "Only active workflow definitions can be added to project structure.");
    }

    private static WorkflowId? ResolveSelectedWorkflowId(
        WorkflowId? requestedWorkflowId,
        IReadOnlyList<ProjectStructureWorkflowDefinitionOption> options)
    {
        if (requestedWorkflowId.HasValue)
        {
            var requested = options.FirstOrDefault(item => item.WorkflowId == requestedWorkflowId.Value);
            if (requested is null)
            {
                throw new ProjectStructureAgentException(
                    404,
                    "WorkflowDefinitionNotFound",
                    $"Workflow definition '{requestedWorkflowId.Value}' was not found.");
            }

            if (!requested.IsSelectable)
            {
                throw new ProjectStructureAgentException(
                    400,
                    "WorkflowDefinitionInactive",
                    $"Workflow definition '{requested.DisplayName}' is not active.");
            }

            return requested.WorkflowId;
        }

        return options.FirstOrDefault(item => item.IsSelectable)?.WorkflowId;
    }

    private static WorkflowVersionId? ResolveSelectedVersionId(
        WorkflowId? selectedWorkflowId,
        WorkflowVersionId? requestedVersionId,
        IReadOnlyList<WorkflowCatalogItem> definitions)
    {
        if (!selectedWorkflowId.HasValue)
        {
            return null;
        }

        if (requestedVersionId.HasValue)
        {
            return requestedVersionId.Value;
        }

        return definitions.FirstOrDefault(item => item.Id == selectedWorkflowId.Value)?.VersionId;
    }

    private static ProjectStructureWorkflowInputPreview BuildPreview(
        ProjectSummary project,
        ProjectStructureNode parentNode,
        ProjectStructureSurface surface,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById,
        ProjectStructureWorkflowInputSettings inputSettings,
        ProjectStructureNode? workflowNode = null,
        ProjectStructureAgentContext? agent = null,
        string requestedBy = "")
    {
        using var manualInput = JsonDocument.Parse(inputSettings.ManualInputJson);
        var selectedNodes = ResolveSelectedNodes(nodesById, inputSettings.SelectedNodeIds);
        var parentSubtreeNodes = inputSettings.IncludeParentSubtree
            ? ResolveDescendants(surface.Nodes, parentNode.Id)
            : [];
        var payload = new ProjectStructureWorkflowInputPayload(
            project.Id.ToString("D"),
            workflowNode?.Id ?? parentNode.Id,
            new ProjectStructureWorkflowProjectPayload(
                project.Id,
                project.Name,
                project.Status.ToString(),
                project.CurrentPhase,
                project.PrimaryCustomerName,
                project.PrimaryOwnerName,
                project.PrimaryDeliveryUnitName),
            workflowNode is null || agent is null
                ? null
                : new ProjectStructureWorkflowRunContextPayload(
                    workflowNode.Id,
                    workflowNode.Title,
                    string.IsNullOrWhiteSpace(requestedBy) ? "project-structure" : requestedBy.Trim(),
                    agent.AgentId,
                    agent.AgentName,
                    agent.MachineName,
                    agent.RepositoryRoot,
                    agent.BranchName,
                    agent.SessionId),
            MapNode(parentNode, includeAssets: inputSettings.IncludeAssets),
            selectedNodes.Select(node => MapNode(node, inputSettings.IncludeAssets)).ToList(),
            parentSubtreeNodes.Select(node => MapNode(node, inputSettings.IncludeAssets)).ToList(),
            inputSettings.AdditionalSources,
            manualInput.RootElement.Clone());
        var inputJson = JsonSerializer.Serialize(payload, JsonOptions);
        var sections = BuildPreviewSections(project, parentNode, selectedNodes, parentSubtreeNodes, inputSettings);

        return new ProjectStructureWorkflowInputPreview(
            string.Join(Environment.NewLine, sections.Select(section => $"{section.Title}: {section.Summary}")),
            inputJson,
            sections);
    }

    private static IReadOnlyList<ProjectStructureWorkflowInputPreviewSection> BuildPreviewSections(
        ProjectSummary project,
        ProjectStructureNode parentNode,
        IReadOnlyList<ProjectStructureNode> selectedNodes,
        IReadOnlyList<ProjectStructureNode> parentSubtreeNodes,
        ProjectStructureWorkflowInputSettings inputSettings)
    {
        List<ProjectStructureWorkflowInputPreviewSection> sections =
        [
            new(
                "Project",
                $"{project.Name} ({project.Status})",
                [
                    new("Project id", project.Id.ToString("D")),
                    new("Name", project.Name),
                    new("Status", project.Status.ToString()),
                    new("Current phase", project.CurrentPhase)
                ]),
            new(
                "Parent node",
                $"{parentNode.Title} ({parentNode.ObjectType})",
                [
                    new("Node id", parentNode.Id),
                    new("Title", parentNode.Title),
                    new("Object type", parentNode.ObjectType.ToString()),
                    new("Object subtype", parentNode.ObjectSubtype),
                    new("Status", parentNode.Status),
                    new("Notes", parentNode.Notes),
                    new("Metadata", parentNode.MetadataJson)
                ])
        ];

        if (parentSubtreeNodes.Count > 0)
        {
            sections.Add(new ProjectStructureWorkflowInputPreviewSection(
                "Parent subtree",
                $"{parentSubtreeNodes.Count} descendant node(s)",
                parentSubtreeNodes.Select(node => new ProjectStructureWorkflowInputPreviewRow(node.Id, $"{node.Title} ({node.ObjectType})")).ToList()));
        }

        if (selectedNodes.Count > 0)
        {
            sections.Add(new ProjectStructureWorkflowInputPreviewSection(
                "Selected nodes",
                $"{selectedNodes.Count} selected node(s)",
                selectedNodes.Select(node => new ProjectStructureWorkflowInputPreviewRow(node.Id, $"{node.Title} ({node.ObjectType})")).ToList()));
        }

        if (inputSettings.AdditionalSources.Count > 0)
        {
            var sourceSummary = string.Join(
                ", ",
                inputSettings.AdditionalSources
                    .Select(source => string.IsNullOrWhiteSpace(source.Label) ? source.Key : source.Label)
                    .Take(3));
            sections.Add(new ProjectStructureWorkflowInputPreviewSection(
                "Additional sources",
                sourceSummary,
                inputSettings.AdditionalSources.Select(source => new ProjectStructureWorkflowInputPreviewRow(
                    string.IsNullOrWhiteSpace(source.Label) ? source.Key : source.Label,
                    $"{source.Kind}: {source.Value}")).ToList()));
        }

        sections.Add(new ProjectStructureWorkflowInputPreviewSection(
            "Manual input",
            inputSettings.ManualInputJson,
            [new("JSON", inputSettings.ManualInputJson)]));

        return sections;
    }

    private static IReadOnlyList<ProjectStructureNode> ResolveSelectedNodes(
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById,
        IReadOnlyList<string> selectedNodeIds)
    {
        if (selectedNodeIds.Count == 0)
        {
            return [];
        }

        var resolved = new List<ProjectStructureNode>();
        foreach (var nodeId in selectedNodeIds)
        {
            if (!nodesById.TryGetValue(nodeId, out var node))
            {
                throw new ProjectStructureAgentException(
                    404,
                    "WorkflowSelectedNodeNotFound",
                    $"Selected workflow input node '{nodeId}' was not found.");
            }

            resolved.Add(node);
        }

        return resolved;
    }

    private static IReadOnlyList<ProjectStructureNode> ResolveDescendants(
        IReadOnlyList<ProjectStructureNode> nodes,
        string parentNodeId)
    {
        var childrenByParent = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var descendants = new List<ProjectStructureNode>();
        var queue = new Queue<string>();
        queue.Enqueue(parentNodeId);
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!childrenByParent.TryGetValue(currentId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                descendants.Add(child);
                queue.Enqueue(child.Id);
            }
        }

        return descendants;
    }

    private static ProjectStructureWorkflowNodePayload MapNode(ProjectStructureNode node, bool includeAssets)
    {
        return new ProjectStructureWorkflowNodePayload(
            node.Id,
            node.ParentId,
            node.ObjectType.ToString(),
            node.ObjectSubtype,
            node.Title,
            node.Subtitle,
            node.Status,
            node.Notes,
            node.MetadataJson,
            includeAssets ? node.MediaRelativePath : string.Empty,
            includeAssets ? node.MediaContentType : string.Empty,
            includeAssets ? node.MediaOriginalFileName : string.Empty);
    }

    private async Task<ProjectStructureWorkflowRunStatus> BuildStatusAsync(
        WorkflowDefinition definition,
        ProjectWorkflowNodeMetadata workflowMetadata,
        WorkflowRunSnapshot? run,
        CancellationToken cancellationToken)
    {
        var events = run is null
            ? []
            : await workflowRuntimeManager.ListEventsAsync(run.RunId, cancellationToken);
        var artifacts = run is null
            ? []
            : await workflowRunStore.ListArtifactsAsync(run.RunId, cancellationToken);
        var state = run?.State ?? workflowMetadata.LastRunState ?? WorkflowRunState.NotStarted;
        var message = run?.Summary ?? workflowMetadata.LastRunSummary;

        return BuildStatus(
            definition,
            workflowMetadata,
            run,
            state,
            events,
            artifacts,
            message,
            workflowMetadata.LastCreatedNodeIds,
            workflowMetadata.LastCreatedAssetIds);
    }

    private static ProjectStructureWorkflowRunStatus BuildStatus(
        WorkflowDefinition definition,
        ProjectWorkflowNodeMetadata workflowMetadata,
        WorkflowRunSnapshot? run,
        WorkflowRunState state,
        IReadOnlyList<WorkflowEventRecord> events,
        IReadOnlyList<WorkflowArtifactRecord> artifacts,
        string? message,
        IReadOnlyList<string>? createdNodeIds = null,
        IReadOnlyList<string>? createdAssetIds = null)
    {
        var stepCount = Math.Max(1, definition.Graph.Nodes.Count);
        var currentStepIndex = ResolveCurrentStepIndex(definition, state, events, stepCount);
        var presentation = ResolvePresentation(state);
        var progressPercent = ResolveProgressPercent(state, currentStepIndex, stepCount);
        var normalizedMessage = ResolveStatusMessage(state, message);
        var artifactSummaries = artifacts
            .OrderBy(item => item.CreatedAtUtc)
            .Select(MapArtifactSummary)
            .ToList();
        var createdFilePaths = NormalizeDistinct(
            artifactSummaries
            .Select(item => item.StoragePath)
            .Concat(workflowMetadata.LastCreatedFilePaths)
            .Where(item => !string.IsNullOrWhiteSpace(item)),
            StringComparer.OrdinalIgnoreCase);
        var summary = new ProjectStructureWorkflowExecutionSummary(
            run?.RunId ?? workflowMetadata.LastRunId,
            state,
            definition.Name,
            normalizedMessage,
            currentStepIndex,
            stepCount,
            artifactSummaries,
            NormalizeDistinct(createdNodeIds ?? [], StringComparer.Ordinal),
            NormalizeDistinct(createdAssetIds ?? [], StringComparer.Ordinal),
            createdFilePaths);
        var recentEvents = events
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(12)
            .OrderBy(item => item.CreatedAtUtc)
            .Select(MapEventSummary)
            .ToList();

        return new ProjectStructureWorkflowRunStatus(
            run?.RunId ?? workflowMetadata.LastRunId,
            state,
            presentation.Status,
            presentation.ProgressMode,
            progressPercent,
            presentation.MarkerIcon,
            presentation.MarkerTone,
            presentation.MarkerLabel,
            currentStepIndex,
            stepCount,
            normalizedMessage,
            summary,
            recentEvents);
    }

    private async Task<ProjectStructureWorkflowResultProjection> BuildResultProjectionAsync(
        Guid projectId,
        IReadOnlySet<string> existingNodeIds,
        CancellationToken cancellationToken)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var createdNodes = surface.Nodes
            .Where(node => !existingNodeIds.Contains(node.Id))
            .OrderBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToList();
        var createdAssetIds = createdNodes
            .Where(IsAssetNode)
            .Select(ResolveAssetId)
            .ToList();

        return new ProjectStructureWorkflowResultProjection(
            NormalizeDistinct(createdNodes.Select(node => node.Id), StringComparer.Ordinal),
            NormalizeDistinct(createdAssetIds, StringComparer.Ordinal));
    }

    private static ProjectStructureWorkflowRunStatus MergeResultProjection(
        ProjectStructureWorkflowRunStatus status,
        ProjectStructureWorkflowResultProjection projection)
    {
        if (projection.CreatedNodeIds.Count == 0 && projection.CreatedAssetIds.Count == 0)
        {
            return status;
        }

        var summary = status.Summary with
        {
            CreatedNodeIds = NormalizeDistinct(
                status.Summary.CreatedNodeIds.Concat(projection.CreatedNodeIds),
                StringComparer.Ordinal),
            CreatedAssetIds = NormalizeDistinct(
                status.Summary.CreatedAssetIds.Concat(projection.CreatedAssetIds),
                StringComparer.Ordinal)
        };

        return status with { Summary = summary };
    }

    private static bool IsAssetNode(ProjectStructureNode node)
        => node.ObjectType is ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset;

    private static string ResolveAssetId(ProjectStructureNode node)
        => node.ArtifactId?.ToString("D") ?? node.Id;

    private static IReadOnlyList<string> NormalizeDistinct(
        IEnumerable<string> values,
        StringComparer comparer)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(comparer)
            .ToList();
    }

    private async Task ApplyStatusAsync(
        Guid projectId,
        string nodeId,
        ProjectWorkflowNodeMetadata workflowMetadata,
        WorkflowDefinition definition,
        ProjectStructureWorkflowRunStatus status,
        WorkflowRunSnapshot? run,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        workflowMetadata.WorkflowId = definition.Id;
        workflowMetadata.WorkflowVersionId = definition.VersionId;
        workflowMetadata.WorkflowName = definition.Name;
        workflowMetadata.WorkflowDescription = definition.Description;
        workflowMetadata.LastRunId = status.RunId;
        workflowMetadata.LastRunState = status.State;
        workflowMetadata.LastRunSummary = status.Summary.RunSummary;
        workflowMetadata.LastCreatedNodeIds = status.Summary.CreatedNodeIds;
        workflowMetadata.LastCreatedAssetIds = status.Summary.CreatedAssetIds;
        workflowMetadata.LastCreatedFilePaths = status.Summary.CreatedFilePaths;
        workflowMetadata.LastStepIndex = status.CurrentStepIndex;
        workflowMetadata.LastStepCount = status.StepCount;
        workflowMetadata.LastStartedAtUtc = run?.CreatedAtUtc ??
            (status.State == WorkflowRunState.NotStarted ? workflowMetadata.LastStartedAtUtc : workflowMetadata.LastStartedAtUtc ?? now);
        workflowMetadata.LastUpdatedAtUtc = run?.UpdatedAtUtc ??
            (status.State == WorkflowRunState.NotStarted ? workflowMetadata.LastUpdatedAtUtc : now);

        var updatedNode = await projectWorkbenchService.UpdateObjectMetadataAsync(
            projectId,
            nodeId,
            ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
            {
                Workflow = workflowMetadata
            }),
            status: status.Status,
            cancellationToken: cancellationToken);
        if (updatedNode is null)
        {
            throw new ProjectStructureAgentException(404, "NodeNotFound", $"Node '{nodeId}' was not found.");
        }

        await projectWorkbenchService.UpdateObjectProgressDetailedAsync(
            projectId,
            [nodeId],
            status.ProgressMode,
            status.ProgressPercent,
            cancellationToken);
        await ApplyWorkflowMarkerAsync(projectId, nodeId, status, cancellationToken);
    }

    private async Task ApplyWorkflowMarkerAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureWorkflowRunStatus status,
        CancellationToken cancellationToken)
    {
        foreach (var markerIcon in new[] { "alert", "pause", "stop" })
        {
            await projectWorkbenchService.RemoveObjectMarkerDetailedAsync(
                projectId,
                [nodeId],
                markerIcon,
                string.Empty,
                string.Empty,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(status.MarkerIcon))
        {
            return;
        }

        await projectWorkbenchService.AddObjectMarkerDetailedAsync(
            projectId,
            [nodeId],
            status.MarkerIcon,
            status.MarkerTone,
            status.MarkerLabel,
            cancellationToken);
    }

    private static ProjectStructureWorkflowRunArtifactSummary MapArtifactSummary(WorkflowArtifactRecord artifact)
    {
        return new ProjectStructureWorkflowRunArtifactSummary(
            artifact.Kind,
            artifact.Name,
            artifact.ContentType,
            artifact.StoragePath,
            artifact.Summary);
    }

    private static ProjectStructureWorkflowRunEventSummary MapEventSummary(WorkflowEventRecord workflowEvent)
    {
        return new ProjectStructureWorkflowRunEventSummary(
            workflowEvent.Kind,
            workflowEvent.Message,
            workflowEvent.NodeId?.Value ?? string.Empty,
            workflowEvent.CreatedAtUtc);
    }

    private static ProjectStructureNodeStatePresentation ResolvePresentation(WorkflowRunState state)
    {
        return state switch
        {
            WorkflowRunState.Running => new("Running", "started", string.Empty, string.Empty, string.Empty),
            WorkflowRunState.WaitingForInput => new("Waiting for input", "progress", "pause", "warn", "Waiting"),
            WorkflowRunState.Completed => new("Completed", "complete", string.Empty, string.Empty, string.Empty),
            WorkflowRunState.Failed => new("Failed", "progress", "alert", "danger", "Failed"),
            WorkflowRunState.Cancelled => new("Cancelled", "progress", "stop", "primary", "Cancelled"),
            WorkflowRunState.Idle => new("Idle", "progress", string.Empty, string.Empty, string.Empty),
            _ => NotStartedPresentation
        };
    }

    private static int ResolveCurrentStepIndex(
        WorkflowDefinition definition,
        WorkflowRunState state,
        IReadOnlyList<WorkflowEventRecord> events,
        int stepCount)
    {
        if (state is WorkflowRunState.NotStarted or WorkflowRunState.Idle)
        {
            return 0;
        }

        if (state == WorkflowRunState.Completed)
        {
            return stepCount;
        }

        if (TryResolveLatestEventNodeIndex(definition, events, out var eventStepIndex))
        {
            return eventStepIndex;
        }

        if (state == WorkflowRunState.WaitingForInput &&
            TryResolveFirstNodeKindIndex(definition, WorkflowNodeKind.HumanInput, out var humanInputStepIndex))
        {
            return humanInputStepIndex;
        }

        return Math.Min(1, stepCount);
    }

    private static bool TryResolveLatestEventNodeIndex(
        WorkflowDefinition definition,
        IReadOnlyList<WorkflowEventRecord> events,
        out int stepIndex)
    {
        var nodesById = definition.Graph.Nodes
            .Select((node, index) => new { node.Id, Index = index + 1 })
            .ToDictionary(item => item.Id, item => item.Index);
        foreach (var nodeId in events
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.NodeId)
            .Where(item => item is not null)
            .Cast<WorkflowNodeId>())
        {
            if (nodesById.TryGetValue(nodeId, out stepIndex))
            {
                return true;
            }
        }

        stepIndex = 0;
        return false;
    }

    private static bool TryResolveFirstNodeKindIndex(
        WorkflowDefinition definition,
        WorkflowNodeKind nodeKind,
        out int stepIndex)
    {
        for (var index = 0; index < definition.Graph.Nodes.Count; index++)
        {
            if (definition.Graph.Nodes[index].Kind != nodeKind)
            {
                continue;
            }

            stepIndex = index + 1;
            return true;
        }

        stepIndex = 0;
        return false;
    }

    private static int ResolveProgressPercent(WorkflowRunState state, int currentStepIndex, int stepCount)
    {
        if (state is WorkflowRunState.NotStarted or WorkflowRunState.Idle)
        {
            return 0;
        }

        if (state == WorkflowRunState.Completed)
        {
            return 100;
        }

        var percent = stepCount <= 0
            ? 0
            : (int)Math.Round(currentStepIndex * 100d / stepCount, MidpointRounding.AwayFromZero);
        return Math.Clamp(percent, 5, 99);
    }

    private static string ResolveStatusMessage(WorkflowRunState state, string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            return message.Trim();
        }

        return state switch
        {
            WorkflowRunState.NotStarted => "Workflow is ready to start from project structure.",
            WorkflowRunState.Running => "Workflow run is running.",
            WorkflowRunState.WaitingForInput => "Workflow run is waiting for external input.",
            WorkflowRunState.Completed => "Workflow run completed.",
            WorkflowRunState.Failed => "Workflow run failed.",
            WorkflowRunState.Cancelled => "Workflow run was cancelled.",
            _ => $"Workflow run is {state}."
        };
    }

    private static string BuildSubtitle(WorkflowDefinition definition)
    {
        var nodeCount = definition.Graph.Nodes.Count;
        var stepLabel = nodeCount == 1 ? "node" : "nodes";
        return $"{definition.Status} workflow · {nodeCount} {stepLabel}";
    }

    private static string BuildNotes(WorkflowDefinition definition, ProjectStructureNode parentNode)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                definition.Description?.Trim(),
                $"Parent context: {parentNode.Title} ({parentNode.ObjectType}).",
                "Project and parent node details are always included when this workflow starts from project structure."
            }.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static string BuildWorkflowRoute(Guid projectId, WorkflowId workflowId)
    {
        return $"/agents/workflows?projectId={projectId:D}&workflowId={workflowId.Value:D}";
    }

    private static string BuildWorkflowRunRoute(Guid projectId, WorkflowId workflowId, WorkflowRunId runId)
    {
        return $"/agents/workflows?projectId={projectId:D}&workflowId={workflowId.Value:D}&runId={runId.Value:D}";
    }

    private static JsonSerializerOptions BuildJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static void EnsureActiveDefinition(WorkflowDefinition definition)
    {
        if (definition.Status == WorkflowLifecycleStatus.Active)
        {
            return;
        }

        throw new ProjectStructureAgentException(
            400,
            "WorkflowDefinitionInactive",
            $"Workflow definition '{definition.Name}' is not active.");
    }

    private sealed record ProjectStructureWorkflowInputPayload(
        string ProjectId,
        string NodeId,
        ProjectStructureWorkflowProjectPayload Project,
        ProjectStructureWorkflowRunContextPayload? RunContext,
        ProjectStructureWorkflowNodePayload ParentNode,
        IReadOnlyList<ProjectStructureWorkflowNodePayload> SelectedNodes,
        IReadOnlyList<ProjectStructureWorkflowNodePayload> ParentSubtree,
        IReadOnlyList<ProjectStructureWorkflowInputSource> Sources,
        JsonElement ManualInput);

    private sealed record ProjectStructureWorkflowProjectPayload(
        Guid Id,
        string Name,
        string Status,
        string CurrentPhase,
        string CustomerName,
        string OwnerName,
        string DeliveryUnitName);

    private sealed record ProjectStructureWorkflowRunContextPayload(
        string WorkflowNodeId,
        string WorkflowNodeTitle,
        string RequestedBy,
        string AgentId,
        string AgentName,
        string MachineName,
        string RepositoryRoot,
        string BranchName,
        string SessionId);

    private sealed record ProjectStructureWorkflowNodePayload(
        string Id,
        string? ParentId,
        string ObjectType,
        string ObjectSubtype,
        string Title,
        string Subtitle,
        string Status,
        string Notes,
        string MetadataJson,
        string MediaRelativePath,
        string MediaContentType,
        string MediaOriginalFileName);

    private sealed record ProjectStructureWorkflowNodeContext(
        ProjectSummary Project,
        ProjectStructureSurface Surface,
        IReadOnlyDictionary<string, ProjectStructureNode> NodesById,
        ProjectStructureNode Node,
        ProjectStructureNode ParentNode);

    private sealed record ProjectStructureWorkflowResultProjection(
        IReadOnlyList<string> CreatedNodeIds,
        IReadOnlyList<string> CreatedAssetIds);

    private sealed record ProjectStructureNodeStatePresentation(
        string Status,
        string ProgressMode,
        string MarkerIcon,
        string MarkerTone,
        string MarkerLabel);
}
