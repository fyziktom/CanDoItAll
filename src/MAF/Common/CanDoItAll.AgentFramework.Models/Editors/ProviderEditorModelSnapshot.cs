namespace CanDoItAll.AgentFramework.Models;

public static class ProviderEditorModelSnapshot {
    public static ProviderProfileEditorModel Copy(ProviderProfileEditorModel source) => new() {
        Id = source.Id,
        ExpectedConcurrencyToken = source.ExpectedConcurrencyToken,
        Name = source.Name,
        Kind = source.Kind,
        BaseUrl = source.BaseUrl,
        ApiKeyEnvironmentVariable = source.ApiKeyEnvironmentVariable,
        DefaultModel = source.DefaultModel,
        Transport = source.Transport,
        Purpose = source.Purpose,
        IsEnabled = source.IsEnabled,
        SupportsStreaming = source.SupportsStreaming,
        SupportsTools = source.SupportsTools,
        PreferFrameworkManagedChatHistory = source.PreferFrameworkManagedChatHistory,
        SupportsBackgroundResponses = source.SupportsBackgroundResponses,
        ConfigurationJson = source.ConfigurationJson,
        Notes = source.Notes,
        IsPrivateProvider = source.IsPrivateProvider,
        SuggestedModels = source.SuggestedModels.ToList(),
        ModelPrices = ProviderPricingDefaults.ToEditorModels(ProviderPricingDefaults.FromEditorModels(source.ModelPrices)),
        Tags = source.Tags.ToList(),
        ModelThinkingEffortCapabilities = source.ModelThinkingEffortCapabilities?.Select(capability => capability with { AllowedEfforts = capability.AllowedEfforts.ToArray() }).ToList()
    };
}
