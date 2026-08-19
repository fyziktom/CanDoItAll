using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public interface IProviderModelCapabilityResolver
{
    ProviderModelThinkingEffortCapability ResolveThinkingEffort(
        ProviderProfile provider,
        string model);

    AgentReasoningEffortLevel? ResolveProviderDefaultThinkingEffort(
        ProviderProfile provider,
        string model);
}

public sealed class ProviderModelCapabilityResolver : IProviderModelCapabilityResolver
{
    public ProviderModelThinkingEffortCapability ResolveThinkingEffort(
        ProviderProfile provider,
        string model)
        => AgentThinkingEffortPolicy.ResolveCapability(provider, model);

    public AgentReasoningEffortLevel? ResolveProviderDefaultThinkingEffort(
        ProviderProfile provider,
        string model)
        => AgentThinkingEffortPolicy.ResolveProviderDefault(provider, model);
}
