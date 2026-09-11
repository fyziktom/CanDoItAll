using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.AgentFramework;

public enum AssetRecoveryCheckpointState {
    Pending,
    Completed,
    Unavailable
}

public sealed record AgentAssetCheckpointIdentity(Guid ExecutionRunId, Guid IntentId);

public enum AgentAssetCancelledReceiptState {
    Ready,
    ReceiptRecorded,
    OriginalProposalUnavailable,
    OriginalRunNotCancelled
}

public sealed class AgentAssetCheckpointQuery {
    private readonly FileSandboxWorkspaceStore store;
    private readonly ResolvedDatabaseProfile profile;
    private readonly long generation;

    public AgentAssetCheckpointQuery(ICanonicalRuntimeDatabase database, IOptions<StorageOptions> storageOptions,
        IHostEnvironment environment) {
        profile = database.Profile;
        generation = database.Generation;
        var paths = new WorkspacePathResolver(storageOptions, environment, new BoundProfile(profile));
        store = new(paths.ResolveWorkspaceRoot(), WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N")));
    }

    public async Task<IReadOnlyDictionary<AgentAssetCheckpointIdentity, AssetRecoveryCheckpointState>> ReadAsync(
        IReadOnlyCollection<AgentAssetCheckpointIdentity> identities, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identities);
        if (identities.Count > 128 || identities.Any(item => item.ExecutionRunId == Guid.Empty || item.IntentId == Guid.Empty)) {
            throw new ArgumentException("Asset recovery observes at most 128 exact execution intents.", nameof(identities));
        }
        var result = new Dictionary<AgentAssetCheckpointIdentity, AssetRecoveryCheckpointState>();
        foreach (var group in identities.Distinct().GroupBy(item => item.ExecutionRunId)) {
            var run = await store.GetExecutionRunAsync(group.Key, cancellationToken);
            var journal = run?.ToolAdmission;
            journal?.Validate();
            var matches = run?.Id == group.Key && journal is not null && journal.Session.Reference.ExecutionRunId == group.Key &&
                journal.Session.Purpose == AgentRuntimeContextPurpose.GovernedProcessAutomation &&
                journal.Session.Profile.ProfileId == profile.Profile.Id &&
                journal.Session.Profile.Fingerprint == profile.Profile.Runtime.Fingerprint;
            foreach (var identity in group) {
                var proposal = matches ? journal!.Batches.SelectMany(batch => batch.Proposals)
                    .SingleOrDefault(item => item.IntentId.Value == identity.IntentId) : null;
                result[identity] = proposal is null ? AssetRecoveryCheckpointState.Unavailable : Observe(proposal);
            }
        }
        return result;
    }

    public async Task<AgentAssetCancelledReceiptState> ReadCancelledReceiptStateAsync(AgentToolSessionReference originalSession,
        AgentToolApprovalBinding originalProposal, string toolName, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(originalSession);
        ArgumentNullException.ThrowIfNull(originalProposal);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        var run = await store.GetExecutionRunAsync(originalSession.ExecutionRunId, cancellationToken);
        var journal = run?.ToolAdmission;
        journal?.Validate();
        if (run is null || journal is null || journal.Session.Reference != originalSession || originalSession.BackgroundSource is null ||
            run.Id != originalSession.ExecutionRunId || run.AgentId != journal.Session.AgentId || run.ChatSessionId is not null ||
            journal.Session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
            journal.Session.Profile.ProfileId != profile.Profile.Id || journal.Session.Profile.Fingerprint != profile.Profile.Runtime.Fingerprint ||
            journal.Session.Profile.Generation.Value != generation) {
            return AgentAssetCancelledReceiptState.OriginalProposalUnavailable;
        }
        var proposal = journal.Batches.SingleOrDefault(batch => batch.Id == originalProposal.BatchId)?.Proposals
            .SingleOrDefault(item => item.IntentId == originalProposal.IntentId);
        if (proposal is null || proposal.Payload.ToolName != toolName || proposal.Payload.SemanticVersion != originalProposal.SemanticVersion ||
            proposal.Payload.Digest != originalProposal.Digest || proposal.Payload.Effect != AgentToolProposalEffect.Mutation ||
            proposal.Payload.Recovery != AgentToolProposalRecovery.OwnerReceipt || proposal.ApprovalStatus != ExecutionApprovalStatus.Approved ||
            proposal.ApprovedDigest != proposal.Payload.Digest || proposal.DispatchClaimId is null || proposal.DispatchClaimId == Guid.Empty ||
            proposal.State is not (AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired or AgentToolProposalState.Cancelled) ||
            proposal.State == AgentToolProposalState.Cancelled && proposal.Cancellation?.Disposition is not
                (AgentToolCancellationDisposition.CancelledUnreconciled or AgentToolCancellationDisposition.ReceiptCommitted)) {
            return AgentAssetCancelledReceiptState.OriginalProposalUnavailable;
        }
        if (run.Outcome != RunOutcome.Cancelled) {
            return AgentAssetCancelledReceiptState.OriginalRunNotCancelled;
        }
        return proposal.Cancellation is { Disposition: AgentToolCancellationDisposition.ReceiptCommitted,
            CommittedEffect: not null, Receipt: not null } && proposal.EffectState == AgentToolEffectState.Committed
            ? AgentAssetCancelledReceiptState.ReceiptRecorded : AgentAssetCancelledReceiptState.Ready;
    }

    private static AssetRecoveryCheckpointState Observe(AgentToolProposalRecord proposal) {
        if (proposal.State == AgentToolProposalState.Completed && proposal.Result is not null &&
            proposal.EffectState == AgentToolEffectState.Committed ||
            proposal.State == AgentToolProposalState.Cancelled && proposal.EffectState == AgentToolEffectState.Committed &&
            proposal.Cancellation is { Disposition: AgentToolCancellationDisposition.ReceiptCommitted, CommittedEffect: not null, Receipt: not null }) {
            return AssetRecoveryCheckpointState.Completed;
        }
        return proposal.State is AgentToolProposalState.Prepared or AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired
            ? AssetRecoveryCheckpointState.Pending : AssetRecoveryCheckpointState.Unavailable;
    }

    private sealed class BoundProfile(ResolvedDatabaseProfile value) : IActiveDatabaseProfileResolver {
        public ResolvedDatabaseProfile ResolveCurrentProfile() => value;
    }
}
