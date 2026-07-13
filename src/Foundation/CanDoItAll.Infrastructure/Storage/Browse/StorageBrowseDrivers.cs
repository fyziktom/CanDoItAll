namespace CanDoItAll.Infrastructure.Storage;

public interface IStorageBrowseDriver
{
    StorageProviderKind ProviderKind { get; }

    StorageBrowseCapability Capabilities { get; }

    StorageBrowseWorkBudget MaximumBudget { get; }

    Task<StorageBrowsePage> BrowseAsync(
        StorageCatalogRecord storage,
        StorageBrowseRequest request,
        CancellationToken cancellationToken = default);
}

public interface IStorageBrowseSearchDriver
{
    StorageBrowseSearchBudget MaximumSearchBudget { get; }

    Task<StorageBrowsePage> SearchAsync(
        StorageCatalogRecord storage,
        StorageBrowseSearchRequest request,
        CancellationToken cancellationToken = default);
}

public interface IStorageBrowseStatDriver
{
    Task<StorageBrowseEntry> StatAsync(
        StorageCatalogRecord storage,
        StorageBrowseStatRequest request,
        CancellationToken cancellationToken = default);
}

public interface IStorageBrowseDriverRegistry
{
    IReadOnlyCollection<StorageProviderKind> RegisteredKinds { get; }

    bool TryResolve(StorageProviderKind providerKind, out IStorageBrowseDriver driver);

    IStorageBrowseDriver Resolve(StorageProviderKind providerKind);

    IStorageBrowseSearchDriver ResolveSearch(StorageProviderKind providerKind);

    IStorageBrowseStatDriver ResolveStat(StorageProviderKind providerKind);
}

public sealed class StorageBrowseDriverRegistry : IStorageBrowseDriverRegistry
{
    private readonly IReadOnlyDictionary<StorageProviderKind, IStorageBrowseDriver> _drivers;
    private readonly IReadOnlyCollection<StorageProviderKind> _registeredKinds;

    public StorageBrowseDriverRegistry(IEnumerable<IStorageBrowseDriver> drivers)
    {
        ArgumentNullException.ThrowIfNull(drivers);
        var driversByKind = new Dictionary<StorageProviderKind, IStorageBrowseDriver>();
        foreach (IStorageBrowseDriver driver in drivers)
        {
            ArgumentNullException.ThrowIfNull(driver);
            ValidateDriver(driver);
            if (!driversByKind.TryAdd(driver.ProviderKind, driver))
            {
                throw new StorageBrowseException(new StorageBrowseError(
                    StorageBrowseErrorCode.DuplicateProviderRegistration,
                    $"More than one storage browse driver is registered for provider '{driver.ProviderKind}'."));
            }
        }

        _drivers = driversByKind;
        _registeredKinds = driversByKind.Keys.OrderBy(kind => kind).ToArray();
    }

    public IReadOnlyCollection<StorageProviderKind> RegisteredKinds => _registeredKinds;

    public bool TryResolve(StorageProviderKind providerKind, out IStorageBrowseDriver driver)
        => _drivers.TryGetValue(providerKind, out driver!);

    public IStorageBrowseDriver Resolve(StorageProviderKind providerKind)
        => _drivers.TryGetValue(providerKind, out IStorageBrowseDriver? driver)
            ? driver
            : throw ProviderNotRegistered(providerKind);

    public IStorageBrowseSearchDriver ResolveSearch(StorageProviderKind providerKind)
    {
        IStorageBrowseDriver driver = Resolve(providerKind);
        return driver.Capabilities.HasFlag(StorageBrowseCapability.Search) &&
               driver is IStorageBrowseSearchDriver searchDriver
            ? searchDriver
            : throw Unsupported(providerKind, "search");
    }

    public IStorageBrowseStatDriver ResolveStat(StorageProviderKind providerKind)
    {
        IStorageBrowseDriver driver = Resolve(providerKind);
        return driver.Capabilities.HasFlag(StorageBrowseCapability.Stat) &&
               driver is IStorageBrowseStatDriver statDriver
            ? statDriver
            : throw Unsupported(providerKind, "stat");
    }

    private static void ValidateDriver(IStorageBrowseDriver driver)
    {
        if (!Enum.IsDefined(driver.ProviderKind))
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidConfiguration,
                "A storage browse driver has an invalid provider kind."));
        }

        const StorageBrowseCapability supportedCapabilities =
            StorageBrowseCapability.Browse |
            StorageBrowseCapability.Stat |
            StorageBrowseCapability.Search |
            StorageBrowseCapability.ProviderNativeOrdering |
            StorageBrowseCapability.GlobalNameOrdering |
            StorageBrowseCapability.ConsistentContinuation |
            StorageBrowseCapability.Metadata |
            StorageBrowseCapability.ImmutableVersion;
        if ((driver.Capabilities & ~supportedCapabilities) != StorageBrowseCapability.None)
        {
            throw CapabilityMismatch(driver.ProviderKind, "unknown");
        }

        if (!driver.Capabilities.HasFlag(StorageBrowseCapability.Browse))
        {
            throw CapabilityMismatch(driver.ProviderKind, "browse");
        }

        if (!driver.Capabilities.HasFlag(StorageBrowseCapability.ProviderNativeOrdering) &&
            !driver.Capabilities.HasFlag(StorageBrowseCapability.GlobalNameOrdering))
        {
            throw CapabilityMismatch(driver.ProviderKind, "ordering");
        }

        ArgumentNullException.ThrowIfNull(driver.MaximumBudget);

        bool advertisesSearch = driver.Capabilities.HasFlag(StorageBrowseCapability.Search);
        if (advertisesSearch != (driver is IStorageBrowseSearchDriver))
        {
            throw CapabilityMismatch(driver.ProviderKind, "search");
        }

        if (driver is IStorageBrowseSearchDriver searchDriver)
        {
            ArgumentNullException.ThrowIfNull(searchDriver.MaximumSearchBudget);
        }

        bool advertisesStat = driver.Capabilities.HasFlag(StorageBrowseCapability.Stat);
        if (advertisesStat != (driver is IStorageBrowseStatDriver))
        {
            throw CapabilityMismatch(driver.ProviderKind, "stat");
        }
    }

    private static StorageBrowseException ProviderNotRegistered(StorageProviderKind providerKind)
        => new(new StorageBrowseError(
            StorageBrowseErrorCode.ProviderNotRegistered,
            $"No storage browse driver is registered for provider '{providerKind}'."));

    private static StorageBrowseException Unsupported(StorageProviderKind providerKind, string operation)
        => new(new StorageBrowseError(
            StorageBrowseErrorCode.UnsupportedOperation,
            $"Storage provider '{providerKind}' does not support the requested {operation} operation."));

    private static StorageBrowseException CapabilityMismatch(
        StorageProviderKind providerKind,
        string operation)
        => new(new StorageBrowseError(
            StorageBrowseErrorCode.InvalidConfiguration,
            $"Storage provider '{providerKind}' has inconsistent {operation} capabilities."));
}
