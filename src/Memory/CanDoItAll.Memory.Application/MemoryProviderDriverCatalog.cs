using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal sealed class MemoryProviderDriverCatalog<TDriver>
    where TDriver : class
{
    private readonly TDriver[] drivers;
    private readonly Func<TDriver, MemoryProviderDriverKind> kindSelector;

    public MemoryProviderDriverCatalog(
        IEnumerable<TDriver> drivers,
        Func<TDriver, MemoryProviderDriverKind> kindSelector)
    {
        ArgumentNullException.ThrowIfNull(drivers);
        ArgumentNullException.ThrowIfNull(kindSelector);
        this.drivers = drivers.ToArray();
        this.kindSelector = kindSelector;
    }

    public TDriver? ResolveUnique(
        MemoryProviderDriverKind driverKind,
        out string failure)
    {
        var matches = drivers
            .Where(driver => kindSelector(driver) == driverKind)
            .Take(2)
            .ToArray();
        if (matches.Length == 1)
        {
            failure = string.Empty;
            return matches[0];
        }

        failure = matches.Length == 0
            ? $"No '{typeof(TDriver).Name}' is registered for '{driverKind}'."
            : $"Multiple '{typeof(TDriver).Name}' registrations exist for '{driverKind}'; dispatch is not allowed.";
        return null;
    }
}
