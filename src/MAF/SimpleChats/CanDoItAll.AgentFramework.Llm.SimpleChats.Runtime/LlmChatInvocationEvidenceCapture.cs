using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime;

internal sealed record LlmChatInvocationPricingCapture(
    LlmChatInvocationPricingEvidenceStatus Status,
    decimal? ProviderCostUsd,
    decimal? CalculatedCostUsd,
    string PricingProfileHash,
    string PricingVersion);

internal static class LlmChatInvocationEvidenceCapture
{
    public static LlmChatInvocationUsageEvidenceStatus ResolveReturnedUsage(LlmUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        return usage.InputTokens > 0 || usage.OutputTokens > 0 || usage.CachedInputTokens > 0
            ? LlmChatInvocationUsageEvidenceStatus.Observed
            : LlmChatInvocationUsageEvidenceStatus.UsageUnavailable;
    }

    public static LlmChatInvocationPricingCapture CapturePricing(
        LlmInvocationRequest request,
        string model,
        LlmUsage usage,
        LlmChatInvocationUsageEvidenceStatus usageStatus)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(usage);
        var profileHash = ProviderPricingSnapshot.CreateProfileHash(request.Provider);
        if (usageStatus == LlmChatInvocationUsageEvidenceStatus.Observed &&
            ProviderPricingCalculator.TryCalculate(
                request.Provider.Name,
                model,
                usage.InputTokens,
                usage.CachedInputTokens,
                usage.OutputTokens,
                request.Provider.ModelPrices,
                out var cost))
        {
            return new(
                LlmChatInvocationPricingEvidenceStatus.CalculatedAtExecution,
                ProviderCostUsd: null,
                cost.TotalUsd,
                profileHash,
                ProviderPricingSnapshot.Version);
        }

        return new(
            LlmChatInvocationPricingEvidenceStatus.Unpriced,
            ProviderCostUsd: null,
            CalculatedCostUsd: null,
            profileHash,
            ProviderPricingSnapshot.Version);
    }
}
