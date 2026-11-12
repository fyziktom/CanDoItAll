using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessDecisionFinalizationCoordinator(IProcessDecisionResultQuery query,
    IProcessIdempotencyStore receipts, IProcessRuntimeUnitOfWork unitOfWork) {
    private const string StepChanged = "Runtime.DecisionFinalizationStepChanged";
    private const string WorkPending = "Runtime.DecisionFinalizationWorkPending";

    public async Task<ProcessRuntimeCommitResult> CompleteAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionFinalizationRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Actor);
        ArgumentNullException.ThrowIfNull(request);
        context.Validate();
        request.Validate();
        if (!Enum.IsDefined(context.Actor.Kind) || context.Actor.Kind == ProcessEventActorKind.Unknown ||
                string.IsNullOrWhiteSpace(context.Actor.Id.Value)) {
            throw new InvalidDataException("Terminal decision finalization requires an explicit original action actor.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        var command = request.Command;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new {
            Domain = "process-decision-terminal-finalization/v1", state.RootRunId, state.RunId, request, Actor = context.Actor
        });
        RuntimeCommandId identity = new(new Guid(SHA256.HashData(bytes).AsSpan(0, 16), bigEndian: true));
        if (await receipts.FindCompletedCommandAsync(identity, cancellationToken).ConfigureAwait(false) is { } replay) {
            RequireReceipt(replay, state, command);
            return replay with { Outcome = ProcessRuntimeTransitionOutcome.Duplicate, CommittedNow = false };
        }
        var step = state.Steps.SingleOrDefault(item => item.StepInstanceId == request.StepInstanceId);
        if (step is null || step.Status != request.StepStatus || step.ActiveClaimToken is not null ||
                state.Claims.Any(claim => claim.StepInstanceId == step.StepInstanceId &&
                    claim.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed)) {
            return Reject(state, StepChanged, "Finalization requires the displayed terminal step without live dispatch work.");
        }
        var selected = await query.ReadAsync(state.RunId, step.StepInstanceId, null, cancellationToken).ConfigureAwait(false);
        selected?.RequireOriginal(state, step.StepInstanceId, null);
        if (selected?.Head is not { Phase: ProcessDecisionPhase.Active or ProcessDecisionPhase.WaitingForReview or ProcessDecisionPhase.Reconciling } head ||
                head.Expectation != request.Decision || head.UnclosedChildCount != 0 || selected.Child is not null ||
                selected.Budget.BudgetId != request.Budget.BudgetId || selected.Budget.Revision != request.Budget.Revision ||
                selected.Budget.ReservedWorkTicks != 0 || selected.Budget.ReservedReplans != 0 || selected.OtherWorkPending) {
            return Reject(state, WorkPending, "Finalization requires the exact displayed decision and budget with all original work settled.");
        }
        var now = context.OccurredAtUtc.UtcTicks;
        var updated = Math.Max(now - now % 10, checked(state.UpdatedAtUtc.UtcTicks + 10));
        ProcessRuntimeMutation mutation = new(ProcessRuntimeTransitionOutcome.Applied,
            state with { UpdatedAtUtc = new(updated, TimeSpan.Zero) }, [], [], [], []);
        return await unitOfWork.CommitAsync(new(identity, state, mutation) { Decision = command }, cancellationToken).ConfigureAwait(false);
    }

    private static void RequireReceipt(ProcessRuntimeCommitResult replay, ProcessRuntimeStateSnapshot state, ProcessDecisionFinalization command) {
        if (!replay.Succeeded || replay.State.RootRunId != state.RootRunId || replay.State.RunId != state.RunId ||
                replay.Events.Count != 0 || replay.OutboxMessages.Count != 0 || replay.ArtifactLedgerEvents.Count != 0 ||
                replay.DecisionBudget is not null || replay.DecisionRunScope is not null ||
                replay.Decision is not { Changed: true, Previous: null, Child: null } decision || decision.Command != command ||
                decision.Current.Reference != command.Expectation.Reference || decision.Current.BudgetId != command.Budget.BudgetId ||
                decision.Current.Revision != checked(command.Expectation.Revision + 1) || decision.Current.UnclosedChildCount != 0 ||
                decision.Current.Phase != ProcessDecisionTransitions.TerminalPhase(command.StepStatus)) {
            throw new InvalidDataException("The terminal decision receipt differs from its original operation or contains business effects.");
        }
        decision.Current.Validate();
    }

    private static ProcessRuntimeCommitResult Reject(ProcessRuntimeStateSnapshot state, string code, string message)
        => ProcessRuntimeCommitResult.FromMutation(ProcessRuntimeMutation.Rejected(state, code, message));
}
