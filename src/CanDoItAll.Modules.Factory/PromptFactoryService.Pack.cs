using System.Text.Json;
using CanDoItAll.ComponentKit.Canvas;

namespace CanDoItAll.Modules.Factory;

public sealed partial class PromptFactoryService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PromptLibraryCatalogSummary> GetLibraryCatalogAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeedsAsync(cancellationToken);
        var pack = promptLibraryPackLoader.Load();
        var packBlocks = (await ListBlocksAsync(cancellationToken))
            .Where(item => string.Equals(item.CatalogSource, PromptLibraryPackLoader.CatalogSource, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var packFlows = (await ListTemplatesAsync(cancellationToken))
            .Where(item => string.Equals(item.CatalogSource, PromptLibraryPackLoader.CatalogSource, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var packBlueprints = (await ListBlueprintsAsync(cancellationToken))
            .Where(item => string.Equals(item.CatalogSource, PromptLibraryPackLoader.CatalogSource, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var groups = pack.Groups
            .Select(group =>
            {
                var components = packBlocks
                    .Where(item => string.Equals(item.GroupKey, group.Key, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(item => item.OrderIndex)
                    .ToList();

                return new PromptLibraryGroupSummary(
                    group.Key,
                    group.Name,
                    group.Summary,
                    group.Purpose,
                    group.UiMode,
                    group.Order,
                    components.Count,
                    components);
            })
            .ToList();

        return new PromptLibraryCatalogSummary(groups, packFlows, packBlueprints, packBlocks.Count, packFlows.Count, packBlueprints.Count);
    }

    public async Task<PromptSessionAttachmentSummary> PrepareAttachmentAsync(
        PromptSessionAttachmentSummary draft,
        CanvasWorkbenchUploadedFile? uploadedFile,
        CancellationToken cancellationToken = default)
    {
        var normalized = new PromptSessionAttachmentSummary
        {
            Id = string.IsNullOrWhiteSpace(draft.Id) ? Guid.NewGuid().ToString("N") : draft.Id.Trim(),
            Kind = NormalizeAttachmentKind(draft.Kind),
            Title = draft.Title?.Trim() ?? string.Empty,
            Subtitle = draft.Subtitle?.Trim() ?? string.Empty,
        Notes = draft.Notes?.Trim() ?? string.Empty,
        LinkUrl = draft.LinkUrl?.Trim() ?? string.Empty,
        MediaRelativePath = draft.MediaRelativePath?.Trim() ?? string.Empty,
        MediaRoute = draft.MediaRoute?.Trim() ?? string.Empty,
        MediaContentType = draft.MediaContentType?.Trim() ?? string.Empty,
        MediaOriginalFileName = draft.MediaOriginalFileName?.Trim() ?? string.Empty,
        MetadataJson = draft.MetadataJson?.Trim() ?? string.Empty
    };

        if (string.Equals(normalized.Kind, "link", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(normalized.LinkUrl))
        {
            normalized.LinkUrl = normalized.Subtitle;
        }

        if (uploadedFile is null || string.IsNullOrWhiteSpace(uploadedFile.Base64Data))
        {
            return normalized;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(uploadedFile.Base64Data);
        }
        catch
        {
            return normalized;
        }

        var extension = Path.GetExtension(uploadedFile.FileName);
        var safeExtension = string.IsNullOrWhiteSpace(extension)
            ? normalized.Kind switch
            {
                "image" => ".png",
                "video" => ".mp4",
                _ => ".bin"
            }
            : extension;
        var category = normalized.Kind switch
        {
            "image" => "prompt-session-assets/images",
            "video" => "prompt-session-assets/videos",
            _ => "prompt-session-assets/files"
        };
        var safeFileName = $"{SanitizeSlug(Path.GetFileNameWithoutExtension(uploadedFile.FileName))}-{Guid.NewGuid():N}{safeExtension}";
        var relativePath = Path.Combine("managed-files", category, safeFileName).Replace('\\', '/');
        await fileStore.SaveBytesAsync(relativePath, bytes, cancellationToken);

        normalized.MediaRelativePath = relativePath;
        normalized.MediaRoute = $"/{relativePath}";
        normalized.MediaContentType = uploadedFile.ContentType?.Trim() ?? "application/octet-stream";
        normalized.MediaOriginalFileName = Path.GetFileName(uploadedFile.FileName);
        return normalized;
    }

    private async Task<List<ResolvedPromptBlock>> LoadResolvedBlocksAsync(
        IReadOnlyCollection<Guid> selectedBlockIds,
        PromptFactoryEditorModel model,
        Guid? flowTemplateId,
        CancellationToken cancellationToken)
    {
        var definitions = await LoadBlocksAsync(selectedBlockIds, flowTemplateId, cancellationToken);
        var customizationLookup = model.ComponentCustomizations
            .Where(item => item.BlockId != Guid.Empty)
            .GroupBy(item => item.BlockId)
            .ToDictionary(group => group.Key, group => group.Last());

        return definitions
            .Select(definition =>
            {
                var renderedContent = customizationLookup.TryGetValue(definition.Id, out var customization) &&
                                      !string.IsNullOrWhiteSpace(customization.RenderedContent)
                    ? customization.RenderedContent
                    : definition.Content;
                return new ResolvedPromptBlock(definition, renderedContent);
            })
            .ToList();
    }

    private static T DeserializeJson<T>(string json) where T : class, new()
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new T();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? new T();
        }
        catch
        {
            return new T();
        }
    }

    private static string SerializeJson<T>(T value)
        => JsonSerializer.Serialize(value, SerializerOptions);

private static PromptBlockSummary MapBlockSummary(PromptBlockDefinition item)
    => new(
        item.Id,
        item.Key,
        item.GroupKey,
            item.Name,
            item.BlockKind,
            item.Summary,
            item.IsRecommendedByDefault,
            item.ToolboxEligible,
            SplitTokens(item.PromptTypeRules),
            SplitTokens(item.BlueprintRules),
            SplitTokens(item.PhaseRules),
        DeserializeJson<List<string>>(item.TagsJson),
        DeserializeJson<List<string>>(item.StackTagsJson),
        DeserializeJson<List<string>>(item.TemplateTokensJson),
        BuildContentPreview(item.Content),
        item.OrderIndex,
        item.CatalogSource);

private static string BuildContentPreview(string? content)
{
    if (string.IsNullOrWhiteSpace(content))
    {
        return string.Empty;
    }

    var normalized = string.Join(
        " ",
        content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line)));

    if (normalized.Length <= 260)
    {
        return normalized;
    }

    return normalized[..257].TrimEnd() + "...";
}

    private static PromptFlowTemplateSummary MapTemplateSummary(PromptFlowTemplate item)
        => new(
            item.Id,
            item.Key,
            item.Name,
            item.Summary,
            DeserializeIds(item.BlockIdsJson),
            DeserializeJson<List<string>>(item.BlockKeysJson),
            SplitTokens(item.PromptTypeRules),
            DeserializeJson<List<PromptFlowAgentSeed>>(item.AgentSequenceJson)
                .Select(step => new PromptFlowAgentSummary(
                    step.Order,
                    step.RoleComponentId,
                    step.RoleComponentKey,
                    step.BlueprintKey,
                    step.Phase,
                    step.Goal,
                    step.BlockKeys))
                .ToList(),
            item.OrderIndex,
            item.CatalogSource);

    private static PromptBlueprintSummary MapBlueprintSummary(PromptBlueprint item)
        => new(
            item.Id,
            item.Key,
            item.Name,
            item.PromptType,
            item.Summary,
            item.Guidance,
            item.RecommendedFlowTemplateId,
            item.RecommendedFlowKey,
            DeserializeJson<List<string>>(item.RecommendedBlockKeysJson),
            item.OrderIndex,
            item.CatalogSource);

    private static string BuildAttachmentLine(PromptSessionAttachmentSummary attachment)
    {
        var kind = string.IsNullOrWhiteSpace(attachment.Kind) ? "input" : attachment.Kind;
        var title = string.IsNullOrWhiteSpace(attachment.Title) ? "Untitled input" : attachment.Title;
        if (!string.IsNullOrWhiteSpace(attachment.LinkUrl))
        {
            return $"- {kind}: {title} ({attachment.LinkUrl})";
        }

        if (!string.IsNullOrWhiteSpace(attachment.MediaOriginalFileName))
        {
            return $"- {kind}: {title} ({attachment.MediaOriginalFileName})";
        }

        var detail = string.IsNullOrWhiteSpace(attachment.Subtitle) ? attachment.Notes : attachment.Subtitle;
        return string.IsNullOrWhiteSpace(detail)
            ? $"- {kind}: {title}"
            : $"- {kind}: {title} ({detail})";
    }

    private static PromptBlockKind MapPromptBlockKind(string blockKind)
        => blockKind.Trim().ToLowerInvariant() switch
        {
            "constraint" => PromptBlockKind.Constraint,
            "validation" => PromptBlockKind.Validation,
            "delivery" => PromptBlockKind.Delivery,
            "security" => PromptBlockKind.Security,
            "testing" => PromptBlockKind.Testing,
            _ => PromptBlockKind.Instruction
        };

    private static bool IsPackManaged(PromptBlockDefinition item)
        => string.Equals(item.CatalogSource, PromptLibraryPackLoader.CatalogSource, StringComparison.OrdinalIgnoreCase);

    private static bool IsPackManaged(PromptFlowTemplate item)
        => string.Equals(item.CatalogSource, PromptLibraryPackLoader.CatalogSource, StringComparison.OrdinalIgnoreCase);

    private static bool IsPackManaged(PromptBlueprint item)
        => string.Equals(item.CatalogSource, PromptLibraryPackLoader.CatalogSource, StringComparison.OrdinalIgnoreCase);

    private static string BuildKey(string value)
    {
        var buffer = new string((value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        while (buffer.Contains("--", StringComparison.Ordinal))
        {
            buffer = buffer.Replace("--", "-", StringComparison.Ordinal);
        }

        buffer = buffer.Trim('-');
        return string.IsNullOrWhiteSpace(buffer) ? $"custom-{Guid.NewGuid():N}" : buffer;
    }

    private static string NormalizeAttachmentKind(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "image" => "image",
            "video" => "video",
            "link" => "link",
            "note" => "note",
            _ => "file"
        };

    private static string SanitizeSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "asset";
        }

        var buffer = new string(value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        while (buffer.Contains("--", StringComparison.Ordinal))
        {
            buffer = buffer.Replace("--", "-", StringComparison.Ordinal);
        }

        return buffer.Trim('-');
    }

    private sealed record ResolvedPromptBlock(PromptBlockDefinition Definition, string RenderedContent);
}
