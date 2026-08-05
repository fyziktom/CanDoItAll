using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafProviderRequestCompatibilityEvidenceFactory
{
    public static ProviderRequestCompatibilityEvidence Create(
        ProviderProfile provider,
        string requestedModel,
        string effectiveModel,
        IReadOnlyList<AITool> tools,
        AgentReasoningEffortLevel? requestedEffort)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(tools);

        var features = tools.Any(static tool => tool is AIFunctionDeclaration)
            ? ProviderInvocationFeatures.FunctionTools
            : ProviderInvocationFeatures.None;
        var evidence = OpenAiRequestCompatibilityPolicy.Evaluate(
            provider.Kind,
            provider.Id,
            provider.Transport,
            effectiveModel,
            features,
            requestedEffort);
        return evidence with
        {
            RequestedModel = requestedModel.Trim()
        };
    }

    public static string? CreateAdjustmentProgressMessage(
        ProviderRequestCompatibilityEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (evidence.Disposition != ProviderRequestCompatibilityDisposition.Adjusted)
        {
            return null;
        }

        return $"Adjusted {evidence.Transport} request for model '{evidence.EffectiveModel}' before provider dispatch. " +
               $"Reasoning effort: {FormatEffort(evidence.RequestedEffort)} -> {FormatEffort(evidence.EffectiveEffort)}. " +
               $"Compatibility code: {evidence.Adjustment}.";
    }

    private static string FormatEffort(AgentReasoningEffortLevel? effort)
    {
        return effort?.ToString() ?? "provider-default";
    }
}
