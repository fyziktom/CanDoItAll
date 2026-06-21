using CanDoItAll.Modules.Processes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureProcessNodeService(
    ProjectWorkbenchService projectWorkbenchService,
    ProcessesService processesService,
    ProjectStructureLeaseService leaseService,
    ILogger<ProjectStructureProcessNodeService> logger)
{
    private const string DefaultRequestedBy = "project-structure-api";

    public Task<ProjectStructureProcessNodeStartResult> StartAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureProcessNodeStartInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        return leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            request.LeaseToken,
            agent,
            "start-process-node",
            cancellationToken => StartCoreAsync(projectId, nodeId, request, agent, cancellationToken),
            cancellationToken);
    }

    private async Task<ProjectStructureProcessNodeStartResult> StartCoreAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureProcessNodeStartInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var nodesById = surface.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        if (!nodesById.TryGetValue(nodeId, out var node))
        {
            throw new ProjectStructureAgentException(404, "NodeNotFound", $"Node '{nodeId}' was not found.");
        }

        var processDefinitionId = ResolveProcessDefinitionId(node, request.ProcessDefinitionId);
        var startContext = CreateStartContext(projectId, node, processDefinitionId, surface, nodesById, agent);
        var requestedBy = ResolveRequestedBy(request.RequestedBy);
        var launchName = string.Equals(startContext.ResolveTargetNodeId(), node.Id, StringComparison.Ordinal)
            ? startContext.ResolveTargetNodeTitle()
            : $"{startContext.ResolveTargetNodeTitle()} / {node.Title}";
        var createResult = await processesService.CreateLaunchPlanAsync(
            new ProcessLaunchCreateRequest
            {
                ProcessDefinitionId = processDefinitionId,
                ProjectId = projectId,
                LaunchName = launchName,
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Started from project structure API.",
                ProjectStructureContext = startContext,
                RequestedBy = requestedBy
            },
            cancellationToken);
        var launchPlanId = ResolveResult(createResult, "ProcessLaunchPlanCreateFailed");

        if (request.RunHrMatch)
        {
            ResolveResult(
                await processesService.MatchLaunchPlanWithHrManagerAsync(launchPlanId, requestedBy, cancellationToken),
                "ProcessLaunchHrMatchFailed");
        }

        var launchPlan = await LoadLaunchPlanAsync(launchPlanId, cancellationToken);
        if (!request.Execute)
        {
            return new ProjectStructureProcessNodeStartResult(
                projectId,
                nodeId,
                processDefinitionId,
                launchPlanId,
                null,
                "launch-plan-ready",
                BuildLaunchRoute(projectId, processDefinitionId, launchPlanId),
                request.IncludeLaunchPlan ? launchPlan : null,
                []);
        }

        if (HasRequiredRoleGaps(launchPlan))
        {
            throw new ProjectStructureAgentException(
                400,
                "ProcessLaunchRoleGaps",
                "Every required role must be resolved before the process can start.",
                new
                {
                    LaunchPlanId = launchPlanId,
                    Roles = launchPlan.Roles
                        .Where(role => role.IsRequired && !role.IsResolved)
                        .Select(role => new { role.Id, role.DisplayName })
                        .ToList()
                });
        }

        ResolveResult(
            await processesService.SubmitLaunchPlanForApprovalAsync(launchPlanId, requestedBy, cancellationToken),
            "ProcessLaunchSubmitFailed");
        ResolveResult(
            await processesService.DecideLaunchPlanApprovalAsync(
                new ProcessLaunchApprovalDecisionRequest
                {
                    LaunchPlanId = launchPlanId,
                    Status = ProcessLaunchApprovalStatus.Approved,
                    ResolutionSummary = $"Approved from project structure API for '{startContext.ResolveTargetNodeTitle()}' using '{node.Title}'.",
                    DecidedBy = requestedBy
                },
                cancellationToken),
            "ProcessLaunchApprovalFailed");
        ResolveResult(
            await processesService.ProvisionLaunchPlanAsync(launchPlanId, requestedBy, cancellationToken),
            "ProcessLaunchProvisionFailed");
        var runId = ResolveResult(
            await processesService.ExecuteLaunchPlanAsync(
                new ProcessLaunchExecutionRequest
                {
                    LaunchPlanId = launchPlanId,
                    RequestedBy = requestedBy
                },
                cancellationToken),
            "ProcessLaunchExecutionFailed");

        var warnings = await TryLinkRunAsync(projectId, startContext, runId, cancellationToken);
        var refreshedLaunchPlan = request.IncludeLaunchPlan
            ? await LoadLaunchPlanAsync(launchPlanId, cancellationToken)
            : null;

        return new ProjectStructureProcessNodeStartResult(
            projectId,
            nodeId,
            processDefinitionId,
            launchPlanId,
            runId,
            "run-started",
            BuildRunRoute(projectId, processDefinitionId, runId),
            refreshedLaunchPlan,
            warnings);
    }

    private async Task<IReadOnlyList<string>> TryLinkRunAsync(
        Guid projectId,
        ProcessProjectStructureContext startContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var sourceNodeId = startContext.ResolveTargetNodeId();
        if (string.IsNullOrWhiteSpace(sourceNodeId))
        {
            return ["Process run was started but no project-structure target node was available to link."];
        }

        try
        {
            await projectWorkbenchService.LinkObjectsAsync(
                projectId,
                sourceNodeId,
                ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId),
                ProjectObjectLinkKind.Uses,
                cancellationToken);
            return [];
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(
                exception,
                "Process run {RunId} started but could not be linked to project structure node {NodeId}. ProjectId={ProjectId}",
                runId,
                sourceNodeId,
                projectId);
            return [$"Process run '{runId:D}' started but could not be linked to node '{sourceNodeId}'."];
        }
    }

    private async Task<ProcessLaunchPlanDetails> LoadLaunchPlanAsync(Guid launchPlanId, CancellationToken cancellationToken)
    {
        var launchPlan = await processesService.GetLaunchPlanAsync(launchPlanId, cancellationToken);
        if (launchPlan is null)
        {
            throw new ProjectStructureAgentException(404, "ProcessLaunchPlanNotFound", $"Launch plan '{launchPlanId:D}' was not found.");
        }

        return launchPlan;
    }

    private static ProcessProjectStructureContext CreateStartContext(
        Guid projectId,
        ProjectStructureNode node,
        Guid processDefinitionId,
        ProjectStructureSurface surface,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById,
        ProjectStructureAgentContext agent)
    {
        var launchAgent = MapLaunchAgent(agent);
        if (!ProjectStructureProcessNodeKeys.TryParseProcessDefinitionNodeKey(node.Id, out _))
        {
            var processNodeId = ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(processDefinitionId);
            nodesById.TryGetValue(processNodeId, out var processNode);
            return new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = processNode?.Id ?? processNodeId,
                NodeTitle = processNode?.Title ?? "Process definition",
                ParentNodeId = node.Id,
                ParentNodeTitle = node.Title,
                LaunchAgent = launchAgent
            };
        }

        var parentNode = ResolveProcessStartTargetNode(projectId, node, surface, nodesById) ??
            (!string.IsNullOrWhiteSpace(node.ParentId) && nodesById.TryGetValue(node.ParentId, out var parent) ? parent : null);

        return new ProcessProjectStructureContext
        {
            ProjectId = projectId,
            NodeId = node.Id,
            NodeTitle = node.Title,
            ParentNodeId = parentNode?.Id,
            ParentNodeTitle = parentNode?.Title ?? string.Empty,
            LaunchAgent = launchAgent
        };
    }

    private static ProjectStructureAgentIdentityDescriptor MapLaunchAgent(ProjectStructureAgentContext agent)
    {
        return new ProjectStructureAgentIdentityDescriptor(
            agent.AgentId.Trim(),
            agent.AgentName.Trim(),
            agent.MachineName.Trim(),
            agent.RepositoryRoot.Trim(),
            agent.BranchName.Trim(),
            agent.SessionId.Trim());
    }

    private static ProjectStructureNode? ResolveProcessStartTargetNode(
        Guid projectId,
        ProjectStructureNode node,
        ProjectStructureSurface surface,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById)
    {
        var projectRootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        return surface.Links
            .Where(link =>
                link.IsUserAuthored &&
                string.Equals(link.TargetId, node.Id, StringComparison.Ordinal))
            .OrderBy(link => link.Kind == ProjectObjectLinkKind.Uses ? 0 : 1)
            .ThenBy(link => string.Equals(link.SourceId, projectRootNodeId, StringComparison.Ordinal) ? 1 : 0)
            .Select(link => nodesById.TryGetValue(link.SourceId, out var candidate) ? candidate : null)
            .FirstOrDefault(candidate => candidate is not null);
    }

    private static Guid ResolveProcessDefinitionId(ProjectStructureNode node, Guid? requestedDefinitionId)
    {
        if (requestedDefinitionId.HasValue && requestedDefinitionId.Value != Guid.Empty)
        {
            return requestedDefinitionId.Value;
        }

        if (node.ArtifactId.HasValue)
        {
            return node.ArtifactId.Value;
        }

        if (ProjectStructureProcessNodeKeys.TryParseProcessDefinitionNodeKey(node.Id, out var definitionId))
        {
            return definitionId;
        }

        throw new ProjectStructureAgentException(
            400,
            "ProcessDefinitionMissing",
            $"Node '{node.Id}' is missing a process definition id.");
    }

    private static bool HasRequiredRoleGaps(ProcessLaunchPlanDetails launchPlan)
    {
        return launchPlan.Roles.Any(role => role.IsRequired && !role.IsResolved);
    }

    private static string ResolveRequestedBy(string requestedBy)
    {
        return string.IsNullOrWhiteSpace(requestedBy)
            ? DefaultRequestedBy
            : requestedBy.Trim();
    }

    private static void ResolveResult(Result result, string errorCode)
    {
        if (result.IsSuccess)
        {
            return;
        }

        ThrowResultFailure(result.Errors, errorCode);
    }

    private static T ResolveResult<T>(Result<T> result, string errorCode)
    {
        if (result.IsFailure)
        {
            throw CreateResultFailure(result.Errors, errorCode);
        }

        return result.Value!;
    }

    private static void ThrowResultFailure(IReadOnlyCollection<Error> errors, string errorCode)
    {
        throw CreateResultFailure(errors, errorCode);
    }

    private static ProjectStructureAgentException CreateResultFailure(IReadOnlyCollection<Error> errors, string errorCode)
    {
        var message = errors.FirstOrDefault()?.Message ?? "The process launch operation failed.";
        return new ProjectStructureAgentException(400, errorCode, message, errors);
    }

    private static string BuildLaunchRoute(Guid projectId, Guid definitionId, Guid launchPlanId)
    {
        return $"/projects/{projectId:D}/processes?processId={definitionId:D}&launchPlanId={launchPlanId:D}";
    }

    private static string BuildRunRoute(Guid projectId, Guid definitionId, Guid runId)
    {
        return $"/projects/{projectId:D}/processes?processId={definitionId:D}&runId={runId:D}";
    }
}
