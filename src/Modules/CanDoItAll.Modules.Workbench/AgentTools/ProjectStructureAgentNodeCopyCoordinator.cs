namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureAgentNodeCopyCoordinator
{
    private readonly ProjectStructureAgentNodeCopyOperation copyNodes;

    public ProjectStructureAgentNodeCopyCoordinator(ProjectStructureAgentService agentService)
        : this(agentService.CopyNodesAsync)
    {
    }

    internal ProjectStructureAgentNodeCopyCoordinator(ProjectStructureAgentNodeCopyOperation copyNodes)
    {
        ArgumentNullException.ThrowIfNull(copyNodes);
        this.copyNodes = copyNodes;
    }

    public Task<ProjectStructureNodesCopyResult> CopyAsync(
        Guid projectId,
        ProjectStructureNodesCopyInput request,
        ProjectStructureAgentContext agentContext,
        bool requiresNonTaskWriteGuard,
        CancellationToken cancellationToken)
    {
        var taskPolicy = requiresNonTaskWriteGuard
            ? ProjectStructureClipboardCopyTaskPolicy.NonTaskStructureOnly
            : ProjectStructureClipboardCopyTaskPolicy.AllowCanonicalTasks;
        return copyNodes(projectId, request, agentContext, taskPolicy, cancellationToken);
    }
}

internal delegate Task<ProjectStructureNodesCopyResult> ProjectStructureAgentNodeCopyOperation(
    Guid projectId,
    ProjectStructureNodesCopyInput request,
    ProjectStructureAgentContext agentContext,
    ProjectStructureClipboardCopyTaskPolicy taskPolicy,
    CancellationToken cancellationToken);
