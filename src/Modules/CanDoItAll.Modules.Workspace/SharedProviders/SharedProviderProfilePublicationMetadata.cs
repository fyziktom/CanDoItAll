using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.Workspace;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;

internal sealed record SharedProviderProfilePublicationMetadata(
    AgentFrameworkProviderKind ProviderKind,
    ProviderTransportKind Transport,
    ProviderProfilePurpose Purpose,
    IReadOnlyList<string> Models);

public static class SharedProviderProfilePublicationMetadataWriter
{
    public static string Write(
        string? configurationJson,
        AgentFrameworkProviderKind providerKind,
        ProviderTransportKind transport,
        ProviderProfilePurpose purpose,
        string defaultModel,
        IEnumerable<string>? suggestedModels = null)
    {
        if (!Enum.IsDefined(providerKind))
        {
            throw new ArgumentOutOfRangeException(nameof(providerKind));
        }

        if (!Enum.IsDefined(transport))
        {
            throw new ArgumentOutOfRangeException(nameof(transport));
        }

        if (!Enum.IsDefined(purpose))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose));
        }

        var normalizedModels = NormalizeSuggestedModels(
            defaultModel,
            suggestedModels);
        var configuration = ParseConfiguration(configurationJson);
        foreach (var canonicalName in
                 SharedProviderProfilePublicationMetadataSchema.RequiredPropertyNames)
        {
            foreach (var propertyName in configuration
                         .Select(property => property.Key)
                         .Where(propertyName => string.Equals(
                             propertyName,
                             canonicalName,
                             StringComparison.OrdinalIgnoreCase))
                         .ToArray())
            {
                configuration.Remove(propertyName);
            }
        }

        configuration[ProviderProfileMetadataPropertyNames.ProviderKind] =
            providerKind.ToString();
        configuration[ProviderProfileMetadataPropertyNames.ProviderTransport] =
            transport.ToString();
        configuration[ProviderProfileMetadataPropertyNames.ProviderPurpose] =
            purpose.ToString();
        var suggestedModelArray = new JsonArray();
        foreach (var model in normalizedModels)
        {
            suggestedModelArray.Add(JsonValue.Create(model));
        }

        configuration[ProviderProfileMetadataPropertyNames.SuggestedModels] =
            suggestedModelArray;
        return configuration.ToJsonString();
    }

    private static JsonObject ParseConfiguration(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new JsonObject();
        }

        try
        {
            using var document = JsonDocument.Parse(
                configurationJson,
                SharedProviderProfilePublicationMetadataSchema.DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    "Provider configuration must be a JSON object.");
            }

            var seenProperties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var canonicalName =
                    SharedProviderProfilePublicationMetadataSchema
                        .ResolveCanonicalPropertyName(property.Name);
                if (canonicalName is null || seenProperties.Add(canonicalName))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Provider publication metadata property '{canonicalName}' cannot be defined more than once with case-insensitive aliases.");
            }

            return JsonNode.Parse(
                       configurationJson,
                       documentOptions:
                           SharedProviderProfilePublicationMetadataSchema
                               .DocumentOptions) as JsonObject
                   ?? throw new InvalidOperationException(
                       "Provider configuration must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Provider configuration is not valid JSON.",
                exception);
        }
    }

    private static IReadOnlyList<string> NormalizeSuggestedModels(
        string defaultModel,
        IEnumerable<string>? suggestedModels)
    {
        if (!SharedProviderProfilePublicationMetadataModelValidator.IsValid(
                defaultModel))
        {
            throw new ArgumentException(
                "The provider default model is invalid for publication.",
                nameof(defaultModel));
        }

        var normalized = new List<string>();
        foreach (var model in suggestedModels ?? [])
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                continue;
            }

            var candidate = model.Trim();
            if (!SharedProviderProfilePublicationMetadataModelValidator.IsValid(
                    candidate))
            {
                throw new ArgumentException(
                    "A suggested provider model is invalid for publication.",
                    nameof(suggestedModels));
            }

            if (string.Equals(
                    candidate,
                    defaultModel,
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            normalized.Add(candidate);
            if (normalized.Count + 1 >
                SharedProviderProfilePublicationMetadataSchema.MaximumModels)
            {
                throw new ArgumentException(
                    $"A provider can publish at most {SharedProviderProfilePublicationMetadataSchema.MaximumModels} models.",
                    nameof(suggestedModels));
            }
        }

        return Array.AsReadOnly(normalized.ToArray());
    }
}

internal static class SharedProviderProfilePublicationMetadataReader
{
    public static bool TryRead(
        ProviderProfile profile,
        out SharedProviderProfilePublicationMetadata metadata,
        out string sanitizedReason)
    {
        ArgumentNullException.ThrowIfNull(profile);

        metadata = null!;
        sanitizedReason = string.Empty;
        if (string.IsNullOrWhiteSpace(profile.ExtraSettingsJson))
        {
            sanitizedReason = "Provider publication metadata is malformed JSON.";
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                profile.ExtraSettingsJson,
                SharedProviderProfilePublicationMetadataSchema.DocumentOptions);
        }
        catch (JsonException)
        {
            sanitizedReason = "Provider publication metadata is malformed JSON.";
            return false;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !TryReadRequiredProperties(document.RootElement, out var properties, out sanitizedReason) ||
                !TryReadEnum(properties[ProviderProfileMetadataPropertyNames.ProviderKind], out AgentFrameworkProviderKind providerKind) ||
                !TryReadEnum(properties[ProviderProfileMetadataPropertyNames.ProviderTransport], out ProviderTransportKind transport) ||
                !TryReadEnum(properties[ProviderProfileMetadataPropertyNames.ProviderPurpose], out ProviderProfilePurpose purpose))
            {
                sanitizedReason = string.IsNullOrEmpty(sanitizedReason)
                    ? "Provider publication metadata contains an invalid typed classification."
                    : sanitizedReason;
                return false;
            }

