using System.Collections.Frozen;
using System.Text.Json;

namespace CanDoItAll.SharedProviders.Abstractions;

public enum SharedProviderRelayUsageCompleteness
{
    Unavailable,
    Partial,
    Complete
}

public sealed record SharedProviderRelayUsage
{
    public SharedProviderRelayUsage(
        long? inputTokens,
        long? outputTokens,
        int? imageCount,
        SharedProviderRelayUsageCompleteness completeness)
    {
        if (inputTokens is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputTokens));
        }

        if (outputTokens is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outputTokens));
        }

        if (imageCount is <= 0 or > SharedProviderRelaySupportDescriptor.MaximumAllowedImageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(imageCount));
        }

        var hasInputTokens = inputTokens.HasValue;
        var hasOutputTokens = outputTokens.HasValue;
        var hasImageCount = imageCount.HasValue;
        var isConsistent = completeness switch
        {
            SharedProviderRelayUsageCompleteness.Unavailable =>
                !hasInputTokens && !hasOutputTokens && !hasImageCount,
            SharedProviderRelayUsageCompleteness.Partial =>
                !hasImageCount && hasInputTokens != hasOutputTokens,
            SharedProviderRelayUsageCompleteness.Complete =>
                !hasImageCount && hasInputTokens && hasOutputTokens ||
                hasImageCount && !hasInputTokens && !hasOutputTokens,
            _ => false
        };
        if (!isConsistent)
        {
            throw new ArgumentException(
                "Relay usage values do not match their completeness state.",
                nameof(completeness));
        }

        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        ImageCount = imageCount;
        Completeness = completeness;
    }

    public long? InputTokens { get; }

    public long? OutputTokens { get; }

    public int? ImageCount { get; }

    public SharedProviderRelayUsageCompleteness Completeness { get; }

    public static SharedProviderRelayUsage Unavailable { get; } = new(
        inputTokens: null,
        outputTokens: null,
        imageCount: null,
        SharedProviderRelayUsageCompleteness.Unavailable);
}

public sealed record SharedProviderRelayResponseHeaders
{
    public SharedProviderRelayResponseHeaders(
        string? upstreamRequestId = null,
        TimeSpan? retryAfter = null)
    {
        if (upstreamRequestId is not null && !IsBoundedHeaderValue(upstreamRequestId))
        {
            throw new ArgumentException("The upstream request id is invalid.", nameof(upstreamRequestId));
        }

        if (retryAfter.HasValue &&
            (retryAfter.Value < TimeSpan.Zero || retryAfter.Value > SharedProviderFailure.MaximumRetryAfter))
        {
            throw new ArgumentOutOfRangeException(nameof(retryAfter));
        }

        UpstreamRequestId = upstreamRequestId;
        RetryAfter = retryAfter;
    }

    public string? UpstreamRequestId { get; }

    public TimeSpan? RetryAfter { get; }

    public static SharedProviderRelayResponseHeaders Empty { get; } = new();

    private static bool IsBoundedHeaderValue(string value)
        => value is { Length: > 0 and <= 256 } &&
            value == value.Trim() &&
            !value.Any(character => char.IsControl(character));
}

public sealed class SharedProviderRelayCredential
{
    public const int MaximumLength = 16 * 1024;

    private readonly string value;

    public SharedProviderRelayCredential(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaximumLength ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException("The relay credential is invalid.", nameof(value));
        }

        this.value = value;
    }

    public TResult UseValue<TResult>(Func<string, TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action(value);
    }

    public override string ToString()
        => "[REDACTED]";
}

public sealed class SharedProviderRelayTarget
{
    public const int MaximumConfigurationJsonCharacters = 64 * 1024;
    public static readonly TimeSpan MinimumTimeout = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MaximumTimeout = TimeSpan.FromMinutes(10);

