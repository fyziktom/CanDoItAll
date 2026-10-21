using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public enum ProcessPlanningReviewState { AwaitingChoice, RecoveryRequired, Settled, Unavailable }

public sealed record ProjectTaskPlanProcessReview(ExecutionRunRecord Run, ProcessPlanningReviewState State,
    ProcessDecisionPendingReview? Process, AgentPlanningDecisionExpectation Expected,
    AgentPlanningPublicationReview? Publication, ImmutableArray<AgentPlanningReviewChoice> AvailableChoices,
    DateTimeOffset ObservedAtUtc, DateTimeOffset? BasisCapturedAtUtc, TimeSpan ChargedWork,
    TimeSpan? RemainingWork, bool HasUnsettledWork, string? RefusalCode);

public sealed class ProjectTaskPlanProcessReviewReader(AgentInheritedPlanningRunReader runs,
    IProcessRuntimeStateStore states, ProcessDecisionReviewCoordinator reviews, ProjectProcessLaunchAuthorityService sources,
    ICanonicalRuntimeDatabase database, IAgentExecutionProfileGenerationSource generations, TimeProvider clock) {
    private const string ObservationChangedCode = "process-review.observation-changed";

    public async Task<ProjectTaskPlanProcessReview> ReadAsync(ProcessRunId runId, ProcessStepInstanceId stepId, Guid executionRunId,
        ProcessPlanningManagerAuthority manager, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentOutOfRangeException.ThrowIfEqual(runId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(stepId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(executionRunId, Guid.Empty);
        await manager.RequireCurrentAsync(cancellationToken);
        await using var authority = await sources.AcquireAsync(manager.Original, manager.Original, cancellationToken);
        var detail = await runs.ReadAsync(executionRunId, cancellationToken);
        var run = detail.Run;
        if (run.ToolAdmission?.PlanningDecision is not { Owner: AgentPlanningOwner.ProjectTaskPlan, InheritedOrigin: { } origin } decision) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var original = ProjectTaskPlanProcessCodec.Parent(origin);
        if (original.Child.Address.RunId != runId || original.Child.Address.StepInstanceId != stepId ||
                original.Child.Address.ChildId.Value != executionRunId) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var state = await states.LoadAsync(runId, cancellationToken)
            ?? throw new InvalidDataException("The original Process runtime state is unavailable.");
        var observed = await reviews.ObserveAsync(state, stepId, new(executionRunId), cancellationToken);
        if (observed.Child.Original != original) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var now = clock.GetUtcNow();
        if (now < decision.Work.LastObservedAtUtc) {
            throw new AgentPlanningAdmissionException(decision.DecisionId, decision.CurrentGeneration, AgentPlanningAdmissionRefusal.ClockInvalid);
        }
        var basis = decision.Current.Bases.LastOrDefault();
        var unsettled = decision.Work.Open is not null;
        TimeSpan? remaining = unsettled ? null
            : TimeSpan.FromTicks(Math.Max(0, decision.Policy.Budget.MaximumWorkDuration.Ticks - decision.Work.ClosedWorkTicks));
        var status = observed.IsSettled ? ProcessPlanningReviewState.Settled
            : observed.Child.Observation.Closure is not null ? ProcessPlanningReviewState.RecoveryRequired
            : observed.Pending is null ? ProcessPlanningReviewState.Unavailable : ProcessPlanningReviewState.RecoveryRequired;
        AgentPlanningPublicationReview? publication = null;
        List<AgentPlanningReviewChoice> choices = [];
        if (observed.Pending is not null && run.Outcome is null && !unsettled &&
                decision.Current.State == AgentPlanningGenerationState.AwaitingApproval && !run.ToolAdmission.HasUnresolvedProviderDispatch &&
                run.PendingApprovals.SingleOrDefault() is { ToolAdmission: { } binding } pending) {
            var selected = AgentToolJournalTransitions.RequireProposal(run.ToolAdmission, binding);
            var retained = detail.Approvals.SingleOrDefault(item => item.ApprovalId == pending.ApprovalId);
            if (decision.Current.Steps.LastOrDefault()?.Publications.SingleOrDefault()?.Proposal == binding &&
                    (retained is null || retained is { Status: ExecutionApprovalStatus.Pending, DecidedAtUtc: null } &&
                        retained.ExecutionRunId == run.Id && retained.ToolAdmission == binding) &&
                    selected is {
                        State: AgentToolProposalState.Prepared, ApprovalStatus: ExecutionApprovalStatus.Pending,
                        DispatchClaimId: null, ApprovedDigest: null, Result: null, OwnerReceipt: null, ProviderCall: null,
                        Payload: { Effect: AgentToolProposalEffect.Mutation, Recovery: AgentToolProposalRecovery.OwnerReceipt, SourcePreparation: not null }
                    }) {
                publication = new(binding, pending.ApprovalId, ProjectTaskPlanPlanningContent.Describe(
                    ProjectTaskPlanProcessCodec.Resolve(decision, run.ToolAdmission.Session, selected.Payload, binding).Command));
                status = ProcessPlanningReviewState.AwaitingChoice;
                if (remaining > TimeSpan.Zero && basis is not null && now >= basis.CapturedAtUtc &&
                        now - basis.CapturedAtUtc < decision.Policy.Budget.MaximumBasisAge) {
                    choices.Add(AgentPlanningReviewChoice.Approve);
                }
                choices.Add(AgentPlanningReviewChoice.Reject);
            }
        }
        var current = await runs.ReadAsync(executionRunId, cancellationToken);
        if (current.Run.Revision != run.Revision) {
            throw new AgentToolAdmissionException(ObservationChangedCode, "The original child changed while its review was read.");
        }
        RequireProfile();
        await manager.RequireCurrentAsync(cancellationToken);
        try {
            await authority.RequireForMutationAsync(null, cancellationToken);
        } catch (ProcessLaunchAuthorityRejectedException) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        RequireProfile();
        cancellationToken.ThrowIfCancellationRequested();
        return new(run, status, status == ProcessPlanningReviewState.AwaitingChoice ? observed.Pending : null,
            AgentPlanningDecisionExpectation.From(run), publication, [.. choices], now, basis?.CapturedAtUtc,
            TimeSpan.FromTicks(decision.Work.ClosedWorkTicks), remaining, unsettled, observed.Refusal?.Code);

        void RequireProfile() {
            if (manager.Original.DatabaseProfileId != origin.Profile.ProfileId || database.Profile.Profile.Id != origin.Profile.ProfileId ||
                    database.Profile.Profile.Runtime.Fingerprint != origin.Profile.Fingerprint || generations.GetGeneration() != origin.Profile.Generation) {
                throw new AgentToolReceiptAccessDeniedException();
            }
        }
    }
}
