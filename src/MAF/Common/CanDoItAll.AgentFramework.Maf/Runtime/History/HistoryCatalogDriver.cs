using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class HistoryCatalogDriver(IProviderModelCatalogDriver inner, IProviderHistoryRecorder recorder, TimeProvider clock) : IProviderModelCatalogDriver {
    public ProviderKind ProviderKind => inner.ProviderKind;
    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities => inner.Capabilities;
    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query) => inner.GetDispatchLimits(query);

    public async Task<IReadOnlyList<ProviderModelDescriptor>> ListModelsAsync(ProviderModelCatalogRequest request, CancellationToken cancellationToken = default) {
        var observation = await ProviderHistoryObservation.BeginAsync(recorder, clock, request.Provider, null,
            HistoryOperation.ListModels, request.History, cancellationToken);
        return await observation.ExecuteAsync(() => inner.ListModelsAsync(request, cancellationToken),
            result => new(new(HistoryUsageState.Unavailable)), cancellationToken);
    }
}
