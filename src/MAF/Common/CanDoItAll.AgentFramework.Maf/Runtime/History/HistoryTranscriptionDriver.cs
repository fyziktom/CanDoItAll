using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class HistoryTranscriptionDriver(IProviderSpeechToTextDriver inner, IProviderHistoryRecorder recorder, TimeProvider clock) : IProviderSpeechToTextDriver {
    public ProviderKind ProviderKind => inner.ProviderKind;
    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities => inner.Capabilities;
    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query) => inner.GetDispatchLimits(query);

    public async Task<ProviderSpeechToTextResult> TranscribeSpeechAsync(ProviderSpeechToTextRequest request, CancellationToken cancellationToken = default) {
        var observation = await ProviderHistoryObservation.BeginAsync(recorder, clock, request.Provider, request.Model,
            HistoryOperation.TranscribeSpeech, request.History, cancellationToken);
        return await observation.ExecuteAsync(() => inner.TranscribeSpeechAsync(request, cancellationToken),
            result => new(new(HistoryUsageState.Unavailable), result.Text), cancellationToken);
    }
}
