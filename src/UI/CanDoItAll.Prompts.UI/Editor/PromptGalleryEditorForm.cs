using CanDoItAll.Modules.Prompts;

namespace CanDoItAll.Prompts.UI.Editor;

// Mutable form draft owned by one editor surface instance. Collections are replaced, never mutated in place.
public sealed class PromptGalleryEditorForm
{
    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public PromptGalleryItemKind Kind { get; set; } = PromptGalleryItemKind.FullPrompt;

    public string Phase { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public IReadOnlyList<string> Tags { get; set; } = [];

    public IReadOnlyList<PromptProviderModel> SupportedModels { get; set; } = [];

    public IReadOnlyList<PromptGalleryConsumer> SupportedConsumers { get; set; } = [];

    public double? Temperature { get; set; }

    public int? MaxOutputTokens { get; set; }

    public double? TopP { get; set; }

    public bool HasRequiredValues
        => !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Content);

    public static PromptGalleryEditorForm FromSource(PromptGalleryEditorSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new PromptGalleryEditorForm
        {
            Title = source.Title,
            Summary = source.Summary,
            Kind = source.Kind,
            Phase = source.Phase,
            Content = source.Content,
            Tags = [.. source.Tags],
            SupportedModels = [.. source.SupportedModels],
            SupportedConsumers = [.. source.SupportedConsumers],
            Temperature = source.Recommendations.Temperature,
            MaxOutputTokens = source.Recommendations.MaxOutputTokens,
            TopP = source.Recommendations.TopP
        };
    }

    public PromptGalleryEditorSubmission ToSubmission()
        => new(
            Title,
            Summary,
            Kind,
            Phase,
            Content,
            [.. Tags],
            [.. SupportedModels],
            [.. SupportedConsumers],
            new PromptModelRecommendations(Temperature, MaxOutputTokens, TopP));

    public void SetConsumer(PromptGalleryConsumer consumer, bool enabled)
    {
        SupportedConsumers = enabled
            ? SupportedConsumers.Append(consumer).Distinct().Order().ToArray()
            : SupportedConsumers.Where(item => item != consumer).ToArray();
    }

    public string? TryAddSupportedModel(string? provider, string? model)
    {
        var trimmedProvider = provider?.Trim() ?? string.Empty;
        var trimmedModel = model?.Trim() ?? string.Empty;
        if (trimmedProvider.Length == 0 || trimmedModel.Length == 0)
        {
            return "Enter both a provider and a model before adding a supported model.";
        }

        if (SupportedModels.Any(item =>
                string.Equals(item.Provider, trimmedProvider, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Model, trimmedModel, StringComparison.OrdinalIgnoreCase)))
        {
            return "This provider and model combination is already listed.";
        }

        SupportedModels = Order(SupportedModels.Append(
            new PromptProviderModel(trimmedProvider, trimmedModel, IsPreferred: SupportedModels.Count == 0)));
        return null;
    }

    public void RemoveSupportedModel(PromptProviderModel supported)
    {
        SupportedModels = SupportedModels.Where(item => item != supported).ToArray();
    }

    public void SetPreferredModel(PromptProviderModel supported, bool preferred)
    {
        SupportedModels = Order(SupportedModels.Select(item => item == supported
            ? item with { IsPreferred = preferred }
            : preferred ? item with { IsPreferred = false } : item));
    }

    private static PromptProviderModel[] Order(IEnumerable<PromptProviderModel> models)
        => models
            .OrderByDescending(item => item.IsPreferred)
            .ThenBy(item => item.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Model, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
