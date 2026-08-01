using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum WorkflowNodeExecutionProgressState
{
    Started,
    Completed,
    Failed
}

public sealed record WorkflowNodeExecutionProgress(
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    WorkflowRunId? RunId,
    WorkflowNodeId NodeId,
    WorkflowNodeExecutionProgressState State,
    DateTimeOffset OccurredAtUtc)
{
    public WorkflowExecutorId? ExecutorId { get; init; }

    public string PayloadJson { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public WorkflowUsageMetrics? Usage { get; init; }

    public IReadOnlyList<WorkflowUsageObservation> UsageObservations { get; init; } = [];
}

public interface IWorkflowNodeExecutionProgressObserver
{
    ValueTask RecordAsync(
        WorkflowNodeExecutionProgress progress,
        CancellationToken cancellationToken = default);
}

public sealed class WorkflowNodeExecutionProgressScope : IDisposable
{
    private static readonly AsyncLocal<IWorkflowNodeExecutionProgressObserver?> CurrentObserver = new();
    private readonly IWorkflowNodeExecutionProgressObserver? previousObserver;

    private WorkflowNodeExecutionProgressScope(IWorkflowNodeExecutionProgressObserver observer)
    {
        previousObserver = CurrentObserver.Value;
        CurrentObserver.Value = observer;
    }

    public static IWorkflowNodeExecutionProgressObserver? Current => CurrentObserver.Value;

    public static WorkflowNodeExecutionProgressScope Push(IWorkflowNodeExecutionProgressObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        return new WorkflowNodeExecutionProgressScope(observer);
    }

    public void Dispose()
    {
        CurrentObserver.Value = previousObserver;
    }
}
