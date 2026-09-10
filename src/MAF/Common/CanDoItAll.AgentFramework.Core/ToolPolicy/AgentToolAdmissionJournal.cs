using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentToolAdmissionJournal : IAgentToolAdmissionVerifier {
    private readonly ISandboxWorkspaceExecutionRunStore reader;
    private readonly ISandboxWorkspaceExecutionRunMutationStore writer;
    private readonly ISandboxWorkspaceExecutionRunLeaseStore leases;
    private readonly AgentToolProfileBinding profile;
    private readonly TimeProvider clock;

    public AgentToolAdmissionJournal(ISandboxWorkspaceStore store, AgentToolProfileBinding profile, TimeProvider? clock = null) {
        reader = store as ISandboxWorkspaceExecutionRunStore
            ?? throw new InvalidOperationException("Tool admission requires the canonical execution-run reader.");
        writer = store as ISandboxWorkspaceExecutionRunMutationStore
            ?? throw new InvalidOperationException("Tool admission requires atomic current-state run mutations.");
        leases = store as ISandboxWorkspaceExecutionRunLeaseStore
            ?? throw new InvalidOperationException("Tool admission requires a real cross-instance run dispatch lease.");
        this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        this.clock = clock ?? TimeProvider.System;
    }

    public AgentToolJournalRecord CreateForNewRun(ExecutionRunRecord run, ChatSessionRecord chat,
        AgentToolAdmissionSupport support = AgentToolAdmissionSupport.Recoverable,
        AgentToolOriginalInput? originalInput = null, AgentRuntimeTransientContext? runtimeContext = null) {
        if (run.ToolAdmission is not null || run.PendingApprovals.Count != 0 || run.Revision != 1 ||
            run.State != ExecutionState.Preparing || run.ChatSessionId != chat.Id) {
            throw new AgentToolAdmissionException("tool-admission.legacy-run",
                "Tool admission must be created by the owner with a new execution run; existing runs cannot acquire fabricated effect identity.");
        }

        var governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson)
            ?? throw Failure("The new run has no persisted execution authority.");
        var session = new AgentToolSessionAdmission(new(run.Id, chat.Id, governance.AuthorityId), run.AgentId,
            AgentRuntimeContextPurpose.InteractiveChat, profile);
        RequireCurrent(session, new(run, chat, [], []));
        if (originalInput is not null && !chat.Messages.Any(message => message.Id == originalInput.MessageId &&
                message.Role == ChatMessageRole.User && message.Content == originalInput.Content)) {
            throw Failure("The admitted input does not match the new run's persisted user message.");
        }

        if (runtimeContext is not null && (!runtimeContext.Attachments.IsEmpty ||
                AgentChatContextDigest.Compute(runtimeContext) != ExecutionInvocationMetadata.ResolveTransientContextDigest(run))) {
            throw Failure("The saved runtime context does not match its admitted digest or requires an unsupported typed attachment.");
        }

        return new(AgentToolJournalRecord.CurrentSchemaVersion, 1, session, [], [], Support: support,
            OriginalInput: originalInput, RuntimeContext: runtimeContext is null ? null : new(runtimeContext.Content, runtimeContext.WorkspaceScope));
    }

    public async ValueTask<AgentToolRunLease> AcquireRunAsync(AgentToolSessionReference reference, CancellationToken cancellationToken) {
        var handle = await leases.AcquireToolDispatchLeaseAsync(reference.ExecutionRunId, cancellationToken);
        try {
            var lease = new AgentToolRunLease(this, reference, Guid.NewGuid(), handle);
            await MutateAsync(lease, requireClaim: false, journal => journal with {
                Revision = checked(journal.Revision + 1), ActiveDispatchLeaseId = lease.Id
            }, cancellationToken);
            return lease;
        } catch {
            await handle.DisposeAsync();
            throw;
        }
    }

    public async Task<AgentToolJournalRecord> ReadAsync(AgentToolRunLease lease, CancellationToken cancellationToken) {
        return await ReadVerifiedAsync(lease, reconciliationOnly: false, cancellationToken);
    }

    private async Task<AgentToolJournalRecord> ReadVerifiedAsync(AgentToolRunLease lease, bool reconciliationOnly,
        CancellationToken cancellationToken) {
        lease.RequireOwner(this);
        RequireLeaseMode(lease, reconciliationOnly);
        var detail = await RequireDetailAsync(lease.Session.ExecutionRunId, cancellationToken);
        var journal = RequireJournal(detail);
        RequireCurrent(journal.Session, detail, reconciliationOnly);
        RequireLease(lease, journal);
        return journal;
    }

    public Task<AgentToolJournalRecord> BeginSegmentAsync(AgentToolRunLease lease,
        AgentToolProtocolEnvelope checkpoint, Guid? continuesSegmentId, CancellationToken cancellationToken)
        => MutateAsync(lease, true, journal => {
            RequireRecoverable(journal);
            var existing = journal.Segments.SingleOrDefault(segment => segment.ContinuesSegmentId == continuesSegmentId);
            if (existing is not null) {
                return journal;
            }

            if (journal.Segments.Length == 0 && continuesSegmentId is not null ||
                journal.Segments.Length != 0 && journal.Segments[^1].Id != continuesSegmentId) {
                throw Failure("An SDK continuation must follow the latest saved invocation segment.");
            }

            if (continuesSegmentId is not null) {
                var previous = journal.Segments[^1];
                if (previous.ApprovalCheckpoint is null || previous.PendingApprovals.Any(approval =>
                        AgentToolJournalTransitions.RequireProposal(journal, approval.ToolAdmission
                            ?? throw Failure("The saved SDK approval has no admitted proposal.")).ApprovalStatus == ExecutionApprovalStatus.Pending)) {
                    throw Failure("The exact saved approval decisions must commit before SDK continuation.");
                }
            }

            var segment = new AgentToolInvocationSegment(Guid.NewGuid(), journal.Segments.Length,
                journal.Batches.Length, checkpoint, continuesSegmentId, PendingApprovals: []);
            return journal with { Revision = checked(journal.Revision + 1), Segments = journal.Segments.Add(segment) };
        }, cancellationToken);

    public async Task<IReadOnlyList<PendingToolApprovalRecord>> SaveApprovalCheckpointAsync(AgentToolRunLease lease,
        Guid segmentId, AgentToolProtocolEnvelope checkpoint, string serializedSessionStateJson,
        IReadOnlyList<PendingToolApprovalRecord> approvals, CancellationToken cancellationToken) {
        lease.RequireOwner(this);
        IReadOnlyList<PendingToolApprovalRecord>? bound = null;
        await writer.UpdateExecutionRunDetailAsync(lease.Session.ExecutionRunId, detail => {
            var journal = RequireJournal(detail);
            RequireCurrent(journal.Session, detail);
            RequireLease(lease, journal);
            var segment = journal.Segments.Single(item => item.Id == segmentId);
            if (segment.Ordinal != journal.Segments.Length - 1 || approvals.Count == 0) {
                throw Failure("Only the current SDK invocation can publish pending approvals.");
            }

            var proposals = journal.Batches.Skip(segment.FirstBatchOrdinal).SelectMany(batch =>
                batch.Proposals.Select(proposal => (Batch: batch.Id, Proposal: proposal))).ToArray();
            bound = approvals.Select(approval => {
                var match = proposals.SingleOrDefault(item => item.Proposal.CallId == approval.CallId &&
                    item.Proposal.Payload.ToolName == approval.ToolName && item.Proposal.ApprovalStatus == ExecutionApprovalStatus.Pending);
                if (match.Proposal is null) {
                    throw Failure("The SDK approval is not an exact pending proposal in this invocation.");
                }

                return approval with { ToolAdmission = Binding(match.Batch, match.Proposal) };
            }).ToArray();
            var changed = AgentToolJournalTransitions.BindApprovals(journal, bound);
            changed = changed with {
                Revision = checked(changed.Revision + 1),
                Segments = changed.Segments.SetItem(segment.Ordinal, segment with {
                    ApprovalCheckpoint = checkpoint, PendingApprovals = bound.ToImmutableArray()
                })
            };
            AgentToolJournalTransitions.ValidatePersistence(journal, changed);
            return detail with { Run = detail.Run with {
                ToolAdmission = changed, PendingApprovals = bound, SerializedSessionStateJson = serializedSessionStateJson,
                State = ExecutionState.WaitingOnTool, Revision = checked(detail.Run.Revision + 1), UpdatedAtUtc = clock.GetUtcNow()
            } };
        }, cancellationToken);
        return bound ?? throw Failure("The SDK approval checkpoint was not saved.");
    }

    public Task<AgentToolJournalRecord> AdmitBatchAsync(AgentToolRunLease lease, AgentToolSemanticDigest requestDigest,
        AgentToolProtocolEnvelope response, IReadOnlyList<AgentToolPreparedCall> calls, CancellationToken cancellationToken)
        => MutateAsync(lease, true, journal => {
            RequireRecoverable(journal);
            if (journal.Segments.Length == 0 || journal.HasUnresolvedEffects) {
                throw Failure("Restore and drain the saved tool batch before admitting another provider proposal.");
            }

            var proposals = calls.Select((call, ordinal) => new AgentToolProposalRecord(
                new(Guid.NewGuid()), ordinal, call.CallId, call.Payload, call.RequiresApproval,
                AgentToolProposalState.Prepared,
                call.RequiresApproval ? ExecutionApprovalStatus.Pending : ExecutionApprovalStatus.Approved,
                call.RequiresApproval ? null : call.Payload.Digest)).ToImmutableArray();
            var batch = new AgentToolBatchRecord(new(Guid.NewGuid()), journal.Batches.Length, requestDigest, response, proposals);
            return journal with { Revision = checked(journal.Revision + 1), Batches = journal.Batches.Add(batch) };
        }, cancellationToken);

    public async Task<AgentToolInvocationClaim> ClaimInvocationAsync(AgentToolRunLease lease, AgentToolBatchId batchId,
        string callId, AgentToolPreparedPayload payload, CancellationToken cancellationToken) {
        var claimId = Guid.NewGuid();
        AgentToolProposalRecord? selected = null;
        var journal = await MutateAsync(lease, true, current => {
            var batch = current.Batches.SingleOrDefault(item => item.Id == batchId)
                ?? throw Failure("The current serial batch does not exist.");
            var proposal = batch.Proposals.SingleOrDefault(item => item.CallId == callId)
                ?? throw Failure("The protocol call is not part of the admitted batch.");
            if (proposal.Payload != payload || proposal.ApprovalStatus != ExecutionApprovalStatus.Approved ||
                proposal.ApprovedDigest != payload.Digest) {
                throw Failure("The invocation does not match its exact approved payload.");
            }

            if (batch.Proposals.Take(proposal.Ordinal).Any(item =>
                    item.State is not (AgentToolProposalState.Completed or AgentToolProposalState.Rejected))) {
                throw Failure("Tool invocation cannot bypass an earlier unresolved serial proposal.");
            }

            selected = proposal;
            if (proposal.State is AgentToolProposalState.Rejected or AgentToolProposalState.Cancelled) {
                throw Failure("A rejected or cancelled proposal cannot execute.");
            }

            if (proposal.State == AgentToolProposalState.Completed) {
                return current;
            }

            if (proposal.State is AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired &&
                payload.Recovery == AgentToolProposalRecovery.ReconcileBeforeRetry) {
                throw new AgentToolAdmissionException("tool-admission.reconciliation-required",
                    "A previously dispatched non-idempotent effect is uncertain. Reconcile it before any retry or new provider proposal.");
            }

            return AgentToolJournalTransitions.Replace(current, Binding(batch.Id, proposal), prior => prior with {
                State = AgentToolProposalState.Executing, DispatchClaimId = claimId
            });
        }, cancellationToken);
        var admitted = selected ?? throw Failure("The current dispatch did not select a saved proposal.");
        return new(this, lease, Binding(batchId, admitted), admitted, claimId, journal.Revision);
    }

    public async Task CompleteInvocationAsync(AgentToolInvocationClaim claim, AgentToolProtocolEnvelope result,
        AgentToolEffectState effect, CancellationToken cancellationToken) {
        claim.RequireOwner(this);
        if (claim.Proposal.State == AgentToolProposalState.Completed) {
            return;
        }

        await MutateAsync(claim.RunLease, true, journal => AgentToolJournalTransitions.Replace(journal, claim.Binding, proposal => {
            RequireClaim(claim, proposal);
            return proposal with { State = AgentToolProposalState.Completed, Result = result, EffectState = effect };
        }), cancellationToken);
    }

    public async Task PreserveUncertainInvocationAsync(AgentToolInvocationClaim claim, CancellationToken cancellationToken) {
        claim.RequireOwner(this);
        if (claim.Proposal.State == AgentToolProposalState.Completed) {
            return;
        }

        await MutateAsync(claim.RunLease, true, journal => AgentToolJournalTransitions.Replace(journal, claim.Binding, proposal => {
            RequireClaim(claim, proposal);
            return proposal with { State = AgentToolProposalState.ReconciliationRequired };
        }), cancellationToken);
    }

    public async ValueTask<AgentToolSessionAdmission> RequireSessionAsync(
        AgentToolSessionReference reference, CancellationToken cancellationToken = default) {
        var lease = AgentToolRunLease.Current ?? throw Failure("No active admitted runtime lease is held.");
        lease.RequireOwner(this);
        if (lease.Session != reference) {
            throw Failure("The requested session does not match the active admitted runtime.");
        }

        return (await ReadAsync(lease, cancellationToken)).Session;
    }

    public async ValueTask<AgentToolAdmittedInvocation> RequireInvocationAsync(AgentToolSessionReference session,
        string toolName, AgentToolSemanticDigest digest, CancellationToken cancellationToken = default) {
        var claim = AgentToolInvocationClaim.Current ?? throw Failure("No current serial proposal is bound to this invocation.");
        claim.RequireOwner(this);
        if (claim.RunLease.Session != session) {
            throw Failure("The current serial proposal belongs to a different execution session.");
        }

        var journal = await ReadAsync(claim.RunLease, cancellationToken);
        var proposal = AgentToolJournalTransitions.RequireProposal(journal, claim.Binding);
        if (proposal.Payload.ToolName != toolName || proposal.Payload.Digest != digest ||
            proposal.State is not (AgentToolProposalState.Executing or AgentToolProposalState.Completed) ||
            proposal.State == AgentToolProposalState.Executing && proposal.DispatchClaimId != claim.Id) {
            throw Failure("The active serial dispatch no longer owns this exact proposal.");
        }

        return new(session, claim.Binding.BatchId, proposal.IntentId, proposal.Payload, proposal.ApprovalStatus, proposal.ApprovedDigest);
    }

    private async Task<AgentToolJournalRecord> MutateAsync(AgentToolRunLease lease, bool requireClaim,
        Func<AgentToolJournalRecord, AgentToolJournalRecord> update, CancellationToken cancellationToken,
        bool reconciliationOnly = false) {
        lease.RequireOwner(this);
        RequireLeaseMode(lease, reconciliationOnly);
        var detail = await writer.UpdateExecutionRunDetailAsync(lease.Session.ExecutionRunId, current => {
            var journal = RequireJournal(current);
            RequireCurrent(journal.Session, current, reconciliationOnly);
            if (requireClaim) {
                RequireLease(lease, journal);
            }

            var changed = update(journal);
            AgentToolJournalTransitions.ValidatePersistence(journal, changed);
            return ReferenceEquals(journal, changed) ? current : current with { Run = current.Run with {
                ToolAdmission = changed, Revision = checked(current.Run.Revision + 1), UpdatedAtUtc = clock.GetUtcNow()
            } };
        }, cancellationToken);
        return RequireJournal(detail);
    }

    private void RequireCurrent(AgentToolSessionAdmission session, ExecutionRunDetail detail, bool reconciliationOnly = false) {
        var run = detail.Run;
        var chat = detail.ChatSession;
        var authority = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson);
        if (session.Profile != profile || session.Purpose != AgentRuntimeContextPurpose.InteractiveChat ||
            run.Id != session.Reference.ExecutionRunId || run.AgentId != session.AgentId ||
            run.ChatSessionId != session.Reference.ChatSessionId || chat is null || chat.Id != session.Reference.ChatSessionId ||
            chat.AgentId != session.AgentId || !reconciliationOnly && chat.LatestExecutionRunId != run.Id ||
            authority is null || authority.AuthorityId != session.Reference.AuthorityId || authority.AgentId != session.AgentId ||
            authority.DatabaseProfileId != profile.ProfileId || authority.DatabaseProfileGeneration != profile.Generation ||
            !authority.ReadAllowed) {
            throw Failure("The saved execution, chat ownership, purpose, authority or profile no longer matches tool admission.");
        }

        if (reconciliationOnly ? run.Outcome != RunOutcome.Cancelled : run.State == ExecutionState.Completed || run.Outcome == RunOutcome.Cancelled) {
            throw Failure("A terminal execution cannot dispatch or disclose admitted tool data.");
        }
    }

    private static void RequireLeaseMode(AgentToolRunLease lease, bool reconciliationOnly) {
        if (lease.ReconciliationOnly != reconciliationOnly) {
            throw Failure("A cancellation reconciliation lease cannot authorize tool dispatch or ordinary session disclosure.");
        }
    }

    public async ValueTask<AgentToolRunLease> AcquireCancelledReconciliationAsync(AgentToolSessionReference reference,
        CancellationToken cancellationToken) {
        var handle = await leases.AcquireToolDispatchLeaseAsync(reference.ExecutionRunId, cancellationToken);
        try {
            var lease = new AgentToolRunLease(this, reference, Guid.NewGuid(), handle, reconciliationOnly: true);
            await MutateAsync(lease, false, journal => journal with {
                Revision = checked(journal.Revision + 1), ActiveDispatchLeaseId = lease.Id
            }, cancellationToken, reconciliationOnly: true);
            return lease;
        } catch {
            await handle.DisposeAsync();
            throw;
        }
    }

    internal async ValueTask<AgentToolReceiptReconciliationAdmission> RequireReceiptReconciliationAsync(
        AgentToolReceiptReconciliationClaim claim, CancellationToken cancellationToken) {
        claim.RequireOwner(this);
        var journal = await ReadVerifiedAsync(claim.Lease, reconciliationOnly: true, cancellationToken);
        var proposal = AgentToolJournalTransitions.RequireProposal(journal, claim.Binding);
        var allowedState = claim.Purpose switch {
            AgentToolReceiptReconciliationPurpose.CachedReceiptDisclosure => proposal.State == AgentToolProposalState.Cancelled &&
                proposal.Cancellation?.Disposition == AgentToolCancellationDisposition.ReceiptCommitted,
            AgentToolReceiptReconciliationPurpose.UncertainEffect => proposal.State is AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired ||
                proposal.State == AgentToolProposalState.Cancelled && proposal.Cancellation?.Disposition == AgentToolCancellationDisposition.CancelledUnreconciled,
            _ => false
        };
        if (proposal.Payload.Recovery != AgentToolProposalRecovery.OwnerReceipt || proposal.DispatchClaimId is null ||
            proposal.ApprovalStatus != ExecutionApprovalStatus.Approved || proposal.ApprovedDigest != proposal.Payload.Digest ||
            !allowedState) {
            throw Failure("The cancelled proposal has no exact originally approved dispatched owner-receipt intent.");
        }

        return new(journal.Session, claim.Binding, proposal.Payload);
    }

    public async Task<AgentToolRunCancellationReconciliation> ReconcileCancelledAsync(AgentToolRunLease lease,
        IReadOnlyList<IAgentToolReceiptReconciliationProvider> providers, bool currentReadAllowed, CancellationToken cancellationToken) {
        var saved = await MutateAsync(lease, true, AgentToolJournalTransitions.RejectUndispatchedOnCancellation,
            cancellationToken, reconciliationOnly: true);
        var observedThisRequest = new HashSet<AgentToolBusinessIntentId>();
        foreach (var batch in saved.Batches) {
            foreach (var proposal in batch.Proposals) {
                if (proposal.State is AgentToolProposalState.Completed or AgentToolProposalState.Rejected ||
                    proposal.State == AgentToolProposalState.Cancelled &&
                        proposal.Cancellation?.Disposition != AgentToolCancellationDisposition.CancelledUnreconciled) {
                    continue;
                }

                var binding = Binding(batch.Id, proposal);
                var resolution = new AgentToolCancellationResolution(AgentToolCancellationDisposition.CancelledUnreconciled,
                    AgentToolCancellationReason.NoReceiptProtocol);
                if (proposal.Payload.Effect == AgentToolProposalEffect.Read) {
                    resolution = new(AgentToolCancellationDisposition.NoExternalMutation, AgentToolCancellationReason.ReadOnlyInvocation);
                } else if (proposal.Payload.Recovery == AgentToolProposalRecovery.OwnerReceipt) {
                    var matches = providers.Where(provider => provider.Supports(proposal.Payload.ToolName)).ToArray();
                    if (matches.Length > 1) {
                        throw new InvalidOperationException("Multiple receipt reconciliation owners registered for one admitted tool.");
                    }

                    if (!currentReadAllowed) {
                        resolution = new(AgentToolCancellationDisposition.CancelledUnreconciled, AgentToolCancellationReason.CurrentAccessDenied);
                    } else if (matches.Length == 1) {
                        var claim = new AgentToolReceiptReconciliationClaim(this, lease, binding);
                        try {
                            var observed = await matches[0].ReconcileAsync(claim, cancellationToken);
                            if ((observed.CommittedEffect is null) != (observed.Receipt is null)) {
                                throw new InvalidDataException("Owner receipt evidence must carry both the original effect identity and its bounded receipt.");
                            }

                            resolution = observed.CommittedEffect is null
                                ? new(AgentToolCancellationDisposition.CancelledUnreconciled, AgentToolCancellationReason.ReceiptNotObserved)
                                : new(AgentToolCancellationDisposition.ReceiptCommitted, AgentToolCancellationReason.ReceiptFound,
                                    observed.CommittedEffect, observed.Receipt);
                            if (observed.CommittedEffect is not null) {
                                observedThisRequest.Add(proposal.IntentId);
                            }
                        } catch (AgentToolReceiptAccessDeniedException) {
                            resolution = new(AgentToolCancellationDisposition.CancelledUnreconciled, AgentToolCancellationReason.CurrentAccessDenied);
                        }
                    }
                }

                await MutateAsync(lease, true, current => AgentToolJournalTransitions.Replace(current, binding, latest => {
                    if (latest.State == AgentToolProposalState.Cancelled && resolution.Disposition != AgentToolCancellationDisposition.ReceiptCommitted) {
                        return latest;
                    }

                    return latest with {
                        State = AgentToolProposalState.Cancelled,
                        Cancellation = resolution,
                        EffectState = resolution.Disposition switch {
                            AgentToolCancellationDisposition.ReceiptCommitted => AgentToolEffectState.Committed,
                            AgentToolCancellationDisposition.NoExternalMutation => AgentToolEffectState.None,
                            _ => AgentToolEffectState.Unknown
                        }
                    };
                }), cancellationToken, reconciliationOnly: true);
            }
        }

        var completed = await ReadVerifiedAsync(lease, reconciliationOnly: true, cancellationToken);
        foreach (var batch in completed.Batches) {
            foreach (var proposal in batch.Proposals.Where(proposal =>
                proposal.Cancellation?.Disposition == AgentToolCancellationDisposition.ReceiptCommitted &&
                !observedThisRequest.Contains(proposal.IntentId))) {
                await ValidateCachedReceiptDisclosureAsync(lease, batch.Id, proposal, providers, currentReadAllowed, cancellationToken);
            }
        }
        return new(lease.Session.ExecutionRunId, lease.Session.ChatSessionId, completed.Batches.SelectMany(batch => batch.Proposals)
            .Select(proposal => new AgentToolCancellationOutcome(proposal.IntentId, proposal.Payload.ToolName,
                proposal.EffectState, proposal.Cancellation)).ToArray());
    }

    private async Task ValidateCachedReceiptDisclosureAsync(AgentToolRunLease lease, AgentToolBatchId batch,
        AgentToolProposalRecord proposal, IReadOnlyList<IAgentToolReceiptReconciliationProvider> providers,
        bool currentReadAllowed, CancellationToken cancellationToken) {
        if (!currentReadAllowed) {
            throw new AgentToolAdmissionException("tool-admission.receipt-read-denied", "Current authorization does not permit disclosing this saved owner receipt.");
        }

        var matches = providers.Where(provider => provider.Supports(proposal.Payload.ToolName)).ToArray();
        if (matches.Length != 1) {
            throw new AgentToolAdmissionException("tool-admission.receipt-read-unavailable", "Exactly one owner receipt reader is required before saved receipt disclosure.");
        }

        var claim = new AgentToolReceiptReconciliationClaim(this, lease, Binding(batch, proposal),
            AgentToolReceiptReconciliationPurpose.CachedReceiptDisclosure);
        AgentToolReceiptObservation observed;
        try {
            observed = await matches[0].ReconcileAsync(claim, cancellationToken);
        } catch (AgentToolReceiptAccessDeniedException) {
            throw new AgentToolAdmissionException("tool-admission.receipt-read-denied", "Current authorization does not permit disclosing this saved owner receipt.");
        }

        if (observed.CommittedEffect is null || observed.Receipt is null) {
            throw new AgentToolAdmissionException("tool-admission.receipt-read-unavailable", "The owner could not confirm the saved receipt for disclosure. Durable committed evidence was retained.");
        }

        if (observed.CommittedEffect != proposal.Cancellation!.CommittedEffect || observed.Receipt != proposal.Cancellation.Receipt) {
            throw new AgentToolAdmissionException("tool-admission.receipt-read-mismatch", "The owner receipt differs from the retained committed evidence. Disclosure was rejected.");
        }
    }

    private async Task<ExecutionRunDetail> RequireDetailAsync(Guid runId, CancellationToken cancellationToken)
        => await reader.GetExecutionRunDetailAsync(runId, cancellationToken)
            ?? throw Failure("The admitted execution run was not found.");

    private static AgentToolJournalRecord RequireJournal(ExecutionRunDetail detail) {
        var journal = detail.Run.ToolAdmission ?? throw Failure("This legacy run has no durable tool admission journal.");
        journal.Validate();
        return journal;
    }

    private static void RequireRecoverable(AgentToolJournalRecord journal) {
        if (journal.Support != AgentToolAdmissionSupport.Recoverable) {
            throw new AgentToolAdmissionException("tool-admission.request-scoped-input",
                "This run contains request-scoped attachments without retained immutable recovery references. Durable effect admission is unavailable.");
        }
    }

    private static void RequireLease(AgentToolRunLease lease, AgentToolJournalRecord journal) {
        if (journal.ActiveDispatchLeaseId != lease.Id || journal.Session.Reference != lease.Session) {
            throw Failure("The active runtime no longer owns the persisted dispatch lease.");
        }
    }

    private static void RequireClaim(AgentToolInvocationClaim claim, AgentToolProposalRecord proposal) {
        if (proposal.State != AgentToolProposalState.Executing || proposal.DispatchClaimId != claim.Id) {
            throw Failure("The serial dispatch claim changed before its checkpoint.");
        }
    }

    private static AgentToolApprovalBinding Binding(AgentToolBatchId batch, AgentToolProposalRecord proposal)
        => new(batch, proposal.IntentId, proposal.Payload.SemanticVersion, proposal.Payload.Digest);

    private static AgentToolAdmissionException Failure(string message)
        => new("tool-admission.denied", message);
}
