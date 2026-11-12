using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentHistoricalReceiptReviewService(
    ResolvedDatabaseProfile profile,
    IDatabaseRuntimeState runtimeState,
    IOptions<StorageOptions> storageOptions,
    IHostEnvironment environment,
    IEnumerable<IAgentHistoricalReceiptSourceAuthorityProvider> sources,
    Func<AgentHistoricalCatalogBasis, AgentHistoricalReceiptReadParticipants> participants) : IAgentHistoricalReceiptReviewService {
    private readonly IReadOnlyList<IAgentHistoricalReceiptSourceAuthorityProvider> sourceReaders = sources.ToArray();

    internal static DatabaseRuntimeSnapshot RequireInitialized(IDatabaseRuntimeState? runtimeState) {
        var observed = runtimeState?.GetSnapshot();
        if (observed?.ActiveProfileId is null || observed.ActiveProfileId == Guid.Empty || string.IsNullOrWhiteSpace(observed.ActiveFingerprint)) {
            throw new NotSupportedException("Historical receipt review requires prior application profile initialization.");
        }
        return observed;
    }

    public async Task<AgentHistoricalReceiptReview> ReadSavedEffectReceiptsAsync(AgentHistoricalReceiptReviewRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        var snapshot = RequireInitialized(runtimeState);
        var store = new CanonicalAgentHistoricalReadStore(profile, snapshot, runtimeState, storageOptions, environment);
        var fence = store.Fence;
        var basis = new AgentHistoricalCatalogBasis(store, fence, () => store.IsCurrent(fence));
        try {
            var selected = participants(basis);
            var currentRead = new CanonicalAgentHistoricalReceiptReadObserver(basis, sourceReaders);
            var result = await AgentToolAdmissionJournal.ReadHistoricalReceiptReviewAsync(store, fence.Profile, request,
                fence.Scope, currentRead.ObserveAsync, selected.Sources, selected.Providers, basis, cancellationToken);
            if (!store.IsCurrent(fence) || result.CurrentContext != fence.Context) {
                return new(request, fence.Context, null, null, AgentHistoricalReceiptReviewStop.ContextChanged, []);
            }
            if (result.Request != request) {
                throw new InvalidDataException("Historical receipt review returned a different original reference or page.");
            }
            return result;
        } finally {
            basis.Retire();
        }
    }
}
