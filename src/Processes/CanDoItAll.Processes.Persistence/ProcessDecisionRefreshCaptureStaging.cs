using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

internal sealed class ProcessDecisionRefreshCaptureStaging(ProcessPersistenceDbContext context, ICanonicalRuntimeDatabase? database) {
    internal static void RequireShape(ProcessRuntimeCommitRequest request) {
        if (request.RefreshCapture is not { } capture) {
            return;
        }
        capture.Reservation.Validate();
        var original = capture.Reservation;
        if (request.OriginalState.RunId != original.RunId || request.OriginalState.RootRunId != original.RootRunId ||
                request.InitialPlan is not null || request.InitialAssignments is not null ||
                request.Mutation.Outcome != ProcessRuntimeTransitionOutcome.Applied || request.DecisionBudget is null ||
                request.RefreshAccounting is not null || request.ConfirmRootCancellation || request.ParentStepPrecondition is not null ||
                request.InitialRunDecisionAdmission is not null || request.InitialLaunchAdmission is not null ||
                request.BlockedRecoveryAuthorization is not null || request.DispatchPrecondition is not null) {
            throw new InvalidOperationException("Refresh capture must accompany only its original runtime reservation or settlement.");
        }
        var identity = new ProcessDecisionRefreshCaptureIdentity(original.AdmissionId);
        var reserve = original.IsRoot ? identity.RootReserve : identity.LocalReserve;
        var allocation = original.IsRoot ? identity.RootAllocation : identity.LocalAllocation;
        var complete = original.IsRoot ? identity.RootComplete : identity.Successor;
        var abort = original.IsRoot ? identity.RootAbort : identity.LocalAbort;
        if (original.ReservationId != reserve || original.Budget.AllocationId != allocation || original.Material.MaterialId != identity.Material ||
                capture.Disposition is { } disposition && !Enum.IsDefined(disposition) ||
                request.CommandId != (capture.Disposition is null ? reserve : capture.Disposition == ProcessDecisionRefreshCaptureDisposition.Advanced ? complete : abort)) {
            throw new InvalidDataException("Refresh capture changed its original stage identity.");
        }
        if (capture.Disposition is null) {
            if (request.DecisionBudget != original.Budget || request.Decision is not null || request.DecisionMaterial is not null) {
                throw new InvalidOperationException("Refresh journal creation requires the original budget reservation.");
            }
        } else if (request.DecisionBudget is not ProcessDecisionBudgetSettlement settlement ||
                settlement.Expectation.BudgetId != original.Budget.Expectation.BudgetId ||
                settlement.Expectation.StepInstanceId != original.Budget.Expectation.StepInstanceId || settlement.AllocationId != allocation) {
            throw new InvalidOperationException("Refresh journal completion requires the same original allocation settlement.");
        }
        if (original.IsRoot || capture.Disposition != ProcessDecisionRefreshCaptureDisposition.Advanced) {
            if (request.Mutation.State with { UpdatedAtUtc = request.OriginalState.UpdatedAtUtc } != request.OriginalState ||
                    request.Mutation.Events.Count != 0 || request.Mutation.OutboxMessages.Count != 0 ||
                    request.Mutation.ArtifactLedgerEvents.Count != 0 || request.Mutation.Diagnostics.Count != 0) {
                throw new InvalidOperationException("Refresh capture bookkeeping cannot change unrelated runtime state or effects.");
            }
        }
    }

