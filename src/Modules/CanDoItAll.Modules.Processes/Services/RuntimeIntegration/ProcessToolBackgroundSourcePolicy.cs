using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessToolBackgroundSourcePolicy(
    EfProcessExecutionAuthorityQuery owner,
    ICanonicalRuntimeDatabase database,
    IAgentExecutionProfileGenerationSource generations,
    IProcessSourceAuthorityObservationPolicy sourcePolicy) : IAgentToolBackgroundReservationPolicy {
    public string SourceKind => ProcessMockAgentCatalog.ProcessSourceKind;

    public async ValueTask<AgentToolBackgroundReservationObservation> ObserveReservationAsync(ExecutionRunRecord candidate,
        AgentToolProfileBinding profile, CancellationToken cancellationToken = default) {
        var current = await ObserveAsync(candidate, profile, cancellationToken);
        if (!current.ReadAllowed || !current.DispatchAllowed) {
            throw new ProcessExecutionAuthorityMismatchException("The current Process source cannot reserve an execution.");
        }
        var evidence = ProcessExecutionClaimEvidenceReader.Read(candidate, out _)
            ?? throw new ProcessExecutionAuthorityMismatchException("The Process reservation has no exact claim evidence.");
        var acknowledgement = await owner.ReadReservationAsync(evidence, cancellationToken);
        if (AgentToolProtocolEnvelope.ComputeDigest(acknowledgement.Dispatch.OwnerFingerprint) != current.OwnerFingerprint) {
            throw new ProcessExecutionAuthorityMismatchException("The Process claim changed while reading its accepted outcomes.");
        }
        return new(current.OwnerFingerprint, acknowledgement.AcknowledgedExecutionRunIds);
    }

    public async ValueTask<AgentToolBackgroundSourceObservation> ObserveAsync(ExecutionRunRecord execution,
        AgentToolProfileBinding profile, CancellationToken cancellationToken = default) {
        if (profile.ProfileId != database.Profile.Profile.Id || profile.Fingerprint != database.Profile.Profile.Runtime.Fingerprint ||
                profile.Generation != generations.GetGeneration()) {
            throw new ProcessExecutionAuthorityMismatchException("The Process execution belongs to a different runtime profile or generation.");
        }
        var evidence = ProcessExecutionClaimEvidenceReader.Read(execution, out var disposition)
            ?? throw new ProcessExecutionAuthorityMismatchException($"The Process execution has no trusted dispatch binding ({disposition}).");
        var result = await owner.ReadDispatchAsync(evidence, cancellationToken);
        var snapshot = result.Snapshot ?? throw new ProcessExecutionAuthorityMismatchException(
            $"The Process execution no longer matches its saved dispatch binding ({result.Disposition}).");
        if (snapshot.SourceAuthority is { } authority) {
            if (authority.DatabaseProfileId != profile.ProfileId) {
                throw new ProcessExecutionAuthorityMismatchException("The saved Process source belongs to a different runtime profile.");
            }
            var access = await sourcePolicy.ObserveAsync(authority, cancellationToken);
            return new(AgentToolProtocolEnvelope.ComputeDigest(snapshot.OwnerFingerprint), access.ReadAllowed,
                snapshot.ObservedCurrentDispatch && access.DispatchAllowed);
        }
        return new(AgentToolProtocolEnvelope.ComputeDigest(snapshot.OwnerFingerprint), true, snapshot.ObservedCurrentDispatch);
    }
}
