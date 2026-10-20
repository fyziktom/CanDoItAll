using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessToolBackgroundSourcePolicy(
    EfProcessExecutionAuthorityQuery owner,
    ICanonicalRuntimeDatabase database,
    IAgentExecutionProfileGenerationSource generations,
    IProcessSourceAuthorityObservationPolicy sourcePolicy,
    IEnumerable<ProcessDecisionExecutionRegistration>? selectedContracts = null) : IAgentToolBackgroundReservationPolicy {
    private readonly IReadOnlyDictionary<ProcessDecisionExecutionContract, ProcessDecisionContentContract?> contracts =
        (selectedContracts ?? []).ToDictionary(item => item.Execution, item => item.Content);
    public string SourceKind => ProcessMockAgentCatalog.ProcessSourceKind;

    public async ValueTask<AgentToolBackgroundReservationObservation> ObserveReservationAsync(ExecutionRunRecord candidate,
        AgentToolProfileBinding profile, CancellationToken cancellationToken = default) {
        if (candidate.ToolAdmission?.SchemaVersion == AgentToolJournalRecord.InheritedPlanningSchemaVersion) {
            throw new NotSupportedException("An inherited Process child uses its exact child admission, not an ordinary background reservation.");
        }
        var current = await ObserveAsync(candidate, profile, cancellationToken);
        if (!current.ReadAllowed || !current.DispatchAllowed) {
            throw new ProcessExecutionAuthorityMismatchException("The current Process source cannot reserve an execution.");
        }
        var evidence = ProcessExecutionClaimEvidenceReader.Read(candidate, out _)
            ?? throw new ProcessExecutionAuthorityMismatchException("The Process reservation has no exact claim evidence.");
        var acknowledgement = await owner.ReadReservationAsync(evidence, cancellationToken);
        RequireCurrent(profile);
        cancellationToken.ThrowIfCancellationRequested();
        if (AgentToolProtocolEnvelope.ComputeDigest(acknowledgement.Dispatch.OwnerFingerprint) != current.OwnerFingerprint) {
            throw new ProcessExecutionAuthorityMismatchException("The Process claim changed while reading its accepted outcomes.");
        }
        return new(current.OwnerFingerprint, acknowledgement.AcknowledgedExecutionRunIds);
    }

    public ValueTask<AgentToolBackgroundSourceObservation> ObserveInitialInheritedAsync(ExecutionRunRecord candidate,
        AgentToolProfileBinding profile, AgentToolProtocolEnvelope originalParent, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(originalParent);
        if (candidate.ToolAdmission is not null) {
            throw new ProcessExecutionAuthorityMismatchException("Initial inherited source observation requires an unadmitted child.");
        }
        return ObserveCoreAsync(candidate, profile, originalParent, cancellationToken);
    }

    public ValueTask<AgentToolBackgroundSourceObservation> ObserveAsync(ExecutionRunRecord execution,
        AgentToolProfileBinding profile, CancellationToken cancellationToken = default)
        => ObserveCoreAsync(execution, profile, null, cancellationToken);

    private async ValueTask<AgentToolBackgroundSourceObservation> ObserveCoreAsync(ExecutionRunRecord execution,
        AgentToolProfileBinding profile, AgentToolProtocolEnvelope? originalParent, CancellationToken cancellationToken) {
        RequireCurrent(profile);
        try {
            var evidence = ProcessExecutionClaimEvidenceReader.Read(execution, out var disposition)
                ?? throw new ProcessExecutionAuthorityMismatchException($"The Process execution has no trusted dispatch binding ({disposition}).");
            var result = await ReadSourceAsync(execution, profile, evidence, originalParent, cancellationToken);
            RequireCurrent(profile);
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = result.Snapshot ?? throw new ProcessExecutionAuthorityMismatchException(
                $"The Process execution no longer matches its saved dispatch binding ({result.Disposition}).");
            if (snapshot.Evidence != evidence) {
                throw new ProcessExecutionAuthorityMismatchException("The observed Process source differs from the exact execution evidence.");
            }
            var access = new ProcessSourceAuthorityObservation(true, true);
            if (snapshot.SourceAuthority is { } authority) {
                if (authority.DatabaseProfileId != profile.ProfileId) {
                    throw new ProcessExecutionAuthorityMismatchException("The saved Process source belongs to a different runtime profile.");
                }
                access = await sourcePolicy.ObserveAsync(authority, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new(AgentToolProtocolEnvelope.ComputeDigest(snapshot.OwnerFingerprint), access.ReadAllowed,
                snapshot.ObservedCurrentDispatch && access.DispatchAllowed);
        } finally {
            RequireCurrent(profile);
        }
    }

    private async Task<ProcessExecutionDispatchAuthorityResult> ReadSourceAsync(ExecutionRunRecord execution, AgentToolProfileBinding profile,
        ProcessExecutionClaimEvidence evidence, AgentToolProtocolEnvelope? originalParent, CancellationToken cancellationToken) {
        var inherited = execution.ToolAdmission?.SchemaVersion == AgentToolJournalRecord.InheritedPlanningSchemaVersion;
        if (originalParent is null && !inherited) {
            return await owner.ReadDispatchAsync(evidence, cancellationToken);
        }
        if (inherited && execution.ToolAdmission!.Session.Profile != profile) {
            throw new ProcessExecutionAuthorityMismatchException("The inherited Process journal belongs to a different original runtime profile.");
        }
        var selected = originalParent is null ? ProcessInheritedSourceBinding.Read(execution, evidence, contracts)
            : ProcessInheritedSourceBinding.ReadInitial(execution, profile, evidence, originalParent, contracts);
        var result = await owner.ReadDecisionDispatchAsync(evidence, selected.Parent, selected.Parent.ExecutionContract, selected.Content, cancellationToken);
        if (inherited) {
            ProcessInheritedSourceBinding.RequireOriginalPolicy(execution.ToolAdmission!.PlanningDecision!.InheritedOrigin!, result);
        }
        if (result.Snapshot is { SourceAuthority: null }) {
            throw new ProcessExecutionAuthorityMismatchException("An inherited Process child requires its original accepted source authority.");
        }
        return result;
    }

    private void RequireCurrent(AgentToolProfileBinding profile) {
        if (profile.ProfileId != database.Profile.Profile.Id || profile.Fingerprint != database.Profile.Profile.Runtime.Fingerprint ||
            profile.Generation != generations.GetGeneration()) {
            throw new ProcessExecutionAuthorityMismatchException("The Process execution belongs to a different runtime profile or generation.");
        }
    }
}