    public SharedProviderRelayTarget(
        SharedProviderPublicationId publicationId,
        Guid providerProfileId,
        string connectorPluginKey,
        SharedProviderPurpose purpose,
        Uri baseUri,
        string upstreamModelId,
        SharedProviderRoutingModelId publicModelId,
        TimeSpan timeout,
        string configurationJson,
        SharedProviderRelayCredential? credential,
        SharedProviderRelaySupportDescriptor support)
    {
        if (publicationId.Value == Guid.Empty)
        {
            throw new ArgumentException("The publication id is invalid.", nameof(publicationId));
        }

        if (providerProfileId == Guid.Empty)
        {
            throw new ArgumentException("The provider profile id is invalid.", nameof(providerProfileId));
        }

        if (!IsConnectorPluginKeyValid(connectorPluginKey))
        {
            throw new ArgumentException("The connector plugin key is invalid.", nameof(connectorPluginKey));
        }

        if (!Enum.IsDefined(purpose))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose));
        }

        ArgumentNullException.ThrowIfNull(baseUri);
        if (!baseUri.IsAbsoluteUri ||
            (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
            !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new ArgumentException("The relay base URI is invalid.", nameof(baseUri));
        }

        if (string.IsNullOrWhiteSpace(upstreamModelId) ||
            upstreamModelId.Length > SharedProviderRoutingModelIdCodec.MaximumUpstreamModelIdLength ||
            upstreamModelId != upstreamModelId.Trim() ||
            upstreamModelId.Any(char.IsControl))
        {
            throw new ArgumentException("The upstream model id is invalid.", nameof(upstreamModelId));
        }

        if (publicModelId != SharedProviderRoutingModelIdCodec.Create(publicationId, upstreamModelId))
        {
            throw new ArgumentException(
                "The public routing id does not identify the target publication and model.",
                nameof(publicModelId));
        }

        if (timeout < MinimumTimeout || timeout > MaximumTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        ValidateConfigurationJson(configurationJson);
        ArgumentNullException.ThrowIfNull(support);

        PublicationId = publicationId;
        ProviderProfileId = providerProfileId;
        ConnectorPluginKey = connectorPluginKey;
        Purpose = purpose;
        BaseUri = new Uri(baseUri.AbsoluteUri, UriKind.Absolute);
        UpstreamModelId = upstreamModelId;
        PublicModelId = publicModelId;
        Timeout = timeout;
        ConfigurationJson = configurationJson;
        Credential = credential;
        Support = support;
    }

    public SharedProviderPublicationId PublicationId { get; }

    public Guid ProviderProfileId { get; }

    public string ConnectorPluginKey { get; }

    public SharedProviderPurpose Purpose { get; }

    public Uri BaseUri { get; }

    public string UpstreamModelId { get; }

    public SharedProviderRoutingModelId PublicModelId { get; }

    public TimeSpan Timeout { get; }

    public string ConfigurationJson { get; }

    public SharedProviderRelayCredential? Credential { get; }

    public SharedProviderRelaySupportDescriptor Support { get; }

    public bool Supports(SharedProviderRelayOperation operation)
        => Enum.IsDefined(operation) && Support.Operations.Contains(operation);

    private static bool IsConnectorPluginKeyValid(string? value)
        => value is { Length: > 0 and <= SharedProviderRelayAdapterDescriptor.MaximumConnectorPluginKeyLength } &&
            value == value.Trim() &&
            value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-');

    private static void ValidateConfigurationJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumConfigurationJsonCharacters)
        {
            throw new ArgumentException("The relay configuration JSON is invalid.", nameof(value));
        }

        try
        {
            using var document = JsonDocument.Parse(value, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("The relay configuration JSON must be an object.", nameof(value));
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The relay configuration JSON is invalid.", nameof(value), exception);
        }
    }
}

public sealed class SharedProviderRelayNormalizedRequest
{
    private readonly byte[] canonicalPayloadUtf8;

