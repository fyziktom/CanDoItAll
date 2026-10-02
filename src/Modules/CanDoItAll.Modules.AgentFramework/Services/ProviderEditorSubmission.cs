using System.Text.Json;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers.UI;
using Microsoft.AspNetCore.Components.Forms;
using ProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class ProviderEditorSubmission {
    private readonly ProviderProfileEditorModel original;
    private readonly byte[] originalState;
    private readonly ProviderEditorDraft? editor;
    private readonly IReadOnlyDictionary<FieldIdentifier, long> revisions;
    private readonly IReadOnlyList<ProviderModelTokenPriceEditorModel> priceRows;

    private ProviderEditorSubmission(ProviderProfileEditorModel draft, ProviderEditorDraft? editor) {
        original = Copy(draft);
        originalState = JsonSerializer.SerializeToUtf8Bytes(original);
        this.editor = editor;
        revisions = editor?.CaptureRevisions() ?? new Dictionary<FieldIdentifier, long>();
        priceRows = draft.ModelPrices.ToArray();
    }

    public static ProviderEditorSubmission Capture(ProviderProfileEditorModel draft, ProviderEditorDraft? editor = null) => new(draft, editor);
    public ProviderMutationAttempt? Attempt { get; private set; }

    public static ProviderEditorSubmission CaptureForSave(ProviderProfileEditorModel draft, ProviderEditorDraft? editor = null) {
        var result = Capture(draft, editor);
        var request = Copy(result.original);
        var candidate = request.Id ?? Guid.NewGuid();
        var kind = request.Id.HasValue ? ProviderMutationKind.Update : ProviderMutationKind.Create;
        request.Id = candidate;
        if (kind == ProviderMutationKind.Create) {
            request.ExpectedConcurrencyToken = Guid.Empty;
        }
        result.Attempt = ProviderMutationAttempt.Capture(request, candidate, kind);
        return result;
    }

    public ProviderProfileEditorModel CreateRequest() {
        var request = Copy(original);
        if (Attempt is { } attempt) {
            request.Id = attempt.ProviderId;
            if (attempt.IsCreate) {
                request.ExpectedConcurrencyToken = Guid.Empty;
            }
        }
        return request;
    }

    public bool HasLaterEdits(ProviderProfileEditorModel draft) {
        var current = Copy(draft);
        current.Id = original.Id;
        current.ExpectedConcurrencyToken = original.ExpectedConcurrencyToken;
        return !originalState.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(current)) ||
            editor is not null && editor.CaptureRevisions().Any(pair => pair.Value != revisions.GetValueOrDefault(pair.Key));
    }

    public void Reconcile(ProviderProfileEditorModel draft, ProviderProfileEditorModel authoritative) {
        ReconcileFields(draft, original, authoritative);
        for (var index = 0; index < priceRows.Count; index++) {
            var live = priceRows[index];
            var submitted = original.ModelPrices[index];
            if (!draft.ModelPrices.Contains(live)) {
                continue;
            }
            var accepted = authoritative.ModelPrices.Where(row => string.Equals(row.Model.Trim(), submitted.Model.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
            if (accepted.Length == 1) {
                ReconcilePrice(live, submitted, accepted[0]);
            }
        }
        draft.Id = authoritative.Id;
        draft.ExpectedConcurrencyToken = authoritative.ExpectedConcurrencyToken;
    }

    public static ProviderProfileEditorModel Copy(ProviderProfileEditorModel source) => ProviderEditorDraft.Copy(source);

    private void ReconcileFields(ProviderProfileEditorModel target, ProviderProfileEditorModel sent, ProviderProfileEditorModel accepted) {
        Merge(target, nameof(target.Name), target.Name, sent.Name, accepted.Name, value => target.Name = value);
        Merge(target, nameof(target.Kind), target.Kind, sent.Kind, accepted.Kind, value => target.Kind = value);
        Merge(target, nameof(target.BaseUrl), target.BaseUrl, sent.BaseUrl, accepted.BaseUrl, value => target.BaseUrl = value);
        Merge(target, nameof(target.ApiKeyEnvironmentVariable), target.ApiKeyEnvironmentVariable, sent.ApiKeyEnvironmentVariable, accepted.ApiKeyEnvironmentVariable, value => target.ApiKeyEnvironmentVariable = value);
        Merge(target, nameof(target.DefaultModel), target.DefaultModel, sent.DefaultModel, accepted.DefaultModel, value => target.DefaultModel = value);
        Merge(target, nameof(target.Transport), target.Transport, sent.Transport, accepted.Transport, value => target.Transport = value);
        Merge(target, nameof(target.Purpose), target.Purpose, sent.Purpose, accepted.Purpose, value => target.Purpose = value);
        Merge(target, nameof(target.IsEnabled), target.IsEnabled, sent.IsEnabled, accepted.IsEnabled, value => target.IsEnabled = value);
        Merge(target, nameof(target.SupportsStreaming), target.SupportsStreaming, sent.SupportsStreaming, accepted.SupportsStreaming, value => target.SupportsStreaming = value);
        Merge(target, nameof(target.SupportsTools), target.SupportsTools, sent.SupportsTools, accepted.SupportsTools, value => target.SupportsTools = value);
        Merge(target, nameof(target.PreferFrameworkManagedChatHistory), target.PreferFrameworkManagedChatHistory, sent.PreferFrameworkManagedChatHistory, accepted.PreferFrameworkManagedChatHistory, value => target.PreferFrameworkManagedChatHistory = value);
        Merge(target, nameof(target.SupportsBackgroundResponses), target.SupportsBackgroundResponses, sent.SupportsBackgroundResponses, accepted.SupportsBackgroundResponses, value => target.SupportsBackgroundResponses = value);
        Merge(target, nameof(target.ConfigurationJson), target.ConfigurationJson, sent.ConfigurationJson, accepted.ConfigurationJson, value => target.ConfigurationJson = value);
        Merge(target, nameof(target.Notes), target.Notes, sent.Notes, accepted.Notes, value => target.Notes = value);
        Merge(target, nameof(target.IsPrivateProvider), target.IsPrivateProvider, sent.IsPrivateProvider, accepted.IsPrivateProvider, value => target.IsPrivateProvider = value);
        MergeList(target, nameof(target.SuggestedModels), target.SuggestedModels, sent.SuggestedModels, accepted.SuggestedModels, value => target.SuggestedModels = value);
        MergeList(target, nameof(target.Tags), target.Tags, sent.Tags, accepted.Tags, value => target.Tags = value);
        MergeList(target, nameof(target.ModelThinkingEffortCapabilities), target.ModelThinkingEffortCapabilities, sent.ModelThinkingEffortCapabilities, accepted.ModelThinkingEffortCapabilities, value => target.ModelThinkingEffortCapabilities = value);
    }

    private void ReconcilePrice(ProviderModelTokenPriceEditorModel target, ProviderModelTokenPriceEditorModel sent, ProviderModelTokenPriceEditorModel accepted) {
        Merge(target, nameof(target.Model), target.Model, sent.Model, accepted.Model, value => target.Model = value);
        Merge(target, nameof(target.InputPerMillionTokensUsd), target.InputPerMillionTokensUsd, sent.InputPerMillionTokensUsd, accepted.InputPerMillionTokensUsd, value => target.InputPerMillionTokensUsd = value);
        Merge(target, nameof(target.CachedInputPerMillionTokensUsd), target.CachedInputPerMillionTokensUsd, sent.CachedInputPerMillionTokensUsd, accepted.CachedInputPerMillionTokensUsd, value => target.CachedInputPerMillionTokensUsd = value);
        Merge(target, nameof(target.OutputPerMillionTokensUsd), target.OutputPerMillionTokensUsd, sent.OutputPerMillionTokensUsd, accepted.OutputPerMillionTokensUsd, value => target.OutputPerMillionTokensUsd = value);
        Merge(target, nameof(target.TariffKind), target.TariffKind, sent.TariffKind, accepted.TariffKind, value => target.TariffKind = value);
        Merge(target, nameof(target.CacheWritePerMillionTokensUsd), target.CacheWritePerMillionTokensUsd, sent.CacheWritePerMillionTokensUsd, accepted.CacheWritePerMillionTokensUsd, value => target.CacheWritePerMillionTokensUsd = value);
        Merge(target, nameof(target.ImageInputPerMillionTokensUsd), target.ImageInputPerMillionTokensUsd, sent.ImageInputPerMillionTokensUsd, accepted.ImageInputPerMillionTokensUsd, value => target.ImageInputPerMillionTokensUsd = value);
        Merge(target, nameof(target.CachedImageInputPerMillionTokensUsd), target.CachedImageInputPerMillionTokensUsd, sent.CachedImageInputPerMillionTokensUsd, accepted.CachedImageInputPerMillionTokensUsd, value => target.CachedImageInputPerMillionTokensUsd = value);
        Merge(target, nameof(target.LongContextThresholdTokens), target.LongContextThresholdTokens, sent.LongContextThresholdTokens, accepted.LongContextThresholdTokens, value => target.LongContextThresholdTokens = value);
        Merge(target, nameof(target.LongContextInputPerMillionTokensUsd), target.LongContextInputPerMillionTokensUsd, sent.LongContextInputPerMillionTokensUsd, accepted.LongContextInputPerMillionTokensUsd, value => target.LongContextInputPerMillionTokensUsd = value);
        Merge(target, nameof(target.LongContextCachedInputPerMillionTokensUsd), target.LongContextCachedInputPerMillionTokensUsd, sent.LongContextCachedInputPerMillionTokensUsd, accepted.LongContextCachedInputPerMillionTokensUsd, value => target.LongContextCachedInputPerMillionTokensUsd = value);
        Merge(target, nameof(target.LongContextCacheWritePerMillionTokensUsd), target.LongContextCacheWritePerMillionTokensUsd, sent.LongContextCacheWritePerMillionTokensUsd, accepted.LongContextCacheWritePerMillionTokensUsd, value => target.LongContextCacheWritePerMillionTokensUsd = value);
        Merge(target, nameof(target.LongContextOutputPerMillionTokensUsd), target.LongContextOutputPerMillionTokensUsd, sent.LongContextOutputPerMillionTokensUsd, accepted.LongContextOutputPerMillionTokensUsd, value => target.LongContextOutputPerMillionTokensUsd = value);
    }

    private bool Unchanged(object owner, string field) {
        var identity = new FieldIdentifier(owner, field);
        return editor is null || editor.Revision(identity) == revisions.GetValueOrDefault(identity) &&
            !editor.Context.GetValidationMessages(identity).Any();
    }

    private void Merge<T>(object owner, string field, T live, T sent, T accepted, Action<T> apply) {
        if (Unchanged(owner, field) && EqualityComparer<T>.Default.Equals(live, sent)) {
            apply(accepted);
        }
    }

    private void MergeList<T>(object owner, string field, List<T>? live, List<T>? sent, List<T>? accepted, Action<List<T>> apply) {
        if (Unchanged(owner, field) && JsonSerializer.Serialize(live) == JsonSerializer.Serialize(sent)) {
            apply(accepted?.ToList() ?? []);
        }
    }
}
