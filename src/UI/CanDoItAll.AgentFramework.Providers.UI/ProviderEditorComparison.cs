using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;

namespace CanDoItAll.AgentFramework.Providers.UI;

public sealed record ProviderConnectionSettings(ProviderKind Kind, string BaseUrl, string SecretReference,
    string DefaultModel, ProviderTransportKind Transport, ProviderProfilePurpose Purpose, bool Enabled,
    bool Streaming, bool Tools, bool FrameworkHistory, bool Background, bool Private,
    string Configuration, string Models, string Prices, string Thinking) {
    public static ProviderConnectionSettings From(ProviderProfileEditorModel value) => new(value.Kind,
        value.BaseUrl, value.ApiKeyEnvironmentVariable, value.DefaultModel, value.Transport, value.Purpose,
        value.IsEnabled, value.SupportsStreaming, value.SupportsTools, value.PreferFrameworkManagedChatHistory,
        value.SupportsBackgroundResponses, value.IsPrivateProvider, value.ConfigurationJson,
        JsonSerializer.Serialize(value.SuggestedModels), JsonSerializer.Serialize(value.ModelPrices),
        JsonSerializer.Serialize(value.ModelThinkingEffortCapabilities));
}

public sealed class ProviderEditorComparison {
    private const string SecretReferencePrefix = "secret:";
    private readonly ProviderProfileEditorModel local;
    private readonly ProviderProfileEditorModel current;
    private readonly string localConfigurationSummary;
    private readonly string currentConfigurationSummary;

    public ProviderEditorComparison(ProviderProfileEditorModel original, ProviderProfileEditorModel local,
        ProviderProfileEditorModel current, bool incompleteSettings) {
        if (original.Id is null || original.Id != local.Id || local.Id != current.Id ||
            current.ExpectedConcurrencyToken is not { } token || token == Guid.Empty) {
            throw new InvalidOperationException("Provider comparison requires one saved identity and its original version.");
        }
        this.local = ProviderEditorDraft.Copy(local);
        this.current = ProviderEditorDraft.Copy(current);
        localConfigurationSummary = ConfigurationSummary(original.ConfigurationJson, local.ConfigurationJson);
        currentConfigurationSummary = ConfigurationSummary(original.ConfigurationJson, current.ConfigurationJson);
        Name = new(original.Name, local.Name, current.Name, StringComparer.Ordinal);
        Notes = TextMerge.Compare(original.Notes, local.Notes, current.Notes);
        Settings = new(ProviderConnectionSettings.From(original), ProviderConnectionSettings.From(local), ProviderConnectionSettings.From(current));
        if (Settings.CurrentChanged) {
            Settings.RequireChoice();
        }
        Tags = CollectionMerge.Membership(original.Tags, local.Tags, current.Tags, StringComparer.OrdinalIgnoreCase);
        HasIncompleteSettings = incompleteSettings;
    }

    public MergeField<string> Name { get; }
    public TextMergeResult Notes { get; }
    public MergeField<ProviderConnectionSettings> Settings { get; }
    public IReadOnlyList<string> Tags { get; }
    public bool HasIncompleteSettings { get; }
    public bool KeepIncompleteSettings { get; set; }
    public bool IsStale { get; private set; }
    public ProviderProfileEditorModel CurrentSnapshot() => ProviderEditorDraft.Copy(current);
    public bool CanApply => !IsStale && Name.IsResolved && Notes.Field.IsResolved &&
        (HasIncompleteSettings ? KeepIncompleteSettings : Settings.IsResolved);
    public void Invalidate() => IsStale = true;

    public string DescribeSettings(ProviderConnectionSettings value) {
        var configuration = value.Configuration == Settings.Original.Configuration ? "Stored configuration matches the original."
            : value.Configuration == Settings.Local.Configuration ? localConfigurationSummary : currentConfigurationSummary;
        return DescribeSettings(value, configuration);
    }

    public static string DescribeRecovery(ProviderProfileEditorModel other, ProviderProfileEditorModel value)
        => $"{value.Name}\nNotes: {value.Notes}\nTags: {string.Join(", ", value.Tags)}\n" +
            DescribeSettings(ProviderConnectionSettings.From(value), ConfigurationSummary(other.ConfigurationJson, value.ConfigurationJson));