    public SharedProviderRelayNormalizedRequest(
        SharedProviderRelayOperation operation,
        SharedProviderRoutingModelId routingModelId,
        bool stream,
        ReadOnlySpan<byte> canonicalPayloadUtf8,
        IReadOnlySet<SharedProviderCapability> requiredCapabilities,
        int requestedImageCount = 0)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (!SharedProviderRoutingModelIdCodec.TryParse(routingModelId.Value, out _, out _))
        {
            throw new ArgumentException("The routing model id is invalid.", nameof(routingModelId));
        }

        if (stream && operation == SharedProviderRelayOperation.ImageGenerations)
        {
            throw new ArgumentException("Image generation cannot stream.", nameof(stream));
        }

        if (canonicalPayloadUtf8.IsEmpty)
        {
            throw new ArgumentException("The relay payload is empty.", nameof(canonicalPayloadUtf8));
        }

        ArgumentNullException.ThrowIfNull(requiredCapabilities);
        if (requiredCapabilities.Any(capability => !Enum.IsDefined(capability)))
        {
            throw new ArgumentException("A required capability is invalid.", nameof(requiredCapabilities));
        }

        if ((operation == SharedProviderRelayOperation.ImageGenerations && requestedImageCount <= 0) ||
            (operation != SharedProviderRelayOperation.ImageGenerations && requestedImageCount != 0))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedImageCount));
        }

        Operation = operation;
        RoutingModelId = routingModelId;
        Stream = stream;
        this.canonicalPayloadUtf8 = canonicalPayloadUtf8.ToArray();
        RequiredCapabilities = requiredCapabilities.ToFrozenSet();
        RequestedImageCount = requestedImageCount;
    }

    public SharedProviderRelayOperation Operation { get; }

    public SharedProviderRoutingModelId RoutingModelId { get; }

    public bool Stream { get; }

    public ReadOnlyMemory<byte> CanonicalPayloadUtf8 => canonicalPayloadUtf8;

    public IReadOnlySet<SharedProviderCapability> RequiredCapabilities { get; }

    public int RequestedImageCount { get; }

    public ReadOnlyMemory<byte> CreateUpstreamPayload(string upstreamModelId)
    {
        if (string.IsNullOrWhiteSpace(upstreamModelId) || upstreamModelId.Any(char.IsControl))
        {
            throw new ArgumentException("The upstream model id is invalid.", nameof(upstreamModelId));
        }

        using var document = JsonDocument.Parse(canonicalPayloadUtf8);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (property.NameEquals("model"))
                {
                    writer.WriteStringValue(upstreamModelId);
                }
                else
                {
                    property.Value.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }
}

public abstract record SharedProviderRelayRequestPolicyResult
{
    private SharedProviderRelayRequestPolicyResult()
    {
    }

    public sealed record Accepted : SharedProviderRelayRequestPolicyResult
    {
        public Accepted(SharedProviderRelayNormalizedRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            Request = request;
        }

        public SharedProviderRelayNormalizedRequest Request { get; }
    }

    public sealed record Rejected : SharedProviderRelayRequestPolicyResult
    {
        public Rejected(SharedProviderFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            Failure = failure;
        }

        public SharedProviderFailure Failure { get; }
    }
}

public sealed record SharedProviderRelayRequestContext
{
    public SharedProviderRelayRequestContext(
        string requestId,
        string authenticatedSubject,
        AccessContextReference? accessContextReference,
        string traceId,
        string correlationId)
    {
        RequestId = ValidateText(requestId, 128, nameof(requestId));
        AuthenticatedSubject = ValidateText(authenticatedSubject, 256, nameof(authenticatedSubject));
        if (accessContextReference.HasValue)
        {
            _ = accessContextReference.Value.Value;
        }

        AccessContextReference = accessContextReference;
        TraceId = ValidateText(traceId, 128, nameof(traceId));
        CorrelationId = ValidateText(correlationId, 128, nameof(correlationId));
    }

    public string RequestId { get; }

    public string AuthenticatedSubject { get; }

    public AccessContextReference? AccessContextReference { get; }

