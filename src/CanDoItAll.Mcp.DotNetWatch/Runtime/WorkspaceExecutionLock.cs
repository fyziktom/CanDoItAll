namespace CanDoItAll.Mcp.DotNetWatch.Runtime;

public sealed class WorkspaceExecutionLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly object _gate = new();
    private string? _currentHolder;

    public async Task<MutationLease> AcquireMutationAsync(string reason, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        lock (_gate)
        {
            _currentHolder = reason;
        }

        return new MutationLease(this);
    }

    public string? GetCurrentHolder()
    {
        lock (_gate)
        {
            return _currentHolder;
        }
    }

    private ValueTask ReleaseAsync()
    {
        lock (_gate)
        {
            _currentHolder = null;
        }

        _semaphore.Release();
        return ValueTask.CompletedTask;
    }

    public sealed class MutationLease(WorkspaceExecutionLock owner) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return ValueTask.CompletedTask;
            }

            return owner.ReleaseAsync();
        }
    }
}
