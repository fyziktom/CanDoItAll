using CanDoItAll.Modules.Prompts;

namespace CanDoItAll.Prompts.UI;

public static class PromptGalleryText
{
    public static string FormatKind(PromptGalleryItemKind value)
        => value == PromptGalleryItemKind.FullPrompt ? "Full prompt" : "Prompt part";

    public static string FormatConsumer(PromptGalleryConsumer value)
        => value switch
        {
            PromptGalleryConsumer.AgentRuntime => "Agent runtime",
            PromptGalleryConsumer.ProjectWorkbench => "Project workbench",
            _ => value.ToString()
        };

    public static string FormatWarningIssue(PromptCompatibilityIssueCode value)
        => value switch
        {
            PromptCompatibilityIssueCode.ItemKindMismatch => "item-kind warnings",
            PromptCompatibilityIssueCode.ProviderModelNotSupported => "provider/model warnings",
            _ => value.ToString()
        };

    public static string FormatIssueCode(PromptCompatibilityIssueCode code)
        => code switch
        {
            PromptCompatibilityIssueCode.MissingFinalVersion => "Missing final version",
            PromptCompatibilityIssueCode.ConsumerNotSupported => "Consumer not supported",
            PromptCompatibilityIssueCode.ItemKindMismatch => "Item kind mismatch",
            PromptCompatibilityIssueCode.ProviderModelNotSupported => "Provider/model mismatch",
            _ => code.ToString()
        };

    public static string StatusTone(PromptGallerySearchItem item)
        => item.IsArchived ? "warning" : item.Status == PromptArtifactStatus.Final ? "success" : "info";

    public static string StatusLabel(PromptGallerySearchItem item)
        => item.IsArchived ? "Archived" : item.Status.ToString();

    public static string Excerpt(PromptGallerySearchItem item)
    {
        var preview = string.IsNullOrWhiteSpace(item.ContentPreview)
            ? item.Summary
            : item.ContentPreview;
        return string.IsNullOrWhiteSpace(preview)
            ? "No prompt content provided."
            : string.Join(' ', preview.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static string ModelSummary(PromptGallerySearchItem item)
    {
        var preferred = item.SupportedModels.FirstOrDefault(model => model.IsPreferred);
        if (preferred is not null)
        {
            return $"{preferred.Provider} · {preferred.Model} · preferred";
        }

        return item.SupportedModels.Count switch
        {
            0 => "Any provider / model",
            1 => $"{item.SupportedModels[0].Provider} · {item.SupportedModels[0].Model}",
            _ => $"{item.SupportedModels.Count} supported models"
        };
    }

    public static string RecommendationSummary(PromptModelRecommendations value)
    {
        var parts = new List<string>(3);
        if (value.Temperature.HasValue)
        {
            parts.Add($"temp {value.Temperature.Value:0.##}");
        }

        if (value.TopP.HasValue)
        {
            parts.Add($"top-p {value.TopP.Value:0.##}");
        }

        if (value.MaxOutputTokens.HasValue)
        {
            parts.Add($"max {value.MaxOutputTokens.Value:N0}");
        }

        return parts.Count == 0 ? "No parameter recommendations" : string.Join(" · ", parts);
    }
}
