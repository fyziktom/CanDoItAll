using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit;

internal sealed class ProviderHistoryTestDriver : IProviderChatCompletionDriver, IProviderStreamingChatCompletionDriver,
    IProviderImageGenerationDriver, IProviderSpeechToTextDriver, IProviderTextToSpeechDriver,
    IProviderHealthDriver, IProviderModelCatalogDriver, IProviderModelMaintenanceDriver {
    public ProviderKind ProviderKind => ProviderKind.OpenAi;
    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities { get; } = Enum.GetValues<AgentProviderCapabilityKind>().ToHashSet();
    public int Calls { get; private set; }
    public Action? OnInvoke { get; set; }
    public bool EmptyFirstResponse { get; init; }
    public bool NoUsage { get; init; }
    public bool FailAfterStreamUsage { get; init; }
    public TaskCompletionSource? ContinueStream { get; init; }
    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query) => ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(5));
    public ProviderChatStreamingMode ResolveStreamingMode(ProviderChatCompletionRequest request) => ProviderChatStreamingMode.Incremental;

    public Task<ProviderChatCompletionResult> CompleteChatAsync(ProviderChatCompletionRequest request, CancellationToken cancellationToken = default) {
        Invoke();
        return Task.FromResult(new ProviderChatCompletionResult(request.Model, EmptyFirstResponse && Calls == 1 ? "" : "answer", 10, 5) {
            ObservedUsage = NoUsage ? null : new(HistoryUsageState.Complete, 10, 5, 0)
        });
    }

    public async IAsyncEnumerable<ProviderChatStreamingUpdate> StreamChatAsync(ProviderChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        Invoke();
        yield return new ProviderChatTextDelta("first");
        yield return new ProviderChatUsageObserved(new(HistoryUsageState.Complete, 10, 5, 0));
        if (ContinueStream is { } wait) {
            await wait.Task.WaitAsync(cancellationToken);
        }
        if (FailAfterStreamUsage) {
            throw new IOException("Fixture stream failure.");
        }
        yield return new ProviderChatCompleted(request.Model, 10, 5, "stop") {
            ObservedUsage = new(HistoryUsageState.Complete, 10, 5, 0)
        };
    }

    public Task<ProviderImageGenerationResult> GenerateImageAsync(ProviderImageGenerationRequest request, CancellationToken cancellationToken = default) {
        Invoke();
        return Task.FromResult(new ProviderImageGenerationResult(request.Model, request.Format, [new("image/png", [1, 2])]));
    }

    public Task<ProviderSpeechToTextResult> TranscribeSpeechAsync(ProviderSpeechToTextRequest request, CancellationToken cancellationToken = default) {
        Invoke();
        return Task.FromResult(new ProviderSpeechToTextResult(request.Model, "transcript"));
    }

    public Task<ProviderTextToSpeechResult> SynthesizeSpeechAsync(ProviderTextToSpeechRequest request, CancellationToken cancellationToken = default) {
        Invoke();
        return Task.FromResult(new ProviderTextToSpeechResult(request.Model, request.VoiceId, request.ResponseFormat, "audio/wav", [1, 2]));
    }

    public Task<ProviderHealthResult> TestHealthAsync(ProviderProfile provider, CancellationToken cancellationToken = default) {
        Invoke();
        return Task.FromResult(new ProviderHealthResult(true, "healthy", []));
    }

    public Task<IReadOnlyList<ProviderModelDescriptor>> ListModelsAsync(ProviderModelCatalogRequest request, CancellationToken cancellationToken = default) {
        Invoke();
        return Task.FromResult<IReadOnlyList<ProviderModelDescriptor>>([]);
    }

    public Task<ProviderModelMaintenanceResult> CreateOrUpdateModelAsync(ProviderModelMaintenanceRequest request, CancellationToken cancellationToken = default) {
        Invoke();
        return Task.FromResult(new ProviderModelMaintenanceResult(request.Model, request.BaseModel, request.SystemPrompt, 100, "definition", "updated"));
    }

    private void Invoke() {
        Calls++;
        OnInvoke?.Invoke();
    }

    public static ProviderProfile Provider() => new(Guid.NewGuid(), "History fixture", ProviderKind.OpenAi,
        "https://example.invalid/v1", "", "history-model", ProviderTransportKind.ChatCompletions,
        true, true, false, true, false, "{}", "", "", null, ["history-model"]);
}
