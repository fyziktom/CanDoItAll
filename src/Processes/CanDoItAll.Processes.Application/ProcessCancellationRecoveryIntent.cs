using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public abstract record ProcessCancellationRecoveryIntent(ProcessRunId RunId, RuntimeCommandContext Context) {
    public static ProcessEventActor Actor { get; } = new(ProcessEventActorKind.System, new("process-root-cancellation-recovery"));

    public static RuntimeCommandContext CreateContext(ProcessRunId rootRunId, DateTimeOffset occurredAtUtc)
        => new(RuntimeCommandId.New(), Actor, new(rootRunId.Value.ToString("D")), occurredAtUtc);

    public void Validate(ProcessRunId rootRunId, ProcessCancellationRecoveryStage stage) {
        ArgumentNullException.ThrowIfNull(Context);
        Context.Validate();
        if (rootRunId.Value == Guid.Empty || RunId.Value == Guid.Empty || Context.CommandId.Value == Guid.Empty || Context.Actor != Actor ||
                Context.CorrelationId.Value != rootRunId.Value.ToString("D") || Context.OccurredAtUtc == default) {
            throw new InvalidDataException("Recovery must retain its original action, root correlation and System owner.");
        }
        switch (this) {
            case ProcessCancellationCancelRun when stage == ProcessCancellationRecoveryStage.Runtime:
                break;
            case ProcessCancellationAccountChild account when stage == ProcessCancellationRecoveryStage.Decisions:
                account.Request.Validate();
                if (account.Request.Child.RootRunId != rootRunId || account.Request.Child.RunId != RunId || account.Request.ActionId != Context.CommandId) {
                    throw new InvalidDataException("Recovery accounting must retain the exact child root, run and action.");
                }
                break;
            case ProcessCancellationFinalizeDecision finalization when stage == ProcessCancellationRecoveryStage.Decisions:
                finalization.Request.Validate();
                if (finalization.Request.ActionId != Context.CommandId) {
                    throw new InvalidDataException("Recovery finalization must retain its exact action.");
                }
                break;
            case ProcessCancellationChangeScope scope when stage == ProcessCancellationRecoveryStage.SharedScope && RunId == rootRunId:
                scope.Command.Validate();
                if (scope.Command.Kind is not (ProcessDecisionRunScopeCommandKind.BeginReconciliation or ProcessDecisionRunScopeCommandKind.Cancel)) {
                    throw new InvalidDataException("Recovery cannot fabricate a shared capture or its measured settlement.");
                }
                break;
            case ProcessCancellationFinalizeRoot when stage == ProcessCancellationRecoveryStage.Quiescence && RunId == rootRunId:
                break;
            default:
                throw new InvalidDataException("The recovery intent does not belong to its selected stage and root.");
        }
    }
}

public sealed record ProcessCancellationCancelRun(ProcessRunId RunId, RuntimeCommandContext Context)
    : ProcessCancellationRecoveryIntent(RunId, Context);
public sealed record ProcessCancellationAccountChild(ProcessRunId RunId, RuntimeCommandContext Context, ProcessDecisionTerminalAccountingRequest Request)
    : ProcessCancellationRecoveryIntent(RunId, Context);
public sealed record ProcessCancellationFinalizeDecision(ProcessRunId RunId, RuntimeCommandContext Context, ProcessDecisionFinalizationRequest Request)
    : ProcessCancellationRecoveryIntent(RunId, Context);
public sealed record ProcessCancellationChangeScope(ProcessRunId RunId, RuntimeCommandContext Context, ProcessDecisionRunScopeCommand Command)
    : ProcessCancellationRecoveryIntent(RunId, Context);
public sealed record ProcessCancellationFinalizeRoot(ProcessRunId RunId, RuntimeCommandContext Context)
    : ProcessCancellationRecoveryIntent(RunId, Context);
