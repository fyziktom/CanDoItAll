using CanDoItAll.Modules.Memory.Services;
namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderUiSurfaceComponentRegistry(
    IEnumerable<MemoryProviderUiSurfaceComponentRegistration> registrations) : IMemoryProviderUiSurfaceComponentRegistry
{
    private readonly IReadOnlyDictionary<string, Type> components = registrations
        .GroupBy(registration => registration.ComponentKey, StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => group.Last().ComponentType,
            StringComparer.Ordinal);

    public bool TryResolve(string componentKey, out Type componentType) =>
        components.TryGetValue(componentKey, out componentType!);
}
