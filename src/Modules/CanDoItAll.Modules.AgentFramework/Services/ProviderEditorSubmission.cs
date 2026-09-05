using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class ProviderEditorSubmission {
    private readonly ProviderProfileEditorModel original;
    private readonly byte[] originalState;

    private ProviderEditorSubmission(ProviderProfileEditorModel draft) {
        original = Copy(draft);
        originalState = JsonSerializer.SerializeToUtf8Bytes(original);
    }

    public static ProviderEditorSubmission Capture(ProviderProfileEditorModel draft) => new(draft);
    public ProviderProfileEditorModel CreateRequest() => Copy(original);

    public bool HasLaterEdits(ProviderProfileEditorModel draft) {
        var current = Copy(draft);
        current.Id = original.Id;
        current.ExpectedConcurrencyToken = original.ExpectedConcurrencyToken;
        return !originalState.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(current));
    }

    public void Reconcile(ProviderProfileEditorModel draft, ProviderProfileEditorModel authoritative) {
        if (!HasLaterEdits(draft)) {
            Apply(draft, authoritative);
        }
        draft.Id = authoritative.Id;
        draft.ExpectedConcurrencyToken = authoritative.ExpectedConcurrencyToken;
    }

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
        ModelPrices = source.ModelPrices.Select(price => new ProviderModelTokenPriceEditorModel { Model = price.Model, InputPerMillionTokensUsd = price.InputPerMillionTokensUsd, CachedInputPerMillionTokensUsd = price.CachedInputPerMillionTokensUsd, OutputPerMillionTokensUsd = price.OutputPerMillionTokensUsd, TariffKind = price.TariffKind, CacheWritePerMillionTokensUsd = price.CacheWritePerMillionTokensUsd, LongContextThresholdTokens = price.LongContextThresholdTokens, LongContextInputPerMillionTokensUsd = price.LongContextInputPerMillionTokensUsd, LongContextCachedInputPerMillionTokensUsd = price.LongContextCachedInputPerMillionTokensUsd, LongContextCacheWritePerMillionTokensUsd = price.LongContextCacheWritePerMillionTokensUsd, LongContextOutputPerMillionTokensUsd = price.LongContextOutputPerMillionTokensUsd }).ToList(),
        Tags = source.Tags.ToList(),
        ModelThinkingEffortCapabilities = source.ModelThinkingEffortCapabilities?.Select(capability => capability with { AllowedEfforts = capability.AllowedEfforts.ToArray() }).ToList()
    };

    private static void Apply(ProviderProfileEditorModel target, ProviderProfileEditorModel source) {
        var copy = Copy(source);
        target.Id = copy.Id;
        target.ExpectedConcurrencyToken = copy.ExpectedConcurrencyToken;
        target.Name = copy.Name;
        target.Kind = copy.Kind;
        target.BaseUrl = copy.BaseUrl;
        target.ApiKeyEnvironmentVariable = copy.ApiKeyEnvironmentVariable;
        target.DefaultModel = copy.DefaultModel;
        target.Transport = copy.Transport;
        target.Purpose = copy.Purpose;
        target.IsEnabled = copy.IsEnabled;
        target.SupportsStreaming = copy.SupportsStreaming;
        target.SupportsTools = copy.SupportsTools;
        target.PreferFrameworkManagedChatHistory = copy.PreferFrameworkManagedChatHistory;
        target.SupportsBackgroundResponses = copy.SupportsBackgroundResponses;
        target.ConfigurationJson = copy.ConfigurationJson;
        target.Notes = copy.Notes;
        target.IsPrivateProvider = copy.IsPrivateProvider;
        target.SuggestedModels = copy.SuggestedModels;
        target.ModelPrices = copy.ModelPrices;
        target.Tags = copy.Tags;
        target.ModelThinkingEffortCapabilities = copy.ModelThinkingEffortCapabilities;
    }
}
