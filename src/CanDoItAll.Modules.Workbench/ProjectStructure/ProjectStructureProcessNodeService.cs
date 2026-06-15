namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureProcessNodeService
{
    public Task<ProjectStructureProcessNodeStartResult> StartAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureProcessNodeStartInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(agent);

        throw new ProjectStructureAgentException(
            410,
            "ProcessModuleRewriteInProgress",
            "Project-structure process launching is unavailable until the rebuilt Process application layer is introduced.");
    }
}
