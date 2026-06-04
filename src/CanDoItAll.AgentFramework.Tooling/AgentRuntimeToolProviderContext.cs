using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Tooling;

public sealed record AgentRuntimeToolProviderContext(
    AgentDefinition Agent,
    ProviderProfile Provider,
    IReadOnlyList<CapabilityCatalogItem> Capabilities,
    bool SuppressApprovalRequirements,
    AgentRuntimeToolProviderPurpose Purpose,
    string RuntimeSessionKey,
    IReadOnlyDictionary<string, string> Tags);
