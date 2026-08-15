using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Llm.Abstractions;

/// <summary>
/// The role a single lightweight LLM message plays in a conversation turn sequence.
/// </summary>
public enum LlmMessageRole
{
    System,
    User,
    Assistant
}

/// <summary>
/// A single repository-owned chat message. Never an agent, provider SDK, or capability type.
/// Text is bounded so a runaway caller cannot smuggle unbounded payloads into a stateless call.
/// </summary>
public sealed record LlmMessage
{
    public const int MaximumTextLength = 400_000;

    public LlmMessage(LlmMessageRole role, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown lightweight LLM message role.");
        }

        if (text.Length > MaximumTextLength)
        {
            throw new ArgumentException(
                $"A lightweight LLM message cannot exceed {MaximumTextLength} characters.",
                nameof(text));
        }

        Role = role;
        Text = text;
    }

    public LlmMessageRole Role { get; }

    public string Text { get; }
}

/// <summary>
/// A bounded, immutable binary attachment (for example an image) supplied alongside an invocation
/// request. The payload is copied into an immutable array at construction so no caller can mutate
/// it after validation.
/// </summary>
public sealed record LlmAttachment
{
    public const int MaximumNameLength = 200;
    public const int MaximumContentTypeLength = 100;
    public const int MaximumBytes = 20 * 1024 * 1024;

    public LlmAttachment(string name, string contentType, byte[] bytes)
        : this(name, contentType, bytes is null
            ? throw new ArgumentNullException(nameof(bytes))
            : ImmutableArray.Create(bytes))
    {
    }

    public LlmAttachment(string name, string contentType, ImmutableArray<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (name.Trim().Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"An attachment name cannot exceed {MaximumNameLength} characters.", nameof(name));
        }

        if (contentType.Trim().Length > MaximumContentTypeLength)
        {
            throw new ArgumentException(
                $"An attachment content type cannot exceed {MaximumContentTypeLength} characters.", nameof(contentType));
        }

        if (bytes.IsDefaultOrEmpty)
        {
            throw new ArgumentException("An attachment requires a non-empty payload.", nameof(bytes));
        }

        if (bytes.Length > MaximumBytes)
        {
            throw new ArgumentException(
                $"An attachment cannot exceed {MaximumBytes} bytes.", nameof(bytes));
        }

        Name = name.Trim();
        ContentType = contentType.Trim();
        Bytes = bytes;
    }

    public string Name { get; }

    public string ContentType { get; }

    public ImmutableArray<byte> Bytes { get; }
}

/// <summary>
/// The requested structured-output shape for a single invocation. <see cref="SchemaJson"/> blank with
/// <see cref="RequireJson"/> true asks for unconstrained JSON; a non-blank schema asks for schema-enforced JSON
/// where the provider supports it.
/// </summary>
public sealed record LlmResponseFormat(
    bool RequireJson,
    string SchemaJson = "",
    string SchemaName = "",
    string SchemaDescription = "");

/// <summary>
/// Model-level parameters for a single invocation. <see cref="ModelParameterConfigurationJson"/> carries the
/// existing provider-neutral model-parameter override envelope already understood by the provider drivers
/// (for example reasoning effort and max output tokens).
/// </summary>
public sealed record LlmModelSettings(
    double? Temperature = null,
    string ModelParameterConfigurationJson = "")
{
    public AgentReasoningEffortLevel? ThinkingEffort { get; init; }
}

/// <summary>
/// A single stateless, provider-neutral LLM invocation request. Contains only provider/model selection,
/// ordered messages, bounded attachments, response-format/model-parameter preferences, an optional
/// per-invocation deadline, and a caller correlation id for log-safe failure attribution. Deliberately
/// excludes agent, session, capability, workspace, authority, or process identifiers — payload text
/// (including any project id or path a caller embeds inside a message) is data, never an authority
/// selector. Messages and attachments are copied into immutable arrays at construction.
/// </summary>
public sealed record LlmInvocationRequest
{
    public const int MaximumMessages = 200;
    public const int MaximumAttachments = 16;
    public const long MaximumTotalAttachmentBytes = 64L * 1024 * 1024;
    public const int MaximumCorrelationIdLength = 128;
    public static readonly TimeSpan MaximumTimeout = TimeSpan.FromMinutes(30);

