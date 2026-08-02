using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

[Flags]
public enum ProviderInvocationFeatures
{
    None = 0,
    FunctionTools = 1 << 0
}

public enum ProviderModelParameterAdjustment
{
    None = 0,
    ReasoningDisabledForFunctionTools = 1
}

public sealed record ProviderReasoningEffortResolution(
    AgentReasoningEffortLevel? RequestedEffort,
    AgentReasoningEffortLevel? EffectiveEffort,
    ProviderModelParameterAdjustment Adjustment);

public static class OpenAiRequestCompatibilityPolicy
{
    public static ProviderReasoningEffortResolution ResolveReasoningEffort(
        ProviderKind providerKind,
        ProviderTransportKind transport,
        string model,
        ProviderInvocationFeatures features,
        AgentReasoningEffortLevel? requestedEffort)
    {
        if (!RequiresExplicitReasoningNone(providerKind, transport, model, features))
        {
            return new ProviderReasoningEffortResolution(
                requestedEffort,
                requestedEffort,
                ProviderModelParameterAdjustment.None);
        }

        return new ProviderReasoningEffortResolution(
            requestedEffort,
            AgentReasoningEffortLevel.None,
            requestedEffort == AgentReasoningEffortLevel.None
                ? ProviderModelParameterAdjustment.None
                : ProviderModelParameterAdjustment.ReasoningDisabledForFunctionTools);
    }

    public static bool RequiresExplicitReasoningNone(
        ProviderKind providerKind,
        ProviderTransportKind transport,
        string model,
        ProviderInvocationFeatures features)
    {
        return providerKind == ProviderKind.OpenAi &&
               transport == ProviderTransportKind.ChatCompletions &&
               (features & ProviderInvocationFeatures.FunctionTools) != 0 &&
               string.Equals(
                   model.Trim(),
                   OpenAiModelIds.Gpt56Terra,
                   StringComparison.OrdinalIgnoreCase);
    }
}
