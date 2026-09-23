using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class HistoryImageDriver(IProviderImageGenerationDriver inner, IProviderHistoryRecorder recorder, TimeProvider clock) : IProviderImageGenerationDriver {
    public ProviderKind ProviderKind => inner.ProviderKind;
    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities => inner.Capabilities;
    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query) => inner.GetDispatchLimits(query);

    public async Task<ProviderImageGenerationResult> GenerateImageAsync(ProviderImageGenerationRequest request, CancellationToken cancellationToken = default) {
        if (request.History.Owner?.Kind == HistorySourceKind.SharedRelay) {
            return await inner.GenerateImageAsync(request, cancellationToken);
        }
        var observation = await ProviderHistoryObservation.BeginAsync(recorder, clock, request.Provider, request.Model,
            request.Sources.Count > 0 ? HistoryOperation.EditImage : HistoryOperation.GenerateImage, request.History, cancellationToken);
        return await observation.ExecuteAsync(() => inner.GenerateImageAsync(request, cancellationToken),
            result => new(new(HistoryUsageState.Complete, ImageCount: result.Images.Count)), cancellationToken);
    }
}
