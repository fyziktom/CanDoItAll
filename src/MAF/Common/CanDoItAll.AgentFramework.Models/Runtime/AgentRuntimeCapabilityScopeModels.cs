using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentRuntimeCapabilityScopeOverride(
    IReadOnlyList<CapabilityAccessPolicy> Policies,
    IReadOnlyList<CapabilityIdentity> RequiredCapabilities)
{
    public static AgentRuntimeCapabilityScopeOverride Empty { get; } = new([], []);

    public bool IsEmpty => (Policies?.Count ?? 0) == 0 && (RequiredCapabilities?.Count ?? 0) == 0;
}
