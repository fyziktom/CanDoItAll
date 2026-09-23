using System.Collections.Frozen;

namespace CanDoItAll.SharedProviders.Abstractions;

public enum SharedProviderFailureCategory
{
    Validation,
    Unauthorized,
    InsufficientScope,
    NotFound,
    Conflict,
    Unavailable,
    RateLimited,
    UpstreamFailure,
    Timeout,
    Cancelled,
    VersionUnsupported
}

public readonly record struct SharedProviderFailureCode
{
    public const int MaximumLength = 128;

    public SharedProviderFailureCode(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException("A shared-provider failure code is invalid.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString()
        => IsValid(Value)
            ? Value
            : throw new InvalidOperationException("The shared-provider failure code is invalid.");

    private static bool IsValid(string? value)
        => value is { Length: > 0 and <= MaximumLength } &&
            value.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '_' or '-');
}

public sealed record SharedProviderFailure
{
    public const int MaximumMessageLength = 512;
    public const int MaximumParameterLength = 128;
    public static readonly TimeSpan MaximumRetryAfter = TimeSpan.FromDays(1);

    public SharedProviderFailure(
        SharedProviderFailureCategory category,
        SharedProviderFailureCode code,
        string sanitizedMessage,
        string? parameter = null,
        TimeSpan? retryAfter = null)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        if (string.IsNullOrEmpty(code.Value))
        {
            throw new ArgumentException("The shared-provider failure code is invalid.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(sanitizedMessage) ||
            sanitizedMessage.Length > MaximumMessageLength ||
            sanitizedMessage.Any(char.IsControl))
        {
            throw new ArgumentException("The sanitized failure message is invalid.", nameof(sanitizedMessage));
        }

        if (parameter is not null &&
            (parameter.Length == 0 ||
                parameter.Length > MaximumParameterLength ||
                parameter != parameter.Trim() ||
                parameter.Any(char.IsControl)))
        {
            throw new ArgumentException("The failure parameter is invalid.", nameof(parameter));
        }

        if (retryAfter.HasValue &&
            (retryAfter.Value < TimeSpan.Zero || retryAfter.Value > MaximumRetryAfter))
        {
            throw new ArgumentOutOfRangeException(nameof(retryAfter));
        }

        Category = category;
        Code = code;
        SanitizedMessage = sanitizedMessage;
        Parameter = parameter;
        RetryAfter = retryAfter;
    }

    public SharedProviderFailureCategory Category { get; }

    public SharedProviderFailureCode Code { get; }

    public string SanitizedMessage { get; }

    public string? Parameter { get; }

    public TimeSpan? RetryAfter { get; }
}

public enum SharedProviderRelayOperation
{
    ChatCompletions,
    Responses,
    ImageGenerations
}

public enum SharedProviderStreamingMode
{
    None,
    ServerSentEvents
}

public sealed record SharedProviderRelaySupportDescriptor
{
    public const int MaximumAllowedRequestBytes = 16 * 1024 * 1024;
    public const int MaximumAllowedOutputTokens = 1_000_000;
    public const int MaximumAllowedImageCount = 16;

    public SharedProviderRelaySupportDescriptor(
        IReadOnlySet<SharedProviderRelayOperation> operations,
        SharedProviderStreamingMode streamingMode,
        bool supportsFunctionTools,
        bool supportsParallelFunctionTools,
        bool supportsStructuredOutput,
        bool supportsVisionInput,
        bool supportsBase64Images,
        int maximumRequestBytes,
        int maximumOutputTokens,
        int maximumImageCount)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Count == 0 || operations.Any(operation => !Enum.IsDefined(operation)))
        {
            throw new ArgumentException("At least one defined relay operation is required.", nameof(operations));
        }

        if (!Enum.IsDefined(streamingMode))
        {
            throw new ArgumentOutOfRangeException(nameof(streamingMode));
        }

        if (maximumRequestBytes is <= 0 or > MaximumAllowedRequestBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRequestBytes));
        }

        if (maximumOutputTokens is <= 0 or > MaximumAllowedOutputTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOutputTokens));
        }

        if (maximumImageCount is <= 0 or > MaximumAllowedImageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumImageCount));
        }

        if (supportsParallelFunctionTools && !supportsFunctionTools)
        {
            throw new ArgumentException(
                "Parallel function tools require function-tool support.",
                nameof(supportsParallelFunctionTools));
        }

        var imageOnly = operations.Count == 1 && operations.Contains(SharedProviderRelayOperation.ImageGenerations);
        if (imageOnly &&
            (streamingMode != SharedProviderStreamingMode.None ||
                supportsFunctionTools ||
                supportsParallelFunctionTools ||
                supportsStructuredOutput ||
                supportsVisionInput))
        {
            throw new ArgumentException(
                "Image-only relay support cannot advertise text-generation capabilities.",
                nameof(operations));
        }

        if (supportsBase64Images && !operations.Contains(SharedProviderRelayOperation.ImageGenerations))
        {
            throw new ArgumentException(
                "Base64 image support requires the image-generation operation.",
                nameof(supportsBase64Images));
        }

        Operations = operations.ToFrozenSet();
        StreamingMode = streamingMode;
        SupportsFunctionTools = supportsFunctionTools;
        SupportsParallelFunctionTools = supportsParallelFunctionTools;
        SupportsStructuredOutput = supportsStructuredOutput;
        SupportsVisionInput = supportsVisionInput;
        SupportsBase64Images = supportsBase64Images;
        MaximumRequestBytes = maximumRequestBytes;
        MaximumOutputTokens = maximumOutputTokens;
        MaximumImageCount = maximumImageCount;
    }

    public IReadOnlySet<SharedProviderRelayOperation> Operations { get; }

    public SharedProviderStreamingMode StreamingMode { get; }

    public bool SupportsFunctionTools { get; }

    public bool SupportsParallelFunctionTools { get; }

    public bool SupportsStructuredOutput { get; }

    public bool SupportsVisionInput { get; }

    public bool SupportsBase64Images { get; }

    public int MaximumRequestBytes { get; }

    public int MaximumOutputTokens { get; }

    public int MaximumImageCount { get; }
}

