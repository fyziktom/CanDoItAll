using CanDoItAll.Prompts.UI.Editor;

namespace CanDoItAll.Modules.Prompts.Components;

// One supported provider/model declaration in the persistence owner's normalized form. The fields stay separate so
// two declarations can only compare equal when provider, model and preference each match; a joined string would let
// a delimiter inside a provider or model name make different declarations indistinguishable.
public readonly record struct PromptGalleryModelKey(string Provider, string Model, bool IsPreferred)
{
    public static PromptGalleryModelKey From(PromptProviderModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new PromptGalleryModelKey(model.Provider.Trim().ToUpperInvariant(), model.Model.Trim().ToUpperInvariant(), model.IsPreferred);
    }
}

// Normalized editable content of one persisted revision. Two revisions with matching content differ only by
// non-editable commands (archive, restore, finalize), so their token can be adopted without a lost update.
public sealed record PromptGalleryPersistedContent(
    string Title,
    string Summary,
    PromptGalleryItemKind Kind,
    string Phase,
    string Content,
    IReadOnlyList<string> TagKeys,
    IReadOnlyList<PromptGalleryModelKey> ModelKeys,
    IReadOnlyList<PromptGalleryConsumer> Consumers,
    PromptModelRecommendations Recommendations)
{
    public static PromptGalleryPersistedContent FromDetails(PromptGalleryItemDetails item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Create(
            item.Title,
            item.Summary,
            item.Kind,
            item.Phase,
            item.DraftContent,
            item.Tags,
            item.SupportedModels,
            item.SupportedConsumers,
            item.Recommendations);
    }

    public static PromptGalleryPersistedContent FromSubmission(PromptGalleryEditorSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        return Create(
            submission.Title,
            submission.Summary,
            submission.Kind,
            submission.Phase,
            submission.Content,
            submission.Tags,
            submission.SupportedModels,
            submission.SupportedConsumers,
            submission.Recommendations);
    }

    public bool Matches(PromptGalleryPersistedContent other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(Title, other.Title, StringComparison.Ordinal) &&
               string.Equals(Summary, other.Summary, StringComparison.Ordinal) &&
               Kind == other.Kind &&
               string.Equals(Phase, other.Phase, StringComparison.Ordinal) &&
               string.Equals(Content, other.Content, StringComparison.Ordinal) &&
               TagKeys.SequenceEqual(other.TagKeys, StringComparer.Ordinal) &&
               ModelKeys.SequenceEqual(other.ModelKeys) &&
               Consumers.SequenceEqual(other.Consumers) &&
               Recommendations.Temperature == other.Recommendations.Temperature &&
               Recommendations.MaxOutputTokens == other.Recommendations.MaxOutputTokens &&
               Recommendations.TopP == other.Recommendations.TopP;
    }

    // Mirrors the persistence normalization: trimmed scalars, case-insensitive distinct tags and provider/model pairs.
    private static PromptGalleryPersistedContent Create(
        string title,
        string summary,
        PromptGalleryItemKind kind,
        string phase,
        string content,
        IReadOnlyList<string> tags,
        IReadOnlyList<PromptProviderModel> models,
        IReadOnlyList<PromptGalleryConsumer> consumers,
        PromptModelRecommendations recommendations)
        => new(
            title.Trim(),
            summary.Trim(),
            kind,
            phase.Trim(),
            content,
            tags.Select(tag => tag.Trim().ToUpperInvariant())
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray(),
            models.Select(PromptGalleryModelKey.From)
                .Distinct()
                .OrderBy(key => key.Provider, StringComparer.Ordinal)
                .ThenBy(key => key.Model, StringComparer.Ordinal)
                .ThenBy(key => key.IsPreferred)
                .ToArray(),
            consumers.Distinct().OrderBy(consumer => consumer).ToArray(),
            recommendations);
}

