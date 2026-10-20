using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentPlanningDecisionOwner(AgentToolAdmissionJournal journal) {
    private readonly TimeProvider clock = journal.Clock;
    private const string RefreshDecisionSource = "planning-refresh";

    public Task<AgentToolRunLease> StartAsync(AgentPlanningDecisionExpectation expected,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, CancellationToken cancellationToken)
        => journal.AcquirePlanningStepAsync(this, expected, currentRead, cancellationToken);

    public Task<AgentToolRunLease> StartInheritedAsync(AgentPlanningDecisionExpectation expected,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, CancellationToken cancellationToken)
        => journal.AcquirePlanningStepAsync(this, expected, currentRead, cancellationToken, inherited: true);

    public Task<AgentToolRunLease> ResumeSavedEffectAsync(AgentSavedEffectContinuationRequest request,
        AgentPlanningDecisionExpectation expected, Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead,
        CancellationToken cancellationToken)
        => journal.AcquirePlanningEffectDispatchAsync(this, request, expected, currentRead, cancellationToken);

    public Task<ExecutionRunDetail> RefreshAsync(AgentPlanningDecisionExpectation expected, bool automatic,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, CancellationToken cancellationToken)
        => journal.ChangeClosedPlanningAsync(expected, currentRead, current => {
            var admitted = current.Run.ToolAdmission!;
            var decision = admitted.PlanningDecision!;
            if (automatic && decision.Current.State != AgentPlanningGenerationState.PausedBasisChanged) {
                throw Refuse(decision, AgentPlanningAdmissionRefusal.DecisionInactive);
            }
            var now = clock.GetUtcNow();
            var rejected = current.Run.PendingApprovals.Select(item => new PendingToolApprovalDecision(item.ApprovalId, false) {
                ToolAdmission = item.ToolAdmission
            }).ToImmutableArray();
            var retired = AgentToolJournalTransitions.ApplyDecisions(admitted, current.Run.PendingApprovals, rejected, automatic: false);
            var approvals = ExecutionRunStateTransitions.ApplyApprovalDecision(current.Approvals, current.Run, rejected,
                now, RefreshDecisionSource, current.Run.Id.ToString("D")).RunApprovals;
            var changed = Change(current.Run, retired with {
                PlanningDecision = AgentPlanningDecisionTransitions.NewGeneration(decision, automatic, now)
            });
            return current with {
                Run = changed with { PendingApprovals = [], State = ExecutionState.Preparing },
                Approvals = approvals,
                ChatSession = current.ChatSession! with { UpdatedAtUtc = now, Compatibility = null }
            };
        }, cancellationToken);

    public Task<ExecutionRunDetail> CancelAsync(AgentPlanningDecisionExpectation expected,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, CancellationToken cancellationToken)
        => journal.ChangeClosedPlanningAsync(expected, currentRead,
            current => AgentToolAdmissionJournal.CancelSavedDetail(current, clock.GetUtcNow()), cancellationToken);

    public Task<ExecutionRunRecord> PauseForBasisChangeAsync(AgentPlanningLiveWork work, AgentToolApprovalBinding binding,
        CancellationToken cancellationToken)
        => UpdateAsync(work, outcomeOnly: true, run => {
            var admitted = run.ToolAdmission!;
            var decision = work.Require(this, run);
            var proposal = AgentToolJournalTransitions.RequireProposal(admitted, binding);
            if (decision.Current.State != AgentPlanningGenerationState.Active ||
                decision.Current.Steps[^1].Publications.SingleOrDefault()?.Proposal != binding ||
                proposal is not { State: AgentToolProposalState.Completed, EffectState: AgentToolEffectState.NotCommitted, Result: not null } ||
                admitted.HasUnresolvedEffects || admitted.Batches.SelectMany(batch => batch.Proposals).Any(item =>
                    item.EffectState is AgentToolEffectState.Committed or AgentToolEffectState.Unknown)) {
                throw Refuse(decision, AgentPlanningAdmissionRefusal.OriginalStepChanged);
            }
            return Change(run, admitted with {
                PlanningDecision = AgentPlanningDecisionTransitions.Settle(decision, work.IntervalId, Elapsed(work), clock.GetUtcNow(),
                    AgentPlanningWorkSettlementReason.PausedBasisChanged, AgentPlanningGenerationState.PausedBasisChanged)
            }) with { State = ExecutionState.WaitingOnTool };
        }, cancellationToken);

    internal TimeSpan RequireMutation(AgentPlanningLiveWork work, ExecutionRunRecord run, AgentToolApprovalBinding binding) {
        work.Lease.RequireOwner(journal);
        var decision = work.Require(this, run);
        if (!ReferenceEquals(AgentToolRunLease.Current, work.Lease) ||
            run.Id != work.OriginalRun.Id || run.ToolAdmission!.Session != work.OriginalRun.ToolAdmission!.Session ||
            decision.Current.State != AgentPlanningGenerationState.Active ||
            decision.Work.Open!.Phase != AgentPlanningWorkPhase.SavedEffectContinuation ||
            decision.Current.Steps[^1].Publications.SingleOrDefault()?.Proposal != binding) {
            throw Refuse(decision, AgentPlanningAdmissionRefusal.OriginalStepChanged);
        }
        var now = clock.GetUtcNow();
        var remaining = AgentPlanningDecisionTransitions.RemainingWork(decision, Elapsed(work), now);
        AgentPlanningDecisionTransitions.RequireBasisAge(decision, decision.Current.Steps[^1], now);
        return remaining;
    }

    internal TimeSpan RequirePublication(AgentPlanningLiveWork work, IAgentPlanningPublicationSnapshotLease held,
        AgentToolApprovalBinding binding) {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(binding);
        held.RequireActive();
        var claim = AgentToolInvocationClaim.Current;
        if (claim is null || !ReferenceEquals(claim.RunLease, work.Lease) || claim.Binding != binding ||
            held.Original.Session != work.OriginalRun.ToolAdmission!.Session) {
            throw new AgentPlanningAdmissionException(work.DecisionId, work.Generation, AgentPlanningAdmissionRefusal.OriginalStepChanged);
        }
        claim.RequireOwner(journal);
        var run = held.OriginalRun;
        var selected = AgentToolJournalTransitions.RequireProposal(run.ToolAdmission!, binding);
        if (selected.State != AgentToolProposalState.Executing || selected.DispatchClaimId != claim.Id ||
            selected.ApprovalStatus != ExecutionApprovalStatus.Approved || selected.ApprovedDigest != selected.Payload.Digest ||
            selected.Payload != claim.Proposal.Payload || run.Outcome is not null || run.State != ExecutionState.Running ||
            run.MetadataJson != work.OriginalRun.MetadataJson || held.OriginalChatSession.LatestExecutionRunId != run.Id) {
            throw new AgentPlanningAdmissionException(work.DecisionId, work.Generation, AgentPlanningAdmissionRefusal.OriginalStepChanged);
        }
        return RequireMutation(work, run, binding);
    }

    public async Task<AgentPlanningStepObservation> ObserveStepAsync(AgentPlanningLiveWork work, CancellationToken cancellationToken) {
        var snapshot = await journal.ReadPlanningSnapshotAsync(work.Lease, cancellationToken);
        var decision = work.Require(this, snapshot.Run);
        var step = decision.Current.Steps[^1];
        if (decision.Current.State != AgentPlanningGenerationState.Active ||
            step.State is not (AgentPlanningStepState.PreparedInput or AgentPlanningStepState.ProviderEntered)) {
            throw Refuse(decision, AgentPlanningAdmissionRefusal.OriginalStepChanged);
        }
        var now = clock.GetUtcNow();
        var remaining = AgentPlanningDecisionTransitions.RemainingWork(decision, Elapsed(work), now);
        AgentPlanningDecisionTransitions.RequireBasisAge(decision, step, now);
        return new(snapshot.Run, decision.Current.Bases.Single(item => item.Id == step.BasisId), remaining);
    }

    public async Task<AgentPlanningLiveWork> StartStepAsync(AgentToolRunLease lease,
        AgentPlanningDecisionExpectation expected, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(expected);
        var snapshot = await journal.ReadPlanningSnapshotAsync(lease, cancellationToken);
        if (snapshot.Run.ToolAdmission!.PlanningDecision?.InheritedOrigin is not null) {
            throw Refuse(snapshot.Run.ToolAdmission.PlanningDecision!, AgentPlanningAdmissionRefusal.OriginalStepChanged);
        }
        long started = 0;
        var saved = await journal.UpdatePlanningAsync(lease, snapshot.Run, expected, outcomeOnly: false, run => {
            var decision = run.ToolAdmission!.PlanningDecision!;
            if (decision.Work.Open is not null) {
                throw Refuse(decision, AgentPlanningAdmissionRefusal.InterruptedWorkAccountingUnavailable);
            }
            started = clock.GetTimestamp();
            return Change(run, run.ToolAdmission with {
                PlanningDecision = AgentPlanningDecisionTransitions.BeginStep(decision, lease.Id, clock.GetUtcNow())
            });
        }, cancellationToken);
        return new(this, lease, saved, started);
    }

    public Task<ExecutionRunRecord> CaptureBasisAsync(AgentPlanningLiveWork work,
        Func<AgentPlanningDecisionRecord, CancellationToken, Task<AgentToolProtocolEnvelope>> capture,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(capture);
        return BindBasisAsync(work, async (decision, now, token) => new(Guid.NewGuid(), now, await capture(decision, token)),
            inherited: false, cancellationToken);
    }

    public Task<ExecutionRunRecord> InheritBasisAsync(AgentPlanningLiveWork work,
        Func<AgentPlanningInheritedOrigin, CancellationToken, Task<AgentPlanningBasisRecord>> read,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(read);
        return BindBasisAsync(work, (decision, _, token) => read(decision.InheritedOrigin!, token), inherited: true, cancellationToken);
    }

    private async Task<ExecutionRunRecord> BindBasisAsync(AgentPlanningLiveWork work,
        Func<AgentPlanningDecisionRecord, DateTimeOffset, CancellationToken, Task<AgentPlanningBasisRecord>> read,
        bool inherited, CancellationToken cancellationToken) {
        var snapshot = await journal.ReadPlanningSnapshotAsync(work.Lease, cancellationToken);
        var decision = work.Require(this, snapshot.Run);
        if (inherited != (decision.InheritedOrigin is not null) || decision.Current.State != AgentPlanningGenerationState.Active ||
            decision.Current.Steps[^1].State != AgentPlanningStepState.CapturingBasis) {
            throw Refuse(decision, AgentPlanningAdmissionRefusal.OriginalStepChanged);
        }
        var now = clock.GetUtcNow();
        AgentPlanningDecisionTransitions.RequireBudget(decision, Elapsed(work), now);
        var basis = decision.Policy.ReadBasisMode == ReadBasisMode.RunSnapshot ? decision.Current.Bases.FirstOrDefault() : null;
        basis ??= await read(decision, now, cancellationToken);
        decision.InheritedOrigin?.RequireOriginalBasis(basis);
        var segmentId = Guid.NewGuid();
        return await UpdateAsync(work, outcomeOnly: false, run => {
            var current = run.ToolAdmission!;
            var active = work.Require(this, run);
            var observed = clock.GetUtcNow();
            AgentPlanningDecisionTransitions.RequireBudget(active, Elapsed(work), observed);
            var segment = new AgentToolInvocationSegment(segmentId, current.Segments.Length, current.Batches.Length,
                null, current.Segments.LastOrDefault()?.Id, PendingApprovals: [],
                CheckpointKind: AgentToolCheckpointKind.EffectsOnly, OriginDispatchLeaseId: work.Lease.Id);
            return Change(run, current with {
                Segments = current.Segments.Add(segment),
                PlanningDecision = AgentPlanningDecisionTransitions.AttachBasis(active, work.StepId, basis, segment.Id, observed)
            });
        }, cancellationToken);
    }

    public async Task<AgentToolProviderDispatchRecord> EnterProviderAsync(AgentPlanningLiveWork work,
        AgentToolSemanticDigest requestDigest, AgentToolSemanticDigest contractDigest, CancellationToken cancellationToken) {
        var dispatchId = new AgentToolProviderDispatchId(Guid.NewGuid());
        var saved = await UpdateAsync(work, outcomeOnly: false, run => {
            var current = run.ToolAdmission!;
            var decision = work.Require(this, run);
            if (current.HasUnresolvedEffects) {
                throw Refuse(decision, AgentPlanningAdmissionRefusal.OriginalStepChanged);
            }
            AgentPlanningProviderBounds.RequireCapacity(current);
            var next = AgentPlanningDecisionTransitions.EnterProvider(decision, work.StepId, dispatchId,
                Elapsed(work), clock.GetUtcNow());
            var dispatches = current.ProviderDispatches.IsDefault ? [] : current.ProviderDispatches;
            var dispatch = new AgentToolProviderDispatchRecord(dispatchId, dispatches.Length,
                next.Current.Steps[^1].OriginSegmentId!.Value, requestDigest, contractDigest, [], AgentToolProviderDispatchState.Started);
            return Change(run, current with { PlanningDecision = next, ProviderDispatches = dispatches.Add(dispatch) });
        }, cancellationToken);
        return saved.ToolAdmission!.ProviderDispatches.Single(item => item.Id == dispatchId);
    }

    public async Task<AgentPlanningResponseAdmission> AdmitResponseAsync(AgentPlanningLiveWork work,
        AgentToolProviderDispatchId dispatchId, AgentToolSemanticDigest requestDigest, AgentToolProtocolEnvelope response,
        AgentToolPreparedCall? publication, CancellationToken cancellationToken, AgentPlanningOutputRefusal? outputRefusal = null) {
        ArgumentNullException.ThrowIfNull(response);
        if (System.Text.Encoding.UTF8.GetByteCount(response.PayloadJson) > AgentPlanningProviderBounds.MaximumResponseUtf8Bytes ||
            outputRefusal is { } supplied && !Enum.IsDefined(supplied) || outputRefusal is not null && publication is not null) {
            throw new ArgumentException("The planning response exceeds its reserved size or has conflicting preparation outcomes.");
        }
        var batchId = new AgentToolBatchId(Guid.NewGuid());
        var intentId = new AgentToolBusinessIntentId(Guid.NewGuid());
        AgentPlanningAdmissionRefusal? refusal = null;
        var saved = await UpdateAsync(work, outcomeOnly: true, run => {
            var current = run.ToolAdmission!;
            var decision = work.Require(this, run);
            var step = decision.Current.Steps[^1];
            var dispatch = current.ProviderDispatches.SingleOrDefault(item => item.Id == dispatchId);
            if (step.ProviderDispatchId != dispatchId || dispatch?.State != AgentToolProviderDispatchState.Started ||
                dispatch.RequestDigest != requestDigest) {
                throw Refuse(decision, AgentPlanningAdmissionRefusal.OriginalStepChanged);
            }
            if (run.Outcome == RunOutcome.Cancelled && decision.Current.State != AgentPlanningGenerationState.Cancelled) {
                decision = AgentPlanningDecisionTransitions.Cancel(decision);
            }
            var now = clock.GetUtcNow();
            refusal = ResponseRefusal(decision, step, work, now);
            refusal ??= outputRefusal is null ? null : AgentPlanningAdmissionRefusal.InvalidProviderOutput;
            if (refusal is null && publication is not null && (!publication.RequiresApproval || publication.ProviderCall is not null ||
                    publication.Payload is not { Effect: AgentToolProposalEffect.Mutation, Recovery: AgentToolProposalRecovery.OwnerReceipt,
                        SourcePreparation: not null } || !AgentPlanningProviderBounds.SupportsPublication(publication))) {
                refusal = AgentPlanningAdmissionRefusal.InvalidPublication;
            }
            if (refusal is not null && decision.Current.State == AgentPlanningGenerationState.Active) {
                decision = AgentPlanningDecisionTransitions.Fail(decision);
            }
            ImmutableArray<AgentToolProposalRecord> proposals = [];
            ImmutableArray<AgentPlanningPublicationBinding> publications = [];
            if (refusal is null && publication is not null) {
                var payload = publication.Payload;
                var basis = decision.Current.Bases.Single(item => item.Id == step.BasisId);
                proposals = [new(intentId, 0, publication.CallId, payload, true, AgentToolProposalState.Prepared,
                    ExecutionApprovalStatus.Pending, null)];
                publications = [new(new(batchId, intentId, payload.SemanticVersion, payload.Digest),
                    basis.Id, basis.Value.Digest, payload.SourcePreparation!)];
            }
            var observed = now.Offset == TimeSpan.Zero && now >= decision.Work.LastObservedAtUtc ? now : decision.Work.LastObservedAtUtc;
            return Change(run, current with {
                Batches = current.Batches.Add(new(batchId, current.Batches.Length, requestDigest, response, proposals, dispatchId)),
                ProviderDispatches = current.ProviderDispatches.SetItem(dispatch.Ordinal, dispatch with {
                    State = AgentToolProviderDispatchState.ResponseAdmitted, ResponseBatchId = batchId
                }),
                PlanningDecision = AgentPlanningDecisionTransitions.AdmitResponse(decision, work.StepId, batchId, publications, observed)
            });
        }, cancellationToken);
        return new(saved, refusal);
    }

    public async Task<AgentPlanningLiveWork> AdvanceStepAsync(AgentPlanningLiveWork work, CancellationToken cancellationToken) {
        long started = 0;
        var saved = await UpdateAsync(work, outcomeOnly: false, run => {
            var decision = work.Require(this, run);
            started = clock.GetTimestamp();
            return Change(run, run.ToolAdmission! with {
                PlanningDecision = AgentPlanningDecisionTransitions.AdvanceStep(decision, work.IntervalId, work.Lease.Id,
                    work.ObserveElapsed(clock, started), clock.GetUtcNow())
            });
        }, cancellationToken);
        return new(this, work.Lease, saved, started);
    }

    public Task<ExecutionRunRecord> PauseForApprovalAsync(AgentPlanningLiveWork work, PendingToolApprovalRecord approval,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(approval);
        return UpdateAsync(work, outcomeOnly: false, run => {
            var current = run.ToolAdmission!;
            var decision = work.Require(this, run);
            var step = decision.Current.Steps[^1];
            var selected = step.Publications.SingleOrDefault()
                ?? throw Refuse(decision, AgentPlanningAdmissionRefusal.OriginalStepChanged);
            var proposal = AgentToolJournalTransitions.RequireProposal(current, selected.Proposal);
            if (run.PendingApprovals.Count != 0 || approval.ArgumentsJson != proposal.Payload.ArgumentsJson ||
                approval.ToolAdmission is not null && approval.ToolAdmission != selected.Proposal) {
                throw Refuse(decision, AgentPlanningAdmissionRefusal.OriginalStepChanged);
            }
            var now = clock.GetUtcNow();
            AgentPlanningDecisionTransitions.RequireBudget(decision, Elapsed(work), now);
            AgentPlanningDecisionTransitions.RequireBasisAge(decision, step, now);
            var pending = approval with { ToolAdmission = selected.Proposal };
            var changed = AgentToolJournalTransitions.BindApprovals(current, [pending]);
            var segment = changed.Segments.Single(item => item.Id == step.OriginSegmentId);
            return Change(run, changed with {
                Segments = changed.Segments.SetItem(segment.Ordinal, segment with { PendingApprovals = segment.PendingApprovals.Add(pending) }),
                PlanningDecision = AgentPlanningDecisionTransitions.Settle(decision, work.IntervalId, Elapsed(work), now,
                    AgentPlanningWorkSettlementReason.AwaitingApproval, AgentPlanningGenerationState.AwaitingApproval)
            }) with { PendingApprovals = [pending], State = ExecutionState.WaitingOnTool };
        }, cancellationToken);
    }

    public Task<ExecutionRunRecord> SettleTerminalAsync(AgentPlanningLiveWork work, AgentPlanningGenerationState state,
        CancellationToken cancellationToken) {
        if (state is not (AgentPlanningGenerationState.Completed or AgentPlanningGenerationState.Cancelled or AgentPlanningGenerationState.Failed)) {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
        return UpdateAsync(work, outcomeOnly: true, run => {
            var decision = work.Require(this, run);
            var terminal = run.Outcome == RunOutcome.Cancelled ? AgentPlanningGenerationState.Cancelled :
                decision.Current.State is AgentPlanningGenerationState.Cancelled or AgentPlanningGenerationState.Failed ? decision.Current.State : state;
            var now = clock.GetUtcNow();
            return Change(run, run.ToolAdmission! with {
                PlanningDecision = AgentPlanningDecisionTransitions.Settle(decision, work.IntervalId, Elapsed(work), now,
                    AgentPlanningWorkSettlementReason.Terminal, terminal)
            }) with {
                State = terminal == AgentPlanningGenerationState.Failed ? ExecutionState.Failed : ExecutionState.Completed,
                Outcome = terminal switch {
                    AgentPlanningGenerationState.Completed => RunOutcome.Succeeded,
                    AgentPlanningGenerationState.Cancelled => RunOutcome.Cancelled,
                    _ => RunOutcome.Failed
                },
                CompletedAtUtc = now
            };
        }, cancellationToken);
    }

    private Task<ExecutionRunRecord> UpdateAsync(AgentPlanningLiveWork work, bool outcomeOnly,
        Func<ExecutionRunRecord, ExecutionRunRecord> update, CancellationToken cancellationToken)
        => journal.UpdatePlanningAsync(work.Lease, work.OriginalRun, null, outcomeOnly, run => {
            work.Require(this, run);
            return update(run);
        }, cancellationToken);

    private AgentPlanningAdmissionRefusal? ResponseRefusal(AgentPlanningDecisionRecord decision,
        AgentPlanningStepRecord step, AgentPlanningLiveWork work, DateTimeOffset now) {
        if (decision.Current.State != AgentPlanningGenerationState.Active) {
            return AgentPlanningAdmissionRefusal.DecisionInactive;
        }
        try {
            AgentPlanningDecisionTransitions.RequireBudget(decision, Elapsed(work), now);
            AgentPlanningDecisionTransitions.RequireBasisAge(decision, step, now);
            return null;
        } catch (AgentPlanningAdmissionException failure) {
            return failure.Reason;
        }
    }

    private TimeSpan Elapsed(AgentPlanningLiveWork work) => work.ObserveElapsed(clock, clock.GetTimestamp());

    private static ExecutionRunRecord Change(ExecutionRunRecord run, AgentToolJournalRecord changed)
        => run with { ToolAdmission = changed with { Revision = checked(run.ToolAdmission!.Revision + 1) } };

    private static AgentPlanningAdmissionException Refuse(AgentPlanningDecisionRecord decision, AgentPlanningAdmissionRefusal reason)
        => new(decision.DecisionId, decision.CurrentGeneration, reason);
}
