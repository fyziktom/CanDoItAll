using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessDecisionRefreshAccounting(RuntimeCommandId AdmissionCommandId) {
    public RuntimeCommandId CommandId {
        get {
            ArgumentOutOfRangeException.ThrowIfEqual(AdmissionCommandId.Value, Guid.Empty);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new {
                Domain = "process-decision-refresh-accounting/v1", AdmissionCommandId
            });
            return new(new Guid(SHA256.HashData(bytes).AsSpan(0, 16), bigEndian: true));
        }
    }

    public StrategyResultIdempotencyKey CompletionKey => new(CommandId.Value);

    public void RequireShape(ProcessRuntimeCommitRequest request) {
        if (request.RefreshAccounting != this || request.CommandId != CommandId ||
                request.Mutation.Outcome != ProcessRuntimeTransitionOutcome.Applied ||
                request.Decision is not ProcessDecisionClosure || request.DecisionBudget is not ProcessDecisionBudgetSettlement ||
                request.ParentStepPrecondition is not null || request.InitialPlan is not null || request.InitialAssignments is not null ||
                request.InitialLaunchAdmission is not null || request.InitialRunDecisionAdmission is not null ||
                request.BlockedRecoveryAuthorization is not null || request.DispatchPrecondition is not null || request.ConfirmRootCancellation ||
                request.DecisionMaterial is not null ||
                request.Mutation.ArtifactLedgerEvents.Count != 0 || request.Mutation.Diagnostics.Count != 0 ||
                request.Mutation.State.UpdatedAtUtc.Offset != TimeSpan.Zero || request.Mutation.State.UpdatedAtUtc.UtcTicks % 10 != 0 ||
                request.Mutation.State.UpdatedAtUtc <= request.OriginalState.UpdatedAtUtc) {
            throw new InvalidOperationException("Refresh accounting requires only its deterministic original-child closure, settlement and permitted result.");
        }
    }

    public void RequireMutation(ProcessRuntimeCommitRequest request, ProcessDecisionRefreshIntent intent) {
        RequireShape(request);
        intent.Validate();
        var original = request.OriginalState;
        var mutation = request.Mutation;
        var closure = (ProcessDecisionClosure)request.Decision!;
        var settlement = (ProcessDecisionBudgetSettlement)request.DecisionBudget!;
        closure.Validate();
        settlement.Settlement.Validate();
        if (AdmissionCommandId != intent.Request.CommandId || closure.StepInstanceId != intent.Request.Child.StepInstanceId ||
                closure.ChildId != intent.Request.Child.ChildId || closure.Expectation.Reference != intent.Request.Child.Reference ||
                settlement.Expectation != intent.Request.Budget || closure.Closure.OwnerRevision != settlement.Settlement.OwnerWorkRevision) {
            throw InvalidMutation();
        }
        if (closure.Closure.Outcome == ProcessDecisionChildOutcome.NoEffect) {
            if (closure.Completion != ProcessDecisionCompletion.Reconcile || request.DecisionRunScope is not null ||
                    mutation.Events.Count != 0 || mutation.OutboxMessages.Count != 0 ||
                    mutation.State with { UpdatedAtUtc = original.UpdatedAtUtc } != original) {
                throw InvalidMutation();
            }
            return;
        }
        var results = mutation.State.AppliedResults;
        if (results.Count != original.AppliedResults.Count + 1 || mutation.Events.Count == 0 ||
                !results.Take(original.AppliedResults.Count).SequenceEqual(original.AppliedResults)) {
            throw InvalidMutation();
        }
        var result = results[^1];
        if (result.IdempotencyKey != CompletionKey || result.Outcome is not (StrategyOutcome.Succeeded or StrategyOutcome.Failed or StrategyOutcome.Canceled) ||
                closure.Completion != (result.Outcome == StrategyOutcome.Canceled ? ProcessDecisionCompletion.Cancel : ProcessDecisionCompletion.Complete)) {
            throw InvalidMutation();
        }
        var context = new RuntimeCommandContext(CommandId, intent.Actor, mutation.Events[0].CorrelationId, mutation.State.UpdatedAtUtc);
        var expected = ProcessDecisionReviewCompletion.Prepare(original, context,
            new(intent.Request.Child, intent.Request.StrategyId, intent.Request.WaitingResultKey, CompletionKey,
                result.Outcome, closure.Closure, settlement.Settlement));
        if (!expected.Succeeded || expected.Outcome != ProcessRuntimeTransitionOutcome.Applied ||
                mutation.State with { Steps = expected.State.Steps, AppliedResults = expected.State.AppliedResults } != expected.State ||
                !mutation.State.Steps.SequenceEqual(expected.State.Steps) ||
                result.Diagnostics.Count != 0 || result.ProducedArtifacts.Count != 0 ||
                result with { Diagnostics = expected.State.AppliedResults[^1].Diagnostics,
                    ProducedArtifacts = expected.State.AppliedResults[^1].ProducedArtifacts } != expected.State.AppliedResults[^1] ||
                mutation.Events.Count != expected.Events.Count || mutation.Events.Where((item, index) =>
                    item with { EventId = expected.Events[index].EventId } != expected.Events[index]).Any() ||
                mutation.OutboxMessages.Count != mutation.Events.Count ||
                mutation.OutboxMessages.Select(item => item.MessageId).Distinct().Count() != mutation.OutboxMessages.Count ||
                mutation.OutboxMessages.Where((item, index) => item.MessageId.Value == Guid.Empty ||
                    item.EventId != mutation.Events[index].EventId || item.PayloadHash != mutation.Events[index].PayloadHash ||
                    item.SubscriberKind != ProcessOutboxSubscriberKind.RuntimeProjection).Any()) {
            throw InvalidMutation();
        }
        var scope = intent.Request.SharedScope is { } shared ? shared with { Revision = checked(shared.Revision + 1) } : null;
        if (request.DecisionRunScope != ProcessDecisionRunScopeCompletion.Prepare(scope, original, mutation)) {
            throw InvalidMutation();
        }
    }

    private static InvalidOperationException InvalidMutation()
        => new("Refresh accounting changed its original admitted result, runtime state or measured work.");
}
