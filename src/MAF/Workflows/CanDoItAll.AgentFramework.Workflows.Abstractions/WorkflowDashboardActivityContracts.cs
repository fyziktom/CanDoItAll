using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public enum WorkflowDashboardActivityMode
{
    Active,
    RecentFallback
}

public static class WorkflowRunActivityPolicy
{
    public const WorkflowRunState RunningState = WorkflowRunState.Running;
    public const WorkflowRunState WaitingForInputState = WorkflowRunState.WaitingForInput;

    public static bool IsActive(WorkflowRunState state)
        => state is RunningState or WaitingForInputState;
}

public sealed record WorkflowDashboardActivityQuery
{
    public const int DefaultTake = 5;
    public const int MaximumTake = 5;

    public WorkflowDashboardActivityQuery(int take = DefaultTake)
    {
        if (take is < 1 or > MaximumTake)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                take,
                $"Workflow dashboard activity take must be between 1 and {MaximumTake}.");
        }

        Take = take;
    }

    public int Take { get; }
}

public sealed record WorkflowDashboardActivityStoreResult(
    WorkflowDashboardActivityMode Mode,
    IReadOnlyList<WorkflowDashboardActivityRun> Runs);

public sealed record WorkflowDashboardActivityRun(
    WorkflowRunId RunId,
    WorkflowId WorkflowId,
    WorkflowRunState State,
    string Summary,
    DateTimeOffset UpdatedAtUtc);

public interface IWorkflowDashboardActivityStore
{
    Task<WorkflowDashboardActivityStoreResult> QueryActivityAsync(
        WorkflowDashboardActivityQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowDashboardActivityItem(
    WorkflowDashboardActivityRun Run,
    string WorkflowName);

public sealed record WorkflowDashboardActivityResult(
    WorkflowDashboardActivityMode Mode,
    IReadOnlyList<WorkflowDashboardActivityItem> Items);

public interface IWorkflowDashboardActivityQueryService
{
    Task<WorkflowDashboardActivityResult> QueryAsync(
        WorkflowDashboardActivityQuery query,
        CancellationToken cancellationToken = default);
}
