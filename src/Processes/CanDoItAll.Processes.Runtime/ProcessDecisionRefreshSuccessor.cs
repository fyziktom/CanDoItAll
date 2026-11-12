using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessDecisionRefreshSuccessor(RuntimeCommandId AdmissionCommandId) {
    public RuntimeCommandId CommandId {
        get {
            ArgumentOutOfRangeException.ThrowIfEqual(AdmissionCommandId.Value, Guid.Empty);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new {
                Domain = "process-decision-refresh-successor/v1", AdmissionCommandId
            });
            return new(new Guid(SHA256.HashData(bytes).AsSpan(0, 16), bigEndian: true));
        }
    }

    public ProcessRuntimeMutation Prepare(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionRefreshIntent intent, ProcessDecisionRefresh command) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(command);
        context.Validate();
        intent.Validate();
        command.Validate();
        var original = intent.Request;
        if (AdmissionCommandId != original.CommandId || command.AdmissionCommandId != AdmissionCommandId ||
                command.StepInstanceId != original.Child.StepInstanceId || command.Expectation.Reference != original.Child.Reference ||
                command.MaterialId == intent.OriginalMaterialId || command.InheritedScope?.ScopeId != original.SharedScope?.ScopeId ||
                command.Kind != (original.SharedScope is null ? original.Kind : ProcessDecisionRefreshKind.ParentRefresh)) {
            throw new InvalidOperationException("A successor requires the original refresh action and distinct material from its original owner.");
        }
        if (ProcessDecisionReviewCompletion.ValidatePending(state, original.Child, original.StrategyId, original.WaitingResultKey) is { } refusal) {
            throw new InvalidOperationException(refusal.Message);
        }
        var ticks = Math.Max(context.OccurredAtUtc.UtcTicks / 10 * 10, checked(state.UpdatedAtUtc.UtcTicks + 10));
        context = context with { CommandId = CommandId, Actor = intent.Actor, OccurredAtUtc = new(ticks, TimeSpan.Zero) };
        var next = state with {
            Status = ProcessRuntimeStatus.Active,
            Steps = state.Steps.Select(step => step.StepInstanceId == command.StepInstanceId
                ? step with { Status = ProcessRuntimeStepStatus.Ready } : step).ToArray(),
            UpdatedAtUtc = context.OccurredAtUtc
        };
        var payload = JsonSerializer.SerializeToUtf8Bytes(new {
            Domain = "process-decision-refresh-successor-event/v1", AdmissionCommandId, command.StepInstanceId,
            command.Expectation, command.MaterialId, command.Material, command.InheritedScope
        });
        var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        List<ProcessRuntimeEventEnvelope> events = [
            ProcessRuntimeEventFactory.Create(next, context, ProcessRuntimeEventTypes.StepReworkRequested, digest)
        ];
        if (state.Status != ProcessRuntimeStatus.Active) {
            events.Add(ProcessRuntimeEventFactory.Create(next, context, ProcessRuntimeEventTypes.ProcessRunReactivated, digest));
        }
        events.Add(ProcessRuntimeEventFactory.Create(next, context, ProcessRuntimeEventTypes.StepReady, digest));
        return ProcessRuntimeMutation.Applied(next, events, []);
    }

    public void RequireShape(ProcessRuntimeCommitRequest request) {
        if (request.CommandId != CommandId || request.Decision is not ProcessDecisionRefresh { AdmissionCommandId: { } admission } ||
                admission != AdmissionCommandId || request.DecisionBudget is not ProcessDecisionBudgetSettlement ||
                request.Mutation.Outcome != ProcessRuntimeTransitionOutcome.Applied || request.Mutation.Events.Count == 0 ||
                request.ParentStepPrecondition is not null || request.InitialPlan is not null || request.InitialAssignments is not null ||
                request.InitialLaunchAdmission is not null || request.InitialRunDecisionAdmission is not null ||
                request.BlockedRecoveryAuthorization is not null || request.DispatchPrecondition is not null || request.ConfirmRootCancellation ||
                request.RefreshAccounting is not null || request.DecisionRunScope is not null ||
                request.Mutation.ArtifactLedgerEvents.Count != 0 || request.Mutation.Diagnostics.Count != 0 ||
                request.Mutation.State.UpdatedAtUtc.Offset != TimeSpan.Zero || request.Mutation.State.UpdatedAtUtc.UtcTicks % 10 != 0 ||
                request.Mutation.State.UpdatedAtUtc <= request.OriginalState.UpdatedAtUtc) {
            throw new InvalidOperationException("A refresh successor may only settle its capture and resume the original waiting step.");
        }
    }

    public void RequireMutation(ProcessRuntimeCommitRequest request, ProcessDecisionRefreshIntent intent) {
        RequireShape(request);
        var mutation = request.Mutation;
        var expected = Prepare(request.OriginalState,
            new(CommandId, intent.Actor, mutation.Events[0].CorrelationId, mutation.State.UpdatedAtUtc), intent,
            (ProcessDecisionRefresh)request.Decision!);
        if (mutation.State with { Steps = expected.State.Steps } != expected.State ||
                !mutation.State.Steps.SequenceEqual(expected.State.Steps) ||
                mutation.Events.Count != expected.Events.Count || mutation.Events.Where((item, index) =>
                    item with { EventId = expected.Events[index].EventId } != expected.Events[index]).Any() ||
                mutation.OutboxMessages.Count != mutation.Events.Count ||
                mutation.OutboxMessages.Select(item => item.MessageId).Distinct().Count() != mutation.OutboxMessages.Count ||
                mutation.OutboxMessages.Where((item, index) => item.MessageId.Value == Guid.Empty ||
                    item.EventId != mutation.Events[index].EventId || item.PayloadHash != mutation.Events[index].PayloadHash ||
                    item.SubscriberKind != ProcessOutboxSubscriberKind.RuntimeProjection).Any()) {
            throw new InvalidOperationException("A refresh successor changed original runtime history, attempts or its exact readiness projection.");
        }
    }
}
