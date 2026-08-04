using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public static class OpenAiRequestCompatibilityPolicy
{
    private static readonly OpenAiRequestConstraint[] Constraints =
    [
        new(
            ProviderTransportKind.ChatCompletions,
            ProviderInvocationFeatures.FunctionTools,
            [
                OpenAiModelIds.Gpt54Mini,
                OpenAiModelIds.Gpt56Luna,
                OpenAiModelIds.Gpt56Terra
            ],
            AgentReasoningEffortLevel.None,
            ProviderModelParameterAdjustment.ReasoningDisabledForFunctionTools)
    ];

    public static ProviderReasoningEffortResolution ResolveReasoningEffort(
        ProviderKind providerKind,
        ProviderTransportKind transport,
        string model,
        ProviderInvocationFeatures features,
        AgentReasoningEffortLevel? requestedEffort)
    {
        var constraint = FindConstraint(providerKind, transport, model, features);
        if (constraint is null)
        {
            return new ProviderReasoningEffortResolution(
                requestedEffort,
                requestedEffort,
                ProviderRequestCompatibilityDisposition.Preserved,
                ProviderModelParameterAdjustment.None);
        }

        var isAlreadyEffective = requestedEffort == constraint.EffectiveEffort;
        return new ProviderReasoningEffortResolution(
            requestedEffort,
            constraint.EffectiveEffort,
            isAlreadyEffective
                ? ProviderRequestCompatibilityDisposition.Preserved
                : ProviderRequestCompatibilityDisposition.Adjusted,
            isAlreadyEffective
                ? ProviderModelParameterAdjustment.None
                : constraint.Adjustment);
    }

    public static ProviderRequestCompatibilityEvidence Evaluate(
        ProviderKind providerKind,
        Guid? providerProfileId,
        ProviderTransportKind transport,
        string model,
        ProviderInvocationFeatures features,
        AgentReasoningEffortLevel? requestedEffort)
    {
        var resolution = ResolveReasoningEffort(
            providerKind,
            transport,
            model,
            features,
            requestedEffort);
        var requestedModel = model.Trim();
        return new ProviderRequestCompatibilityEvidence(
            ProviderRequestCompatibilityEvidence.CurrentSchemaVersion,
            providerKind,
            providerProfileId,
            transport,
            requestedModel,
            requestedModel,
            features,
            resolution.RequestedEffort,
            resolution.EffectiveEffort,
            resolution.Disposition,
            resolution.Adjustment);
    }

    public static bool RequiresExplicitReasoningNone(
        ProviderKind providerKind,
        ProviderTransportKind transport,
        string model,
        ProviderInvocationFeatures features)
    {
        return FindConstraint(providerKind, transport, model, features)?.EffectiveEffort ==
               AgentReasoningEffortLevel.None;
    }

    private static OpenAiRequestConstraint? FindConstraint(
        ProviderKind providerKind,
        ProviderTransportKind transport,
        string model,
        ProviderInvocationFeatures features)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        if (providerKind != ProviderKind.OpenAi)
        {
            return null;
        }

        var normalizedModel = OpenAiModelIds.NormalizeKnownModelOrSnapshot(model);
        return Constraints.FirstOrDefault(constraint =>
            constraint.Transport == transport &&
            (features & constraint.RequiredFeatures) == constraint.RequiredFeatures &&
            constraint.ModelFamilies.Contains(normalizedModel, StringComparer.OrdinalIgnoreCase));
    }

}

internal sealed record OpenAiRequestConstraint(
    ProviderTransportKind Transport,
    ProviderInvocationFeatures RequiredFeatures,
    IReadOnlyList<string> ModelFamilies,
    AgentReasoningEffortLevel EffectiveEffort,
    ProviderModelParameterAdjustment Adjustment);
