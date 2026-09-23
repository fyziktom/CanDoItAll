using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentRuntimeCapabilityPolicyContributor {
    IReadOnlyList<CapabilityAccessPolicy>? Contribute(AgentRuntimeContextIntent contextIntent, AgentToolPolicyCatalog toolPolicies);
}