    public string TraceId { get; }

    public string CorrelationId { get; }

    private static string ValidateText(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > maximumLength ||
            value != value.Trim() ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException("Relay context metadata is invalid.", parameterName);
        }

        return value;
    }
}

public sealed class SharedProviderRelayApplicationRequest
{
    private readonly byte[] payloadUtf8;

    public SharedProviderRelayApplicationRequest(
        SharedProviderRelayOperation operation,
        ReadOnlySpan<byte> payloadUtf8,
        SharedProviderRelayRequestContext context)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (payloadUtf8.IsEmpty || payloadUtf8.Length > SharedProviderRelaySupportDescriptor.MaximumAllowedRequestBytes)
        {
            throw new ArgumentException("The relay request payload size is invalid.", nameof(payloadUtf8));
        }

        ArgumentNullException.ThrowIfNull(context);
        Operation = operation;
        this.payloadUtf8 = payloadUtf8.ToArray();
        Context = context;
    }

    public SharedProviderRelayOperation Operation { get; }

    public ReadOnlyMemory<byte> PayloadUtf8 => payloadUtf8;

    public SharedProviderRelayRequestContext Context { get; }
}

public sealed record SharedProviderRelayDispatchRequest
{
    public SharedProviderRelayDispatchRequest(
        SharedProviderRelayTarget target,
        SharedProviderRelayNormalizedRequest request)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(request);
        if (target.PublicModelId != request.RoutingModelId || !target.Supports(request.Operation))
        {
            throw new ArgumentException("The relay request does not match its resolved target.", nameof(request));
        }

        if (request.RequiredCapabilities.Any(capability =>
                !SharedProviderRelayCapabilityMap.Supports(target.Support, capability)))
        {
            throw new ArgumentException("The relay target does not support every requested capability.", nameof(request));
        }

        Target = target;
        Request = request;
    }

    public SharedProviderRelayTarget Target { get; }

    public SharedProviderRelayNormalizedRequest Request { get; }
}

public abstract record SharedProviderRelayDispatchResult
{
    private SharedProviderRelayDispatchResult()
    {
    }

    public sealed record Buffered : SharedProviderRelayDispatchResult
    {
        private readonly byte[] payloadUtf8;

        public Buffered(
            ReadOnlySpan<byte> payloadUtf8,
            string contentType,
            SharedProviderRelayResponseHeaders headers,
            SharedProviderRelayUsage usage)
        {
            if (payloadUtf8.IsEmpty)
            {
                throw new ArgumentException("The buffered relay payload is empty.", nameof(payloadUtf8));
            }

            if (!string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The buffered relay content type is invalid.", nameof(contentType));
            }

            ArgumentNullException.ThrowIfNull(headers);
            ArgumentNullException.ThrowIfNull(usage);
            this.payloadUtf8 = payloadUtf8.ToArray();
            ContentType = contentType;
            Headers = headers;
            Usage = usage;
        }

        public ReadOnlyMemory<byte> PayloadUtf8 => payloadUtf8;

        public string ContentType { get; }

        public SharedProviderRelayResponseHeaders Headers { get; }

        public SharedProviderRelayUsage Usage { get; }
    }

    public sealed record Streaming : SharedProviderRelayDispatchResult
    {
        public Streaming(ISharedProviderRelayStream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            Stream = stream;
        }

        public ISharedProviderRelayStream Stream { get; }
    }

    public sealed record Failed : SharedProviderRelayDispatchResult
    {
        public Failed(SharedProviderFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            Failure = failure;
        }

        public SharedProviderFailure Failure { get; }
    }
}

public sealed record SharedProviderRelayStreamFrame
{
    public const int MaximumEventNameLength = 128;
    public const int MaximumDataCharacters = 256 * 1024;

