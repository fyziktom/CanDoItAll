using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal static class MemoryProviderSelectionRules
{
    public static bool IsProviderAllowed(
        MemoryProviderSelectionPolicy policy,
        MemoryProviderInstanceId providerId) =>
        policy.AllowedProviderIds.Count == 0 || policy.AllowedProviderIds.Contains(providerId);

    public static bool SupportsCapability(
        MemoryProviderProfile provider,
        MemoryCapabilityId capability) =>
        provider.Manifest.Capabilities.Any(candidate => candidate.Supported && candidate.Id == capability);

    public static bool SupportsWorkspaceScope(MemoryProviderProfile provider) =>
        provider.WorkspaceScope == MemoryProviderWorkspaceScope.AllWorkspaces;
}