    public LlmInvocationRequest(
        ProviderProfile provider,
        string model,
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmAttachment>? attachments = null,
        LlmResponseFormat? responseFormat = null,
        LlmModelSettings? settings = null,
        TimeSpan? timeout = null,
        string correlationId = "")
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Model = string.IsNullOrWhiteSpace(model)
            ? throw new ArgumentException("A lightweight LLM invocation requires a non-blank model.", nameof(model))
            : model;
        if (messages is not { Count: > 0 })
        {
            throw new ArgumentException("A lightweight LLM invocation requires at least one message.", nameof(messages));
        }

        if (messages.Count > MaximumMessages)
        {
            throw new ArgumentException(
                $"A lightweight LLM invocation cannot carry more than {MaximumMessages} messages.", nameof(messages));
        }

        Messages = [.. messages.Select(message => message ?? throw new ArgumentException(
            "Messages cannot contain null entries.", nameof(messages)))];

        var normalizedAttachments = attachments ?? [];
        if (normalizedAttachments.Count > MaximumAttachments)
        {
            throw new ArgumentException(
                $"A lightweight LLM invocation cannot carry more than {MaximumAttachments} attachments.",
                nameof(attachments));
        }

        Attachments = [.. normalizedAttachments.Select(attachment => attachment ?? throw new ArgumentException(
            "Attachments cannot contain null entries.", nameof(attachments)))];
        var totalAttachmentBytes = Attachments.Sum(attachment => (long)attachment.Bytes.Length);
        if (totalAttachmentBytes > MaximumTotalAttachmentBytes)
        {
            throw new ArgumentException(
                $"Attachments cannot exceed {MaximumTotalAttachmentBytes} bytes in total.", nameof(attachments));
        }

        if (timeout is { } requestedTimeout &&
            (requestedTimeout <= TimeSpan.Zero || requestedTimeout > MaximumTimeout))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                requestedTimeout,
                $"An invocation timeout must be positive and at most {MaximumTimeout}.");
        }

        Timeout = timeout;
        var normalizedCorrelationId = correlationId?.Trim() ?? string.Empty;
        if (normalizedCorrelationId.Length > MaximumCorrelationIdLength)
        {
            throw new ArgumentException(
                $"A correlation id cannot exceed {MaximumCorrelationIdLength} characters.", nameof(correlationId));
        }

        CorrelationId = normalizedCorrelationId;
        ResponseFormat = responseFormat;
        Settings = settings;
    }

    public ProviderProfile Provider { get; }

    public string Model { get; }

    public ImmutableArray<LlmMessage> Messages { get; }

    public ImmutableArray<LlmAttachment> Attachments { get; }

    public LlmResponseFormat? ResponseFormat { get; init; }

    public LlmModelSettings? Settings { get; init; }

    /// <summary>Optional per-invocation deadline enforced by the port.</summary>
    public TimeSpan? Timeout { get; }

    /// <summary>Caller-supplied identifier echoed in typed failures; never interpreted.</summary>
    public string CorrelationId { get; }
}

/// <summary>
/// Token usage reported for a single lightweight LLM invocation.
/// </summary>
public sealed record LlmUsage
{
    public static LlmUsage Zero { get; } = new(0, 0, 0);

