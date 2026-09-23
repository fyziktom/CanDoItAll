using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class HistoryChatDriver(IProviderChatCompletionDriver inner, IProviderHistoryRecorder recorder, TimeProvider clock) : IProviderChatCompletionDriver {
    public ProviderKind ProviderKind => inner.ProviderKind;
    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities => inner.Capabilities;
    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query) => inner.GetDispatchLimits(query);

    public async Task<ProviderChatCompletionResult> CompleteChatAsync(ProviderChatCompletionRequest request, CancellationToken cancellationToken = default) {
        var observation = await ProviderHistoryObservation.BeginAsync(recorder, clock, request.Provider, request.Model,
            request.Attachments is { Count: > 0 } ? HistoryOperation.AnalyzeImage : HistoryOperation.CompleteChat, request.History, cancellationToken);
        return await observation.ExecuteAsync(() => inner.CompleteChatAsync(request, cancellationToken),
            result => new(result.ObservedUsage ?? new(HistoryUsageState.Unavailable), result.ResponseText, RemoteRequest: result.RemoteRequest), cancellationToken);
    }
}