    public SharedProviderRelayStreamFrame(string? eventName, string data)
    {
        if (eventName is not null &&
            (eventName.Length == 0 ||
                eventName.Length > MaximumEventNameLength ||
                eventName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))))
        {
            throw new ArgumentException("The relay stream event name is invalid.", nameof(eventName));
        }

        if (string.IsNullOrEmpty(data) ||
            data.Length > MaximumDataCharacters ||
            data.Contains('\r') ||
            data.Contains('\n'))
        {
            throw new ArgumentException("The relay stream data is invalid.", nameof(data));
        }

        EventName = eventName;
        Data = data;
    }

    public string? EventName { get; }

    public string Data { get; }

    public bool IsDone => string.Equals(Data, "[DONE]", StringComparison.Ordinal);
}

public sealed record SharedProviderRelayStreamCompletion
{
    public SharedProviderRelayStreamCompletion(
        SharedProviderRelayUsage usage,
        SharedProviderFailure? failure = null)
    {
        ArgumentNullException.ThrowIfNull(usage);
        Usage = usage;
        Failure = failure;
    }

    public SharedProviderRelayUsage Usage { get; }

    public SharedProviderFailure? Failure { get; }
}

public interface ISharedProviderRelayStream : IAsyncDisposable
{
    SharedProviderRelayResponseHeaders Headers { get; }

    Task<SharedProviderRelayStreamCompletion> Completion { get; }

    IAsyncEnumerable<SharedProviderRelayStreamFrame> ReadFramesAsync(
        CancellationToken cancellationToken = default);
}

public interface ISharedProviderRelayRequestPolicy
{
    SharedProviderRelayRequestPolicyResult Normalize(
        SharedProviderRelayOperation operation,
        ReadOnlyMemory<byte> payloadUtf8,
        SharedProviderRelaySupportDescriptor support);
}

public interface ISharedProviderRelayDispatcher
{
    ValueTask<SharedProviderRelayDispatchResult> DispatchAsync(
        SharedProviderRelayDispatchRequest request,
        CancellationToken cancellationToken = default);
}

public interface ISharedProviderRelayApplicationService
{
    ValueTask<SharedProviderRelayDispatchResult> InvokeAsync(
        SharedProviderRelayApplicationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record SharedProviderImageCapabilityRequest(
    SharedProviderPublicationId PublicationId,
    Guid ProviderProfileId,
    string Model,
    string Prompt,
    string Size,
    string Quality,
    string OutputFormat,
    int Count);

public sealed record SharedProviderGeneratedImage(
    string ContentType,
    ReadOnlyMemory<byte> Bytes,
    string? RevisedPrompt);

public interface ISharedProviderImageCapabilityRelay
{
    ValueTask<IReadOnlyList<SharedProviderGeneratedImage>> GenerateAsync(
        SharedProviderImageCapabilityRequest request,
        CancellationToken cancellationToken = default);
}

public static class SharedProviderRelayCapabilityMap
{
    public static bool Supports(
        SharedProviderRelaySupportDescriptor support,
        SharedProviderCapability capability)
    {
        ArgumentNullException.ThrowIfNull(support);
        return capability switch
        {
            SharedProviderCapability.ChatCompletions =>
                support.Operations.Contains(SharedProviderRelayOperation.ChatCompletions),
            SharedProviderCapability.Responses =>
                support.Operations.Contains(SharedProviderRelayOperation.Responses),
            SharedProviderCapability.Streaming =>
                support.StreamingMode == SharedProviderStreamingMode.ServerSentEvents,
            SharedProviderCapability.FunctionTools => support.SupportsFunctionTools,
            SharedProviderCapability.ParallelFunctionTools => support.SupportsParallelFunctionTools,
            SharedProviderCapability.StructuredOutput => support.SupportsStructuredOutput,
            SharedProviderCapability.VisionInput => support.SupportsVisionInput,
            SharedProviderCapability.ImageGenerations =>
                support.Operations.Contains(SharedProviderRelayOperation.ImageGenerations),
            SharedProviderCapability.Base64Json => support.SupportsBase64Images,
            _ => false
        };
    }
}
