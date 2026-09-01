using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentGeneratedImageFormat
{
    Png,
    Jpeg,
    Webp
}

public sealed record AgentImageGenerationSource(
    string Name,
    string ContentType,
    byte[] Bytes,
    string Summary);

public sealed record AgentImageGenerationRequest(
    ProviderProfile Provider,
    string Model,
    string Prompt,
    string Size,
    string Quality,
    AgentGeneratedImageFormat Format,
    IReadOnlyList<AgentImageGenerationSource> Sources)
{
    public int? OutputCompression { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public CanDoItAll.AgentFramework.ProviderHistory.HistoryInvocationContext History { get; init; } =
        CanDoItAll.AgentFramework.ProviderHistory.HistoryInvocationContext.Create(currentTurn: new(Prompt, 0));
}

public sealed record AgentGeneratedImage(
    string ContentType,
    byte[] Bytes,
    string? RevisedPrompt = null);

public sealed record AgentImageGenerationResult(
    string Model,
    AgentGeneratedImageFormat Format,
    IReadOnlyList<AgentGeneratedImage> Images);

public interface IAgentImageGenerationService
{
    Task<AgentImageGenerationResult> GenerateAsync(
        AgentImageGenerationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableAgentImageGenerationService : IAgentImageGenerationService
{
    public Task<AgentImageGenerationResult> GenerateAsync(
        AgentImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Image generation requires a provider-runtime image generation service.");
    }
}