public sealed record SharedProviderInferenceTransportRequest
{
    public const int MaximumPayloadCharacters = 4 * 1024 * 1024;

    public SharedProviderInferenceTransportRequest(
        Uri sourceBaseUri,
        SharedProviderRelayOperation operation,
        SharedProviderRoutingModelId routingModelId,
        string payloadJson,
        bool stream)
    {
        ArgumentNullException.ThrowIfNull(sourceBaseUri);
        ArgumentNullException.ThrowIfNull(payloadJson);

        if (!sourceBaseUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The source base URI must be absolute.", nameof(sourceBaseUri));
        }

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (!SharedProviderRoutingModelIdCodec.TryParse(routingModelId.Value, out _, out _))
        {
            throw new ArgumentException("The routing model id is invalid.", nameof(routingModelId));
        }

        if (payloadJson.Length == 0 || payloadJson.Length > MaximumPayloadCharacters)
        {
            throw new ArgumentException("The inference payload size is invalid.", nameof(payloadJson));
        }

        if (stream && operation == SharedProviderRelayOperation.ImageGenerations)
        {
            throw new ArgumentException("Image generation does not support streaming.", nameof(stream));
        }

        SourceBaseUri = sourceBaseUri;
        Operation = operation;
        RoutingModelId = routingModelId;
        PayloadJson = payloadJson;
        Stream = stream;
    }

    public Uri SourceBaseUri { get; }

    public SharedProviderRelayOperation Operation { get; }

    public SharedProviderRoutingModelId RoutingModelId { get; }

    public string PayloadJson { get; }

    public bool Stream { get; }
}

public abstract record SharedProviderInferenceTransportResult
{
    private SharedProviderInferenceTransportResult()
    {
    }

    public sealed record Buffered : SharedProviderInferenceTransportResult
    {
        public Buffered(string payloadJson)
        {
            ArgumentNullException.ThrowIfNull(payloadJson);
            PayloadJson = payloadJson;
        }

        public string PayloadJson { get; }
    }

    public sealed record Streaming : SharedProviderInferenceTransportResult
    {
        public Streaming(IAsyncEnumerable<ReadOnlyMemory<byte>> chunks)
        {
            ArgumentNullException.ThrowIfNull(chunks);
            Chunks = chunks;
        }

        public IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks { get; }
    }

    public sealed record Failed : SharedProviderInferenceTransportResult
    {
        public Failed(SharedProviderFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            Failure = failure;
        }

        public SharedProviderFailure Failure { get; }
    }
}

public interface ISharedProviderInferenceTransport
{
    ValueTask<SharedProviderInferenceTransportResult> InvokeAsync(
        SharedProviderInferenceTransportRequest request,
        CancellationToken cancellationToken = default);
}
