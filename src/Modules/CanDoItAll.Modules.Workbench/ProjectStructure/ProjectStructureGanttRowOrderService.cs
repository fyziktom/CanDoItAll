using CanDoItAll.Modules.Workbench.CanvasAdapters;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureGanttRowOrderService(
    ProjectStructureLeaseService leaseService,
    ProjectWorkbenchService projectWorkbenchService)
{
    private const int MutationGateCount = 256;
    private static readonly SemaphoreSlim[] ProjectMutationGates = Enumerable
        .Range(0, MutationGateCount)
        .Select(static _ => new SemaphoreSlim(1, 1))
        .ToArray();

    public Task<ProjectStructureGanttViewState> InsertAsync(
        Guid projectId,
        string taskNodeId,
        string? afterTaskNodeId,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return RunProjectMutationAsync(
            projectId,
            agent,
            "insert-gantt-task-row",
            token => InsertWithinProjectMutationAsync(
                projectId,
                taskNodeId,
                afterTaskNodeId,
                token),
            cancellationToken);
    }

    public Task<ProjectStructureGanttViewState> MoveAsync(
        Guid projectId,
        ProjectStructureGanttRowMoveRequest request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(agent);
        return RunProjectMutationAsync(
            projectId,
            agent,
            "move-gantt-task-row",
            token => projectWorkbenchService.MoveGanttTaskInRowOrderAsync(
                projectId,
                request,
                token),
            cancellationToken);
    }

    internal async Task<T> RunProjectMutationAsync<T>(
        Guid projectId,
        ProjectStructureAgentContext agent,
        string reason,
        Func<CancellationToken, Task<T>> mutation,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "ProjectIdRequired", "A project id is required.");
        }

        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(mutation);

        var gate = ResolveMutationGate(projectId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await leaseService.RunWithProjectMutationLeaseAsync(
                projectId,
                leaseToken: null,
                agent,
                reason,
                mutation,
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    internal Task<ProjectStructureGanttViewState> InsertWithinProjectMutationAsync(
        Guid projectId,
        string taskNodeId,
        string? afterTaskNodeId,
        CancellationToken cancellationToken)
        => projectWorkbenchService.InsertGanttTaskIntoRowOrderAsync(
            projectId,
            taskNodeId,
            afterTaskNodeId,
            cancellationToken);

    private static SemaphoreSlim ResolveMutationGate(Guid projectId)
    {
        var index = unchecked((uint)projectId.GetHashCode()) % MutationGateCount;
        return ProjectMutationGates[index];
    }
}
