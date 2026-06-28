using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public interface IProviderDispatchLaneGate
{
    ValueTask<IAsyncDisposable> EnterAsync(
        ProviderDispatchQuery query,
        string? subdriverKind = null,
        CancellationToken cancellationToken = default);
}

public sealed class ProviderDispatchLaneGate(IAgentProviderFactory providerFactory) : IProviderDispatchLaneGate
{
    private readonly ConcurrentDictionary<ProviderDispatchKey, SemaphoreSlim> gates = new();

    public async ValueTask<IAsyncDisposable> EnterAsync(
        ProviderDispatchQuery query,
        string? subdriverKind = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gate = gates.GetOrAdd(CreateKey(query, subdriverKind), _ => CreateGate(query));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(gate);
    }

    private static ProviderDispatchKey CreateKey(
        ProviderDispatchQuery query,
        string? subdriverKind)
    {
        return ProviderDispatchKey.FromQuery(
            query,
            string.IsNullOrWhiteSpace(subdriverKind)
                ? query.Capability.ToString()
                : subdriverKind);
    }

    private SemaphoreSlim CreateGate(ProviderDispatchQuery query)
    {
        var limits = providerFactory.GetDispatchLimits(query);
        var parallelism = limits.SupportsBatching
            ? Math.Max(1, limits.MaxBatchSize * limits.MaxInFlightBatches)
            : Math.Max(1, limits.MaxInFlightBatches);
        return new SemaphoreSlim(parallelism, parallelism);
    }

    private sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
    {
        private int disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                gate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
