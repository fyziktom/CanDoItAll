using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public enum ProviderGeneratedImageFormat
{
    Png,
    Jpeg,
    Webp
}

public sealed record ProviderCapabilityDescriptor(
    AgentProviderCapabilityKind Capability,
    IReadOnlySet<AgentProviderOperationKind> Operations);

public interface IAgentProviderDriver
{
    ProviderKind ProviderKind { get; }

    IReadOnlySet<AgentProviderCapabilityKind> Capabilities { get; }

    ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query);
}

public interface IProviderHealthDriver : IAgentProviderDriver
{
    Task<ProviderHealthResult> TestHealthAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default);
}

public interface IProviderModelCatalogDriver : IAgentProviderDriver
{
    Task<IReadOnlyList<ProviderModelDescriptor>> ListModelsAsync(
        ProviderModelCatalogRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProviderChatCompletionDriver : IAgentProviderDriver
{
    Task<ProviderChatCompletionResult> CompleteChatAsync(
        ProviderChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProviderStreamingChatCompletionDriver : IAgentProviderDriver
{
    ProviderChatStreamingMode ResolveStreamingMode(ProviderChatCompletionRequest request);

    IAsyncEnumerable<ProviderChatStreamingUpdate> StreamChatAsync(
        ProviderChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProviderImageGenerationDriver : IAgentProviderDriver
{
    Task<ProviderImageGenerationResult> GenerateImageAsync(
        ProviderImageGenerationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProviderSpeechToTextDriver : IAgentProviderDriver
{
    Task<ProviderSpeechToTextResult> TranscribeSpeechAsync(
        ProviderSpeechToTextRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProviderTextToSpeechDriver : IAgentProviderDriver
{
    Task<ProviderTextToSpeechResult> SynthesizeSpeechAsync(
        ProviderTextToSpeechRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProviderModelMaintenanceDriver : IAgentProviderDriver
{
    Task<ProviderModelMaintenanceResult> CreateOrUpdateModelAsync(
        ProviderModelMaintenanceRequest request,
        CancellationToken cancellationToken = default);
}
