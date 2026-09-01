using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Models;

public sealed record ProviderTestChatMessage(
    ChatMessageRole Role,
    string Content,
    DateTimeOffset CreatedAtUtc);

public sealed record ProviderTestChatRequest(
    string Model,
    string SystemPrompt,
    IReadOnlyList<ProviderTestChatMessage> Messages,
    string Prompt) {
    [JsonIgnore]
    public HistoryInvocationContext History { get; init; } =
        HistoryInvocationContext.Create(currentTurn: new(Prompt, 0));
}

public sealed record ProviderTestChatResult(
    string Model,
    string ResponseText,
    int InputTokens,
    int OutputTokens);

public sealed record ProviderModelMaintenanceEditorRequest(
    string BaseModel,
    string TargetModel,
    string SystemPrompt,
    int ContextLength);

public sealed record ProviderModelMaintenanceEditorResult(
    string ModelName,
    string BaseModel,
    string SystemPrompt,
    int ContextLength,
    string Modelfile,
    string? StatusMessage);
