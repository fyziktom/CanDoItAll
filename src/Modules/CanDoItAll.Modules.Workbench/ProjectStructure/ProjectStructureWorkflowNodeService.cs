using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureWorkflowNodeService(
    ProjectWorkbenchService projectWorkbenchService,
    ProjectsService projectsService,
    IWorkflowCatalogService workflowCatalogService,
    IWorkflowLaunchService workflowLaunchService,
    ProjectStructureWorkflowLaunchIntentFactory launchIntentFactory,
    IWorkflowRuntimeManager workflowRuntimeManager,
    IWorkflowRunStore workflowRunStore,
    IEnumerable<IWorkflowExecutionBackend> workflowExecutionBackends,
    ProjectStructureLeaseService leaseService,
    ProjectStructureWorkflowAuthorityService workflowAuthority,
    IWorkflowStructureOutputStore workflowOutputs,
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
            cancellationToken => CreateCoreAsync(
                projectId,
                parentNodeId,
                request,
                allowCanonicalTaskParent: false,
                cancellationToken),
            cancellationToken);
    }

    internal Task<ProjectStructureWorkflowNodeCreateResult> CreateForCanonicalTaskAsync(
        Guid projectId,
        string parentTaskNodeId,
        ProjectStructureWorkflowNodeCreateInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "create-task-workflow-node",
            cancellationToken => CreateCoreAsync(
                projectId,
                parentTaskNodeId,
                request,
                allowCanonicalTaskParent: true,
                cancellationToken),
            cancellationToken);
    }

    public async Task<ProjectStructureWorkflowNodeStartResult> StartAsync(
        Guid projectId, string nodeId, ProjectStructureWorkflowNodeStartInput request,
        ProjectStructureAgentContext agent, CancellationToken cancellationToken = default) {
        var intentId = request.IntentId ?? Guid.NewGuid();
        if (intentId == Guid.Empty) {
            throw new ProjectStructureAgentException(400, "WorkflowIntentRequired", "The workflow launch intent cannot be empty.");
        }

        try {
            return await leaseService.RunWithProjectMutationLeaseAsync(projectId, request.LeaseToken, agent,
                "start-workflow-node", ct => StartCoreAsync(projectId, nodeId, intentId, request, agent, ct), cancellationToken);
        } catch (Exception exception) when (exception is not ProjectStructureAgentException { StatusCode: 403 or 409 }) {
            var admission = await projectWorkbenchService.FindWorkflowAdmissionAsync(intentId, CancellationToken.None);
            if (admission is null || admission.ProjectId != projectId || admission.NodeId != nodeId ||
                agent.WorkflowAuthority is not { } source || admission.Binding.Authority.Channel != source.Channel ||
                admission.Binding.Authority.Principal != source.Principal) {
                throw;
            }

            logger.LogWarning(exception, "Workflow admission {IntentId} reserved run {RunId}; later execution or delivery acknowledgement failed.",
                intentId, admission.Binding.RunId);
            WorkflowRunSnapshot? run = null;
            ProjectStructureWorkflowRunStatus status;
            Exception? observationFailure = null;
            try {
                run = await workflowRunStore.GetRunAsync(admission.Binding.RunId, CancellationToken.None);
                status = await BuildAdmissionStatusAsync(admission, run, CancellationToken.None);
            } catch (Exception observationException) {
                observationFailure = observationException;
                status = admission.RecordedStatus ?? BuildStatus(admission.Definition,
                    new ProjectWorkflowNodeMetadata { LastRunId = admission.Binding.RunId }, null,
                    WorkflowRunState.NotStarted, [], [], "Workflow admission was recorded; its execution outcome is awaiting observation.");
            }

            return StartResult(admission, run, status with { Delivery = ProjectWorkflowDeliveryState.Pending },
                ["Workflow admission was recorded. Its execution or projection acknowledgement needs reconciliation."]) with {
                ObservationException = exception,
                ReceiptObservationException = observationFailure
            };
        }
    }

    public async Task<IReadOnlyList<ProjectStructureWorkflowPreviewSimulationOption>> ListStartSimulationOptionsAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default)
        => (await GetStartOptionsAsync(projectId, nodeId, cancellationToken)).SimulationOptions;

    public async Task<ProjectStructureWorkflowStartOptionsResult> GetStartOptionsAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadNodeContextAsync(projectId, nodeId, cancellationToken);
        var workflowMetadata = ResolveWorkflowMetadata(context.Node);
        var detail = await LoadDefinitionAsync(workflowMetadata, cancellationToken);
        EnsureValidDefinition(detail);
        var backendSelection = ResolveStartBackendSelection(detail.Definition, requestedBackend: null);
        return new ProjectStructureWorkflowStartOptionsResult(
            ProjectStructureWorkflowPreviewSimulationSupport.Analyze(detail.Definition),
            detail.Definition.RuntimePolicy.PreferredBackend,
            backendSelection.Backend,
            BuildStartBackendOptions(detail.Definition, backendSelection.Backend),
            backendSelection.Warning);
    }

    public async Task<ProjectStructureWorkflowRunStatus> GetStatusAsync(Guid projectId, string nodeId,
        CancellationToken cancellationToken = default) {
        var context = await LoadNodeContextAsync(projectId, nodeId, cancellationToken);
        var admission = await projectWorkbenchService.FindSelectedWorkflowAdmissionAsync(projectId, nodeId, cancellationToken);
        if (admission is not null) {
            var admittedRun = await workflowRunStore.GetRunAsync(admission.Binding.RunId, cancellationToken);
            return await BuildAdmissionStatusAsync(admission, admittedRun, cancellationToken);
        }

        var workflowMetadata = ResolveWorkflowMetadata(context.Node);
        var detail = await LoadDefinitionAsync(workflowMetadata, cancellationToken);
        var run = workflowMetadata.LastRunId.HasValue
            ? await workflowRuntimeManager.GetRunAsync(workflowMetadata.LastRunId.Value, cancellationToken) : null;
        if (workflowMetadata.LastRunId.HasValue && run is null) {
            throw new ProjectStructureAgentException(404, "WorkflowRunNotFound", "The workflow run linked from this node was not found.");
        }

        return await BuildStatusAsync(detail.Definition, workflowMetadata, run, cancellationToken);
    }

    public async Task<ProjectWorkflowDeliveryState> ReconcileAsync(Guid intentId,
        CancellationToken cancellationToken = default) {
        var admission = await projectWorkbenchService.FindWorkflowAdmissionAsync(intentId, cancellationToken)
            ?? throw new ProjectStructureAgentException(404, "WorkflowAdmissionNotFound", "The workflow launch admission was not found.");
        var run = await workflowRunStore.GetRunAsync(admission.Binding.RunId, cancellationToken);
        if (run is null) {
            await workflowAuthority.EnsureCurrentAsync(admission.Binding.Authority, null, cancellationToken);
            var owner = admission.Binding.LeaseOwner
                ?? throw new InvalidOperationException("The prepared workflow admission has no retained lease owner.");
            var agent = new ProjectStructureAgentContext(owner.AgentId, owner.AgentName, owner.MachineName,
                owner.RepositoryRoot, owner.BranchName, owner.SessionId);
            var recovered = await leaseService.RunWithProjectMutationLeaseAsync(admission.ProjectId, null, agent,
                "reconcile-workflow-admission", async ct => {
                    await workflowAuthority.EnsureCurrentAsync(admission.Binding.Authority, null, ct);
                    return await workflowLaunchService.LaunchAsync(launchIntentFactory.Create(admission), ct);
                }, cancellationToken);
            run = recovered.Run;
        }

        var outputs = await workflowOutputs.ListAsync(admission.Binding.RunId, cancellationToken);
        var status = await BuildAdmissionStatusAsync(admission, run, cancellationToken);
        return await projectWorkbenchService.DeliverWorkflowStatusAsync(admission, status, run,
            outputs.All(output => output.Receipt is not null), cancellationToken);
    }

    private async Task<ProjectStructureWorkflowNodeCreateResult> CreateCoreAsync(
        Guid projectId,
        string parentNodeId,
        ProjectStructureWorkflowNodeCreateInput request,
        bool allowCanonicalTaskParent,
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

        if (allowCanonicalTaskParent)
        {
            if (!ProjectStructureCanonicalTaskMutationPolicy.IsTask(
                    parentNode.ObjectType,
                    parentNode.ObjectSubtype))
            {
                throw new ProjectStructureAgentException(
                    400,
                    "CanonicalTaskRequired",
                    $"Node '{parentNodeId}' is not a canonical WorkItem/task node.");
            }
        }
        else
        {
            ProjectStructureCanonicalTaskMutationPolicy.EnsureGenericResourceAttachmentAllowed(
                parentNode.ObjectType,
                parentNode.ObjectSubtype);
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

        var createRequest = new ProjectObjectCreateRequest(
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
                    detail.Definition.Id.Value),
                PlacementIntent: ProjectObjectPlacementIntent.AutomaticAroundParent);
        var createdNode = allowCanonicalTaskParent
            ? await projectWorkbenchService.CreateCanonicalTaskResourceObjectAsync(
                projectId,
                createRequest,
                cancellationToken)
            : await projectWorkbenchService.CreateObjectAsync(
                projectId,
                createRequest,
                cancellationToken);

        return new ProjectStructureWorkflowNodeCreateResult(
            projectId,
            ProjectStructureAgentService.MapNodeSummaryForInternalUse(createdNode),
            detail.Definition.Id,
            detail.Definition.VersionId,
            []);
    }

    private async Task<ProjectStructureWorkflowNodeStartResult> StartCoreAsync(Guid projectId, string nodeId,
        Guid intentId, ProjectStructureWorkflowNodeStartInput request, ProjectStructureAgentContext agent,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(nodeId)) {
            throw new ProjectStructureAgentException(400, "NodeRequired", "A project-structure node id is required.");
        }

        var authority = await workflowAuthority.CaptureAsync(projectId, agent.WorkflowAuthority, cancellationToken);
        var admission = await projectWorkbenchService.FindWorkflowAdmissionAsync(intentId, cancellationToken);
        if (admission is not null) {
            var simulation = ProjectStructureWorkflowPreviewSimulationSupport.BuildPlan(admission.Definition, request.SimulatedNodeIds);
            await projectWorkbenchService.ValidateWorkflowAdmissionReplayAsync(admission, projectId, nodeId, authority,
                request.RequestedBackend, simulation, cancellationToken);
            await workflowAuthority.EnsureCurrentAsync(admission.Binding.Authority, null, cancellationToken);
        } else {
            var context = await LoadNodeContextAsync(projectId, nodeId, cancellationToken);
            var metadata = ResolveWorkflowMetadata(context.Node);
            var detail = await LoadDefinitionAsync(metadata, cancellationToken);
            EnsureActiveDefinition(detail.Definition);
            EnsureValidDefinition(detail);
            var inputSettings = ProjectStructureWorkflowInputSettingsNormalizer.Normalize(metadata.InputSettings);
            var simulation = ProjectStructureWorkflowPreviewSimulationSupport.BuildPlan(detail.Definition, request.SimulatedNodeIds);
            var preview = BuildPreview(context.Project, context.ParentNode, context.Surface, context.NodesById,
                inputSettings, context.Node, agent, request.RequestedBy);
            _ = ResolveStartBackendSelection(detail.Definition, request.RequestedBackend);
            admission = await projectWorkbenchService.PrepareWorkflowAdmissionAsync(projectId, nodeId, intentId,
                request.IntentId.HasValue, detail.Definition, preview.InputJson, request.RequestedBackend,
                simulation, authority, agent, cancellationToken);
        }

        var launch = await workflowLaunchService.LaunchAsync(launchIntentFactory.Create(admission), cancellationToken);
        var status = await BuildAdmissionStatusAsync(admission, launch.Run, cancellationToken);
        var outputs = await workflowOutputs.ListAsync(admission.Binding.RunId, cancellationToken);
        var delivery = await projectWorkbenchService.DeliverWorkflowStatusAsync(admission, status, launch.Run,
            outputs.All(output => output.Receipt is not null), cancellationToken);
        var warnings = new List<string>();
        var backend = ResolveStartBackendSelection(admission.Definition, admission.LaunchIntent.RequestedBackend);
        if (!string.IsNullOrWhiteSpace(backend.Warning)) {
            warnings.Add(backend.Warning);
        }

        if (launch.Observation != WorkflowLaunchObservation.Confirmed) {
            warnings.Add("Workflow admission succeeded; an execution or admission-receipt observer failed. The original run identity is retained.");
        }

        if (!admission.CallerSuppliedIntent) {
            warnings.Add("This legacy request supplied no intentId. Repeating it intentionally creates another admission.");
        }

        return StartResult(admission, launch.Run, status with { Delivery = delivery }, warnings) with {
            ObservationException = launch.ObservationException,
            ReceiptObservationException = launch.ReceiptObservationException
        };
    }

    private static ProjectStructureWorkflowNodeStartResult StartResult(ProjectWorkflowAdmission admission,
        WorkflowRunSnapshot? run, ProjectStructureWorkflowRunStatus status, IReadOnlyList<string> warnings)
        => new(admission.ProjectId, admission.NodeId, admission.Definition.Id, admission.Definition.VersionId,
            admission.Binding.RunId, BuildWorkflowRunRoute(admission.ProjectId, admission.Definition.Id, admission.Binding.RunId),
            status with { IntentId = admission.Binding.IntentId, AdmissionSequence = admission.Binding.Sequence }, warnings) {
            IntentId = admission.Binding.IntentId,
            CallerSuppliedIntent = admission.CallerSuppliedIntent,
            RunAdmissionObserved = run is not null
        };

    private async Task<ProjectStructureWorkflowRunStatus> BuildAdmissionStatusAsync(ProjectWorkflowAdmission admission,
        WorkflowRunSnapshot? run, CancellationToken cancellationToken) {
        if (run is not null && (run.RunId != admission.Binding.RunId || run.VersionId != admission.Definition.VersionId)) {
            throw new InvalidOperationException("The observed workflow run differs from its exact Structure admission.");
        }

        var outputs = await workflowOutputs.ListAsync(admission.Binding.RunId, cancellationToken);
        var receipts = outputs.Where(output => output.Receipt is not null).Select(output => output.Receipt!).ToList();
        var metadata = new ProjectWorkflowNodeMetadata {
            LastRunId = admission.Binding.RunId,
            LastRunState = run?.State ?? WorkflowRunState.NotStarted,
            LastRunSummary = run?.Summary ?? "Workflow launch is prepared; run admission is awaiting observation.",
            LastCreatedNodeIds = receipts.Select(receipt => receipt.NodeId).ToList(),
            LastCreatedAssetIds = receipts.Where(receipt => receipt.AssetId.HasValue).Select(receipt => receipt.AssetId!.Value.ToString("D")).ToList(),
            LastCreatedFilePaths = receipts.Select(receipt => receipt.StoragePath).Where(path => !string.IsNullOrWhiteSpace(path)).ToList()
        };
        var status = await BuildStatusAsync(admission.Definition, metadata, run, cancellationToken);
        return status with {
            Summary = status.Summary with { CreatedFilePaths = metadata.LastCreatedFilePaths },
            IntentId = admission.Binding.IntentId,
            AdmissionSequence = admission.Binding.Sequence,
            Delivery = admission.Delivery
        };
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

    private ProjectStructureWorkflowStartBackendSelection ResolveStartBackendSelection(
        WorkflowDefinition definition,
        WorkflowRuntimeBackendKind? requestedBackend)
    {
        var backend = requestedBackend ?? definition.RuntimePolicy.PreferredBackend;
        var registeredBackends = ListRegisteredBackendKinds();
        if (registeredBackends.Contains(backend))
        {
            return new ProjectStructureWorkflowStartBackendSelection(backend, string.Empty);
        }

        return new ProjectStructureWorkflowStartBackendSelection(
            backend,
            $"Workflow runtime backend {backend} is not registered in this host.");
    }

    private IReadOnlyList<ProjectStructureWorkflowStartBackendOption> BuildStartBackendOptions(
        WorkflowDefinition definition,
        WorkflowRuntimeBackendKind selectedBackend)
    {
        var descriptors = ListRegisteredBackendDescriptors();
        if (descriptors.Count == 0)
        {
            return
            [
                new ProjectStructureWorkflowStartBackendOption(
                    selectedBackend,
                    selectedBackend.ToString(),
                    "No executable workflow runtime backend is registered in this host.",
                    IsSelected: true)
            ];
        }

        return descriptors
            .OrderByDescending(item => item.Kind == selectedBackend)
            .ThenByDescending(item => item.Kind == definition.RuntimePolicy.PreferredBackend)
            .ThenBy(item => item.Kind.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(item => new ProjectStructureWorkflowStartBackendOption(
                item.Kind,
                item.Kind == definition.RuntimePolicy.PreferredBackend
                    ? $"{item.Kind} (definition preference)"
                    : item.Kind.ToString(),
                item.OperationalNotes,
                item.Kind == selectedBackend))
            .ToList();
    }

    private IReadOnlySet<WorkflowRuntimeBackendKind> ListRegisteredBackendKinds()
        => ListRegisteredBackendDescriptors()
            .Select(item => item.Kind)
            .ToHashSet();

    private IReadOnlyList<WorkflowRuntimeBackendDescriptor> ListRegisteredBackendDescriptors()
        => workflowExecutionBackends
            .Select(item => item.Descriptor)
            .GroupBy(item => item.Kind)
            .Select(group => group.First())
            .ToList();

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
        var normalizedMessage = ResolveWorkflowStatusMessage(state, message, events);
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
            WorkflowFailureDisplayFormatter.ToUserMessage(workflowEvent),
            workflowEvent.NodeId?.Value ?? string.Empty,
            workflowEvent.CreatedAtUtc,
            workflowEvent.PayloadJson);
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

    internal static string ResolveWorkflowStatusMessage(
        WorkflowRunState state,
        string? message,
        IReadOnlyList<WorkflowEventRecord> events)
    {
        if (state == WorkflowRunState.Failed)
        {
            var failureMessage = events
                .OrderByDescending(item => item.CreatedAtUtc)
                .Where(item => item.Kind is WorkflowEventKind.Error or WorkflowEventKind.ExecutorFailed)
                .Select(WorkflowFailureDisplayFormatter.ToUserMessage)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(failureMessage))
            {
                return failureMessage;
            }
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            return state == WorkflowRunState.Failed
                ? WorkflowFailureDisplayFormatter.ToUserMessage(message)
                : message.Trim();
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
        return $"{definition.Status} workflow Ă„â€šĂ˘â‚¬ĹˇÄ‚â€šĂ‚Â· {nodeCount} {stepLabel}";
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

    private sealed record ProjectStructureWorkflowStartBackendSelection(
        WorkflowRuntimeBackendKind Backend,
        string Warning);

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

    private sealed record ProjectStructureNodeStatePresentation(
        string Status,
        string ProgressMode,
        string MarkerIcon,
        string MarkerTone,
        string MarkerLabel);
}
