using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Image generation access of an agent, the <c>imageGenerationAccess</c> member of the agent editor form, stored in the
/// agent's <c>configurationJson</c> under <c>imageGeneration</c>. A save replaces that section, and an omitted or empty
/// object removes it. The runtime image generation tool also needs the agent's <c>canUseTools</c> permission and, by
/// default, approval of each call.
/// </summary>
public sealed class AgentImageGenerationAccessSettings
{
    /// <summary>Adds the image generation tool to the agent's runs.</summary>
    public bool CanGenerateImages { get; set; }

    /// <summary>
    /// Provider profile used for image generation when a tool call names none, or null for the default image provider.
    /// It is a default, not a restriction, and the provider's purpose is checked only when the tool runs. The server
    /// clears it when <c>canGenerateImages</c> is false or the value is the all-zero GUID.
    /// </summary>
    public Guid? PreferredProviderProfileId { get; set; }

    /// <summary>
    /// Model used for image generation when a tool call names none; trimmed, and empty when <c>canGenerateImages</c> is
    /// false. It is checked against the provider only when the tool runs.
    /// </summary>
    public string DefaultModel { get; set; } = string.Empty;

    /// <summary>
    /// Allows the image generation tool to prepare generated images as Project Structure assets. It grants no Project
    /// Structure write access of its own and is kept even when <c>canGenerateImages</c> is false.
    /// </summary>
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