            if (!TryReadModels(
                    profile.DefaultModel,
                    properties[ProviderProfileMetadataPropertyNames.SuggestedModels],
                    out var models,
                    out sanitizedReason))
            {
                return false;
            }

            metadata = new SharedProviderProfilePublicationMetadata(
                providerKind,
                transport,
                purpose,
                models);
            return true;
        }
    }

    private static bool TryReadRequiredProperties(
        JsonElement root,
        out IReadOnlyDictionary<string, JsonElement> properties,
        out string sanitizedReason)
    {
        var resolved = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            var canonicalName =
                SharedProviderProfilePublicationMetadataSchema
                    .ResolveCanonicalPropertyName(property.Name);
            if (canonicalName is null)
            {
                continue;
            }

            if (!string.Equals(canonicalName, property.Name, StringComparison.Ordinal) ||
                !resolved.TryAdd(canonicalName, property.Value))
            {
                properties = null!;
                sanitizedReason =
                    $"Provider publication metadata property '{canonicalName}' is duplicated or has invalid casing.";
                return false;
            }
        }

        var missingProperty =
            SharedProviderProfilePublicationMetadataSchema.RequiredPropertyNames
                .FirstOrDefault(name => !resolved.ContainsKey(name));
        if (missingProperty is not null)
        {
            properties = null!;
            sanitizedReason =
                $"Provider publication metadata property '{missingProperty}' is required.";
            return false;
        }

        properties = resolved;
        sanitizedReason = string.Empty;
        return true;
    }

    private static bool TryReadEnum<TEnum>(JsonElement element, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (element.ValueKind != JsonValueKind.String || element.GetString() is not { } token)
        {
            return false;
        }

        return Enum.TryParse(token, ignoreCase: false, out value) &&
            Enum.IsDefined(value) &&
            string.Equals(Enum.GetName(value), token, StringComparison.Ordinal);
    }

    private static bool TryReadModels(
        string defaultModel,
        JsonElement suggestedModelsElement,
        out IReadOnlyList<string> models,
        out string sanitizedReason)
    {
        var collected = new List<string>();
        if (!TryAddModel(defaultModel, collected))
        {
            models = [];
            sanitizedReason = "The provider default model is invalid for publication.";
            return false;
        }

        if (suggestedModelsElement.ValueKind != JsonValueKind.Array)
        {
            models = [];
            sanitizedReason =
                $"Provider publication metadata property '{ProviderProfileMetadataPropertyNames.SuggestedModels}' must be an array.";
            return false;
        }

        foreach (var element in suggestedModelsElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String ||
                !TryAddModel(element.GetString(), collected))
            {
                models = [];
                sanitizedReason =
                    $"Provider publication metadata property '{ProviderProfileMetadataPropertyNames.SuggestedModels}' contains an invalid model token.";
                return false;
            }

            if (collected.Count >
                SharedProviderProfilePublicationMetadataSchema.MaximumModels)
            {
                models = [];
                sanitizedReason =
                    $"A provider can publish at most {SharedProviderProfilePublicationMetadataSchema.MaximumModels} models.";
                return false;
            }
        }

        models = Array.AsReadOnly(collected.ToArray());
        sanitizedReason = string.Empty;
        return true;
    }

    private static bool TryAddModel(string? model, ICollection<string> models)
    {
        if (model is null ||
            !SharedProviderProfilePublicationMetadataModelValidator.IsValid(
                model))
        {
            return false;
        }

        if (!models.Contains(model, StringComparer.Ordinal))
        {
            models.Add(model);
        }

        return true;
    }
}

internal static class SharedProviderProfilePublicationMetadataSchema
{
    public const int MaximumModels = 128;

    public static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 16
    };

    public static readonly IReadOnlyList<string> RequiredPropertyNames =
    [
        ProviderProfileMetadataPropertyNames.ProviderKind,
        ProviderProfileMetadataPropertyNames.ProviderTransport,
        ProviderProfileMetadataPropertyNames.ProviderPurpose,
        ProviderProfileMetadataPropertyNames.SuggestedModels
    ];

    public static string? ResolveCanonicalPropertyName(string propertyName)
        => RequiredPropertyNames.SingleOrDefault(name => string.Equals(
            name,
            propertyName,
            StringComparison.OrdinalIgnoreCase));
}

internal static class SharedProviderProfilePublicationMetadataModelValidator
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static bool IsValid(string? model)
    {
        if (model is not { Length: > 0 } ||
            model.Length >
            SharedProviderRoutingModelIdCodec.MaximumUpstreamModelIdLength ||
            model != model.Trim() ||
            model.Any(char.IsControl))
        {
            return false;
        }

        try
        {
            StrictUtf8.GetByteCount(model);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }
}