    internal async Task RequireReplayAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
        var row = await FindAsync(request, cancellationToken).ConfigureAwait(false);
        if (request.RefreshCapture is not { } command) {
            if (row is not null) {
                throw new InvalidDataException("A retained refresh journal cannot be bypassed by a legacy budget command.");
            }
            return;
        }
        var state = row?.Read(RequireDatabase().Profile.Profile.Id)
            ?? throw new InvalidDataException("A legacy reservation cannot be backfilled as never invoked.");
        _ = ProcessDecisionRefreshCaptureJournal.Reserve(command.Reservation, state);
        if (command.Disposition is { } disposition && (state.Phase != ProcessDecisionRefreshCapturePhase.Settled ||
                state.Disposition != disposition || row!.Capture.SettlementCommandId != request.CommandId.Value ||
                ((ProcessDecisionBudgetSettlement)request.DecisionBudget!).Settlement != state.Outcome!.Work)) {
            throw new InvalidDataException("Refresh settlement replay changed its journal handoff.");
        }
    }

    internal async Task StageAsync(ProcessRuntimeCommitRequest request, ProcessDecisionBudgetReceipt? budget, CancellationToken cancellationToken) {
        var row = await FindAsync(request, cancellationToken).ConfigureAwait(false);
        if (request.RefreshCapture is not { } command) {
            if (row is not null) {
                throw new InvalidDataException("A retained refresh allocation must settle with its journal in the same transaction.");
            }
            return;
        }
        RequireShape(request);
        if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is null || budget is null || !budget.Change.Changed) {
            throw new InvalidOperationException("Refresh journal staging requires its changed budget inside the original PostgreSQL runtime transaction.");
        }
        if (command.Disposition is null) {
            if (row is not null) {
                throw new InvalidDataException("Refresh journal creation cannot replace a retained reservation.");
            }
            var selected = await EfProcessDecisionRefreshCaptureQuery.ReadCoreAsync(context, RequireDatabase(), request.OriginalState,
                command.Reservation.AdmissionId, cancellationToken).ConfigureAwait(false);
            RequireOriginal(request, command.Reservation, selected);
            context.RefreshCaptures.Add(ProcessDecisionRefreshCaptureRows.New(command.Reservation, selected.Original.Head!.Reference));
            return;
        }
        if (row is null) {
            throw new InvalidDataException("Refresh settlement lost its original invocation journal.");
        }
        var current = row.Read(RequireDatabase().Profile.Profile.Id);
        _ = ProcessDecisionRefreshCaptureJournal.Reserve(command.Reservation, current);
        var settled = ProcessDecisionRefreshCaptureJournal.Settle(current, budget.Change, command.Disposition.Value);
        RequireOutcome(request, settled.State);
        var removed = await context.RefreshCapturePayloads.Where(payload => payload.RunId == row.Capture.RunId &&
            payload.ReservationCommandId == row.Capture.ReservationCommandId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var expectedPayload = current.Outcome!.Kind == ProcessDecisionRefreshCaptureOutcomeKind.Captured && !current.Reservation.InheritsMaterial;
        if (removed != (expectedPayload ? 1 : 0)) {
            throw new InvalidDataException("Refresh settlement lost or substituted its retained capture payload.");
        }
        context.RefreshCaptures.Attach(row.Capture);
        ProcessDecisionRefreshCaptureRows.Write(row.Capture, settled.State);
        row.Capture.SettlementCommandId = request.CommandId.Value;
    }

    private Task<ProcessDecisionRefreshCaptureRow?> FindAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
        if (request.DecisionBudget is not { } budget) {
            return Task.FromResult<ProcessDecisionRefreshCaptureRow?>(null);
        }
        return (from capture in context.RefreshCaptures.AsNoTracking()
            join scope in context.DecisionMaterialScopes.AsNoTracking() on capture.ScopeId equals scope.ScopeId
            join owner in context.DecisionBudgets.AsNoTracking() on capture.BudgetId equals owner.BudgetId
            where capture.BudgetId == budget.Expectation.BudgetId.Value && capture.AllocationId == budget.AllocationId.Value
            select new ProcessDecisionRefreshCaptureRow(capture, scope, owner.RootRunId, owner.RunId, owner.StepInstanceId, owner.OverrunDetected))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private ICanonicalRuntimeDatabase RequireDatabase() => database
        ?? throw new InvalidOperationException("Refresh invocation journal requires the canonical runtime database owner.");

    private static void RequireOriginal(ProcessRuntimeCommitRequest request, ProcessDecisionRefreshCaptureReservation reservation,
        ProcessDecisionRefreshCaptureSelection selected) {
        var intent = ((ProcessDecisionBeginRefresh)selected.Admission.Decision.Command).Intent;
        var original = selected.Original.Child!.Original;
        var budget = reservation.IsRoot ? selected.RootBudget : selected.Original.Budget;
        if (intent.Request.Kind != ProcessDecisionRefreshKind.ManagerRefresh || selected.Input.InputFingerprint != reservation.InputFingerprint ||
                selected.Input.Assignment.DecisionDataPolicy!.ContentContract != reservation.ContentContract ||
                original.Scope != reservation.Scope || original.Material.Reference.Generation + 1 != reservation.Material.Generation ||
                reservation.Budget.Expectation.BudgetId != budget.BudgetId || reservation.Budget.Expectation.Revision != budget.Revision ||
                reservation.Budget.Expectation.StepInstanceId != (reservation.IsRoot ? null : selected.Original.StepInstanceId) ||
                selected.Original.OtherWorkPending || selected.SharedScope is not null && selected.RootWorkPending) {
            throw new InvalidDataException("Refresh journal reservation differs from its original reconciled child, source or available funding.");
        }
        if (reservation.IsRoot) {
            ProcessDecisionRunScopeCapture capture = new(reservation.Budget.AllocationId, reservation.Material.MaterialId, ProcessDecisionRunCaptureKind.ManagerRefresh);
            if (selected.SharedScope is not { Phase: ProcessDecisionRunScopePhase.Reconciling } shared ||
                    request.DecisionRunScope != new ProcessDecisionRunScopeCommand(shared.Expectation, ProcessDecisionRunScopeCommandKind.BeginCapture, capture)) {
                throw new InvalidOperationException("Root capture reservation must begin its exact shared scope in the same transaction.");
            }
        } else if (request.DecisionRunScope is not null || selected.SharedScope is { Phase: not ProcessDecisionRunScopePhase.Active }) {
            throw new InvalidOperationException("Local refresh reservation requires its original available material scope.");
        }
    }

    private static void RequireOutcome(ProcessRuntimeCommitRequest request, ProcessDecisionRefreshCaptureState settled) {
        var reservation = settled.Reservation;
        var advanced = settled.Disposition == ProcessDecisionRefreshCaptureDisposition.Advanced;
        var basis = advanced ? settled.Outcome!.Material : null;
        if (request.DecisionMaterial is { } payload && (payload.Scope != reservation.Scope || payload.ContentContract != reservation.ContentContract ||
                ProcessDecisionRunScopeMaterial.From(payload) != basis) ||
                (request.DecisionMaterial is not null) != (advanced && !reservation.InheritsMaterial)) {
            throw new InvalidDataException("Refresh settlement substituted its returned payload or material stamp.");
        }
        if (reservation.IsRoot) {
            if (request.Decision is not null || request.DecisionRunScope is not { } scope ||
                    scope.Kind != (advanced ? ProcessDecisionRunScopeCommandKind.CompleteCapture : ProcessDecisionRunScopeCommandKind.AbortCapture) ||
                    scope.Material != basis?.Stamp) {
                throw new InvalidOperationException("Root refresh settlement requires its exact returned scope handoff.");
            }
        } else if (request.DecisionRunScope is not null || (advanced
                ? request.Decision is not ProcessDecisionRefresh refresh || refresh.AdmissionCommandId != reservation.AdmissionId || refresh.Material != basis!.Stamp
                : request.Decision is not null)) {
            throw new InvalidOperationException("Local refresh settlement requires its original successor or no-effect abort.");
        }
    }
}
