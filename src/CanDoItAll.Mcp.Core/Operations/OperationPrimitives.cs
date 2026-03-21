using CanDoItAll.Mcp.Core.Observability;

namespace CanDoItAll.Mcp.Core.Operations;

public enum AsyncOperationState
{
    Queued,
    Running,
    Succeeded,
    Failed,
    TimedOut,
    Cancelled
}

public abstract class OperationRecordBase
{
    protected OperationRecordBase(string operationId, string correlationId, RingLogBuffer logBuffer, string? target = null, string? kind = null)
    {
        OperationId = operationId;
        CorrelationId = correlationId;
        LogBuffer = logBuffer;
        Target = target;
        Kind = kind;
        StartedUtc = DateTimeOffset.UtcNow;
    }

    public string OperationId { get; }

    public string CorrelationId { get; }

    public string? Target { get; }

    public string? Kind { get; }

    public RingLogBuffer LogBuffer { get; }

    public AsyncOperationState State { get; protected set; } = AsyncOperationState.Queued;

    public string Summary { get; protected set; } = "Queued.";

    public DateTimeOffset StartedUtc { get; }

    public DateTimeOffset? FinishedUtc { get; protected set; }

    public long ElapsedMs => (long)((FinishedUtc ?? DateTimeOffset.UtcNow) - StartedUtc).TotalMilliseconds;

    protected void MarkState(AsyncOperationState state, string summary, bool markFinished = false)
    {
        State = state;
        Summary = summary;
        if (markFinished)
        {
            FinishedUtc = DateTimeOffset.UtcNow;
        }
    }
}

public sealed class OperationRegistry<TRecord> where TRecord : OperationRecordBase
{
    private readonly object _gate = new();
    private readonly Dictionary<string, TRecord> _operations = new(StringComparer.OrdinalIgnoreCase);

    public void Add(TRecord operation)
    {
        lock (_gate)
        {
            _operations[operation.OperationId] = operation;
        }
    }

    public TRecord? GetById(string? operationId)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(operationId))
            {
                return _operations.Values.OrderByDescending(static operation => operation.StartedUtc).FirstOrDefault();
            }

            _operations.TryGetValue(operationId, out var operation);
            return operation;
        }
    }

    public IReadOnlyList<TRecord> GetActiveOperations()
    {
        lock (_gate)
        {
            return _operations.Values
                .Where(operation => operation.State is AsyncOperationState.Queued or AsyncOperationState.Running)
                .OrderByDescending(static operation => operation.StartedUtc)
                .ToList();
        }
    }

    public IReadOnlyList<TRecord> GetAllOperations()
    {
        lock (_gate)
        {
            return _operations.Values
                .OrderByDescending(static operation => operation.StartedUtc)
                .ToList();
        }
    }

    public TRecord? GetLastFailed()
    {
        lock (_gate)
        {
            return _operations.Values
                .Where(operation => operation.State is AsyncOperationState.Failed or AsyncOperationState.TimedOut or AsyncOperationState.Cancelled)
                .OrderByDescending(static operation => operation.StartedUtc)
                .FirstOrDefault();
        }
    }
}

public sealed record WaitOutcome<TSnapshot>(TSnapshot Snapshot, bool Completed, bool TimedOut, long ElapsedMs);

public sealed class OperationWaitEngine
{
    public async Task<WaitOutcome<TSnapshot>> WaitAsync<TSnapshot>(
        Func<CancellationToken, Task<TSnapshot>> snapshotFactory,
        Func<TSnapshot, bool> completionPredicate,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var deadline = startedAt.Add(timeout);
        TSnapshot? lastSnapshot = default;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lastSnapshot = await snapshotFactory(cancellationToken);
            if (completionPredicate(lastSnapshot))
            {
                return new WaitOutcome<TSnapshot>(lastSnapshot, true, false, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        lastSnapshot ??= await snapshotFactory(cancellationToken);
        return new WaitOutcome<TSnapshot>(lastSnapshot, false, true, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
    }
}