    private static string DescribeSettings(ProviderConnectionSettings value, string configuration) {
        var secret = string.IsNullOrWhiteSpace(value.SecretReference) ? "none"
            : value.SecretReference.StartsWith(SecretReferencePrefix, StringComparison.Ordinal) &&
                Guid.TryParse(value.SecretReference.AsSpan(SecretReferencePrefix.Length), out var secretId)
                ? $"{SecretReferencePrefix}{secretId:D}" : "invalid reference retained privately";
        return $"{value.Kind}; {value.BaseUrl}\nModel: {value.DefaultModel}; {value.Transport}; {value.Purpose}\nSecret reference: {secret}\nEnabled: {value.Enabled}; streaming: {value.Streaming}; tools: {value.Tools}; framework history: {value.FrameworkHistory}; background: {value.Background}; private: {value.Private}\n{configuration}\nModel catalog, pricing and private extensions stay with this complete side.";
    }

    private static string ConfigurationSummary(string original, string changed) {
        if (Math.Max(original.Length, changed.Length) > TextMerge.MaximumAutomaticCharacters) {
            return "Stored configuration differs. It exceeds the bounded summary size; its complete private values are retained.";
        }
        try {
            using var before = JsonDocument.Parse(string.IsNullOrWhiteSpace(original) ? "{}" : original);
            using var after = JsonDocument.Parse(string.IsNullOrWhiteSpace(changed) ? "{}" : changed);
            if (before.RootElement.ValueKind != JsonValueKind.Object || after.RootElement.ValueKind != JsonValueKind.Object) {
                return "Unfinished configuration stays in the editor.";
            }
            var keys = before.RootElement.EnumerateObject().Select(property => property.Name)
                .Concat(after.RootElement.EnumerateObject().Select(property => property.Name)).Distinct(StringComparer.Ordinal)
                .Where(key => !before.RootElement.TryGetProperty(key, out var left) || !after.RootElement.TryGetProperty(key, out var right) ||
                    left.GetRawText() != right.GetRawText()).Order(StringComparer.Ordinal).ToArray();
            return keys.Length == 0 ? "Stored JSON formatting changed; settings are retained."
                : $"Additional settings changed: {string.Join(", ", keys.Take(32))}{(keys.Length > 32 ? $" (+{keys.Length - 32} more)" : "")}. Their values remain private.";
        } catch (JsonException) {
            return "Unfinished configuration stays in the editor.";
        }
    }

    public ProviderProfileEditorModel Result() {
        if (!CanApply) {
            throw new InvalidOperationException("Resolve the provider comparison before applying it locally.");
        }
        var result = ProviderEditorDraft.Copy(!HasIncompleteSettings && Settings.Choice == MergeChoice.Current ? current : local);
        result.Name = Name.Result;
        result.Notes = Notes.Field.Result;
        result.Tags = Tags.ToList();
        result.ExpectedConcurrencyToken = current.ExpectedConcurrencyToken;
        return result;
    }
}

public sealed class ProviderRecoveryReview {
    private readonly ProviderProfileEditorModel current;

    public ProviderRecoveryReview(ProviderProfileEditorModel local, ProviderProfileEditorModel current) {
        if (current.Id is null || current.Id == Guid.Empty || local.Id is { } id && id != current.Id ||
            current.ExpectedConcurrencyToken is not { } token || token == Guid.Empty) {
            throw new InvalidOperationException("Recovery requires the same saved provider and a current configuration version.");
        }
        this.current = ProviderEditorDraft.Copy(current);
        LocalDescription = ProviderEditorComparison.DescribeRecovery(current, local);
        CurrentDescription = ProviderEditorComparison.DescribeRecovery(local, current);
    }

    public string LocalDescription { get; }
    public string CurrentDescription { get; }
    public bool KeepDraft { get; set; }
    public bool IsStale { get; private set; }
    public bool CanApply => KeepDraft && !IsStale;
    public void Invalidate() => IsStale = true;
    public ProviderProfileEditorModel CurrentSnapshot() => ProviderEditorDraft.Copy(current);
}
