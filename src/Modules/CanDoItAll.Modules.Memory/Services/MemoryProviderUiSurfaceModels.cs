using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.Memory.Services;

public enum MemoryProviderUiSurfaceAvailability
{
    Available = 0,
    ProviderUnavailable = 1,
    CapabilityUnavailable = 2,
    MissingComponentRegistration = 3,
    MissingUrl = 4,
    InvalidUrl = 5,
    UnsupportedKind = 6
}

public sealed record MemoryProviderUiSurfaceProjection(
    string SurfaceId,
    MemoryProviderUiSurfaceKind Kind,
    string Name,
    string? ComponentKey,
    string? Url,
    MemoryCapabilityId RequiredCapability,
    MemoryProviderUiSurfaceAvailability Availability,
    string Diagnostic,
    Type? ComponentType)
{
    public bool CanRender => Availability == MemoryProviderUiSurfaceAvailability.Available;
}

public sealed record MemoryProviderUiSurfaceComponentRegistration(
    string ComponentKey,
    Type ComponentType);

public static class MemoryProviderUiSurfaceKeys
{
    public const string MockProviderPanelComponent = "memory.mock.panel";
    public const string ProviderVendorUiUrlExtension = "provider.vendor.uiUrl";
}

public interface IMemoryProviderUiSurfaceComponentRegistry
{
    bool TryResolve(string componentKey, out Type componentType);
}

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
