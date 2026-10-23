using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

internal sealed record ProcessDecisionRefreshCaptureRow(ProcessDecisionRefreshCaptureEntity Capture,
    ProcessDecisionMaterialScopeEntity Scope, Guid BudgetRootRunId, Guid BudgetRunId, Guid? BudgetStepId, bool BudgetOverrun) {
    internal ProcessDecisionRefreshCaptureState Read(Guid profileId) {
        var row = Capture;
        if (row.ProtocolVersion != 1 || Scope.MaterialProtocol != ProcessDecisionMaterialProtocol.SharedContentV1 ||
                Scope.Executor is not null || Scope.DatabaseProfileId != profileId || Scope.RootRunId != row.RootRunId ||
                BudgetRootRunId != row.RootRunId || BudgetRunId != row.RunId ||
                (row.Phase == ProcessDecisionRefreshCapturePhase.Settled) != (row.SettlementCommandId is not null)) {
            throw new InvalidDataException("Refresh capture lost its original protocol, profile, scope or budget owner.");
        }
        ProcessDecisionMaterialScope scope = new(new(Scope.ScopeId), Scope.DatabaseProfileId, Scope.WorkspaceNamespaceId,
            new(Scope.RootRunId), new(Scope.OwnerRunId), Scope.StepInstanceId is { } step ? new(step) : null);
        ProcessDecisionRefreshCaptureReservation reservation = new(new(row.RunId), new(row.AdmissionCommandId), new(row.ReservationCommandId),
            new(new(BudgetStepId is { } budgetStep ? new(budgetStep) : null, new(row.BudgetId), row.OriginalBudgetRevision),
                new(row.AllocationId), new(row.GrantedWorkTicks, 0)), scope, new(Scope.ContractId, Scope.ContractVersion),
            new(scope.Id, row.MaterialGeneration, new(row.MaterialId)), row.InputFingerprint);
        ProcessDecisionRefreshCaptureOutcome? outcome = null;
        if (row.Outcome is { } kind) {
            if (row.ConsumedWorkTicks is not { } work) {
                throw new InvalidDataException("Refresh capture lost its exact returned work.");
            }
            ProcessDecisionRunScopeMaterial? material = row.MaterialFingerprint is { } fingerprint && row.CapturedAtUtcTicks is { } ticks
                ? new(new(reservation.Material, fingerprint), new(ticks, TimeSpan.Zero)) : null;
            outcome = new(kind, new(1, new(work, 0)), material);
        }
        if ((row.Outcome is null) != (row.ConsumedWorkTicks is null) ||
                (row.Outcome == ProcessDecisionRefreshCaptureOutcomeKind.Captured) != (row.MaterialFingerprint is not null && row.CapturedAtUtcTicks is not null) ||
                (row.MaterialFingerprint is null) != (row.CapturedAtUtcTicks is null)) {
            throw new InvalidDataException("Refresh capture contains incomplete or substituted outcome metadata.");
        }
        ProcessDecisionRefreshCaptureState state = new(reservation, row.Revision, row.Phase,
            row.InvocationId is { } invocation ? new(invocation) : null, outcome, row.Disposition);
        state.Validate();
        return state;
    }
}

internal static class ProcessDecisionRefreshCaptureRows {
    internal static IQueryable<ProcessDecisionRefreshCaptureRow> Query(ProcessPersistenceDbContext context, ProcessRunId run, RuntimeCommandId reservation)
        => from capture in context.RefreshCaptures.AsNoTracking()
           join scope in context.DecisionMaterialScopes.AsNoTracking() on capture.ScopeId equals scope.ScopeId
           join budget in context.DecisionBudgets.AsNoTracking() on capture.BudgetId equals budget.BudgetId
           where capture.RunId == run.Value && capture.ReservationCommandId == reservation.Value
           select new ProcessDecisionRefreshCaptureRow(capture, scope, budget.RootRunId, budget.RunId, budget.StepInstanceId, budget.OverrunDetected);

    internal static ProcessDecisionRefreshCaptureEntity New(ProcessDecisionRefreshCaptureReservation reservation, ProcessDecisionReference decision) {
        reservation.Validate();
        var row = new ProcessDecisionRefreshCaptureEntity {
            RunId = reservation.RunId.Value, RootRunId = reservation.RootRunId.Value, AdmissionCommandId = reservation.AdmissionId.Value,
            ReservationCommandId = reservation.ReservationId.Value, BudgetId = reservation.Budget.Expectation.BudgetId.Value,
            AllocationId = reservation.Budget.AllocationId.Value, OriginalBudgetRevision = reservation.Budget.Expectation.Revision,
            GrantedWorkTicks = reservation.Budget.Grant.WorkTicks, ScopeId = reservation.Scope.Id.Value,
            MaterialId = reservation.Material.MaterialId.Value, MaterialGeneration = reservation.Material.Generation,
            InputFingerprint = reservation.InputFingerprint, DecisionId = decision.DecisionId.Value, DecisionGeneration = decision.Generation
        };
        Write(row, ProcessDecisionRefreshCaptureJournal.Reserve(reservation).State);
        return row;
    }

    internal static void Write(ProcessDecisionRefreshCaptureEntity row, ProcessDecisionRefreshCaptureState state) {
        state.Validate();
        row.Revision = state.Revision;
        row.Phase = state.Phase;
        row.InvocationId = state.Invocation?.Value;
        row.Outcome = state.Outcome?.Kind;
        row.ConsumedWorkTicks = state.Outcome?.Work.Consumed.WorkTicks;
        row.MaterialFingerprint = state.Outcome?.Material?.Stamp.Fingerprint;
        row.CapturedAtUtcTicks = state.Outcome?.Material?.CapturedAtUtc.UtcTicks;
        row.Disposition = state.Disposition;
    }
}
