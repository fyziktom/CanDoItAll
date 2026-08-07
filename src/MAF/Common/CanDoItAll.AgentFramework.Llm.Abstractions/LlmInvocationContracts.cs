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
/// </summary>
public sealed record LlmMessage(LlmMessageRole Role, string Text)
{
    public string Text { get; init; } = Text ?? throw new ArgumentNullException(nameof(Text));
}

/// <summary>
/// A bounded binary attachment (for example an image) supplied alongside an invocation request.
/// </summary>
public sealed record LlmAttachment(string Name, string ContentType, byte[] Bytes);

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
    string ModelParameterConfigurationJson = "");

/// <summary>
/// A single stateless, provider-neutral LLM invocation request. Contains only provider/model selection,
/// ordered messages, bounded attachments, response-format/model-parameter preferences. Deliberately excludes
/// agent, session, capability, workspace, authority, or process identifiers - payload text (including any
/// project id or path a caller embeds inside a message) is data, never an authority selector.
/// </summary>
public sealed record LlmInvocationRequest(
    ProviderProfile Provider,
    string Model,
    IReadOnlyList<LlmMessage> Messages,
    IReadOnlyList<LlmAttachment>? Attachments = null,
    LlmResponseFormat? ResponseFormat = null,
    LlmModelSettings? Settings = null)
{
    public ProviderProfile Provider { get; init; } = Provider ?? throw new ArgumentNullException(nameof(Provider));

    public string Model { get; init; } = string.IsNullOrWhiteSpace(Model)
        ? throw new ArgumentException("A lightweight LLM invocation requires a non-blank model.", nameof(Model))
        : Model;

    public IReadOnlyList<LlmMessage> Messages { get; init; } = Messages is { Count: > 0 }
        ? Messages
        : throw new ArgumentException("A lightweight LLM invocation requires at least one message.", nameof(Messages));
}

/// <summary>
/// Token usage reported for a single lightweight LLM invocation.
/// </summary>
public sealed record LlmUsage(int InputTokens, int OutputTokens, int CachedInputTokens = 0);

/// <summary>
/// The result of a single lightweight LLM invocation.
/// </summary>
public sealed record LlmInvocationResult(string Model, string ResponseText, LlmUsage Usage);

/// <summary>
/// Stateless, provider-neutral single-turn LLM invocation boundary. Implementations must not construct agent
/// definitions or sessions, assemble tools/memory/context contributors, or infer authority/workspace scope
/// from message content. This is the lower boundary shared by ordinary workflow LLM nodes and any future
/// ordinary multi-turn chat feature.
/// </summary>
/// <remarks>
/// Future extension point (not implemented by this subbundle - contract note only): an ordinary multi-turn LLM
/// conversation application service (for example a prospective <c>ILlmConversationService</c>) would own
/// transcript persistence, conversation metadata, and summarization/compaction policy, delegating every
/// inference call to this stateless port. It must not be implemented by constructing an agent with disabled
/// tools, and it must not depend on agent execution, capability composition, or MAF agent session types.
/// </remarks>
public interface ILlmInvocationPort
{
    Task<LlmInvocationResult> InvokeAsync(LlmInvocationRequest request, CancellationToken cancellationToken = default);
}
