using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// An earlier message of a provider test chat, sent to the provider before the prompt.
/// </summary>
/// <param name="Role">
/// Author of the message, as a JSON integer: 0 System, 1 User, 2 Assistant; other values are sent as a user message.
/// </param>
/// <param name="Content">Text of the message, trimmed; a blank message is left out.</param>
/// <param name="CreatedAtUtc">When the message was written; messages are sent in the order of this time.</param>
public sealed record ProviderTestChatMessage(
    ChatMessageRole Role,
    string Content,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Conversation to send through a provider profile with
/// <c>POST /api/agents/providers/{providerId}/test-chat</c>. It is sent directly to the provider; no agent, chat
/// session or execution run is involved. The prompt or at least one message is required.
/// </summary>
/// <param name="Model">
/// Model to use; blank uses the profile's default model, then its first suggested model.
/// </param>
/// <param name="SystemPrompt">
/// System instructions; blank uses a short built-in instruction for validating a provider profile.
/// </param>
/// <param name="Messages">
/// Earlier messages to send before the prompt, ordered by their <c>createdAtUtc</c>; may be empty.
/// </param>
/// <param name="Prompt">The new user message; may be blank when <c>messages</c> is not empty.</param>
public sealed record ProviderTestChatRequest(
    string Model,
    string SystemPrompt,
    IReadOnlyList<ProviderTestChatMessage> Messages,
    string Prompt) {
    [JsonIgnore]
    public HistoryInvocationContext History { get; init; } =
        HistoryInvocationContext.Create(currentTurn: new(Prompt, 0));
}

/// <summary>
/// Reply to a provider test chat.
/// </summary>
/// <param name="Model">Model that answered, as reported for the request.</param>
/// <param name="ResponseText">The model's reply; never empty (an empty reply fails the request).</param>
/// <param name="InputTokens">Input tokens reported by the provider; 0 when not reported.</param>
/// <param name="OutputTokens">Output tokens reported by the provider; 0 when not reported.</param>
public sealed record ProviderTestChatResult(
    string Model,
    string ResponseText,
    int InputTokens,
    int OutputTokens);

/// <summary>
/// Request to create or update a derived model on an Ollama server of a provider profile, sent to
/// <c>POST /api/agents/providers/{providerId}/ollama-modelfile</c>. The server asks the Ollama server to create the
/// target model from the base model with the system prompt and the context length.
/// </summary>
/// <param name="BaseModel">Existing model to derive from; required, trimmed.</param>
/// <param name="TargetModel">Name of the model to create or replace on the Ollama server; required, trimmed.</param>
/// <param name="SystemPrompt">System prompt built into the model; required, trimmed.</param>
/// <param name="ContextLength">Context window of the model in tokens, from 2048 through 262144.</param>
public sealed record ProviderModelMaintenanceEditorRequest(
    string BaseModel,
    string TargetModel,
    string SystemPrompt,
    int ContextLength);

/// <summary>
/// Result of creating or updating a derived model on an Ollama server.
/// </summary>
/// <param name="ModelName">Name of the created or updated model.</param>
/// <param name="BaseModel">Model it was derived from.</param>
/// <param name="SystemPrompt">System prompt built into the model.</param>
/// <param name="ContextLength">Context window of the model in tokens.</param>
/// <param name="Modelfile">
/// Ollama Modelfile text equivalent to the request, rendered by this server for display; the Ollama server receives
/// the settings directly.
/// </param>
/// <param name="StatusMessage">Status reported by the Ollama server; empty when it reported none.</param>
public sealed record ProviderModelMaintenanceEditorResult(
    string ModelName,
    string BaseModel,
    string SystemPrompt,
    int ContextLength,
    string Modelfile,
    string? StatusMessage);
