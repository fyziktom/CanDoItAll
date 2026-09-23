using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.AgentFramework.Tooling;

public enum AgentRuntimeToolAttachmentPhase {
    RuntimeProviders = 0,
    ConfiguredWorkspace = 1
}

public sealed record AgentRuntimeConfiguredWorkspacePolicy(
    IReadOnlyList<CapabilityExposureDescriptor> Capabilities,
    IReadOnlyList<CapabilityAccessPolicy> AccessPolicies);
