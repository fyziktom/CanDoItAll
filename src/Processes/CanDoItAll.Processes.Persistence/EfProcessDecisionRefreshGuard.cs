using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal static class EfProcessDecisionRefreshGuard {
    internal static async Task RequireAdvanceAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state,
        ProcessDecisionGeneration current, ProcessDecisionRefresh command, CancellationToken cancellationToken) {
        if (command.AdmissionCommandId != current.RefreshCommandId) {
            throw new InvalidOperationException("A successor must retain its original refresh admission authority.");
        }
        if (current.RefreshCommandId is not { } admissionId) {
            return;
        }
        var receipt = await EfProcessDecisionRefreshReceiptReader.ReadCoreAsync(context, state.RunId, admissionId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The successor has no retained original refresh admission.");
        var intent = ((ProcessDecisionBeginRefresh)receipt.Decision.Command).Intent;
        if (intent.Request.Child.RootRunId != state.RootRunId || intent.Request.Child.StepInstanceId != command.StepInstanceId ||
                intent.Request.Decision.Reference != current.Reference || intent.OriginalMaterialId != current.MaterialId ||
                command.Kind != (intent.Request.SharedScope is null ? intent.Request.Kind : ProcessDecisionRefreshKind.ParentRefresh)) {
            throw new InvalidOperationException("The successor differs from its originally admitted refresh kind or material owner.");
        }
    }

    internal static void RequireShape(ProcessRuntimeCommitRequest request) {
        if (request.Decision is not ProcessDecisionBeginRefresh begin) {
            return;
        }
        begin.Validate();
        var original = request.OriginalState;
        var mutation = request.Mutation;
        if (request.CommandId != begin.Intent.Request.CommandId ||
                mutation.Outcome != ProcessRuntimeTransitionOutcome.Applied ||
                mutation.State with { UpdatedAtUtc = original.UpdatedAtUtc } != original ||
                mutation.State.UpdatedAtUtc <= original.UpdatedAtUtc || mutation.State.UpdatedAtUtc.Offset != TimeSpan.Zero ||
                mutation.State.UpdatedAtUtc.UtcTicks % 10 != 0 ||
                request.ParentStepPrecondition is not null || request.InitialPlan is not null || request.InitialAssignments is not null ||
                request.InitialLaunchAdmission is not null || request.InitialRunDecisionAdmission is not null ||
                request.BlockedRecoveryAuthorization is not null || request.DispatchPrecondition is not null || request.ConfirmRootCancellation ||
                request.DecisionBudget is not null || request.DecisionMaterial is not null ||
                request.DecisionRunScope != ProcessDecisionRefreshReceipt.ScopeCommand(begin.Intent.Request) ||
                mutation.Events.Count != 0 || mutation.OutboxMessages.Count != 0 ||
                mutation.ArtifactLedgerEvents.Count != 0 || mutation.Diagnostics.Count != 0) {
            throw new InvalidOperationException("Refresh admission may only freeze its original decision and exact shared scope without business effects or other commands.");
        }
    }

    internal static async Task RequireFreshAsync(ProcessPersistenceDbContext context, ICanonicalRuntimeDatabase? database,
        ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
        if (request.Decision is not ProcessDecisionBeginRefresh begin) {
            return;
        }
        var state = request.OriginalState;
        var step = begin.Intent.Request.Child.StepInstanceId;
        var input = await EfProcessAcceptedExecutionInputReader.ReadCoreAsync(context,
            database?.Profile.Profile.Id ?? throw new InvalidOperationException("Refresh requires the canonical database owner."),
            state.RunId, step, cancellationToken).ConfigureAwait(false);
        var selection = await EfProcessDecisionResultQuery.ReadCoreAsync(context, state.RunId, step,
            new ProcessExecutionRunId(begin.Intent.Request.Child.ChildId.Value), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Refresh requires its original persisted decision.");
        if (begin != ProcessDecisionRefreshPreparation.Create(state, input, selection, begin.Intent.Request, begin.Intent.Actor)) {
            throw new InvalidDataException("Refresh admission changed its original accepted input or child material.");
        }
    }
}
