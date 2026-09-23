using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class HistorySynthesisDriver(IProviderTextToSpeechDriver inner, IProviderHistoryRecorder recorder, TimeProvider clock) : IProviderTextToSpeechDriver {
    public ProviderKind ProviderKind => inner.ProviderKind;
    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities => inner.Capabilities;
    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query) => inner.GetDispatchLimits(query);

    public async Task<ProviderTextToSpeechResult> SynthesizeSpeechAsync(ProviderTextToSpeechRequest request, CancellationToken cancellationToken = default) {
        var observation = await ProviderHistoryObservation.BeginAsync(recorder, clock, request.Provider, request.Model,
            HistoryOperation.SynthesizeSpeech, request.History, cancellationToken);
        return await observation.ExecuteAsync(() => inner.SynthesizeSpeechAsync(request, cancellationToken),
            result => new(new(HistoryUsageState.Unavailable)), cancellationToken);
    }
}
