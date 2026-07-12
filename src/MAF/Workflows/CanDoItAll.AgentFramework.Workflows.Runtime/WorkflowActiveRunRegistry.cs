using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum WorkflowActiveRunCancellationSignal
{
    Signalled,
    NotActive,
    NotSupported
}

public interface IWorkflowActiveRunRegistry
{
    bool TryRegister(
        WorkflowRunId runId,
        bool supportsCancellation,
        CancellationToken callerCancellationToken,
        out IWorkflowActiveRunLease lease);

    WorkflowActiveRunCancellationSignal TrySignalCancellation(WorkflowRunId runId);
}

public interface IWorkflowActiveRunLease : IDisposable
{
    CancellationToken Token { get; }

    bool IsCancellationRequested { get; }

    bool TryClaimCompletion();
}

public sealed class WorkflowActiveRunRegistry : IWorkflowActiveRunRegistry
{
    private readonly ConcurrentDictionary<WorkflowRunId, WorkflowActiveRunLease> activeRuns = new();

    public bool TryRegister(
        WorkflowRunId runId,
        bool supportsCancellation,
        CancellationToken callerCancellationToken,
        out IWorkflowActiveRunLease lease)
    {
        var candidate = new WorkflowActiveRunLease(
            Release,
            runId,
            supportsCancellation,
            CancellationTokenSource.CreateLinkedTokenSource(callerCancellationToken));
        if (activeRuns.TryAdd(runId, candidate))
        {
            lease = candidate;
            return true;
        }

        candidate.DisposeCancellationSource();
        lease = null!;
        return false;
    }

    public WorkflowActiveRunCancellationSignal TrySignalCancellation(WorkflowRunId runId)
    {
        if (!activeRuns.TryGetValue(runId, out var lease))
        {
            return WorkflowActiveRunCancellationSignal.NotActive;
        }

        return lease.TrySignalCancellation();
    }

    internal void Release(WorkflowActiveRunLease lease)
    {
        activeRuns.TryRemove(new KeyValuePair<WorkflowRunId, WorkflowActiveRunLease>(lease.RunId, lease));
        lease.DisposeCancellationSource();
    }
}

internal sealed class WorkflowActiveRunLease : IWorkflowActiveRunLease
{
    private readonly object sync = new();
    private readonly Action<WorkflowActiveRunLease> release;
    private readonly CancellationTokenSource cancellationSource;
    private readonly bool supportsCancellation;
    private bool cancellationRequested;
    private bool completionClaimed;
    private bool disposed;

    internal WorkflowActiveRunLease(
        Action<WorkflowActiveRunLease> release,
        WorkflowRunId runId,
        bool supportsCancellation,
        CancellationTokenSource cancellationSource)
    {
        this.release = release;
        RunId = runId;
        this.supportsCancellation = supportsCancellation;
        this.cancellationSource = cancellationSource;
    }

    public WorkflowRunId RunId { get; }

    public CancellationToken Token => cancellationSource.Token;

    public bool IsCancellationRequested
    {
        get
        {
            lock (sync)
            {
                return cancellationRequested || cancellationSource.IsCancellationRequested;
            }
        }
    }

    public bool TryClaimCompletion()
    {
        lock (sync)
        {
            if (disposed || cancellationRequested || cancellationSource.IsCancellationRequested || completionClaimed)
            {
                return false;
            }

            completionClaimed = true;
            return true;
        }
    }

    internal WorkflowActiveRunCancellationSignal TrySignalCancellation()
    {
        lock (sync)
        {
            if (disposed)
            {
                return WorkflowActiveRunCancellationSignal.NotActive;
            }

            if (!supportsCancellation)
            {
                return WorkflowActiveRunCancellationSignal.NotSupported;
            }

            if (completionClaimed)
            {
                return WorkflowActiveRunCancellationSignal.NotActive;
            }

            cancellationRequested = true;
            cancellationSource.Cancel();
            return WorkflowActiveRunCancellationSignal.Signalled;
        }
    }

    public void Dispose() => release(this);

    internal void DisposeCancellationSource()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            cancellationSource.Dispose();
        }
    }
}
