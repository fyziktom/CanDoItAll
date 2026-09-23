using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal static class SharedProviderExecutionPricingResolver {
    public static ProviderExecutionTariff Freeze(ProviderProfile profile, string model) =>
        ProviderExecutionPricing.Freeze(profile.Id, model,
            ProviderPricingMetadata.Read(profile.ExtraSettingsJson).ModelPrices,
            profile.ConcurrencyToken.ToString("D"));

    public static ProviderExecutionPrice Evaluate(
        ProviderExecutionTariff tariff, SharedProviderRelayOperation operation, SharedProviderRelayUsage usage) =>
        ProviderExecutionPricing.Evaluate(tariff, usage.InputTokens, usage.CachedInputTokens,
            usage.CacheWriteTokens, usage.OutputTokens,
            operation is SharedProviderRelayOperation.ChatCompletions or SharedProviderRelayOperation.Responses,
            usage.ReportedPrice?.Amount, usage.ReportedPrice?.Currency);
}
