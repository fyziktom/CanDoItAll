using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class HistoryHealthDriver(IProviderHealthDriver inner, IProviderHistoryRecorder recorder, TimeProvider clock) : IProviderHealthDriver {
    public ProviderKind ProviderKind => inner.ProviderKind;
    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities => inner.Capabilities;
    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query) => inner.GetDispatchLimits(query);

    public async Task<ProviderHealthResult> TestHealthAsync(ProviderProfile provider, CancellationToken cancellationToken = default) {
        var observation = await ProviderHistoryObservation.BeginAsync(recorder, clock, provider, provider.DefaultModel,
            HistoryOperation.TestHealth, HistoryInvocationContext.Create(HistoryWorkload.Diagnostic), cancellationToken);
        return await observation.ExecuteAsync(() => inner.TestHealthAsync(provider, cancellationToken),
            result => new(new(HistoryUsageState.Unavailable)) {
                Outcome = result.Success ? HistoryOutcome.Succeeded : HistoryOutcome.Failed
            }, cancellationToken);
    }
}
