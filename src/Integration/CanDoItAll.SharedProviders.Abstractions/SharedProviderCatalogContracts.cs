using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text;

namespace CanDoItAll.SharedProviders.Abstractions;

/// <summary>
/// Version of the shared-provider catalog protocol, as a string; currently always <c>1.1</c>. A catalog with any
/// other version is not accepted.
/// </summary>
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

/// <summary>
/// What a shared-provider publication is for, as a string token: <c>chat</c> (text generation through the chat
/// completions and responses operations) or <c>image-generation</c> (image generation).
/// </summary>
[JsonConverter(typeof(SharedProviderPurposeJsonConverter))]
public enum SharedProviderPurpose
{
    [JsonStringEnumMemberName("chat")]
    Chat,

    [JsonStringEnumMemberName("image-generation")]
    ImageGeneration
}

/// <summary>
/// Protocol a shared-provider publication is invoked with, as a string token: <c>openai-compatible</c> (the
/// OpenAI-compatible operations under <c>/api/shared-providers/openai/v1</c>).
/// </summary>
[JsonConverter(typeof(SharedProviderTransportJsonConverter))]
public enum SharedProviderTransport
{
    [JsonStringEnumMemberName("openai-compatible")]
    OpenAiCompatible
}

/// <summary>
/// Capability of a shared model, as a string token: <c>chat-completions</c> (the chat completions operation),
/// <c>responses</c> (the responses operation), <c>streaming</c> (server-sent events with <c>stream</c> true),
/// <c>function-tools</c>, <c>parallel-function-tools</c>, <c>structured-output</c> (JSON-object or JSON-schema
/// responses), <c>vision-input</c> (image input in user messages), <c>image-generations</c> (the image generations
/// operation) or <c>b64-json</c> (base64 image results).
/// </summary>
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

/// <summary>
/// Health of the provider behind a shared-provider publication, as a string token: <c>available</c> (its last health
/// check passed), <c>unavailable</c> (its last health check failed) or <c>degraded</c> (no health check is recorded
/// yet).
/// </summary>
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

/// <summary>
/// Protocol entry points of the publishing host in a shared-provider catalog.
/// </summary>
/// <param name="OpenAiCompatibleBasePath">
/// Path of the OpenAI-compatible operations on the publishing host; always <c>/api/shared-providers/openai/v1</c>.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderProtocolDescriptor(
    [property: JsonPropertyName("openAiCompatibleBasePath")]
    string OpenAiCompatibleBasePath);

/// <summary>
/// One model of a shared-provider publication: the routing identifier to invoke it with, its capabilities and its
/// public price and reasoning support.
/// </summary>
/// <param name="Id">
/// Opaque routing identifier of the model; send it as <c>model</c> in the inference operations.
/// </param>
/// <param name="DisplayName">
/// Display name of the model, currently the model name of the publisher's upstream provider. Show it to people, but
/// never send it as <c>model</c>; use <c>id</c>.
/// </param>
/// <param name="Capabilities">
/// What the model supports, 1 to 32 distinct string tokens: <c>chat-completions</c>, <c>responses</c>,
/// <c>streaming</c>, <c>function-tools</c>, <c>parallel-function-tools</c>, <c>structured-output</c>,
/// <c>vision-input</c>, <c>image-generations</c> or <c>b64-json</c>. A request that needs a capability not listed is
/// rejected.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderCatalogModel(
    [property: JsonPropertyName("id")]
    SharedProviderRoutingModelId Id,
    [property: JsonPropertyName("displayName")]
    string DisplayName,
    [property: JsonPropertyName("capabilities")]
    IReadOnlyList<SharedProviderCapability> Capabilities) {
    /// <summary>
    /// Public token prices of the model; null when the publisher set no tariff, which means the cost is unknown, not
    /// free.
    /// </summary>
    [JsonPropertyName("price")]
    public SharedProviderCatalogPrice? Price { get; init; }

    /// <summary>
    /// Reasoning (thinking) support of the model and the effort levels a request may choose. Omitted when the
    /// publisher reports none.
    /// </summary>
    [JsonPropertyName("thinking")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SharedProviderThinkingCapability? Thinking { get; init; }

    /// <summary>
    /// True when consumers should offer the model in their model pickers. A model with false is still listed and can
    /// be invoked.
    /// </summary>
    [JsonPropertyName("isSuggested")]
    public bool IsSuggested { get; init; } = true;
}

/// <summary>
/// Health of the provider behind a shared-provider publication.
/// </summary>
/// <param name="State">
/// Health state, as a string token: <c>available</c> (last health check passed), <c>unavailable</c> (last health check
/// failed) or <c>degraded</c> (no health check recorded yet).
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderCatalogHealth(
    [property: JsonPropertyName("state")]
    SharedProviderHealthState State);

/// <summary>
/// A provider that the publishing host shares: its identity and revision, purpose, models and health. Only published
/// providers that are eligible for sharing appear in the catalog; internal profile identifiers, credentials, private
/// endpoint addresses and diagnostics are never included.
/// </summary>
/// <param name="PublicationId">
/// Identifier of the publication, as a GUID string; stable while the publication exists.
/// </param>
/// <param name="Revision">
/// Revision of the publication's public representation, as <c>sha256:</c> and 64 lowercase hexadecimal characters; it
/// changes whenever anything in this publication changes.
/// </param>
/// <param name="DisplayName">Display name of the shared provider, 1 to 256 characters.</param>
/// <param name="Purpose">
/// What the publication is for, as a string token: <c>chat</c> or <c>image-generation</c>.
/// </param>
/// <param name="Transport">
/// Protocol to invoke it with, as a string token: always <c>openai-compatible</c>.
/// </param>
/// <param name="DefaultModelId">
/// Routing identifier of the publication's default model; one of the <c>models</c>.
/// </param>
/// <param name="Models">The models of the publication, 1 to 128, sorted by <c>id</c>.</param>
/// <param name="Health">Health of the provider behind the publication.</param>
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
    /// <summary>
    /// True when the publisher treats the provider as privately operated (self-hosted): always for Ollama and ComfyUI
    /// connectors, otherwise as the publisher configured it.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("isPrivateProvider")]
    public bool IsPrivateProvider { get; init; }
}

/// <summary>
/// Catalog of the providers a host shares, returned by <c>GET /api/shared-providers/v1/catalog</c>. It is versioned,
/// sorted and content-addressed: <c>catalogRevision</c> identifies this exact content and equals the response's
/// <c>ETag</c> without quotes. Unknown members are not allowed in a catalog.
/// </summary>
/// <param name="SchemaVersion">Version of the catalog protocol, as a string: always <c>1.1</c>.</param>
/// <param name="SourceInstanceId">
/// Identifier of the publishing host instance, as a GUID string. It stays the same for the host, so a consumer can
/// detect that a source address now serves a different host.
/// </param>
/// <param name="CatalogRevision">
/// Revision of the whole catalog, as <c>sha256:</c> and 64 lowercase hexadecimal characters; it changes whenever the
/// public catalog changes.
/// </param>
/// <param name="Protocols">Protocol entry points of the publishing host.</param>
/// <param name="Providers">
/// The shared publications, sorted by publication identifier; empty when nothing is shared.
/// </param>
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
        model.Thinking?.Validate();
        if (purpose != SharedProviderPurpose.Chat &&
            model.Thinking?.Support == SharedProviderThinkingSupport.Supported) {
            throw new JsonException("Only chat models can declare thinking support.");
        }
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
                            Thinking = model.Thinking?.Snapshot(),
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
