using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public interface IMemoryProviderRegistry
{
    IReadOnlyList<MemoryProviderProfile> Providers { get; }

    IReadOnlyList<MemoryProviderProfile> GetEnabledProviders();

    IReadOnlyList<MemoryProviderProfile> GetProvidersForCapability(MemoryCapabilityId capability);

    MemoryProviderSelectionResult SelectProvider(
        MemoryProviderSelectionPolicy policy,
        MemoryProviderSelectionContext context);
}
