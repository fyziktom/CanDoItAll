using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public sealed record ProviderModelCatalogRequest(
    ProviderProfile Provider,
    AgentProviderCapabilityKind Capability,
    string? ModelFilter = null);

public sealed record ProviderModelDescriptor(
    string Model,
    string DisplayName,
    AgentProviderCapabilityKind Capability,
    ProviderDispatchLimits DispatchLimits)
{
    public ProviderModelThinkingEffortCapability? ThinkingEffortCapability { get; init; }
}

public sealed record ProviderChatAttachment(
    string Name,
    string ContentType,
    byte[] Bytes);

public sealed record ProviderChatResponseFormat(
    bool RequireJson,
    string SchemaJson = "",
    string SchemaName = "",
    string SchemaDescription = "");

public sealed record ProviderChatCompletionRequest(
    ProviderProfile Provider,
    string Model,
    string SystemPrompt,
    IReadOnlyList<ProviderTestChatMessage> Messages,
    string Prompt,
    IReadOnlyList<ProviderChatAttachment>? Attachments = null,
    string ModelParameterConfigurationJson = "",
    double? Temperature = null,
    ProviderChatResponseFormat? ResponseFormat = null);

public sealed record ProviderChatCompletionResult(
    string Model,
    string ResponseText,
    int InputTokens,
    int OutputTokens)
{
    public int CachedInputTokens { get; init; }
}

public enum ProviderChatStreamingMode
{
    Incremental,
    CompletedFallback,
    Unsupported
}

public abstract record ProviderChatStreamingUpdate;

public sealed record ProviderChatTextDelta : ProviderChatStreamingUpdate
{
    public ProviderChatTextDelta(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        Text = text;
    }

    public string Text { get; }
}

public sealed record ProviderChatCompleted : ProviderChatStreamingUpdate
{
    public ProviderChatCompleted(
        string model,
        int inputTokens,
        int outputTokens,
        string finishReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokens);
        ArgumentException.ThrowIfNullOrWhiteSpace(finishReason);
        Model = model.Trim();
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        FinishReason = finishReason.Trim();
    }

    public string Model { get; }

    public int InputTokens { get; }

    public int OutputTokens { get; }

    public string FinishReason { get; }

    public int CachedInputTokens
    {
        get;
        init => field = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Cached input tokens cannot be negative.");
    }
}

public sealed record ProviderImageSource(
    string Name,
    string ContentType,
    byte[] Bytes);

public sealed record ProviderImageGenerationRequest(
    ProviderProfile Provider,
    string Model,
    string Prompt,
    string Size,
    string Quality,
    ProviderGeneratedImageFormat Format,
    IReadOnlyList<ProviderImageSource> Sources)
{
    public int? OutputCompression { get; init; }
}

public sealed record ProviderGeneratedImage(
    string ContentType,
    byte[] Bytes,
    string? RevisedPrompt = null);

public sealed record ProviderImageGenerationResult(
    string Model,
    ProviderGeneratedImageFormat Format,
    IReadOnlyList<ProviderGeneratedImage> Images);

public sealed record ProviderSpeechToTextAudio(
    string FileName,
    string ContentType,
    byte[] Bytes);

public sealed record ProviderSpeechToTextRequest(
    ProviderProfile Provider,
    string Model,
    IReadOnlyList<ProviderSpeechToTextAudio> Audio,
    string Language,
    string Prompt);

public sealed record ProviderSpeechToTextResult(
    string Model,
    string Text);

public sealed record ProviderTextToSpeechRequest(
    ProviderProfile Provider,
    string Model,
    string Text,
    string VoiceId,
    string ResponseFormat,
    string Instructions);

public sealed record ProviderTextToSpeechResult(
    string Model,
    string VoiceId,
    string ResponseFormat,
    string ContentType,
    byte[] AudioBytes);

public sealed record ProviderModelMaintenanceRequest(
    ProviderProfile Provider,
    string Model,
    string BaseModel,
    string SystemPrompt,
    int ContextLength);

public sealed record ProviderModelMaintenanceResult(
    string Model,
    string BaseModel,
    string SystemPrompt,
    int ContextLength,
    string Definition,
    string? StatusMessage);
