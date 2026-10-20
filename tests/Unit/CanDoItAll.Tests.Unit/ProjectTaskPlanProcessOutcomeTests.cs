using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectTaskPlanProcessOutcomeTests {
    [Theory]
    [InlineData(AgentPlanningGenerationState.Completed, false)]
    [InlineData(AgentPlanningGenerationState.Completed, true)]
    [InlineData(AgentPlanningGenerationState.Failed, false)]
    [InlineData(AgentPlanningGenerationState.Failed, true)]
    [InlineData(AgentPlanningGenerationState.Cancelled, false)]
    [InlineData(AgentPlanningGenerationState.Cancelled, true)]
    public void Terminal_work_and_effect_are_independent_and_keep_exact_original_identity(AgentPlanningGenerationState state, bool accepted) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var (run, operation) = Closed(fixture, state, accepted);
        Assert.NotNull(run.ToolAdmission!.ActiveDispatchLeaseId);
        var result = Assert.IsType<ProcessDecisionChildResult.Closed>(ProjectTaskPlanProcessOutcome.Read(run, fixture.Parent).Outcome);
        var work = run.ToolAdmission!.PlanningDecision!.Work;
        Assert.Equal(work.Revision, result.Work.OwnerWorkRevision);
        Assert.Equal(work.Revision, result.Closure.OwnerRevision);
        Assert.Equal(TimeSpan.FromSeconds(3).Ticks, result.Work.Consumed.WorkTicks);
        Assert.Equal(0, result.Work.Consumed.Replans);
        Assert.Equal(accepted ? ProcessDecisionChildOutcome.Accepted : ProcessDecisionChildOutcome.NoEffect, result.Closure.Outcome);
        Assert.Equal(accepted ? new ProcessDecisionEffectReceiptId(operation) : null, result.Closure.AcceptedReceipt);
        Assert.Equal(state switch {
            AgentPlanningGenerationState.Completed => ProcessDecisionChildTerminalStatus.Succeeded,
            AgentPlanningGenerationState.Failed => ProcessDecisionChildTerminalStatus.Failed,
            _ => ProcessDecisionChildTerminalStatus.Cancelled
        }, result.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Confirmed_owner_and_cancellation_receipts_keep_original_task_operation(bool cancelled) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var (run, operation) = Closed(fixture, AgentPlanningGenerationState.Cancelled, true);
        run = WithOwnerReceipt(run, operation, cancelled);
        var result = Assert.IsType<ProcessDecisionChildResult.Closed>(ProjectTaskPlanProcessOutcome.Read(run, fixture.Parent).Outcome);
        Assert.Equal(new ProcessDecisionEffectReceiptId(operation), result.Closure.AcceptedReceipt);
        Assert.Equal(ProcessDecisionChildTerminalStatus.Cancelled, result.Status);
    }

    [Theory]
    [InlineData(ReceiptChange.Kind)]
    [InlineData(ReceiptChange.Identity)]
    [InlineData(ReceiptChange.Operation)]
    [InlineData(ReceiptChange.TaskCount)]
    [InlineData(ReceiptChange.Refusal)]
    public void Owner_receipt_must_match_its_original_typed_operation(ReceiptChange change) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var (run, operation) = Closed(fixture, AgentPlanningGenerationState.Failed, true);
        run = WithOwnerReceipt(run, operation, false);
        var proposal = run.ToolAdmission!.Batches[0].Proposals[0];
        var receipt = proposal.OwnerReceipt!;
        var value = ProjectTaskPlanAgentCodec.Read<ProjectTaskPlanAgentResult>(receipt.Receipt, ProjectTaskPlanAgentCodec.ResultFormat, 65_536);
        receipt = change switch {
            ReceiptChange.Kind => receipt with { Effect = receipt.Effect with { SourceKind = "unrelated-owner" } },
            ReceiptChange.Identity => receipt with { Effect = receipt.Effect with { SourceId = Guid.NewGuid().ToString("D") } },
            ReceiptChange.Operation => receipt with { Receipt = (value with { OperationId = Guid.NewGuid() }).OwnerResult },
            ReceiptChange.TaskCount => receipt with { Receipt = (value with { TaskIds = [] }).OwnerResult },
            ReceiptChange.Refusal => receipt with { Receipt = (value with { Kind = ProjectTaskPlanAgentResultKind.Refused }).OwnerResult },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        run = ReplaceProposal(run, proposal with { OwnerReceipt = receipt });
        if (change is ReceiptChange.Kind or ReceiptChange.Identity) {
            Assert.Throws<InvalidDataException>(() => ProjectTaskPlanProcessOutcome.Read(run, fixture.Parent));
        } else {
            Assert.Throws<AgentToolAdmissionException>(() => ProjectTaskPlanProcessOutcome.Read(run, fixture.Parent));
        }
    }

    [Theory]
    [InlineData(PendingKind.Approval, ProcessDecisionChildPendingReason.ApprovalRequired)]
    [InlineData(PendingKind.RetainedLease, ProcessDecisionChildPendingReason.ApprovalRequired)]
    [InlineData(PendingKind.OpenWork, ProcessDecisionChildPendingReason.ActiveWork)]
    [InlineData(PendingKind.Reconciliation, ProcessDecisionChildPendingReason.UnresolvedEffects)]
    [InlineData(PendingKind.CancelledUnknown, ProcessDecisionChildPendingReason.UnresolvedEffects)]
    [InlineData(PendingKind.FailedPrepared, ProcessDecisionChildPendingReason.UnresolvedEffects)]
    [InlineData(PendingKind.BasisChanged, ProcessDecisionChildPendingReason.BasisChanged)]
    public void Pending_evidence_never_supplies_work_settlement(PendingKind kind, ProcessDecisionChildPendingReason expected) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var prepared = fixture.Prepare();
        var run = prepared.Run;
        var journal = run.ToolAdmission! with { ActiveDispatchLeaseId = null };
        var decision = journal.PlanningDecision!;
        var proposal = journal.Batches[0].Proposals[0];
        if (kind == PendingKind.RetainedLease) {
            journal = journal with { ActiveDispatchLeaseId = Guid.NewGuid() };
        } else if (kind == PendingKind.OpenWork) {
            journal = journal with { PlanningDecision = AgentPlanningDecisionTransitions.ResumeEffects(decision,
                decision.Current.Steps[^1].Id, Guid.NewGuid(), decision.Work.LastObservedAtUtc) };
        } else if (kind is PendingKind.Reconciliation or PendingKind.CancelledUnknown) {
            proposal = proposal with { State = AgentToolProposalState.ReconciliationRequired, DispatchClaimId = Guid.NewGuid(),
                ApprovalStatus = ExecutionApprovalStatus.Approved, ApprovedDigest = proposal.Payload.Digest };
            if (kind == PendingKind.CancelledUnknown) {
                proposal = proposal with { State = AgentToolProposalState.Cancelled, Cancellation = new(
                    AgentToolCancellationDisposition.CancelledUnreconciled, AgentToolCancellationReason.ReceiptNotObserved) };
            }
            journal = journal with { Batches = journal.Batches.SetItem(0, journal.Batches[0] with { Proposals = [proposal] }) };
        } else if (kind == PendingKind.FailedPrepared) {
            journal = journal with { PlanningDecision = AgentPlanningDecisionTransitions.Fail(decision) };
        } else if (kind == PendingKind.BasisChanged) {
            decision = AgentPlanningDecisionTransitions.ResumeEffects(decision, decision.Current.Steps[^1].Id, Guid.NewGuid(), decision.Work.LastObservedAtUtc);
            journal = journal with { PlanningDecision = AgentPlanningDecisionTransitions.Settle(decision, decision.Work.Open!.Id,
                TimeSpan.Zero, decision.Work.LastObservedAtUtc, AgentPlanningWorkSettlementReason.PausedBasisChanged, AgentPlanningGenerationState.PausedBasisChanged) };
        }
        Assert.Equal(expected, Assert.IsType<ProcessDecisionChildResult.Pending>(
            ProjectTaskPlanProcessOutcome.Read(run with { ToolAdmission = journal }, fixture.Parent).Outcome).Reason);
    }

    [Theory]
    [InlineData(RunChange.Identity)]
    [InlineData(RunChange.Agent)]
    [InlineData(RunChange.Source)]
    [InlineData(RunChange.Created)]
    [InlineData(RunChange.Outcome)]
    [InlineData(RunChange.State)]
    [InlineData(RunChange.CompletedTime)]
    public void Substituted_run_and_contradictory_terminal_evidence_are_rejected(RunChange change) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var run = Closed(fixture, AgentPlanningGenerationState.Completed, true).Run;
        run = change switch {
            RunChange.Identity => run with { Id = Guid.NewGuid() },
            RunChange.Agent => run with { AgentId = Guid.NewGuid() },
            RunChange.Source => run with { SourceId = "another-step" },
            RunChange.Created => run with { CreatedAtUtc = run.CreatedAtUtc.AddTicks(1) },
            RunChange.Outcome => run with { Outcome = RunOutcome.Failed },
            RunChange.State => run with { State = ExecutionState.Running },
            RunChange.CompletedTime => run with { CompletedAtUtc = null },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.Throws<InvalidDataException>(() => ProjectTaskPlanProcessOutcome.Read(run, fixture.Parent));
    }

    [Fact]
    public void A_different_original_allocation_cannot_borrow_closed_work() {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var run = Closed(fixture, AgentPlanningGenerationState.Completed, false).Run;
        var changed = fixture.Parent with { Child = fixture.Parent.Child with { AllocationId = new(Guid.NewGuid()) } };
        Assert.Throws<InvalidDataException>(() => ProjectTaskPlanProcessOutcome.Read(run, changed));
    }

    [Fact]
    public void Overrun_is_preserved_and_never_clamped_to_the_grant() {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var run = Closed(fixture, AgentPlanningGenerationState.Failed, false, TimeSpan.FromMinutes(2)).Run;
        var outcome = Assert.IsType<ProcessDecisionChildResult.Closed>(ProjectTaskPlanProcessOutcome.Read(run, fixture.Parent).Outcome);
        Assert.True(outcome.Work.Consumed.WorkTicks > fixture.Parent.Child.Grant.WorkTicks);
        Assert.Equal(TimeSpan.FromSeconds(121).Ticks, outcome.Work.Consumed.WorkTicks);
    }

    [Fact]
    public void Multiple_committed_proposals_cannot_collapse_into_one_effect_receipt() {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var run = Closed(fixture, AgentPlanningGenerationState.Completed, true).Run;
        var journal = run.ToolAdmission!;
        var batch = journal.Batches[0];
        var second = batch.Proposals[0] with { IntentId = new(Guid.NewGuid()), Ordinal = 1, CallId = "second-call" };
        var decision = journal.PlanningDecision!;
        var step = decision.Current.Steps[0];
        step = step with { Publications = step.Publications.Add(step.Publications[0] with {
            Proposal = new(batch.Id, second.IntentId, second.Payload.SemanticVersion, second.Payload.Digest)
        }) };
        journal = journal with {
            Batches = journal.Batches.SetItem(0, batch with { Proposals = batch.Proposals.Add(second) }),
            PlanningDecision = decision with { Generations = [decision.Current with { Steps = [step] }] }
        };
        journal.Validate();
        Assert.Throws<InvalidDataException>(() => ProjectTaskPlanProcessOutcome.Read(run with { ToolAdmission = journal }, fixture.Parent));
    }

    private static (ExecutionRunRecord Run, Guid Operation) Closed(ProjectTaskPlanProcessCodecFixture fixture,
        AgentPlanningGenerationState state, bool accepted, TimeSpan? elapsed = null) {
        var prepared = fixture.Prepare();
        var run = prepared.Run;
        var journal = run.ToolAdmission!;
        var decision = journal.PlanningDecision!;
        var command = ProjectTaskPlanProcessCodec.Resolve(decision, journal.Session, prepared.Payload, prepared.Binding).Command;
        var proposal = journal.Batches[0].Proposals[0] with {
            State = AgentToolProposalState.Completed, ApprovalStatus = ExecutionApprovalStatus.Approved,
            ApprovedDigest = prepared.Payload.Digest, DispatchClaimId = Guid.NewGuid(),
            EffectState = accepted ? AgentToolEffectState.Committed : AgentToolEffectState.NotCommitted,
            Result = AgentPlanningDecisionTestData.Envelope("{\"opaqueAdapterCheckpoint\":true}")
        };
        decision = AgentPlanningDecisionTransitions.ResumeEffects(decision, decision.Current.Steps[0].Id, Guid.NewGuid(), decision.Work.LastObservedAtUtc);
        var spent = elapsed ?? TimeSpan.FromSeconds(2);
        decision = AgentPlanningDecisionTransitions.Settle(decision, decision.Work.Open!.Id, spent,
            decision.Work.LastObservedAtUtc + spent, AgentPlanningWorkSettlementReason.Terminal, state);
        journal = journal with {
            ActiveDispatchLeaseId = Guid.NewGuid(), PlanningDecision = decision,
            Batches = journal.Batches.SetItem(0, journal.Batches[0] with { Proposals = [proposal] })
        };
        journal.Validate();
        return (run with { ToolAdmission = journal, State = state == AgentPlanningGenerationState.Failed ? ExecutionState.Failed : ExecutionState.Completed,
            Outcome = state == AgentPlanningGenerationState.Completed ? RunOutcome.Succeeded : state == AgentPlanningGenerationState.Failed ? RunOutcome.Failed : RunOutcome.Cancelled,
            CompletedAtUtc = decision.Work.LastObservedAtUtc }, command.Operation.Id);
    }

    private static ExecutionRunRecord WithOwnerReceipt(ExecutionRunRecord run, Guid operation, bool cancelled) {
        var proposal = run.ToolAdmission!.Batches[0].Proposals[0];
        var result = new ProjectTaskPlanAgentResult(ProjectTaskPlanAgentResultKind.Published, operation, null, [Guid.NewGuid()], true, false).OwnerResult;
        AgentToolCommittedEffect effect = new(ProjectTaskPlanAgentCodec.EffectSourceKind, operation.ToString("D"));
        return ReplaceProposal(run, proposal with { State = cancelled ? AgentToolProposalState.Cancelled : AgentToolProposalState.OwnerReceiptConfirmed,
            Result = null, OwnerReceipt = cancelled ? null : new(effect, result),
            Cancellation = cancelled ? new(AgentToolCancellationDisposition.ReceiptCommitted, AgentToolCancellationReason.ReceiptFound, effect, result) : null });
    }

    private static ExecutionRunRecord ReplaceProposal(ExecutionRunRecord run, AgentToolProposalRecord proposal) {
        var journal = run.ToolAdmission!;
        return run with { ToolAdmission = journal with { Batches = journal.Batches.SetItem(0, journal.Batches[0] with { Proposals = [proposal] }) } };
    }

    public enum ReceiptChange { Kind, Identity, Operation, TaskCount, Refusal }
    public enum PendingKind { Approval, RetainedLease, OpenWork, Reconciliation, CancelledUnknown, FailedPrepared, BasisChanged }
    public enum RunChange { Identity, Agent, Source, Created, Outcome, State, CompletedTime }
}