    public LlmUsage(int InputTokens, int OutputTokens, int CachedInputTokens = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(InputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(OutputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(CachedInputTokens);
        this.InputTokens = InputTokens;
        this.OutputTokens = OutputTokens;
        this.CachedInputTokens = CachedInputTokens;
    }

    public int InputTokens { get; }

    public int OutputTokens { get; }

    public int CachedInputTokens { get; }

    public LlmUsage Add(LlmUsage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new LlmUsage(
            checked(InputTokens + other.InputTokens),
            checked(OutputTokens + other.OutputTokens),
            checked(CachedInputTokens + other.CachedInputTokens));
    }
}

/// <summary>
/// The result of a single lightweight LLM invocation.
/// </summary>
public sealed record LlmInvocationResult(string Model, string ResponseText, LlmUsage Usage);

/// <summary>
/// Named failure classes for a stateless LLM invocation.
/// </summary>
public enum LlmInvocationFailureKind
{
    InvalidRequest,
    ProviderFailure,
    EmptyResponse,
    DeadlineExceeded
}

/// <summary>
/// Typed, sanitized failure for a lightweight LLM invocation. The message is
/// composed by the port from stable identifiers (failure kind, provider name,
/// model, correlation id) and never carries raw provider exception text or
/// response payloads; the original provider exception stays available as the
/// inner exception for structured logging only.
/// </summary>
public sealed class LlmInvocationException : Exception
{
    public LlmInvocationException(
        LlmInvocationFailureKind kind,
        string providerName,
        string model,
        string correlationId,
        Exception? innerException = null,
        LlmUsage? usage = null)
        : base(BuildMessage(kind, providerName, model, correlationId), innerException)
    {
        Kind = kind;
        ProviderName = providerName ?? string.Empty;
        Model = model ?? string.Empty;
        CorrelationId = correlationId ?? string.Empty;
        Usage = usage;
    }

    public LlmInvocationFailureKind Kind { get; }

    public string ProviderName { get; }

    public string Model { get; }

    public string CorrelationId { get; }

    public LlmUsage? Usage { get; }

    private static string BuildMessage(
        LlmInvocationFailureKind kind,
        string providerName,
        string model,
        string correlationId)
    {
        var correlationSuffix = string.IsNullOrWhiteSpace(correlationId)
            ? string.Empty
            : $" Correlation id: {correlationId}.";
        var description = kind switch
        {
            LlmInvocationFailureKind.InvalidRequest => "The lightweight LLM invocation request was invalid.",
            LlmInvocationFailureKind.EmptyResponse => "The provider returned no usable response text after the bounded retry.",
            LlmInvocationFailureKind.DeadlineExceeded => "The lightweight LLM invocation exceeded its deadline.",
            _ => "The provider request failed."
        };
        return $"{description} Provider '{providerName}', model '{model}'.{correlationSuffix}";
    }
}

/// <summary>
/// Stateless, provider-neutral single-turn LLM invocation boundary. Implementations must not construct agent
/// definitions or sessions, assemble tools/memory/context contributors, or infer authority/workspace scope
/// from message content. This is the lower boundary shared by ordinary workflow LLM nodes and any future
/// ordinary multi-turn chat feature. Failures surface as <see cref="LlmInvocationException"/>; raw provider
/// exceptions never propagate to callers.
/// </summary>
/// <remarks>
/// The ordinary multi-turn conversation layer above this port is <see cref="ILlmConversationService"/>: it
/// owns transcript persistence, conversation metadata, and context-window/summarization policy, delegating
/// every inference call to this stateless port. It is not implemented by constructing an agent with disabled
/// tools, and it does not depend on agent execution, capability composition, or MAF agent session types.
/// </remarks>
public interface ILlmInvocationPort
{
    Task<LlmInvocationResult> InvokeAsync(LlmInvocationRequest request, CancellationToken cancellationToken = default);
}

public enum LlmStreamingDeliveryMode
{
    Incremental,
    CompletedFallback
}

public abstract record LlmStreamingUpdate
{
    protected LlmStreamingUpdate(int attemptOrdinal)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attemptOrdinal, 1);
        AttemptOrdinal = attemptOrdinal;
    }

    public int AttemptOrdinal { get; }
}

