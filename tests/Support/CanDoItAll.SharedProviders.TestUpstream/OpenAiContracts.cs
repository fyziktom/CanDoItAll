using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.SharedProviders.TestUpstream;

public sealed record ModelListResponse(string Object, IReadOnlyList<ModelDescriptor> Data);

public sealed record ModelDescriptor(string Id, string Object, long Created, string OwnedBy);

public sealed record ChatCompletionRequest(
    string Model,
    IReadOnlyList<ChatMessageRequest> Messages,
    bool Stream = false,
    IReadOnlyList<ChatToolRequest>? Tools = null,
    JsonElement? ResponseFormat = null);

public sealed record ChatMessageRequest(string Role, JsonElement Content);

public sealed record ChatToolRequest(string Type, ChatFunctionDefinition Function);

public sealed record ChatFunctionDefinition(
    string Name,
    string? Description = null,
    JsonElement? Parameters = null);

public sealed record ChatCompletionResponse(
    string Id,
    string Object,
    long Created,
    string Model,
    IReadOnlyList<ChatChoice> Choices,
    TokenUsage Usage);

public sealed record ChatChoice(
    int Index,
    ChatAssistantMessage Message,
    string FinishReason);

public sealed record ChatAssistantMessage(
    string Role,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Content,
    IReadOnlyList<ChatToolCall>? ToolCalls = null);

public sealed record ChatToolCall(
    string Id,
    string Type,
    ChatFunctionCall Function);

public sealed record ChatFunctionCall(string Name, string Arguments);

public sealed record TokenUsage(int PromptTokens, int CompletionTokens, int TotalTokens);

public sealed record ChatStreamChunk(
    string Id,
    string Object,
    long Created,
    string Model,
    IReadOnlyList<ChatStreamChoice> Choices,
    TokenUsage? Usage = null);

public sealed record ChatStreamChoice(
    int Index,
    ChatStreamDelta Delta,
    string? FinishReason = null);

public sealed record ChatStreamDelta(
    string? Role = null,
    string? Content = null,
    IReadOnlyList<ChatStreamToolCall>? ToolCalls = null);

public sealed record ChatStreamToolCall(
    int Index,
    string? Id = null,
    string? Type = null,
    ChatStreamFunctionCall? Function = null);

public sealed record ChatStreamFunctionCall(string? Name = null, string? Arguments = null);

public sealed record ResponsesRequest(
    string Model,
    JsonElement Input,
    bool Stream = false,
    string? Instructions = null,
    IReadOnlyList<ResponseToolRequest>? Tools = null,
    JsonElement? Text = null);

public sealed record ResponseToolRequest(
    string Type,
    string Name,
    string? Description = null,
    JsonElement? Parameters = null);

public sealed record ResponsesResponse(
    string Id,
    string Object,
    long CreatedAt,
    string Status,
    string Model,
    IReadOnlyList<ResponseOutputItem> Output,
    ResponseUsage Usage);

public sealed record ResponseOutputItem(
    string Id,
    string Type,
    string Status,
    string? Role = null,
    IReadOnlyList<ResponseOutputContent>? Content = null,
    string? CallId = null,
    string? Name = null,
    string? Arguments = null);

public sealed record ResponseOutputContent(
    string Type,
    string Text,
    IReadOnlyList<JsonElement> Annotations);

public sealed record ResponseUsage(int InputTokens, int OutputTokens, int TotalTokens);

public sealed record ResponseSummary(
    string Id,
    string Object,
    long CreatedAt,
    string Status,
    string Model,
    IReadOnlyList<ResponseOutputItem> Output,
    ResponseUsage? Usage);

public sealed record ResponseCreatedEvent(
    string Type,
    int SequenceNumber,
    ResponseSummary Response);

public sealed record ResponseOutputTextDeltaEvent(
    string Type,
    int SequenceNumber,
    int OutputIndex,
    int ContentIndex,
    string Delta);

public sealed record ResponseCompletedEvent(
    string Type,
    int SequenceNumber,
    ResponsesResponse Response);

public sealed record ImageGenerationRequest(
    string Model,
    string Prompt,
    int N = 1,
    string Size = "1024x1024",
    string ResponseFormat = "b64_json",
    string OutputFormat = "png");

public sealed record ImageGenerationResponse(
    long Created,
    IReadOnlyList<ImageGenerationData> Data);

public sealed record ImageGenerationData(string B64Json, string RevisedPrompt);
