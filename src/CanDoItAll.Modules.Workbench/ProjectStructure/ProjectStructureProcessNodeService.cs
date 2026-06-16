using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureProcessNodeService(
    ProjectWorkbenchService projectWorkbenchService,
    ProcessLaunchApplicationService processLaunchApplicationService)
{
    public async Task<ProjectStructureProcessNodeStartResult> StartAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureProcessNodeStartInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
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

        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken).ConfigureAwait(false);
        var node = surface.Nodes.FirstOrDefault(candidate => string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
        if (node is null)
        {
            throw new ProjectStructureAgentException(
                404,
                "ProjectStructureNodeNotFound",
                $"Node '{nodeId}' was not found in project '{projectId:D}'.");
        }

        var launch = await processLaunchApplicationService
            .LaunchAsync(
                new ProcessLaunchRequest(
                    DefinitionKey: null,
                    request.ProcessDefinitionId is { } processDefinitionId
                        ? new ProcessDefinitionId(processDefinitionId)
                        : null,
                    LiveRunProfileKey: null,
                    ProjectId: projectId,
                    ProjectNodeId: nodeId,
                    RequestedBy: string.IsNullOrWhiteSpace(request.RequestedBy)
                        ? agent.AgentName
                        : request.RequestedBy,
                    Variables: CreateVariables(surface, node, agent),
                    RunReadiness: request.RunHrMatch,
                    Execute: request.Execute),
                cancellationToken)
            .ConfigureAwait(false);

        return new ProjectStructureProcessNodeStartResult(
            projectId,
            nodeId,
            launch.DefinitionId.Value,
            launch.LaunchPlanId.Value,
            launch.RunId?.Value,
            launch.Stage.ToString(),
            launch.Route,
            request.IncludeLaunchPlan ? launch.LaunchPlan : null,
            launch.Warnings);
    }

    private static IReadOnlyDictionary<string, string> CreateVariables(
        ProjectStructureSurface surface,
        ProjectStructureNode node,
        ProjectStructureAgentContext agent)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ProjectId"] = surface.ProjectId.ToString("D"),
            ["ProjectName"] = surface.ProjectName,
            ["ProjectNodeId"] = node.Id,
            ["ProjectNodeTitle"] = node.Title,
            ["ProjectNodeSubtitle"] = node.Subtitle,
            ["ProjectNodeStatus"] = node.Status,
            ["ProjectNodeNotes"] = node.Notes,
            ["ProjectNodeObjectType"] = node.ObjectType.ToString(),
            ["ProjectNodeObjectSubtype"] = node.ObjectSubtype,
            ["AgentId"] = agent.AgentId,
            ["AgentName"] = agent.AgentName,
            ["MachineName"] = agent.MachineName,
            ["RepositoryRoot"] = agent.RepositoryRoot,
            ["BranchName"] = agent.BranchName,
            ["SessionId"] = agent.SessionId
        };

        if (node.RelatedProjectId is { } relatedProjectId)
        {
            variables["RelatedProjectId"] = relatedProjectId.ToString("D");
        }

        return variables;
    }
}
