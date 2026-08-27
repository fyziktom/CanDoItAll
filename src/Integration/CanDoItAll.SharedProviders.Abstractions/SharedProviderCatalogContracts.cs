using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text;

namespace CanDoItAll.SharedProviders.Abstractions;

[JsonConverter(typeof(SharedProviderProtocolVersionJsonConverter))]
public readonly record struct SharedProviderProtocolVersion
{
    public SharedProviderProtocolVersion(string value)
    {
        if (!string.Equals(value, SharedProviderProtocol.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The shared-provider protocol version is not supported.");
        }

        Value = value;
    }

    public string Value { get; }

    public static SharedProviderProtocolVersion Current { get; } = new(SharedProviderProtocol.CurrentSchemaVersion);

    public static bool TryParse(string? value, out SharedProviderProtocolVersion version)
    {
        if (!string.Equals(value, SharedProviderProtocol.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            version = default;
            return false;
        }

        version = Current;
        return true;
    }

    public override string ToString()
        => this == Current
            ? Value
            : throw new InvalidOperationException("The shared-provider protocol version is invalid.");
}

[JsonConverter(typeof(SharedProviderPurposeJsonConverter))]
public enum SharedProviderPurpose
{
    [JsonStringEnumMemberName("chat")]
    Chat,

    [JsonStringEnumMemberName("image-generation")]
    ImageGeneration
}

[JsonConverter(typeof(SharedProviderTransportJsonConverter))]
public enum SharedProviderTransport
{
    [JsonStringEnumMemberName("openai-compatible")]
    OpenAiCompatible
}

[JsonConverter(typeof(SharedProviderCapabilityJsonConverter))]
public enum SharedProviderCapability
{
    [JsonStringEnumMemberName("chat-completions")]
    ChatCompletions,

    [JsonStringEnumMemberName("responses")]
    Responses,

    [JsonStringEnumMemberName("streaming")]
    Streaming,

    [JsonStringEnumMemberName("function-tools")]
    FunctionTools,

    [JsonStringEnumMemberName("parallel-function-tools")]
    ParallelFunctionTools,

    [JsonStringEnumMemberName("structured-output")]
    StructuredOutput,

    [JsonStringEnumMemberName("vision-input")]
    VisionInput,

    [JsonStringEnumMemberName("image-generations")]
    ImageGenerations,

    [JsonStringEnumMemberName("b64-json")]
    Base64Json
}

[JsonConverter(typeof(SharedProviderHealthStateJsonConverter))]
public enum SharedProviderHealthState
{
    [JsonStringEnumMemberName("available")]
    Available,

    [JsonStringEnumMemberName("degraded")]
    Degraded,

    [JsonStringEnumMemberName("unavailable")]
    Unavailable
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderProtocolDescriptor(
    [property: JsonPropertyName("openAiCompatibleBasePath")]
    string OpenAiCompatibleBasePath);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderCatalogModel(
    [property: JsonPropertyName("id")]
    SharedProviderRoutingModelId Id,
    [property: JsonPropertyName("displayName")]
    string DisplayName,
    [property: JsonPropertyName("capabilities")]
    IReadOnlyList<SharedProviderCapability> Capabilities) {
    [JsonPropertyName("price")]
    public SharedProviderCatalogPrice? Price { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderCatalogHealth(
    [property: JsonPropertyName("state")]
    SharedProviderHealthState State);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderCatalogPublication(
    [property: JsonPropertyName("publicationId")]
    SharedProviderPublicationId PublicationId,
    [property: JsonPropertyName("revision")]
    SharedProviderPublicRevision Revision,
    [property: JsonPropertyName("displayName")]
    string DisplayName,
    [property: JsonPropertyName("purpose")]
    SharedProviderPurpose Purpose,
    [property: JsonPropertyName("transport")]
    SharedProviderTransport Transport,
    [property: JsonPropertyName("defaultModelId")]
    SharedProviderRoutingModelId DefaultModelId,
    [property: JsonPropertyName("models")]
    IReadOnlyList<SharedProviderCatalogModel> Models,
    [property: JsonPropertyName("health")]
    SharedProviderCatalogHealth Health) {
    [JsonRequired]
    [JsonPropertyName("isPrivateProvider")]
    public bool IsPrivateProvider { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderCatalogDocument(
    [property: JsonPropertyName("schemaVersion")]
    SharedProviderProtocolVersion SchemaVersion,
    [property: JsonPropertyName("sourceInstanceId")]
    SharedProviderSourceInstanceId SourceInstanceId,
    [property: JsonPropertyName("catalogRevision")]
    SharedProviderPublicRevision CatalogRevision,
    [property: JsonPropertyName("protocols")]
    SharedProviderProtocolDescriptor Protocols,
    [property: JsonPropertyName("providers")]
    IReadOnlyList<SharedProviderCatalogPublication> Providers);

public static class SharedProviderProtocolJson
{
    private const int MaximumProviders = 256;
    private const int MaximumModelsPerProvider = 128;
    private const int MaximumCapabilitiesPerModel = 32;
    private const int MaximumDisplayNameLength = 256;
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string SerializeCatalog(SharedProviderCatalogDocument catalog)
    {
        ValidateCatalog(catalog);
        return JsonSerializer.Serialize(NormalizeCatalog(catalog), Options);
    }

    public static SharedProviderCatalogDocument DeserializeCatalog(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var catalog = JsonSerializer.Deserialize<SharedProviderCatalogDocument>(json, Options)
            ?? throw new JsonException("The shared-provider catalog body is null.");
        ValidateCatalog(catalog);
        return NormalizeCatalog(catalog);
    }

    public static void ValidateCatalog(SharedProviderCatalogDocument catalog)
    {
        ValidateCatalogShape(catalog);

        foreach (var publication in catalog.Providers)
        {
            if (publication.Revision != SharedProviderCanonicalRevision.ComputePublication(publication))
            {
                throw new JsonException("A publication revision does not match its public representation.");
            }
        }

        if (catalog.CatalogRevision != SharedProviderCanonicalRevision.ComputeCatalog(catalog))
        {
            throw new JsonException("The catalog revision does not match its public representation.");
        }
    }

    internal static void ValidateCatalogShape(SharedProviderCatalogDocument catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog.SchemaVersion != SharedProviderProtocolVersion.Current ||
            catalog.SourceInstanceId.Value == Guid.Empty ||
            !SharedProviderPublicRevision.TryParse(catalog.CatalogRevision.Value, out _) ||
            catalog.Protocols is null ||
            !string.Equals(
                catalog.Protocols.OpenAiCompatibleBasePath,
                SharedProviderRoutes.OpenAiBase,
                StringComparison.Ordinal) ||
            catalog.Providers is null ||
            catalog.Providers.Count > MaximumProviders)
        {
            throw new JsonException("The shared-provider catalog contract is invalid.");
        }

        var publicationIds = new HashSet<SharedProviderPublicationId>();
        var routingModelIds = new HashSet<SharedProviderRoutingModelId>();
        foreach (var publication in catalog.Providers)
        {
            ValidatePublicationCore(publication, publicationIds, routingModelIds);
        }
    }

    internal static void ValidatePublication(SharedProviderCatalogPublication publication)
        => ValidatePublicationCore(
            publication,
            new HashSet<SharedProviderPublicationId>(),
            new HashSet<SharedProviderRoutingModelId>());

    private static void ValidatePublicationCore(
        SharedProviderCatalogPublication? publication,
        ISet<SharedProviderPublicationId> publicationIds,
        ISet<SharedProviderRoutingModelId> routingModelIds)
    {
        if (publication is null ||
            publication.PublicationId.Value == Guid.Empty ||
            !publicationIds.Add(publication.PublicationId) ||
            !SharedProviderPublicRevision.TryParse(publication.Revision.Value, out _) ||
            !IsDisplayNameValid(publication.DisplayName) ||
            !Enum.IsDefined(publication.Purpose) ||
            publication.Transport != SharedProviderTransport.OpenAiCompatible ||
            publication.Models is null ||
            publication.Models.Count == 0 ||
            publication.Models.Count > MaximumModelsPerProvider ||
            publication.Health is null ||
            !Enum.IsDefined(publication.Health.State))
        {
            throw new JsonException("A shared-provider catalog publication is invalid.");
        }

        var defaultModelFound = false;
        foreach (var model in publication.Models)
        {
            ValidateModel(model, publication.PublicationId, publication.Purpose, routingModelIds);
            defaultModelFound |= model.Id == publication.DefaultModelId;
        }

        if (!defaultModelFound)
        {
            throw new JsonException("The publication default model is not present in its model list.");
        }
    }

    private static void ValidateModel(
        SharedProviderCatalogModel? model,
        SharedProviderPublicationId publicationId,
        SharedProviderPurpose purpose,
        ISet<SharedProviderRoutingModelId> routingModelIds)
    {
        if (model is null ||
            !SharedProviderRoutingModelIdCodec.TryParse(model.Id.Value, out _, out var route) ||
            route.PublicationId != publicationId ||
            !routingModelIds.Add(model.Id) ||
            !IsDisplayNameValid(model.DisplayName) ||
            model.Capabilities is null ||
            model.Capabilities.Count == 0 ||
            model.Capabilities.Count > MaximumCapabilitiesPerModel)
        {
            throw new JsonException("A shared-provider catalog model is invalid.");
        }

        var capabilities = new HashSet<SharedProviderCapability>();
        foreach (var capability in model.Capabilities)
        {
            if (!Enum.IsDefined(capability) || !capabilities.Add(capability))
            {
                throw new JsonException("A shared-provider model capability is invalid or duplicated.");
            }
        }

        ValidateCapabilityCoherence(purpose, capabilities);
        model.Price?.Validate();
    }

    private static void ValidateCapabilityCoherence(
        SharedProviderPurpose purpose,
        IReadOnlySet<SharedProviderCapability> capabilities)
    {
        var hasChatOperation = capabilities.Contains(SharedProviderCapability.ChatCompletions) ||
            capabilities.Contains(SharedProviderCapability.Responses);
        var hasChatDependentCapability = capabilities.Contains(SharedProviderCapability.Streaming) ||
            capabilities.Contains(SharedProviderCapability.FunctionTools) ||
            capabilities.Contains(SharedProviderCapability.ParallelFunctionTools) ||
            capabilities.Contains(SharedProviderCapability.StructuredOutput) ||
            capabilities.Contains(SharedProviderCapability.VisionInput);
        var hasImageCapability = capabilities.Contains(SharedProviderCapability.ImageGenerations);
        var hasBase64Images = capabilities.Contains(SharedProviderCapability.Base64Json);
        var parallelWithoutFunctions = capabilities.Contains(SharedProviderCapability.ParallelFunctionTools) &&
            !capabilities.Contains(SharedProviderCapability.FunctionTools);

        var valid = purpose switch
        {
            SharedProviderPurpose.Chat =>
                hasChatOperation &&
                !hasImageCapability &&
                !hasBase64Images &&
                !parallelWithoutFunctions,
            SharedProviderPurpose.ImageGeneration =>
                hasImageCapability &&
                !hasChatOperation &&
                !hasChatDependentCapability &&
                !parallelWithoutFunctions,
            _ => false
        };

        if (!valid || hasChatDependentCapability && !hasChatOperation)
        {
            throw new JsonException("The shared-provider purpose and capabilities are incoherent.");
        }
    }

    private static bool IsDisplayNameValid(string? value)
    {
        if (value is not { Length: > 0 and <= MaximumDisplayNameLength } ||
            value != value.Trim() ||
            value.Any(char.IsControl))
        {
            return false;
        }

        try
        {
            StrictUtf8.GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    internal static SharedProviderCatalogDocument NormalizeCatalog(SharedProviderCatalogDocument catalog)
        => catalog with
        {
            Providers = ReadOnlyCopy(catalog.Providers
                .OrderBy(publication => publication.PublicationId.ToString(), StringComparer.Ordinal)
                .Select(publication => publication with
                {
                    Models = ReadOnlyCopy(publication.Models
                        .OrderBy(model => model.Id.Value, StringComparer.Ordinal)
                        .Select(model => model with
                        {
                            Capabilities = ReadOnlyCopy(model.Capabilities
                                .OrderBy(
                                    SharedProviderCapabilityJsonConverter.GetToken,
                                    StringComparer.Ordinal))
                        })
                        )
                })
                )
        };

    private static IReadOnlyList<T> ReadOnlyCopy<T>(IEnumerable<T> values)
        => Array.AsReadOnly(values.ToArray());

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = false,
            AllowDuplicateProperties = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            MaxDepth = 32,
            NumberHandling = JsonNumberHandling.Strict,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            WriteIndented = false
        };
        options.MakeReadOnly();
        return options;
    }
}

internal sealed class SharedProviderProtocolVersionJsonConverter : JsonConverter<SharedProviderProtocolVersion>
{
    public override SharedProviderProtocolVersion Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (!SharedProviderProtocolVersion.TryParse(value, out var version))
        {
            throw new JsonException("The shared-provider protocol version is not supported.");
        }

        return version;
    }

    public override void Write(
        Utf8JsonWriter writer,
        SharedProviderProtocolVersion value,
        JsonSerializerOptions options)
    {
        if (value != SharedProviderProtocolVersion.Current)
        {
            throw new JsonException("The shared-provider protocol version is not supported.");
        }

        writer.WriteStringValue(value.Value);
    }
}

internal sealed class SharedProviderPurposeJsonConverter : JsonConverter<SharedProviderPurpose>
{
    public override SharedProviderPurpose Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.String && reader.GetString() is { } value
            ? value switch
            {
                "chat" => SharedProviderPurpose.Chat,
                "image-generation" => SharedProviderPurpose.ImageGeneration,
                _ => throw new JsonException("The shared-provider purpose is invalid.")
            }
            : throw new JsonException("The shared-provider purpose must be a string.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        SharedProviderPurpose value,
        JsonSerializerOptions options)
        => writer.WriteStringValue(GetToken(value));

    internal static string GetToken(SharedProviderPurpose value) => value switch
    {
        SharedProviderPurpose.Chat => "chat",
        SharedProviderPurpose.ImageGeneration => "image-generation",
        _ => throw new JsonException("The shared-provider purpose is invalid.")
    };
}

internal sealed class SharedProviderTransportJsonConverter : JsonConverter<SharedProviderTransport>
{
    public override SharedProviderTransport Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.String && reader.GetString() is { } value
            ? value switch
            {
                "openai-compatible" => SharedProviderTransport.OpenAiCompatible,
                _ => throw new JsonException("The shared-provider transport is invalid.")
            }
            : throw new JsonException("The shared-provider transport must be a string.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        SharedProviderTransport value,
        JsonSerializerOptions options)
        => writer.WriteStringValue(GetToken(value));

    internal static string GetToken(SharedProviderTransport value) => value switch
    {
        SharedProviderTransport.OpenAiCompatible => "openai-compatible",
        _ => throw new JsonException("The shared-provider transport is invalid.")
    };
}

internal sealed class SharedProviderCapabilityJsonConverter : JsonConverter<SharedProviderCapability>
{
    public override SharedProviderCapability Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.String && reader.GetString() is { } value
            ? value switch
            {
                "chat-completions" => SharedProviderCapability.ChatCompletions,
                "responses" => SharedProviderCapability.Responses,
                "streaming" => SharedProviderCapability.Streaming,
                "function-tools" => SharedProviderCapability.FunctionTools,
                "parallel-function-tools" => SharedProviderCapability.ParallelFunctionTools,
                "structured-output" => SharedProviderCapability.StructuredOutput,
                "vision-input" => SharedProviderCapability.VisionInput,
                "image-generations" => SharedProviderCapability.ImageGenerations,
                "b64-json" => SharedProviderCapability.Base64Json,
                _ => throw new JsonException("The shared-provider capability is invalid.")
            }
            : throw new JsonException("The shared-provider capability must be a string.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        SharedProviderCapability value,
        JsonSerializerOptions options)
        => writer.WriteStringValue(GetToken(value));

    internal static string GetToken(SharedProviderCapability value) => value switch
    {
        SharedProviderCapability.ChatCompletions => "chat-completions",
        SharedProviderCapability.Responses => "responses",
        SharedProviderCapability.Streaming => "streaming",
        SharedProviderCapability.FunctionTools => "function-tools",
        SharedProviderCapability.ParallelFunctionTools => "parallel-function-tools",
        SharedProviderCapability.StructuredOutput => "structured-output",
        SharedProviderCapability.VisionInput => "vision-input",
        SharedProviderCapability.ImageGenerations => "image-generations",
        SharedProviderCapability.Base64Json => "b64-json",
        _ => throw new JsonException("The shared-provider capability is invalid.")
    };
}

internal sealed class SharedProviderHealthStateJsonConverter : JsonConverter<SharedProviderHealthState>
{
    public override SharedProviderHealthState Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.String && reader.GetString() is { } value
            ? value switch
            {
                "available" => SharedProviderHealthState.Available,
                "degraded" => SharedProviderHealthState.Degraded,
                "unavailable" => SharedProviderHealthState.Unavailable,
                _ => throw new JsonException("The shared-provider health state is invalid.")
            }
            : throw new JsonException("The shared-provider health state must be a string.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        SharedProviderHealthState value,
        JsonSerializerOptions options)
        => writer.WriteStringValue(GetToken(value));

    internal static string GetToken(SharedProviderHealthState value) => value switch
    {
        SharedProviderHealthState.Available => "available",
        SharedProviderHealthState.Degraded => "degraded",
        SharedProviderHealthState.Unavailable => "unavailable",
        _ => throw new JsonException("The shared-provider health state is invalid.")
    };
}
