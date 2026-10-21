using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentInheritedPlanningReviewCoordinator(ISandboxWorkspaceExecutionRunStore runs,
    AgentToolAdmissionJournal journal, AgentInheritedPlanningCoordinator inherited) {
    private readonly AgentPlanningDecisionOwner owner = new(journal);

    public Task<AgentInheritedPlanningReviewResult> ReviewAsync(AgentInheritedPlanningReviewRequest request,
        AgentInheritedSavedEffectContinuation authority, AgentToolSemanticDigest ownerIntent,
        IAgentContinuationRuntime continuation, CancellationToken cancellationToken = default)
        => ReviewWithinLifecycleAsync(request, authority, ownerIntent, continuation,
            static (result, _) => Task.FromResult(result), cancellationToken);

    public async Task<T> ReviewWithinLifecycleAsync<T>(AgentInheritedPlanningReviewRequest request,
        AgentInheritedSavedEffectContinuation authority, AgentToolSemanticDigest ownerIntent,
        IAgentContinuationRuntime continuation,
        Func<AgentInheritedPlanningReviewResult, CancellationToken, Task<T>> settleOriginal,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(continuation);
        ArgumentNullException.ThrowIfNull(settleOriginal);
        authority.RequireSelection(request.Expected, request.Binding);
        var detail = await runs.GetExecutionRunDetailAsync(request.Expected.ExecutionRunId, cancellationToken)
            ?? throw new AgentToolReceiptAccessDeniedException();
        var original = detail.Run;
        authority.RequireOriginal(original);
        var intent = new AgentPlanningReviewIntent(request, original, ownerIntent);
        var boundAuthority = authority.BindReviewIntent(intent);
        await RequireDisclosureAsync(original, boundAuthority, cancellationToken);
        if (intent.IsReplay(detail)) {
            return await FinishAsync(new(original, Replayed: true));
        }
        intent.RequirePending(detail);
        var decision = original.ToolAdmission!.PlanningDecision!;
        var participant = inherited.RequireParticipant(decision.Owner, authority.Original.ParentBinding);
        ExecutionRunRecord completed;
        if (request.Choice == AgentPlanningReviewChoice.Reject) {
            completed = (await owner.RejectInheritedAsync(request.Expected, boundAuthority,
                CurrentReadAsync, cancellationToken)).Run;
        } else {
            if (continuation.SavedEffectDispatchSupport != AgentSavedEffectDispatchSupport.OriginalPreparedOwnerEffect) {
                throw new NotSupportedException("The current runtime cannot dispatch an original prepared planning publication.");
            }
            var saved = new AgentSavedEffectContinuationRequest(original.Id, null,
                request.Expected.RunRevision, request.Expected.JournalRevision, request.Binding,
                [new(request.ApprovalId, true) { ToolAdmission = request.Binding }]);
            await using var lease = await owner.ResumeInheritedSavedEffectAsync(saved, request.Expected,
                boundAuthority, CurrentReadAsync, cancellationToken);
            using var bound = lease.Bind();
            completed = (await new AgentPlanningPublicationCompletion(journal, owner).DispatchAsync(original, lease,
                AgentSavedEffectDispatchRequest.ForInherited(original.ToolAdmission.Session.Reference, request.Binding),
                participant, continuation, cancellationToken)).Run;
        }
        return await FinishAsync(new(completed, Replayed: false));

        async Task<T> FinishAsync(AgentInheritedPlanningReviewResult result) {
            using var settlement = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var completed = await settleOriginal(result, settlement.Token);
            await RequireDisclosureAsync(result.Run, boundAuthority, cancellationToken);
            return completed;
        }

        async ValueTask<bool> CurrentReadAsync(AgentToolSessionObservation observation, CancellationToken token) {
            if (observation.Session != original.ToolAdmission.Session ||
                observation.Planning?.Decision.DecisionId != request.Expected.DecisionId) {
                return false;
            }
            await participant.RequireCurrentReadAsync(observation.Planning.Decision, token);
            return true;
        }
    }

    private async Task RequireDisclosureAsync(ExecutionRunRecord run, AgentInheritedSavedEffectContinuation authority,
        CancellationToken cancellationToken) {
        await authority.RequireCurrentManagerAsync(cancellationToken);
        await inherited.RequireCurrentReadAsync(run, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}

public sealed record AgentInheritedPlanningReviewResult(ExecutionRunRecord Run, bool Replayed);
