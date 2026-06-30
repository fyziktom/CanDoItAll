using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public sealed class AgentImageGenerationAccessSettings
{
    public bool CanGenerateImages { get; set; }

    public Guid? PreferredProviderProfileId { get; set; }

    public string DefaultModel { get; set; } = string.Empty;

    public bool CanStoreImagesAsProjectAssets { get; set; }
}

public static class AgentImageGenerationAccessMetadata
{
    private const string RootPropertyName = "imageGeneration";
    private const string CanGenerateImagesPropertyName = "canGenerateImages";
    private const string PreferredProviderProfileIdPropertyName = "preferredProviderProfileId";
    private const string DefaultModelPropertyName = "defaultModel";
    private const string CanStoreImagesAsProjectAssetsPropertyName = "canStoreImagesAsProjectAssets";

    public static AgentImageGenerationAccessSettings Read(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new AgentImageGenerationAccessSettings();
        }

        try
        {
            var root = JsonNode.Parse(configurationJson)?.AsObject();
            var imageGeneration = root?[RootPropertyName]?.AsObject();
            if (imageGeneration is null)
            {
                return new AgentImageGenerationAccessSettings();
            }

            return Normalize(new AgentImageGenerationAccessSettings
            {
                CanGenerateImages = TryReadBoolean(imageGeneration, CanGenerateImagesPropertyName),
                PreferredProviderProfileId = TryReadGuid(imageGeneration, PreferredProviderProfileIdPropertyName),
                DefaultModel = TryReadString(imageGeneration, DefaultModelPropertyName),
                CanStoreImagesAsProjectAssets = TryReadBoolean(imageGeneration, CanStoreImagesAsProjectAssetsPropertyName)
            });
        }
        catch (JsonException)
        {
            return new AgentImageGenerationAccessSettings();
        }
    }

    public static string Write(
        string? configurationJson,
        AgentImageGenerationAccessSettings? settings)
    {
        var normalized = Normalize(settings ?? new AgentImageGenerationAccessSettings());
        var root = ParseObject(configurationJson);

        if (!normalized.CanGenerateImages &&
            normalized.PreferredProviderProfileId is null &&
            string.IsNullOrWhiteSpace(normalized.DefaultModel) &&
            !normalized.CanStoreImagesAsProjectAssets)
        {
            root.Remove(RootPropertyName);
            return root.ToJsonString();
        }

        root[RootPropertyName] = new JsonObject
        {
            [CanGenerateImagesPropertyName] = normalized.CanGenerateImages,
            [PreferredProviderProfileIdPropertyName] = normalized.PreferredProviderProfileId?.ToString("D"),
            [DefaultModelPropertyName] = normalized.DefaultModel,
            [CanStoreImagesAsProjectAssetsPropertyName] = normalized.CanStoreImagesAsProjectAssets
        };

        return root.ToJsonString();
    }

    public static AgentImageGenerationAccessSettings Normalize(AgentImageGenerationAccessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var canGenerateImages = settings.CanGenerateImages;
        return new AgentImageGenerationAccessSettings
        {
            CanGenerateImages = canGenerateImages,
            PreferredProviderProfileId = canGenerateImages && settings.PreferredProviderProfileId is { } providerId && providerId != Guid.Empty
                ? providerId
                : null,
            DefaultModel = canGenerateImages
                ? NormalizeText(settings.DefaultModel)
                : string.Empty,
            CanStoreImagesAsProjectAssets = settings.CanStoreImagesAsProjectAssets
        };
    }

    private static bool TryReadBoolean(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value &&
               value.TryGetValue<bool>(out var parsedValue) &&
               parsedValue;
    }

    private static Guid? TryReadGuid(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value &&
               value.TryGetValue<string>(out var stringValue) &&
               Guid.TryParse(stringValue, out var parsedValue)
            ? parsedValue
            : null;
    }

    private static string TryReadString(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value &&
               value.TryGetValue<string>(out var parsedValue)
            ? NormalizeText(parsedValue)
            : string.Empty;
    }

    private static JsonObject ParseObject(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(configurationJson)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
