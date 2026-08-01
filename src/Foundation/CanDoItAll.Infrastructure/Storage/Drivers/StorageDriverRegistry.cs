namespace CanDoItAll.Infrastructure.Storage;

public sealed class StorageDriverRegistry(IEnumerable<IStorageDriver> drivers) : IStorageDriverRegistry
{
    private readonly IReadOnlyDictionary<StorageProviderKind, IStorageDriver> _drivers = drivers
        .GroupBy(driver => driver.ProviderKind)
        .ToDictionary(group => group.Key, group => group.Last());

    public IReadOnlyCollection<StorageProviderKind> RegisteredKinds => _drivers.Keys.OrderBy(kind => kind).ToArray();

    public bool TryResolve(StorageProviderKind providerKind, out IStorageDriver driver)
    {
        return _drivers.TryGetValue(providerKind, out driver!);
    }

    public IStorageDriver Resolve(StorageProviderKind providerKind)
    {
        return _drivers.TryGetValue(providerKind, out var driver)
            ? driver
            : throw new InvalidOperationException($"No storage driver is registered for provider '{providerKind}'.");
    }
}
