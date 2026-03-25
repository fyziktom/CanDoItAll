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
        var lease = await _gate.AcquireAsync(WorkspaceResourceKey, reason, cancellationToken, busyCode: "OperationInProgress");
        return new MutationLease([lease]);
    }

    public async Task<MutationLease> AcquireMutationAsync(string reason, IReadOnlyList<string> resourceKeys, CancellationToken cancellationToken)
    {
        var normalizedResourceKeys = (resourceKeys.Count == 0 ? [WorkspaceResourceKey] : resourceKeys)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<ResourceMutationGate.MutationLease> leases = [];
        try
        {
            foreach (var resourceKey in normalizedResourceKeys)
            {
                leases.Add(await _gate.AcquireAsync(resourceKey, reason, cancellationToken, busyCode: "ResourceConflict"));
            }

            return new MutationLease(leases);
        }
        catch
        {
            foreach (var lease in leases.AsEnumerable().Reverse())
            {
                await lease.DisposeAsync();
            }

            throw;
        }
    }

    public string? GetCurrentHolder()
    {
        return _gate.GetCurrentHolder(WorkspaceResourceKey);
    }

    public string? GetCurrentHolder(string resourceKey)
    {
        return _gate.GetCurrentHolder(resourceKey);
    }

    public sealed class MutationLease(IReadOnlyList<ResourceMutationGate.MutationLease> innerLeases) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return ValueTask.CompletedTask;
            }

            foreach (var lease in innerLeases.Reverse())
            {
                lease.DisposeAsync().GetAwaiter().GetResult();
            }

            return ValueTask.CompletedTask;
        }
    }
}
