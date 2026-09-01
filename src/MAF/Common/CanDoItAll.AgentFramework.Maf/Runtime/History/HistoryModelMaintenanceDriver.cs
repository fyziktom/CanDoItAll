using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class HistoryModelMaintenanceDriver(IProviderModelMaintenanceDriver inner, IProviderHistoryRecorder recorder, TimeProvider clock) : IProviderModelMaintenanceDriver {
    public ProviderKind ProviderKind => inner.ProviderKind;
    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities => inner.Capabilities;
    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query) => inner.GetDispatchLimits(query);

    public async Task<ProviderModelMaintenanceResult> CreateOrUpdateModelAsync(ProviderModelMaintenanceRequest request, CancellationToken cancellationToken = default) {
        var observation = await ProviderHistoryObservation.BeginAsync(recorder, clock, request.Provider, request.Model,
            HistoryOperation.CreateOrUpdateModel, request.History, cancellationToken);
        return await observation.ExecuteAsync(() => inner.CreateOrUpdateModelAsync(request, cancellationToken),
            result => new(new(HistoryUsageState.Unavailable)), cancellationToken);
    }
}
