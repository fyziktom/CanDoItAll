using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessHistoricalReceiptSourcePolicy(
    IProcessOriginalReceiptAuthorityReader owner,
    ICanonicalRuntimeDatabase database,
    IAgentExecutionProfileGenerationSource generations,
    IProcessHistoricalSourceAuthorityObserver sourcePolicy) : IAgentToolHistoricalBackgroundSourcePolicy {
    public string SourceKind => ProcessMockAgentCatalog.ProcessSourceKind;

    public async ValueTask<AgentToolBackgroundSourceObservation> ObserveHistoricalReadAsync(ExecutionRunRecord execution,
        AgentHistoricalReceiptReadFence currentFence, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(currentFence);
        void RequireCurrent() {
            if (currentFence.Profile.ProfileId != database.Profile.Profile.Id ||
                currentFence.Profile.Fingerprint != database.Profile.Profile.Runtime.Fingerprint ||
                currentFence.Profile.Generation != generations.GetGeneration()) {
                throw new AgentHistoricalReceiptContextChangedException();
            }
        }
        RequireCurrent();
        try {
            var evidence = ProcessExecutionClaimEvidenceReader.Read(execution, out _)
                ?? throw new AgentToolReceiptAccessDeniedException();
            var result = await owner.ObserveOriginalForReceiptReadAsync(evidence, cancellationToken);
            if (result.Snapshot is not { } snapshot || snapshot.Evidence != evidence ||
                snapshot.SourceAuthority is not { } authority || authority.DatabaseProfileId != currentFence.Profile.ProfileId) {
                throw new AgentToolReceiptAccessDeniedException();
            }
            var profile = currentFence.Profile;
            var access = await sourcePolicy.ObserveHistoricalReadAsync(authority,
                new(profile.ProfileId, profile.Fingerprint, profile.Generation.Value), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new(AgentToolProtocolEnvelope.ComputeDigest(snapshot.OwnerFingerprint), access.ReadAllowed, DispatchAllowed: false);
        } finally {
            RequireCurrent();
        }
    }
}
