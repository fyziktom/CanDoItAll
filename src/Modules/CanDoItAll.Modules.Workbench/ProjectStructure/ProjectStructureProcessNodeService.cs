using System.Globalization;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureProcessNodeService(
    IServiceScopeFactory serviceScopeFactory)
{
    private const string ProjectIdVariableName = "ProjectId";
    private const string ProjectNodeIdVariableName = "ProjectNodeId";
    private const string ProjectNodeTitleVariableName = "ProjectNodeTitle";
    private const string ProjectNodeSubtitleVariableName = "ProjectNodeSubtitle";
    private const string ProjectNodeStatusVariableName = "ProjectNodeStatus";
    private const string ProjectNodeNotesVariableName = "ProjectNodeNotes";
    private const string ProjectNodeObjectTypeVariableName = "ProjectNodeObjectType";
    private const string ProjectNodeObjectSubtypeVariableName = "ProjectNodeObjectSubtype";
    private const string ProjectStructureContextSummaryVariableName = "ProjectStructureContextSummary";
    private const string ProcessRunNodeIdVariableName = "ProcessRunNodeId";
    private const string ParentProcessRunIdVariableName = ProcessRuntimeLaunchVariables.ParentProcessRunId;
    private const string ParentProcessRunNodeIdVariableName = "ParentProcessRunNodeId";
    private const string TargetProcessRunNodeIdVariableName = "TargetProcessRunNodeId";
    private const string ParentProcessStepIdVariableName = ProcessRuntimeLaunchVariables.ParentProcessStepId;
    private const string ParentProcessStepKeyVariableName = "ParentProcessStepKey";
    private const string SubprocessDefinitionKeyVariableName = "SubprocessDefinitionKey";
    private const string SubprocessLiveRunProfileKeyVariableName = "SubprocessLiveRunProfileKey";
    private const string AgentIdVariableName = "AgentId";
    private const string AgentNameVariableName = "AgentName";
    private const string MachineNameVariableName = "MachineName";
    private const string RepositoryRootVariableName = "RepositoryRoot";
    private const string BranchNameVariableName = "BranchName";
    private const string SessionIdVariableName = "SessionId";
    private const string AllowedOperationsVariableName = "AllowedOperations";
    private const string OperationTargetScopeVariableName = "OperationTargetScope";
    private const string ProcessStepAllowedOperationsVariableName = "ProcessStepAllowedOperations";
    private const string ProcessStepTargetScopeVariableName = "ProcessStepTargetScope";
    private const string ProcessStepAllowsProductMutationVariableName = "ProcessStepAllowsProductMutation";
    private const string ProcessStepKindVariableName = ProcessRuntimeLaunchVariables.ProcessStepKind;
    private const string ProcessStepSubprocessDefinitionKeyVariableName = ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey;
    private const string AgentProcessStepAllowedOperationsVariableName = "agentProcessStepAllowedOperations";
    private const string AgentProcessStepTargetScopeVariableName = "agentProcessStepTargetScope";
    private const string AgentProcessStepAllowsProductMutationVariableName = "agentProcessStepAllowsProductMutation";

    private static readonly string[] OutputRootMetadataKeys =
    [
        "outputRoot",
        "productRoot",
        "targetRoot",
        "targetPath",
        "repositoryRoot",
        "workspaceRoot"
    ];

    private static readonly string[] SubprocessReservedLaunchVariableKeys =
    [
        AllowedOperationsVariableName,
        OperationTargetScopeVariableName,
        ProcessStepAllowedOperationsVariableName,
        ProcessStepTargetScopeVariableName,
        ProcessStepAllowsProductMutationVariableName,
        ProcessStepKindVariableName,
        ProcessRuntimeLaunchVariables.ProcessStepSubprocessContractJson,
        ProcessStepSubprocessDefinitionKeyVariableName,
        AgentProcessStepAllowedOperationsVariableName,
        AgentProcessStepTargetScopeVariableName,
        AgentProcessStepAllowsProductMutationVariableName
    ];

    private static readonly string[] SubprocessDerivedScopeLaunchVariableKeys =
    [
        ProjectStructureContextSummaryVariableName,
        "OutputRoot",
        "ProductRoot",
        "ExternalTargetRoot",
        "OutputRootAlias",
        "ProductRootAlias",
        "WorkspaceAlias",
        "ProductTargetFilesystemState",
        "ScopeSummary",
        "ScopeBoundarySummary",
        "ChildScopeMvp",
        "SourceCitations",
        "SourceOfTruth",
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths,
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep,
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts,
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep,
        ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeys,
        ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeysByStep,
        ProcessRuntimeLaunchVariables.RuntimeRoutedBranchOutcomeKeysByStep,
        ProcessRuntimeLaunchVariables.ExecutorPreferredSpecializationTags,
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks,
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep
    ];

    private static readonly string[] ParentOutcomeTargetContextVariableKeys =
    [
        "OutputRoot",
        "ProductRoot",
        "ExternalTargetRoot",
        "OutputRootAlias",
        "ProductRootAlias",
        "WorkspaceAlias",
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths,
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep,
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts,
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep,
        ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeys,
        ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeysByStep,
        ProcessRuntimeLaunchVariables.RuntimeRoutedBranchOutcomeKeysByStep,
        ProcessRuntimeLaunchVariables.ExecutorPreferredSpecializationTags,
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks,
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep
    ];

    public async Task<ProjectStructureProcessNodeStartResult> StartAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureProcessNodeStartInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        return await StartAsync(
            ResolveScopedDependencies(scope.ServiceProvider),
            projectId,
            nodeId,
            request,
            agent,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, string>> BuildProjectScopedLaunchVariablesAsync(
        ProjectStructureProcessLaunchVariableBuildRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ProjectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(
                400,
                "ProjectIdRequired",
                "Project id is required to build project-scoped process launch variables.");
        }

        if (string.IsNullOrWhiteSpace(request.ProjectNodeId))
        {
            throw new ProjectStructureAgentException(
                400,
                "ProjectNodeIdRequired",
                "Project node id is required to build project-scoped process launch variables.");
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dependencies = ResolveScopedDependencies(scope.ServiceProvider);
        var surface = await LoadSurfaceAsync(
            dependencies.ProjectWorkbenchService,
            request.ProjectId,
            cancellationToken).ConfigureAwait(false);
        var targetNode = surface.Nodes.FirstOrDefault(candidate => string.Equals(candidate.Id, request.ProjectNodeId, StringComparison.Ordinal))
            ?? throw new ProjectStructureAgentException(
                404,
                "ProjectStructureNodeNotFound",
                $"Node '{request.ProjectNodeId}' was not found in project '{request.ProjectId:D}'.");

        var processDefinitionId = request.ProcessDefinitionId?.Value ?? Guid.Empty;
        var processDefinitionNodeId = processDefinitionId == Guid.Empty
            ? "process-api"
            : ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(processDefinitionId);
        var definitionKey = FirstNonEmpty(
            request.DefinitionKey,
            processDefinitionId == Guid.Empty
                ? string.Empty
                : dependencies.ProcessDefinitionCatalogService.ResolveDefinitionKey(new ProcessDefinitionId(processDefinitionId)));
        var agent = new ProjectStructureAgentContext(
            AgentId: "process-api",
            AgentName: string.IsNullOrWhiteSpace(request.RequestedBy) ? "Process API" : request.RequestedBy.Trim(),
            MachineName: Environment.MachineName,
            RepositoryRoot: string.Empty,
            BranchName: string.Empty,
            SessionId: $"process-api-{Guid.NewGuid():N}");
        var derivedVariables = CreateVariables(
            surface,
            processNode: null,
            processDefinitionNodeId,
            processDefinitionId,
            targetNode,
            agent,
            dependencies.LaunchVariablePreparationService,
            definitionKey);
        var variables = CopyLaunchVariables(request.Variables);

        foreach (var item in derivedVariables)
        {
            variables[item.Key] = item.Value;
        }

        variables.TryAdd("LaunchSource", "process-api-project-structure");
        if (!string.IsNullOrWhiteSpace(definitionKey))
        {
            variables.TryAdd("RequestedProcessDefinitionKey", definitionKey);
        }

        return variables;
    }

    private static async Task<ProjectStructureProcessNodeStartResult> StartAsync(
        ProjectStructureProcessNodeScopedDependencies dependencies,
        Guid projectId,
        string nodeId,
        ProjectStructureProcessNodeStartInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(agent);

        if (projectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(
                400,
                "ProjectIdRequired",
                "Project id is required to start a process from project structure.");
        }

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ProjectStructureAgentException(
                400,
                "NodeIdRequired",
                "Node id is required to start a process from project structure.");
        }

        var surface = await LoadSurfaceAsync(
            dependencies.ProjectWorkbenchService,
            projectId,
            cancellationToken).ConfigureAwait(false);
        var node = surface.Nodes.FirstOrDefault(candidate => string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
        var processDefinitionId = request.ProcessDefinitionId ?? ResolveProcessDefinitionId(node, nodeId);
        var processDefinitionNodeId = ResolveProcessDefinitionNodeId(processDefinitionId, nodeId);
        var processNode = IsProcessDefinitionNode(node, nodeId)
            ? node
            : null;

        if (!processDefinitionId.HasValue && node is not null)
        {
            processDefinitionId = ResolveLinkedProcessDefinitionId(surface, node.Id);
            processDefinitionNodeId = ResolveProcessDefinitionNodeId(processDefinitionId, nodeId);
        }

        if (!processDefinitionId.HasValue)
        {
            if (node is null)
            {
                throw new ProjectStructureAgentException(
                    404,
                    "ProjectStructureNodeNotFound",
                    $"Node '{nodeId}' was not found in project '{projectId:D}'.");
            }

            throw new ProjectStructureAgentException(
                400,
                "ProcessDefinitionRequired",
                $"Node '{nodeId}' is not linked to a process definition.");
        }

        var targetNode = processNode is null
            ? node
            : ResolveProcessStartTargetNode(surface, projectId, processDefinitionNodeId ?? nodeId, processNode);
        targetNode ??= processDefinitionNodeId is not null
            ? ResolveProcessStartTargetNode(surface, projectId, processDefinitionNodeId, processNode)
            : null;
        if (targetNode is null)
        {
            throw new ProjectStructureAgentException(
                400,
                "ProcessStartTargetRequired",
                $"Process definition node '{processDefinitionNodeId ?? nodeId}' is not linked from a project-structure source node.");
        }

        var definitionKey = dependencies.ProcessDefinitionCatalogService.ResolveDefinitionKey(new ProcessDefinitionId(processDefinitionId.Value));
        var launch = await dependencies.ProcessLaunchApplicationService
            .LaunchAsync(
                new ProcessLaunchRequest(
                    DefinitionKey: NormalizeOptional(definitionKey),
                    new ProcessDefinitionId(processDefinitionId.Value),
                    LiveRunProfileKey: null,
                    ProjectId: projectId,
                    ProjectNodeId: targetNode.Id,
                    RequestedBy: string.IsNullOrWhiteSpace(request.RequestedBy)
                        ? agent.AgentName
                        : request.RequestedBy,
                    Variables: CreateVariables(
                        surface,
                        processNode,
                        processDefinitionNodeId ?? nodeId,
                        processDefinitionId.Value,
                        targetNode,
                        agent,
                        dependencies.LaunchVariablePreparationService,
                        definitionKey),
                    RunReadiness: request.RunHrMatch,
                    Execute: request.Execute),
                cancellationToken)
            .ConfigureAwait(false);

        var warnings = launch.Warnings.ToList();
        if (launch.RunId is { } runId)
        {
            try
            {
                await dependencies.ProjectWorkbenchService.LinkObjectsAsync(
                    projectId,
                    targetNode.Id,
                    ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId.Value),
                    ProjectObjectLinkKind.Uses,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                warnings.Add($"Process run '{runId.Value:D}' started but could not be linked back to project node '{targetNode.Id}': {exception.Message}");
            }
        }

        return new ProjectStructureProcessNodeStartResult(
            projectId,
            nodeId,
            launch.DefinitionId.Value,
            launch.LaunchPlanId.Value,
            launch.RunId?.Value,
            launch.Stage.ToString(),
            launch.Route,
            request.IncludeLaunchPlan ? launch.LaunchPlan : null,
            warnings);
    }

    public async Task<ProjectStructureProcessSubprocessLaunchResult> StartSubprocessAsync(
        Guid projectId,
        string parentProcessRunId,
        string parentProcessStepId,
        ProjectStructureProcessSubprocessLaunchInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        return await StartSubprocessAsync(
            ResolveScopedDependencies(scope.ServiceProvider),
            projectId,
            parentProcessRunId,
            parentProcessStepId,
            request,
            agent,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ProjectStructureProcessSubprocessLaunchResult> StartSubprocessAsync(
        ProjectStructureProcessNodeScopedDependencies dependencies,
        Guid projectId,
        string parentProcessRunId,
        string parentProcessStepId,
        ProjectStructureProcessSubprocessLaunchInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(agent);

        if (projectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(
                400,
                "ProjectIdRequired",
                "Project id is required to launch a subprocess from project structure.");
        }

        var definitionKey = NormalizeRequired(
            request.DefinitionKey,
            "ProcessSubprocessDefinitionKeyRequired",
            "A subprocess definition key is required.");
        var parentRunId = ParseProcessRunId(parentProcessRunId);
        var parentStepId = ParseProcessStepInstanceId(parentProcessStepId);
        var parentAssignment = await dependencies.AssignmentStore.LoadAsync(parentRunId, parentStepId, cancellationToken).ConfigureAwait(false)
            ?? throw new ProjectStructureAgentException(
                404,
                "ProcessSubprocessParentAssignmentMissing",
                $"No runtime assignment exists for parent process run '{parentRunId}' and step '{parentStepId}'.");
        var parentState = await dependencies.StateStore.LoadAsync(parentRunId, cancellationToken).ConfigureAwait(false)
            ?? throw new ProjectStructureAgentException(
                404,
                "ProcessSubprocessParentRunMissing",
                $"No runtime state exists for parent process run '{parentRunId}'.");

        if (!ContainsProcessOperation(parentAssignment.AllowedOperations, ProcessOperationContractNames.ExecuteExternalAction))
        {
            throw new ProjectStructureAgentException(
                403,
                "ProcessSubprocessLaunchDenied",
                $"Parent process step '{parentAssignment.StepKey}' does not allow {ProcessOperationContractNames.ExecuteExternalAction}.");
        }

        ValidateParentProjectScope(projectId, parentAssignment);
        var surface = await LoadSurfaceAsync(
            dependencies.ProjectWorkbenchService,
            projectId,
            cancellationToken).ConfigureAwait(false);
        var assignmentProjectNodeId = NormalizeOptional(ResolveLaunchVariable(parentAssignment.LaunchVariables, ProjectNodeIdVariableName));
        var requestedProjectNodeId = NormalizeOptional(request.ParentProjectNodeId);
        var projectNodeId = assignmentProjectNodeId ?? requestedProjectNodeId ?? string.Empty;
        var preLaunchWarnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(assignmentProjectNodeId) &&
            !string.IsNullOrWhiteSpace(requestedProjectNodeId) &&
            !string.Equals(assignmentProjectNodeId, requestedProjectNodeId, StringComparison.Ordinal))
        {
            preLaunchWarnings.Add($"Ignored subprocess parent project node '{requestedProjectNodeId}' because parent process step '{parentAssignment.StepKey}' is scoped to project node '{assignmentProjectNodeId}'.");
        }

        if (string.IsNullOrWhiteSpace(projectNodeId))
        {
            throw new ProjectStructureAgentException(
                400,
                "ProcessSubprocessProjectNodeRequired",
                $"Parent process step '{parentAssignment.StepKey}' does not carry a project node id. Supply ParentProjectNodeId.");
        }

        var projectNode = surface.Nodes.FirstOrDefault(candidate => string.Equals(candidate.Id, projectNodeId, StringComparison.Ordinal));
        if (projectNode is null)
        {
            throw new ProjectStructureAgentException(
                404,
                "ProjectStructureNodeNotFound",
                $"Parent project node '{projectNodeId}' was not found in project '{projectId:D}'.");
        }

        var launchVariables = CreateSubprocessVariables(
            projectId,
            surface,
            projectNode,
            parentRunId,
            parentStepId,
            parentAssignment,
            parentState,
            definitionKey,
            request,
            agent,
            dependencies.WorkspaceFiles,
            dependencies.LaunchVariablePreparationService);
        var subprocessIdentityVariables = CreateSubprocessIdentityVariables(
            projectId,
            projectNode.Id,
            parentRunId,
            parentStepId,
            definitionKey,
            request.LiveRunProfileKey);
        if (await TryCreateStoppedBlockingSubprocessLaunchResultAsync(
                dependencies,
                projectId,
                projectNode.Id,
                parentRunId,
                parentStepId,
                parentAssignment,
                parentState,
                definitionKey,
                subprocessIdentityVariables,
                request.IncludeLaunchPlan,
                cancellationToken).ConfigureAwait(false) is { } stoppedBlockingChild)
        {
            return stoppedBlockingChild;
        }

        var launch = await dependencies.ProcessLaunchApplicationService
            .FindExistingLaunchAsync(
                new ProcessExistingLaunchLookupRequest(
                    definitionKey,
                    NormalizeOptional(request.LiveRunProfileKey),
                    projectId,
                    subprocessIdentityVariables),
                cancellationToken)
            .ConfigureAwait(false)
            ?? await dependencies.ProcessLaunchApplicationService.LaunchAsync(
                new ProcessLaunchRequest(
                    DefinitionKey: definitionKey,
                    ProcessDefinitionId: null,
                    LiveRunProfileKey: NormalizeOptional(request.LiveRunProfileKey),
                    ProjectId: projectId,
                    ProjectNodeId: projectNode.Id,
                    RequestedBy: string.IsNullOrWhiteSpace(request.RequestedBy)
                        ? agent.AgentName
                        : request.RequestedBy.Trim(),
                    Variables: launchVariables,
                    RunReadiness: request.RunHrMatch,
                    Execute: request.Execute)
                {
                    RootRunIdOverride = parentState.RootRunId
                },
                cancellationToken)
            .ConfigureAwait(false);

        var warnings = preLaunchWarnings
            .Concat(launch.Warnings)
            .ToList();
        if (launch.RunId is { } runId)
        {
            var childRunNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId.Value);
            try
            {
                await dependencies.ProjectWorkbenchService.LinkObjectsAsync(
                    projectId,
                    projectNode.Id,
                    childRunNodeId,
                    ProjectObjectLinkKind.Uses,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                warnings.Add($"Subprocess run '{runId.Value:D}' started but could not be linked back to project node '{projectNode.Id}': {exception.Message}");
            }

            var parentRunNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(parentRunId.Value);
            try
            {
                await dependencies.ProjectWorkbenchService.LinkObjectsAsync(
                    projectId,
                    parentRunNodeId,
                    childRunNodeId,
                    ProjectObjectLinkKind.Uses,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                warnings.Add($"Subprocess run '{runId.Value:D}' started but could not be linked under parent process run '{parentRunId.Value:D}': {exception.Message}");
            }
        }

        var childManagedArtifactRoot = launch.RunId is { } childRunId
            ? ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(childRunId)
            : string.Empty;
        var childStepsArtifactRoot = string.IsNullOrWhiteSpace(childManagedArtifactRoot)
            ? string.Empty
            : $"{childManagedArtifactRoot}/steps";
        var childLiveProcessesRoute = launch.RunId is { } routeRunId
            ? $"/projects/{projectId:D}/processes/live?runId={routeRunId.Value:D}"
            : string.Empty;
        var expectedChildEvidenceRefs = BuildExpectedChildEvidenceRefs(childManagedArtifactRoot, launch.LaunchPlan);
        var parentTargetContext = BuildParentOutcomeTargetContext(launchVariables);
        var parentEvidenceRefs = expectedChildEvidenceRefs
            .Concat(parentTargetContext.EvidenceRefs)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ProjectStructureProcessSubprocessLaunchResult(
            projectId,
            projectNode.Id,
            parentRunId.ToString(),
            parentStepId.ToString(),
            parentAssignment.StepKey,
            definitionKey,
            launch.DefinitionId.Value,
            launch.LaunchPlanId.Value,
            launch.RunId?.Value,
            launch.Stage.ToString(),
            launch.Route,
            request.IncludeLaunchPlan ? launch.LaunchPlan : null,
            childManagedArtifactRoot,
            childStepsArtifactRoot,
            childLiveProcessesRoute,
            parentEvidenceRefs,
            warnings)
        {
            ParentDeferredOutcomeInstruction = BuildParentOutcomeInstruction(launch.RunId, launch.Stage),
            ParentDeferredOutcomeJson = BuildParentOutcomeJson(
                launch.RunId,
                launch.Stage,
                definitionKey,
                parentEvidenceRefs,
                childLiveProcessesRoute,
                parentTargetContext,
                warnings)
        };
    }

    private static async Task<ProjectStructureProcessSubprocessLaunchResult?> TryCreateStoppedBlockingSubprocessLaunchResultAsync(
        ProjectStructureProcessNodeScopedDependencies dependencies,
        Guid projectId,
        string projectNodeId,
        ProcessRunId parentRunId,
        ProcessStepInstanceId parentStepId,
        ProcessRuntimeStepAssignment parentAssignment,
        ProcessRuntimeStateSnapshot parentState,
        string definitionKey,
        IReadOnlyDictionary<string, string> subprocessIdentityVariables,
        bool includeLaunchPlan,
        CancellationToken cancellationToken)
    {
        var matchingAssignments = await dependencies.AssignmentStore
            .FindByLaunchVariablesAsync(subprocessIdentityVariables, cancellationToken)
            .ConfigureAwait(false);
        foreach (var runGroup in matchingAssignments
            .GroupBy(assignment => assignment.RunId)
            .OrderByDescending(group => group.Max(assignment => assignment.CreatedAtUtc)))
        {
            var state = await dependencies.StateStore.LoadAsync(runGroup.Key, cancellationToken).ConfigureAwait(false);
            if (state is null || !IsStoppedBlockingSubprocessStatus(state.Status))
            {
                continue;
            }

            var plan = await dependencies.PlanStore.LoadAsync(state.PlanId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Blocked subprocess lookup found process run '{state.RunId}' with missing plan '{state.PlanId}'.");
            var childAssignments = await dependencies.AssignmentStore
                .LoadByRunAsync(state.RunId, cancellationToken)
                .ConfigureAwait(false);
            var childManagedArtifactRoot = ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(state.RunId);
            var childStepsArtifactRoot = $"{childManagedArtifactRoot}/steps";
            var childLiveProcessesRoute = BuildScopedRunRoute(projectId, state.RunId);
            var expectedChildEvidenceRefs = BuildExpectedChildEvidenceRefs(
                childManagedArtifactRoot,
                childAssignments.Select(assignment => assignment.StepKey));
            var childLaunchVariables = runGroup
                .OrderBy(assignment => assignment.CreatedAtUtc)
                .Select(assignment => assignment.LaunchVariables)
                .FirstOrDefault()
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
            var parentTargetContext = BuildParentOutcomeTargetContext(childLaunchVariables);
            var parentEvidenceRefs = expectedChildEvidenceRefs
                .Concat(parentTargetContext.EvidenceRefs)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var warning = $"Subprocess launch did not start a replacement child because matching child process run '{state.RunId.Value:D}' is {state.Status}. Propagate that stopped child blocker with child run and evidence refs, or explicitly rework the child run before relaunch.";

            return new ProjectStructureProcessSubprocessLaunchResult(
                projectId,
                projectNodeId,
                parentRunId.ToString(),
                parentStepId.ToString(),
                parentAssignment.StepKey,
                definitionKey,
                plan.Definition.DefinitionId.Value,
                state.PlanId.Value,
                state.RunId.Value,
                MapStoppedBlockingSubprocessStage(state.Status),
                childLiveProcessesRoute,
                includeLaunchPlan ? BuildStoppedSubprocessLaunchPlanSummary(plan, definitionKey, childAssignments, state.Status) : null,
                childManagedArtifactRoot,
                childStepsArtifactRoot,
                childLiveProcessesRoute,
                parentEvidenceRefs,
                [warning])
            {
                ParentDeferredOutcomeInstruction = "Do not launch a replacement child for this parent step while the matching child run is stopped with blocker status. Submit ParentDeferredOutcomeJson exactly, or rework the stopped child run explicitly.",
                ParentDeferredOutcomeJson = BuildParentStoppedChildOutcomeJson(state.RunId, state.Status, parentEvidenceRefs, childLiveProcessesRoute, parentTargetContext)
            };
        }

        return null;
    }

    private static bool IsStoppedBlockingSubprocessStatus(ProcessRuntimeStatus status)
        => status is ProcessRuntimeStatus.Blocked or ProcessRuntimeStatus.Failed;

    private static string MapStoppedBlockingSubprocessStage(ProcessRuntimeStatus status)
        => status == ProcessRuntimeStatus.Failed
            ? ProcessLaunchStage.Failed.ToString()
            : ProcessLaunchStage.Blocked.ToString();

    private static string BuildScopedRunRoute(Guid projectId, ProcessRunId runId)
        => $"/projects/{projectId:D}/processes/live?runId={runId.Value:D}";

    private static object BuildStoppedSubprocessLaunchPlanSummary(
        ProcessInstancePlan plan,
        string definitionKey,
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
        ProcessRuntimeStatus status)
        => new
        {
            planId = plan.Header.PlanId.Value,
            definitionId = plan.Definition.DefinitionId.Value,
            definitionKey,
            status = status.ToString(),
            steps = assignments
                .OrderBy(assignment => assignment.StepKey, StringComparer.OrdinalIgnoreCase)
                .Select(assignment => new
                {
                    stepInstanceId = assignment.StepInstanceId.Value,
                    stepKey = assignment.StepKey,
                    roleKey = assignment.RoleKey,
                    executorKind = assignment.ExecutorKind,
                    executorId = assignment.ExecutorId
                })
                .ToArray()
        };

    private static IReadOnlyDictionary<string, string> CreateSubprocessIdentityVariables(
        Guid projectId,
        string projectNodeId,
        ProcessRunId parentRunId,
        ProcessStepInstanceId parentStepId,
        string definitionKey,
        string? liveRunProfileKey)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProjectIdVariableName] = projectId.ToString("D"),
            [ProjectNodeIdVariableName] = projectNodeId,
            [ParentProcessRunIdVariableName] = parentRunId.ToString(),
            [ParentProcessStepIdVariableName] = parentStepId.ToString(),
            [SubprocessDefinitionKeyVariableName] = definitionKey
        };

        if (NormalizeOptional(liveRunProfileKey) is { } normalizedLiveRunProfileKey)
        {
            variables[SubprocessLiveRunProfileKeyVariableName] = normalizedLiveRunProfileKey;
        }

        return variables;
    }

    private static IReadOnlyList<string> BuildExpectedChildEvidenceRefs(
        string childManagedArtifactRoot,
        ProcessLaunchPlanView launchPlan)
        => BuildExpectedChildEvidenceRefs(
            childManagedArtifactRoot,
            launchPlan.Steps.Select(step => step.StepKey));

    private static IReadOnlyList<string> BuildExpectedChildEvidenceRefs(
        string childManagedArtifactRoot,
        IEnumerable<string> stepKeys)
    {
        if (string.IsNullOrWhiteSpace(childManagedArtifactRoot))
        {
            return [];
        }

        var evidenceRefs = new List<string>
        {
            childManagedArtifactRoot,
            $"{childManagedArtifactRoot}/steps"
        };

        foreach (var stepKey in stepKeys.Where(stepKey => !string.IsNullOrWhiteSpace(stepKey)))
        {
            evidenceRefs.Add($"{childManagedArtifactRoot}/steps/{stepKey}.md");
        }

        return evidenceRefs;
    }

    private static string BuildParentOutcomeInstruction(
        ProcessRunId? childRunId,
        ProcessLaunchStage childStage)
    {
        if (childRunId is null)
        {
            return "No child run was started, so no parent outcome is available.";
        }

        return childStage switch
        {
            ProcessLaunchStage.Completed => "The matching child run is completed. Call submit_process_step_outcome with ParentDeferredOutcomeJson exactly to complete the parent step from child evidence. Do not launch a replacement child.",
            ProcessLaunchStage.Blocked or ProcessLaunchStage.Failed => "The matching child run is stopped. Submit ParentDeferredOutcomeJson exactly to propagate the stopped child status, or explicitly rework that child before relaunch.",
            _ => "If the child run is still active, call submit_process_step_outcome with ParentDeferredOutcomeJson exactly. Do not inspect child evidence or return a hand-written blocked result until the child run stops."
        };
    }

    private static string BuildParentOutcomeJson(
        ProcessRunId? childRunId,
        ProcessLaunchStage childStage,
        string definitionKey,
        IReadOnlyList<string> expectedChildEvidenceRefs,
        string childLiveProcessesRoute,
        ParentOutcomeTargetContext targetContext,
        IReadOnlyList<string> launchWarnings)
    {
        if (childRunId is null)
        {
            return childStage is ProcessLaunchStage.Blocked or ProcessLaunchStage.Failed
                ? BuildParentNoChildLaunchStoppedOutcomeJson(definitionKey, childStage, expectedChildEvidenceRefs, targetContext, launchWarnings)
                : string.Empty;
        }

        var childRunIdValue = childRunId.Value;
        return childStage switch
        {
            ProcessLaunchStage.Completed => BuildParentCompletedChildOutcomeJson(childRunIdValue, expectedChildEvidenceRefs, childLiveProcessesRoute, targetContext),
            ProcessLaunchStage.Blocked => BuildParentStoppedChildOutcomeJson(
                childRunIdValue,
                ProcessRuntimeStatus.Blocked,
                expectedChildEvidenceRefs,
                childLiveProcessesRoute,
                targetContext),
            ProcessLaunchStage.Failed => BuildParentStoppedChildOutcomeJson(
                childRunIdValue,
                ProcessRuntimeStatus.Failed,
                expectedChildEvidenceRefs,
                childLiveProcessesRoute,
                targetContext),
            _ => BuildParentActiveChildOutcomeJson(childRunIdValue, expectedChildEvidenceRefs, childLiveProcessesRoute, targetContext)
        };
    }

    private static string BuildParentNoChildLaunchStoppedOutcomeJson(
        string definitionKey,
        ProcessLaunchStage childStage,
        IReadOnlyList<string> evidenceRefs,
        ParentOutcomeTargetContext targetContext,
        IReadOnlyList<string> launchWarnings)
    {
        var normalizedDefinitionKey = NormalizeOptional(definitionKey) ?? "mapped child process";
        var targetSummary = BuildParentOutcomeTargetContextMarkdown(targetContext);
        var warnings = launchWarnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Select(warning => warning.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var warningSummary = warnings.Length == 0
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}Launch warnings:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", warnings)}";
        var summary = $"Mapped child process `{normalizedDefinitionKey}` launch returned `{childStage}` before a child run was started. Parent step is blocked until the child launch contract, readiness, or policy boundary is repaired.{warningSummary}";
        return JsonSerializer.Serialize(
            new
            {
                status = "Blocked",
                reason = $"Mapped child process launch for DefinitionKey {normalizedDefinitionKey} returned {childStage} without starting a child run. Repair the child launch contract, readiness findings, or policy boundary before retrying this parent step.{BuildParentOutcomeTargetContextReasonSuffix(targetContext)}{BuildLaunchWarningReasonSuffix(warnings)}",
                branchOutcomeKey = string.Empty,
                branchOutcomeTitle = string.Empty,
                evidenceRefs,
                nextActions = new[]
                {
                    $"Inspect launch readiness and policy findings for mapped child process DefinitionKey {normalizedDefinitionKey}.",
                    "Retry the parent subprocess step after the child launch can create a child run."
                },
                humanReadableSummaryMarkdown = string.IsNullOrWhiteSpace(targetSummary)
                    ? summary
                    : $"{summary}{Environment.NewLine}{Environment.NewLine}{targetSummary}"
            });
    }

    private static string BuildLaunchWarningReasonSuffix(IReadOnlyList<string> launchWarnings)
    {
        if (launchWarnings.Count == 0)
        {
            return string.Empty;
        }

        return $" Launch warnings: {string.Join(" ", launchWarnings)}";
    }

    private static string BuildParentCompletedChildOutcomeJson(
        ProcessRunId childRunId,
        IReadOnlyList<string> expectedChildEvidenceRefs,
        string childLiveProcessesRoute,
        ParentOutcomeTargetContext targetContext)
    {
        var childRunIdText = childRunId.Value.ToString("D");
        var summary = string.IsNullOrWhiteSpace(childLiveProcessesRoute)
            ? $"Child process run `{childRunIdText}` completed. Parent step is completed from child managed artifact evidence."
            : $"Child process run `{childRunIdText}` completed at `{childLiveProcessesRoute}`. Parent step is completed from child managed artifact evidence.";
        var targetSummary = BuildParentOutcomeTargetContextMarkdown(targetContext);
        return JsonSerializer.Serialize(
            new
            {
                status = "Completed",
                reason = $"Child process run {childRunIdText} completed and produced managed child evidence for this parent step.{BuildParentOutcomeTargetContextReasonSuffix(targetContext)}",
                branchOutcomeKey = string.Empty,
                branchOutcomeTitle = string.Empty,
                evidenceRefs = expectedChildEvidenceRefs,
                nextActions = Array.Empty<string>(),
                humanReadableSummaryMarkdown = string.IsNullOrWhiteSpace(targetSummary)
                    ? summary
                    : $"{summary}{Environment.NewLine}{Environment.NewLine}{targetSummary}"
            });
    }

    private static string BuildParentActiveChildOutcomeJson(
        ProcessRunId childRunId,
        IReadOnlyList<string> expectedChildEvidenceRefs,
        string childLiveProcessesRoute,
        ParentOutcomeTargetContext targetContext)
    {
        var childRunIdText = childRunId.Value.ToString("D");
        var nextActions = string.IsNullOrWhiteSpace(childLiveProcessesRoute)
            ? new[] { $"Wait for active child process run {childRunIdText} to produce required evidence." }
            : new[] { $"Wait for active child process run {childRunIdText} to produce required evidence at {childLiveProcessesRoute}." };
        var targetSummary = BuildParentOutcomeTargetContextMarkdown(targetContext);
        var summary = $"Waiting for active child process run `{childRunIdText}`. The parent step should be deferred until the child run is no longer active.";
        return JsonSerializer.Serialize(
            new
            {
                status = "Blocked",
                reason = $"Waiting for active child process run {childRunIdText} to finish and materialize required evidence.{BuildParentOutcomeTargetContextReasonSuffix(targetContext)}",
                branchOutcomeKey = string.Empty,
                branchOutcomeTitle = string.Empty,
                evidenceRefs = expectedChildEvidenceRefs,
                nextActions,
                humanReadableSummaryMarkdown = string.IsNullOrWhiteSpace(targetSummary)
                    ? summary
                    : $"{summary}{Environment.NewLine}{Environment.NewLine}{targetSummary}"
            });
    }

    private static string BuildParentStoppedChildOutcomeJson(
        ProcessRunId childRunId,
        ProcessRuntimeStatus childStatus,
        IReadOnlyList<string> expectedChildEvidenceRefs,
        string childLiveProcessesRoute,
        ParentOutcomeTargetContext targetContext)
    {
        var childRunIdText = childRunId.Value.ToString("D");
        var nextActions = string.IsNullOrWhiteSpace(childLiveProcessesRoute)
            ? new[] { $"Inspect stopped child process run {childRunIdText} and propagate its concrete blocker, or explicitly rework that child before relaunch." }
            : new[] { $"Inspect stopped child process run {childRunIdText} at {childLiveProcessesRoute} and propagate its concrete blocker, or explicitly rework that child before relaunch." };
        var targetSummary = BuildParentOutcomeTargetContextMarkdown(targetContext);
        var summary = $"Child process run `{childRunIdText}` is `{childStatus}`. Propagate the stopped child blocker with evidence refs, or explicitly rework that child before relaunch.";
        return JsonSerializer.Serialize(
            new
            {
                status = "Blocked",
                reason = $"Child process run {childRunIdText} is {childStatus}; this parent step must not launch a replacement child automatically.{BuildParentOutcomeTargetContextReasonSuffix(targetContext)}",
                branchOutcomeKey = string.Empty,
                branchOutcomeTitle = string.Empty,
                evidenceRefs = expectedChildEvidenceRefs,
                nextActions,
                humanReadableSummaryMarkdown = string.IsNullOrWhiteSpace(targetSummary)
                    ? summary
                    : $"{summary}{Environment.NewLine}{Environment.NewLine}{targetSummary}"
            });
    }

    private static ParentOutcomeTargetContext BuildParentOutcomeTargetContext(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        var facts = ParentOutcomeTargetContextVariableKeys
            .Select(key => new ParentOutcomeTargetContextFact(key, ResolveLaunchVariable(launchVariables, key)))
            .Where(fact => !string.IsNullOrWhiteSpace(fact.Value))
            .ToArray();

        var evidenceRefs = BuildParentOutcomeTargetEvidenceRefs(launchVariables)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ParentOutcomeTargetContext(facts, evidenceRefs);
    }

    private static IReadOnlyList<string> BuildParentOutcomeTargetEvidenceRefs(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        var refs = new List<string>();
        AddExternalTargetRef(refs, ResolveLaunchVariable(launchVariables, "ProductRootAlias"));
        AddExternalTargetRef(refs, ResolveLaunchVariable(launchVariables, "OutputRootAlias"));
        AddExternalTargetRef(refs, ResolveLaunchVariable(launchVariables, "ExternalTargetAlias"));
        AddExternalTargetRef(refs, ResolveLaunchVariable(launchVariables, "ExternalTargetRoot"));
        AddExternalTargetRef(refs, ResolveLaunchVariable(launchVariables, "WorkspaceAlias"));

        return refs;
    }

    private static void AddExternalTargetRef(ICollection<string> refs, string value)
    {
        var alias = value.Trim().Replace('\\', '/');
        if (alias.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
        {
            refs.Add(alias);
        }
    }

    private static string BuildParentOutcomeTargetContextReasonSuffix(ParentOutcomeTargetContext targetContext)
        => targetContext.Facts.Count == 0
            ? string.Empty
            : " Grounded product-target launch variables are included in the parent handoff summary.";

    private static string BuildParentOutcomeTargetContextMarkdown(ParentOutcomeTargetContext targetContext)
    {
        if (targetContext.Facts.Count == 0 && targetContext.EvidenceRefs.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("## Grounded product-target context");
        foreach (var fact in targetContext.Facts)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"- {fact.Key}: `{fact.Value}`");
        }

        if (targetContext.EvidenceRefs.Count > 0)
        {
            builder.AppendLine("- Product target refs for downstream validation:");
            foreach (var evidenceRef in targetContext.EvidenceRefs)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"  - `{evidenceRef}`");
            }
        }

        builder.Append("Downstream QA should use the grounded product root alias or launch-variable native path for restore/build/test validation; the process artifact workspace `.` is not the product root.");
        return builder.ToString();
    }

    private static async Task<ProjectStructureSurface> LoadSurfaceAsync(
        ProjectWorkbenchService projectWorkbenchService,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var loadResult = await projectWorkbenchService.TryGetStructureAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (loadResult.Surface is { } surface)
        {
            return surface;
        }

        throw new ProjectStructureAgentException(
            404,
            "ProjectNotFound",
            $"Project '{projectId:D}' was not found in the active database profile.",
            loadResult.UnavailableState);
    }

    private static ProjectStructureProcessNodeScopedDependencies ResolveScopedDependencies(IServiceProvider serviceProvider)
    {
        return new ProjectStructureProcessNodeScopedDependencies(
            serviceProvider.GetRequiredService<ProjectWorkbenchService>(),
            serviceProvider.GetRequiredService<ProcessLaunchApplicationService>(),
            serviceProvider.GetRequiredService<ProcessDefinitionCatalogProjectionService>(),
            serviceProvider.GetRequiredService<IProcessInstancePlanStore>(),
            serviceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>(),
            serviceProvider.GetRequiredService<IProcessRuntimeStateStore>(),
            serviceProvider.GetRequiredService<IWorkspaceFileService>(),
            serviceProvider.GetRequiredService<ProcessLaunchVariablePreparationService>());
    }

    private static IReadOnlyDictionary<string, string> CreateVariables(
        ProjectStructureSurface surface,
        ProjectStructureNode? processNode,
        string processNodeId,
        Guid processDefinitionId,
        ProjectStructureNode targetNode,
        ProjectStructureAgentContext agent,
        ProcessLaunchVariablePreparationService launchVariablePreparationService,
        string? definitionKey)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ProjectId"] = surface.ProjectId.ToString("D"),
            ["ProjectName"] = surface.ProjectName,
            ["ProjectNodeId"] = targetNode.Id,
            ["ProjectNodeTitle"] = targetNode.Title,
            ["ProjectNodeSubtitle"] = targetNode.Subtitle,
            ["ProjectNodeStatus"] = targetNode.Status,
            ["ProjectNodeNotes"] = targetNode.Notes,
            ["ProjectNodeObjectType"] = targetNode.ObjectType.ToString(),
            ["ProjectNodeObjectSubtype"] = targetNode.ObjectSubtype,
            ["ProcessNodeId"] = processNode?.Id ?? processNodeId,
            ["ProcessNodeTitle"] = processNode?.Title ?? $"Process definition {processDefinitionId:D}",
            ["ProcessNodeSubtitle"] = processNode?.Subtitle ?? string.Empty,
            ["ProcessNodeStatus"] = processNode?.Status ?? string.Empty,
            ["ProcessNodeNotes"] = processNode?.Notes ?? string.Empty,
            ["ProcessNodeObjectType"] = (processNode?.ObjectType ?? ProjectObjectType.ProcessDefinition).ToString(),
            ["ProcessNodeObjectSubtype"] = processNode?.ObjectSubtype ?? string.Empty,
            ["AgentId"] = agent.AgentId,
            ["AgentName"] = agent.AgentName,
            ["MachineName"] = agent.MachineName,
            ["RepositoryRoot"] = agent.RepositoryRoot,
            ["BranchName"] = agent.BranchName,
            ["SessionId"] = agent.SessionId
        };

        if (targetNode.RelatedProjectId is { } relatedProjectId)
        {
            variables["RelatedProjectId"] = relatedProjectId.ToString("D");
        }

        var contextSummary = BuildProjectStructureContextSummary(surface, targetNode);
        if (!string.IsNullOrWhiteSpace(contextSummary))
        {
            variables[ProjectStructureContextSummaryVariableName] = contextSummary;
        }

        var outputRoot = ResolveOutputRoot(surface, targetNode);
        if (!string.IsNullOrWhiteSpace(outputRoot))
        {
            ApplyProductRootLaunchVariables(variables, outputRoot);
        }

        ApplyLaunchVariablePreparation(
            launchVariablePreparationService,
            surface,
            targetNode,
            definitionKey,
            isSubprocess: false,
            variables: variables);

        return variables;
    }

    private static IReadOnlyDictionary<string, string> CreateSubprocessVariables(
        Guid projectId,
        ProjectStructureSurface surface,
        ProjectStructureNode projectNode,
        ProcessRunId parentRunId,
        ProcessStepInstanceId parentStepId,
        ProcessRuntimeStepAssignment parentAssignment,
        ProcessRuntimeStateSnapshot parentState,
        string definitionKey,
        ProjectStructureProcessSubprocessLaunchInput request,
        ProjectStructureAgentContext agent,
        IWorkspaceFileService workspaceFiles,
        ProcessLaunchVariablePreparationService launchVariablePreparationService)
    {
        var variables = CopyInheritableSubprocessLaunchVariables(parentAssignment.LaunchVariables);
        if (request.Variables is not null)
        {
            foreach (var item in request.Variables)
            {
                var key = NormalizeOptional(item.Key);
                if (key is not null &&
                    !IsSubprocessReservedLaunchVariableKey(key) &&
                    !IsSubprocessDerivedScopeLaunchVariableKey(key))
                {
                    variables[key] = NormalizeLaunchVariableValue(item.Value);
                }
            }
        }

        variables[ProjectIdVariableName] = projectId.ToString("D");
        variables["ProjectName"] = surface.ProjectName;
        variables[ProjectNodeIdVariableName] = projectNode.Id;
        variables[ProjectNodeTitleVariableName] = projectNode.Title;
        variables[ProjectNodeSubtitleVariableName] = projectNode.Subtitle;
        variables[ProjectNodeStatusVariableName] = projectNode.Status;
        variables[ProjectNodeNotesVariableName] = projectNode.Notes;
        variables[ProjectNodeObjectTypeVariableName] = projectNode.ObjectType.ToString();
        variables[ProjectNodeObjectSubtypeVariableName] = projectNode.ObjectSubtype;
        variables[ParentProcessRunIdVariableName] = parentRunId.ToString();
        var parentProcessRunNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(parentRunId.Value);
        variables[ProcessRunNodeIdVariableName] = parentProcessRunNodeId;
        variables[ParentProcessRunNodeIdVariableName] = parentProcessRunNodeId;
        variables[TargetProcessRunNodeIdVariableName] = parentProcessRunNodeId;
        variables[ParentProcessStepIdVariableName] = parentStepId.ToString();
        variables[ParentProcessStepKeyVariableName] = parentAssignment.StepKey;
        variables[SubprocessDefinitionKeyVariableName] = definitionKey;
        variables[AgentIdVariableName] = agent.AgentId;
        variables[AgentNameVariableName] = agent.AgentName;
        variables[MachineNameVariableName] = agent.MachineName;
        variables[RepositoryRootVariableName] = agent.RepositoryRoot;
        variables[BranchNameVariableName] = agent.BranchName;
        variables[SessionIdVariableName] = agent.SessionId;

        var outputRoot = ResolveOutputRoot(surface, projectNode);
        if (!string.IsNullOrWhiteSpace(outputRoot))
        {
            ApplyProductRootLaunchVariables(variables, outputRoot);
        }

        var liveRunProfileKey = NormalizeOptional(request.LiveRunProfileKey);
        if (!string.IsNullOrWhiteSpace(liveRunProfileKey))
        {
            variables[SubprocessLiveRunProfileKeyVariableName] = liveRunProfileKey;
        }

        var contextSummary = BuildProjectStructureContextSummary(surface, projectNode);
        if (!string.IsNullOrWhiteSpace(contextSummary))
        {
            variables[ProjectStructureContextSummaryVariableName] = contextSummary;
        }
        else
        {
            variables.Remove(ProjectStructureContextSummaryVariableName);
        }

        ProcessSubprocessParentArtifactContextBuilder.Apply(variables, parentState, parentStepId, workspaceFiles);
        ApplyLaunchVariablePreparation(
            launchVariablePreparationService,
            surface,
            projectNode,
            definitionKey,
            isSubprocess: true,
            variables: variables);
        RemoveSubprocessReservedLaunchVariables(variables);

        return variables;
    }

    private static Dictionary<string, string> CopyInheritableSubprocessLaunchVariables(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in launchVariables)
        {
            if (!IsSubprocessReservedLaunchVariableKey(item.Key) &&
                !IsSubprocessDerivedScopeLaunchVariableKey(item.Key))
            {
                variables[item.Key] = item.Value;
            }
        }

        return variables;
    }

    private static Dictionary<string, string> CopyLaunchVariables(
        IReadOnlyDictionary<string, string>? launchVariables)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (launchVariables is null)
        {
            return variables;
        }

        foreach (var item in launchVariables)
        {
            var key = NormalizeOptional(item.Key);
            if (key is null)
            {
                continue;
            }

            variables[key] = NormalizeLaunchVariableValue(item.Value);
        }

        return variables;
    }

    private static void RemoveSubprocessReservedLaunchVariables(IDictionary<string, string> variables)
    {
        foreach (var key in variables.Keys
            .Where(IsSubprocessReservedLaunchVariableKey)
            .ToArray())
        {
            variables.Remove(key);
        }
    }

    private static bool IsSubprocessReservedLaunchVariableKey(string key)
        => SubprocessReservedLaunchVariableKeys.Contains(key.Trim(), StringComparer.OrdinalIgnoreCase);

    private static bool IsSubprocessDerivedScopeLaunchVariableKey(string key)
        => SubprocessDerivedScopeLaunchVariableKeys.Contains(key.Trim(), StringComparer.OrdinalIgnoreCase);

    private static string NormalizeLaunchVariableValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text.Trim(),
            JsonElement element => NormalizeJsonLaunchVariableValue(element),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
            _ => value.ToString()?.Trim() ?? string.Empty
        };
    }

    private static string NormalizeJsonLaunchVariableValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText()
        };
    }

    private static void ApplyLaunchVariablePreparation(
        ProcessLaunchVariablePreparationService launchVariablePreparationService,
        ProjectStructureSurface surface,
        ProjectStructureNode targetNode,
        string? definitionKey,
        bool isSubprocess,
        IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(launchVariablePreparationService);

        var contextSummary = variables.TryGetValue(ProjectStructureContextSummaryVariableName, out var value)
            ? value
            : string.Empty;
        var context = ProjectStructureProcessLaunchSourceSnapshotMapper.Create(
            surface,
            targetNode,
            definitionKey,
            isSubprocess,
            contextSummary);
        launchVariablePreparationService.Enrich(context, variables);
    }

    private static string BuildProjectStructureContextSummary(ProjectStructureSurface surface, ProjectStructureNode focusNode)
    {
        var contextRows = EnumerateProjectStructureContextNodes(surface, focusNode)
            .Where(row =>
                string.Equals(row.Node.Id, focusNode.Id, StringComparison.Ordinal) ||
                ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext(row.Node))
            .ToArray();
        var rows = contextRows
            .Take(40)
            .ToArray();
        if (rows.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Project structure source: {surface.ProjectName} ({surface.ProjectId:D}).");
        builder.AppendLine($"Selected node: {focusNode.Title} ({focusNode.Id}).");
        AppendVisualTargetAssetSummary(builder, contextRows);
        foreach (var (node, depth) in rows)
        {
            var marker = string.Equals(node.Id, focusNode.Id, StringComparison.Ordinal)
                ? " [selected]"
                : string.Empty;
            var subtype = string.IsNullOrWhiteSpace(node.ObjectSubtype)
                ? node.ObjectType.ToString()
                : $"{node.ObjectType}/{node.ObjectSubtype}";
            var notes = NormalizeContextText(string.Join(" ", node.Subtitle, node.Notes), 420);
            var indent = depth <= 0 ? string.Empty : new string(' ', Math.Min(depth, 8) * 2);

            builder.Append("- ");
            builder.Append(indent);
            builder.Append(node.Title);
            builder.Append(marker);
            builder.Append(" [");
            builder.Append(subtype);
            builder.Append("; ");
            builder.Append(string.IsNullOrWhiteSpace(node.Status) ? "Draft" : node.Status);
            builder.Append(']');
            if (!string.IsNullOrWhiteSpace(notes))
            {
                builder.Append(": ");
                builder.Append(notes);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendVisualTargetAssetSummary(
        StringBuilder builder,
        IReadOnlyList<(ProjectStructureNode Node, int Depth)> contextRows)
    {
        var assets = contextRows
            .Select(row => row.Node)
            .Where(IsVisualTargetAsset)
            .Take(8)
            .ToArray();
        if (assets.Length == 0)
        {
            return;
        }

        builder.AppendLine("Visual target assets:");
        foreach (var asset in assets)
        {
            var subtype = string.IsNullOrWhiteSpace(asset.ObjectSubtype)
                ? asset.ObjectType.ToString()
                : $"{asset.ObjectType}/{asset.ObjectSubtype}";
            var media = string.IsNullOrWhiteSpace(asset.MediaRelativePath)
                ? "no media path"
                : asset.MediaRelativePath;
            var fileName = string.IsNullOrWhiteSpace(asset.MediaOriginalFileName)
                ? "unknown file"
                : asset.MediaOriginalFileName;
            var contentType = string.IsNullOrWhiteSpace(asset.MediaContentType)
                ? "unknown content type"
                : asset.MediaContentType;
            var notes = NormalizeContextText(string.Join(" ", asset.Subtitle, asset.Notes), 360);

            builder.Append("- ");
            builder.Append(asset.Title);
            builder.Append(" (");
            builder.Append(asset.Id);
            builder.Append(") [");
            builder.Append(subtype);
            builder.Append("; ");
            builder.Append(contentType);
            builder.Append("; media=");
            builder.Append(media);
            builder.Append("; file=");
            builder.Append(fileName);
            builder.Append("; parent=");
            builder.Append(asset.ParentId ?? "none");
            builder.Append(']');
            if (!string.IsNullOrWhiteSpace(notes))
            {
                builder.Append(": ");
                builder.Append(notes);
            }

            builder.AppendLine();
        }

        builder.AppendLine("Visual target rule: implementation and QA must fetch or analyze the relevant asset content before accepting visual alignment; do not rely only on this text summary or on generated app screenshots in isolation.");
    }

    private static bool IsVisualTargetAsset(ProjectStructureNode node)
    {
        if (!ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext(node))
        {
            return false;
        }

        if (node.ObjectType != ProjectObjectType.ImageAsset)
        {
            return false;
        }

        if (string.Equals(node.ObjectSubtype, "screenshot", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.ArtifactKind, "process-run-screenshot", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(node.ObjectSubtype, "generated", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.ObjectSubtype, "layout-recommendation", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var searchableText = string.Join(" ", node.Title, node.Subtitle, node.Notes, node.ObjectSubtype, node.ArtifactKind);
        return ContainsVisualTargetKeyword(searchableText);
    }

    private static bool ContainsVisualTargetKeyword(string text)
        => text.Contains("visual", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("target", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("proposal", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("mockup", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("wireframe", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("layout", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("design", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("look", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("ui", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<(ProjectStructureNode Node, int Depth)> EnumerateProjectStructureContextNodes(
        ProjectStructureSurface surface,
        ProjectStructureNode focusNode)
    {
        var projectRootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(surface.ProjectId);
        var contextNodes = surface.Nodes
            .Where(node =>
                string.Equals(node.Id, focusNode.Id, StringComparison.Ordinal) ||
                ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext(node))
            .ToArray();
        var childrenByParent = contextNodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(node => node.Y).ThenBy(node => node.X).ThenBy(node => node.Title, StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.Ordinal);
        var rows = new List<(ProjectStructureNode Node, int Depth)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(ProjectStructureNode node, int depth)
        {
            if (!visited.Add(node.Id))
            {
                return;
            }

            rows.Add((node, depth));
            if (!childrenByParent.TryGetValue(node.Id, out var children))
            {
                return;
            }

            foreach (var child in children)
            {
                Visit(child, depth + 1);
            }
        }

        if (childrenByParent.TryGetValue(projectRootNodeId, out var rootChildren))
        {
            foreach (var rootChild in rootChildren)
            {
                Visit(rootChild, 0);
            }
        }

        foreach (var node in contextNodes.OrderBy(node => node.Title, StringComparer.OrdinalIgnoreCase))
        {
            Visit(node, 0);
        }

        if (rows.Any(row => string.Equals(row.Node.Id, focusNode.Id, StringComparison.Ordinal)))
        {
            return rows;
        }

        return [(focusNode, 0), .. rows];
    }

    private static ProjectStructureNode? ResolveProcessStartTargetNode(
        ProjectStructureSurface surface,
        Guid projectId,
        string nodeId,
        ProjectStructureNode? node)
    {
        var projectRootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        var nodesById = surface.Nodes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        return surface.Links
            .Where(link =>
                link.IsUserAuthored &&
                string.Equals(link.TargetId, nodeId, StringComparison.Ordinal))
            .OrderBy(link => link.Kind == ProjectObjectLinkKind.Uses ? 0 : 1)
            .ThenBy(link => string.Equals(link.SourceId, projectRootNodeId, StringComparison.Ordinal) ? 1 : 0)
            .Select(link => nodesById.GetValueOrDefault(link.SourceId))
            .FirstOrDefault(candidate => candidate is not null)
            ?? (!string.IsNullOrWhiteSpace(node?.ParentId) && nodesById.TryGetValue(node.ParentId, out var parent)
                ? parent
            : null);
    }

    private static Guid? ResolveLinkedProcessDefinitionId(
        ProjectStructureSurface surface,
        string sourceNodeId)
    {
        var linkedDefinitionIds = surface.Links
            .Where(link =>
                link.IsUserAuthored &&
                link.Kind == ProjectObjectLinkKind.Uses &&
                string.Equals(link.SourceId, sourceNodeId, StringComparison.Ordinal) &&
                ProjectStructureProcessNodeKeys.TryParseProcessDefinitionNodeKey(link.TargetId, out _))
            .Select(link =>
            {
                ProjectStructureProcessNodeKeys.TryParseProcessDefinitionNodeKey(link.TargetId, out var definitionId);
                return definitionId;
            })
            .Distinct()
            .ToArray();

        return linkedDefinitionIds.Length switch
        {
            0 => null,
            1 => linkedDefinitionIds[0],
            _ => throw new ProjectStructureAgentException(
                400,
                "ProcessDefinitionAmbiguous",
                $"Node '{sourceNodeId}' is linked to multiple process definitions. Supply ProcessDefinitionId to choose one.")
        };
    }

    private static Guid? ResolveProcessDefinitionId(ProjectStructureNode? node, string nodeId)
    {
        if (node?.ObjectType == ProjectObjectType.ProcessDefinition &&
            node.ArtifactId is { } artifactId)
        {
            return artifactId;
        }

        return ProjectStructureProcessNodeKeys.TryParseProcessDefinitionNodeKey(node?.Id ?? nodeId, out var definitionId)
            ? definitionId
            : null;
    }

    private static string? ResolveProcessDefinitionNodeId(Guid? processDefinitionId, string nodeId)
    {
        if (ProjectStructureProcessNodeKeys.TryParseProcessDefinitionNodeKey(nodeId, out _))
        {
            return nodeId;
        }

        return processDefinitionId.HasValue
            ? ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(processDefinitionId.Value)
            : null;
    }

    private static bool IsProcessDefinitionNode(ProjectStructureNode? node, string nodeId)
    {
        return node?.ObjectType == ProjectObjectType.ProcessDefinition ||
            ProjectStructureProcessNodeKeys.TryParseProcessDefinitionNodeKey(node?.Id ?? nodeId, out _);
    }

    private static string ResolveOutputRoot(ProjectStructureSurface surface, ProjectStructureNode targetNode)
    {
        var direct = ResolveOutputRoot(targetNode);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        foreach (var (node, _) in EnumerateProjectStructureContextNodes(surface, targetNode))
        {
            var candidate = ResolveOutputRoot(node);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static void ApplyProductRootLaunchVariables(
        IDictionary<string, string> variables,
        string outputRoot)
    {
        var normalizedOutputRoot = outputRoot.Trim();
        variables["OutputRoot"] = normalizedOutputRoot;
        variables["ProductRoot"] = normalizedOutputRoot;
    }

    private static string ResolveOutputRoot(ProjectStructureNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        var metadataOutputRoot = TryReadOutputRootFromMetadata(node.MetadataJson);
        if (!string.IsNullOrWhiteSpace(metadataOutputRoot))
        {
            return metadataOutputRoot;
        }

        var text = string.Join(Environment.NewLine, node.Title, node.Subtitle, node.Notes);
        var match = Regex.Match(text, @"[A-Za-z]:\\[^\r\n""<>|]+");
        return match.Success
            ? match.Value.Trim().TrimEnd('.', ',', ';', ')', ']')
            : string.Empty;
    }

    private static string TryReadOutputRootFromMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return string.Empty;
        }

        try
        {
            var metadata = ProjectObjectMetadataSerializer.Parse(metadataJson);
            var typedOutputRoot = FirstNonEmpty(
                metadata.ProjectBlock?.OutputRoot,
                metadata.ProjectBlock?.ProductRoot,
                metadata.ProjectBlock?.TargetRoot,
                metadata.ProjectBlock?.RepositoryRoot,
                metadata.ProjectBlock?.WorkspaceRoot);
            if (!string.IsNullOrWhiteSpace(typedOutputRoot))
            {
                return typedOutputRoot;
            }

            using var document = JsonDocument.Parse(metadataJson);
            return TryReadOutputRootFromElement(document.RootElement);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
        }

        return string.Empty;
    }

    private static string TryReadOutputRootFromElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in OutputRootMetadataKeys)
            {
                if (element.TryGetProperty(key, out var property) &&
                    property.ValueKind == JsonValueKind.String)
                {
                    var value = property.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                var value = TryReadOutputRootFromElement(property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var value = TryReadOutputRootFromElement(item);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return string.Empty;
    }

    private static string NormalizeContextText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = RedactNonCitableContextPaths(Regex.Replace(value, @"\s+", " ").Trim());
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength].TrimEnd() + "...";
    }

    private static string RedactNonCitableContextPaths(string value)
    {
        var withoutNativePaths = Regex.Replace(
            value,
            @"(?:file://[^\s""'<>]+|[A-Za-z]:\\[^\s""'<>|]+|\\\\[^\s""'<>|]+)",
            "[storage-path]");
        return Regex.Replace(
            withoutNativePaths,
            @"\b(?:artifacts/scopes|project-media|managed-files|tool-runs)[^\s""'<>]*",
            "[storage-path]",
            RegexOptions.IgnoreCase);
    }

    private static ProcessRunId ParseProcessRunId(string value)
    {
        if (!Guid.TryParse(value, out var parsed) ||
            parsed == Guid.Empty)
        {
            throw new ProjectStructureAgentException(
                400,
                "ParentProcessRunIdInvalid",
                $"Parent process run id '{value}' is not a valid non-empty GUID.");
        }

        return new ProcessRunId(parsed);
    }

    private static ProcessStepInstanceId ParseProcessStepInstanceId(string value)
    {
        if (!Guid.TryParse(value, out var parsed) ||
            parsed == Guid.Empty)
        {
            throw new ProjectStructureAgentException(
                400,
                "ParentProcessStepIdInvalid",
                $"Parent process step id '{value}' is not a valid non-empty GUID.");
        }

        return new ProcessStepInstanceId(parsed);
    }

    private static void ValidateParentProjectScope(
        Guid projectId,
        ProcessRuntimeStepAssignment parentAssignment)
    {
        var parentProjectId = ResolveLaunchVariable(parentAssignment.LaunchVariables, ProjectIdVariableName);
        if (string.IsNullOrWhiteSpace(parentProjectId))
        {
            return;
        }

        if (!Guid.TryParse(parentProjectId, out var parsed) ||
            parsed != projectId)
        {
            throw new ProjectStructureAgentException(
                403,
                "ProcessSubprocessProjectScopeMismatch",
                $"Parent process step '{parentAssignment.StepKey}' is scoped to project '{parentProjectId}', not '{projectId:D}'.");
        }
    }

    private static string NormalizeRequired(
        string? value,
        string errorCode,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProjectStructureAgentException(400, errorCode, message);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string ResolveLaunchVariable(
        IReadOnlyDictionary<string, string> variables,
        string key)
    {
        if (variables.TryGetValue(key, out var value))
        {
            return value.Trim();
        }

        foreach (var item in variables)
        {
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return item.Value.Trim();
            }
        }

        return string.Empty;
    }

    private static bool ContainsProcessOperation(
        IReadOnlyList<string> operations,
        string operationName)
    {
        return operations.Any(operation => string.Equals(operation, operationName, StringComparison.OrdinalIgnoreCase));
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private sealed record ParentOutcomeTargetContext(
        IReadOnlyList<ParentOutcomeTargetContextFact> Facts,
        IReadOnlyList<string> EvidenceRefs);

    private sealed record ParentOutcomeTargetContextFact(
        string Key,
        string Value);

    private sealed record ProjectStructureProcessNodeScopedDependencies(
        ProjectWorkbenchService ProjectWorkbenchService,
        ProcessLaunchApplicationService ProcessLaunchApplicationService,
        ProcessDefinitionCatalogProjectionService ProcessDefinitionCatalogService,
        IProcessInstancePlanStore PlanStore,
        IProcessRuntimeStepAssignmentStore AssignmentStore,
        IProcessRuntimeStateStore StateStore,
        IWorkspaceFileService WorkspaceFiles,
        ProcessLaunchVariablePreparationService LaunchVariablePreparationService);
}

public sealed record ProjectStructureProcessLaunchVariableBuildRequest(
    Guid ProjectId,
    string ProjectNodeId,
    string? DefinitionKey,
    ProcessDefinitionId? ProcessDefinitionId,
    string RequestedBy,
    IReadOnlyDictionary<string, string> Variables);
