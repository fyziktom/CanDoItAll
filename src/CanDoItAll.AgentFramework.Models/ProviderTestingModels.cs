namespace CanDoItAll.AgentFramework.Models;

public sealed record ProviderTestChatMessage(
    ChatMessageRole Role,
    string Content,
    DateTimeOffset CreatedAtUtc);

public sealed record ProviderTestChatRequest(
    string Model,
    string SystemPrompt,
    IReadOnlyList<ProviderTestChatMessage> Messages,
    string Prompt);

public sealed record ProviderTestChatResult(
    string Model,
    string ResponseText,
    int InputTokens,
    int OutputTokens);

public sealed record OllamaModelfileRequest(
    string BaseModel,
    string TargetModel,
    string SystemPrompt,
    int ContextLength);

public sealed record OllamaModelfileResult(
    string ModelName,
    string BaseModel,
    string SystemPrompt,
    int ContextLength,
    string Modelfile,
    string? StatusMessage);
