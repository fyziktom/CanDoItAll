using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public static class OllamaThinkingEffortAdapter
{
    public static object ToNativeValue(
        ProviderModelThinkingEffortCapability capability,
        AgentReasoningEffortLevel effort)
    {
        ArgumentNullException.ThrowIfNull(capability);
        capability = AgentThinkingEffortPolicy.NormalizeCapability(capability);

        if (capability.Status != AgentThinkingEffortSupportStatus.Supported ||
            !capability.AllowedEfforts.Contains(effort))
        {
            throw new InvalidOperationException(
                $"Ollama model '{capability.Model}' does not support thinking effort '{AgentThinkingEffortPolicy.FormatEffort(effort)}'.");
        }

        return capability.ControlMode switch
        {
            AgentThinkingEffortControlMode.BooleanToggle =>
                effort != AgentReasoningEffortLevel.None,
            AgentThinkingEffortControlMode.EffortLevels =>
                AgentThinkingEffortPolicy.FormatEffort(effort),
            _ => throw new InvalidOperationException(
                $"Ollama model '{capability.Model}' has no defined thinking-effort control mode.")
        };
    }
}