public sealed record LlmStreamingAttemptStarted : LlmStreamingUpdate
{
    public LlmStreamingAttemptStarted(
        int attemptOrdinal,
        Guid providerId,
        ProviderKind providerKind,
        string model,
        LlmStreamingDeliveryMode deliveryMode,
        DateTimeOffset startedAtUtc) : base(attemptOrdinal)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(providerId, Guid.Empty);
        if (!Enum.IsDefined(providerKind))
        {
            throw new ArgumentOutOfRangeException(nameof(providerKind), providerKind, "Unknown provider kind.");
        }

        if (!Enum.IsDefined(deliveryMode))
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryMode), deliveryMode, "Unknown streaming delivery mode.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ProviderId = providerId;
        ProviderKind = providerKind;
        Model = model.Trim();
        DeliveryMode = deliveryMode;
        StartedAtUtc = startedAtUtc;
    }

    public Guid ProviderId { get; }

    public ProviderKind ProviderKind { get; }

    public string Model { get; }

    public LlmStreamingDeliveryMode DeliveryMode { get; }

    public DateTimeOffset StartedAtUtc { get; }
}

public sealed record LlmStreamingTextDelta : LlmStreamingUpdate
{
    public LlmStreamingTextDelta(
        int attemptOrdinal,
        string delta,
        long? providerSequence = null) : base(attemptOrdinal)
    {
        ArgumentException.ThrowIfNullOrEmpty(delta);
        if (providerSequence is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(providerSequence), providerSequence, "Provider sequence must be positive.");
        }

        Delta = delta;
        ProviderSequence = providerSequence;
    }

    public string Delta { get; }

    public long? ProviderSequence { get; }
}

public sealed record LlmStreamingCompleted : LlmStreamingUpdate
{
    public LlmStreamingCompleted(
        int attemptOrdinal,
        string model,
        string finishReason,
        LlmUsage usage,
        LlmStreamingDeliveryMode deliveryMode,
        DateTimeOffset completedAtUtc) : base(attemptOrdinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(finishReason);
        ArgumentNullException.ThrowIfNull(usage);
        if (!Enum.IsDefined(deliveryMode))
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryMode), deliveryMode, "Unknown streaming delivery mode.");
        }

        Model = model.Trim();
        FinishReason = finishReason.Trim();
        Usage = usage;
        AttemptUsage = usage;
        DeliveryMode = deliveryMode;
        CompletedAtUtc = completedAtUtc;
    }

    public string Model { get; }

    public string FinishReason { get; }

    public LlmUsage Usage { get; }

    public LlmUsage AttemptUsage { get; init; }

    public LlmStreamingDeliveryMode DeliveryMode { get; }

    public DateTimeOffset CompletedAtUtc { get; }
}

public sealed record LlmStreamingFailed : LlmStreamingUpdate
{
    public LlmStreamingFailed(
        int attemptOrdinal,
        LlmInvocationFailureKind failureKind,
        LlmUsage usage,
        bool retryScheduled,
        DateTimeOffset completedAtUtc) : base(attemptOrdinal)
    {
        if (!Enum.IsDefined(failureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "Unknown invocation failure kind.");
        }

        ArgumentNullException.ThrowIfNull(usage);
        FailureKind = failureKind;
        Usage = usage;
        AttemptUsage = usage;
        RetryScheduled = retryScheduled;
        CompletedAtUtc = completedAtUtc;
    }

    public LlmInvocationFailureKind FailureKind { get; }

    public LlmUsage Usage { get; }

    public LlmUsage AttemptUsage { get; init; }

    public bool RetryScheduled { get; }

    public DateTimeOffset CompletedAtUtc { get; }
}

public interface ILlmStreamingInvocationPort
{
    IAsyncEnumerable<LlmStreamingUpdate> StreamAsync(
        LlmInvocationRequest request,
        CancellationToken cancellationToken = default);
}
