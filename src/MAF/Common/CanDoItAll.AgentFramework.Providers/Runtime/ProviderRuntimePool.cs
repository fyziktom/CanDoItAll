namespace CanDoItAll.AgentFramework.Providers;

public sealed class ProviderRuntimePool(
    IProviderRuntimeDescriptorSource descriptorSource,
    IProviderRuntimeHandleFactory handleFactory) : IProviderRuntimePool
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<Guid, ProviderRuntimePoolEntry> entriesByProviderId = [];
    private bool isDisposed;

    public async ValueTask<IProviderRuntimeHandle> GetRequiredAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (providerProfileId == Guid.Empty)
        {
            throw new ArgumentException("Provider profile id is required.", nameof(providerProfileId));
        }

        var descriptor = await descriptorSource.GetRequiredAsync(providerProfileId, cancellationToken).ConfigureAwait(false);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();

            if (entriesByProviderId.TryGetValue(providerProfileId, out var existing) &&
                existing.Handle.Key == descriptor.Key &&
                !existing.Handle.IsDisposed)
            {
                return existing.Handle;
            }

            var replacement = handleFactory.Create(descriptor);
            entriesByProviderId[providerProfileId] = new ProviderRuntimePoolEntry(descriptor.Key, replacement);
            if (existing is not null)
            {
                await existing.Handle.DisposeAsync().ConfigureAwait(false);
            }

            return replacement;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask InvalidateAsync(
        Guid providerProfileId,
        ProviderRuntimeInvalidationReason reason,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (entriesByProviderId.Remove(providerProfileId, out var entry))
            {
                await entry.Handle.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var entry in entriesByProviderId.Values)
            {
                await entry.Handle.DisposeAsync().ConfigureAwait(false);
            }

            entriesByProviderId.Clear();
        }
        finally
        {
            gate.Release();
            gate.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(ProviderRuntimePool));
        }
    }

    private sealed record ProviderRuntimePoolEntry(
        ProviderRuntimeKey Key,
        IProviderRuntimeHandle Handle);
}
