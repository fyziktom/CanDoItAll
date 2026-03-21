namespace CanDoItAll.Mcp.DotNetWatch.Runtime;

public sealed class WorkspaceExecutionLock
{
    private const string WorkspaceResourceKey = "workspace";
    private readonly ResourceMutationGate _gate;

    public WorkspaceExecutionLock()
        : this(new ResourceMutationGate())
    {
    }

    public WorkspaceExecutionLock(ResourceMutationGate gate)
    {
        _gate = gate;
    }

    public async Task<MutationLease> AcquireMutationAsync(string reason, CancellationToken cancellationToken)
    {
        var lease = await _gate.AcquireAsync(WorkspaceResourceKey, reason, cancellationToken);
        return new MutationLease(lease);
    }

    public string? GetCurrentHolder()
    {
        return _gate.GetCurrentHolder(WorkspaceResourceKey);
    }

    public sealed class MutationLease(ResourceMutationGate.MutationLease innerLease) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return ValueTask.CompletedTask;
            }

            return innerLease.DisposeAsync();
        }
    }
}
