using System.Data;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionRefreshCaptureStore(IDbContextFactory<ProcessPersistenceDbContext> factory,
    ICanonicalRuntimeDatabase database) : IProcessDecisionRefreshCaptureStore {
    private static readonly TimeSpan OperationLimit = TimeSpan.FromSeconds(5);

    public async Task<ProcessDecisionRefreshCaptureState?> ReadAsync(ProcessRunId runId, RuntimeCommandId reservationId, CancellationToken cancellationToken = default) {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(OperationLimit);
        cancellationToken = deadline.Token;
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await ProcessDecisionRefreshCaptureRows.Query(context, runId, reservationId).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row?.Read(database.Profile.Profile.Id);
    }

    public async Task<ProcessDecisionRefreshCaptureReturned?> ReadReturnedAsync(ProcessRunId runId, RuntimeCommandId reservationId,
        CancellationToken cancellationToken = default) {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(OperationLimit);
        cancellationToken = deadline.Token;
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var selected = await (from capture in context.RefreshCaptures.AsNoTracking()
            join scope in context.DecisionMaterialScopes.AsNoTracking() on capture.ScopeId equals scope.ScopeId
            join budget in context.DecisionBudgets.AsNoTracking() on capture.BudgetId equals budget.BudgetId
            join payload in context.RefreshCapturePayloads.AsNoTracking()
                on new { capture.RunId, capture.ReservationCommandId } equals new { payload.RunId, payload.ReservationCommandId } into payloads
            from payload in payloads.DefaultIfEmpty()
            where capture.RunId == runId.Value && capture.ReservationCommandId == reservationId.Value
            select new { Row = new ProcessDecisionRefreshCaptureRow(capture, scope, budget.RootRunId, budget.RunId, budget.StepInstanceId, budget.OverrunDetected),
                Payload = payload == null ? null : payload.PayloadJson }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (selected is null) {
            return null;
        }
        var state = selected.Row.Read(database.Profile.Profile.Id);
        if (state.Phase != ProcessDecisionRefreshCapturePhase.Returned) {
            return null;
        }
        var original = state.Reservation;
        var payloadMaterial = selected.Payload is { } json && state.Outcome!.Material is { } material
            ? ProcessDecisionMaterial.FromContent(original.Scope, original.Material, original.ContentContract, material.CapturedAtUtc, json) : null;
        ProcessDecisionRefreshCaptureReturned returned = new(state.Outcome!, payloadMaterial);
        returned.Validate(original);
        return returned;
    }

    public async Task<ProcessDecisionRefreshCaptureInvocationResult> BeginAsync(ProcessDecisionRefreshCaptureReservation reservation,
        ProcessDecisionCaptureInvocationId invocation, CancellationToken cancellationToken = default) {
        invocation.Validate();
        var status = ProcessDecisionRefreshCaptureInvocationStatus.PreviouslyAdmitted;
        var state = await ChangeAsync(reservation, async (context, row, current, token) => {
            if (current.Phase != ProcessDecisionRefreshCapturePhase.Reserved) {
                return new(false, current);
            }
            var allocation = await context.DecisionBudgetAllocations.AsNoTracking().SingleAsync(item =>
                item.BudgetId == row.Capture.BudgetId && item.AllocationId == row.Capture.AllocationId, token).ConfigureAwait(false);
            var retained = ProcessDecisionRows.ReadAllocation(allocation);
            if (retained.Grant != reservation.Budget.Grant || retained.Settlement is not null) {
                throw new InvalidDataException("Refresh invocation requires its original open allocation.");
            }
            if (row.BudgetOverrun || !await AllowsInvocationAsync(context, row.Capture, token).ConfigureAwait(false)) {
                status = ProcessDecisionRefreshCaptureInvocationStatus.Stopped;
                return new(false, current);
            }
            status = ProcessDecisionRefreshCaptureInvocationStatus.Started;
            return ProcessDecisionRefreshCaptureJournal.Begin(current, invocation);
        }, cancellationToken).ConfigureAwait(false);
        return new(status, state);
    }

    public Task<ProcessDecisionRefreshCaptureState> ReturnAsync(ProcessDecisionRefreshCaptureReservation reservation,
        ProcessDecisionCaptureInvocationId invocation, ProcessDecisionRefreshCaptureReturned returned, CancellationToken cancellationToken = default) {
        returned.Validate(reservation);
        return ChangeAsync(reservation, (context, _, current, _) => {
            var change = ProcessDecisionRefreshCaptureJournal.Return(current, invocation, returned);
            if (change.Changed && returned.Payload is { } payload) {
                context.RefreshCapturePayloads.Add(new() { RunId = reservation.RunId.Value,
                    ReservationCommandId = reservation.ReservationId.Value, PayloadJson = payload.PayloadJson });
            }
            return Task.FromResult(change);
        }, cancellationToken);
    }

    public Task<ProcessDecisionRefreshCaptureState> CloseNeverInvokedAsync(ProcessDecisionRefreshCaptureReservation reservation, CancellationToken cancellationToken = default)
        => ChangeAsync(reservation, (_, _, current, _) => Task.FromResult(ProcessDecisionRefreshCaptureJournal.CloseNeverInvoked(current)), cancellationToken);

    private async Task<ProcessDecisionRefreshCaptureState> ChangeAsync(ProcessDecisionRefreshCaptureReservation reservation,
        Func<ProcessPersistenceDbContext, ProcessDecisionRefreshCaptureRow, ProcessDecisionRefreshCaptureState, CancellationToken,
            Task<ProcessDecisionRefreshCaptureChange>> change, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(reservation);
        reservation.Validate();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(OperationLimit);
        var token = deadline.Token;
        await using var context = await factory.CreateDbContextAsync(token).ConfigureAwait(false);
        if (!context.Database.IsNpgsql()) {
            throw new NotSupportedException("Durable refresh invocation requires PostgreSQL.");
        }
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("SET LOCAL transaction_timeout = '5s'; SET LOCAL statement_timeout = '2s'; SET LOCAL lock_timeout = '1s'", token).ConfigureAwait(false);
        using var local = await ProcessRuntimeRootSequenceLocks.AcquireAsync([reservation.RootRunId.Value], token).ConfigureAwait(false);
        var key = ProcessRootAdvisoryKey.From(reservation.RootRunId.Value);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", token).ConfigureAwait(false);
        var row = await ProcessDecisionRefreshCaptureRows.Query(context, reservation.RunId, reservation.ReservationId).SingleOrDefaultAsync(token).ConfigureAwait(false)
            ?? throw new InvalidDataException("Refresh reservation has no durable invocation protocol; its work remains unknown.");
        var current = row.Read(database.Profile.Profile.Id);
        _ = ProcessDecisionRefreshCaptureJournal.Reserve(reservation, current);
        var result = await change(context, row, current, token).ConfigureAwait(false);
        if (result.Changed) {
            context.RefreshCaptures.Attach(row.Capture);
            ProcessDecisionRefreshCaptureRows.Write(row.Capture, result.State);
            await context.SaveChangesAsync(token).ConfigureAwait(false);
            if (result.State.Phase == ProcessDecisionRefreshCapturePhase.Returned) {
                await ProcessDecisionRefreshDeliveryStaging.WakeAsync(context, reservation, token).ConfigureAwait(false);
            }
        }
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return result.State;
    }

    private static async Task<bool> AllowsInvocationAsync(ProcessPersistenceDbContext context, ProcessDecisionRefreshCaptureEntity capture, CancellationToken cancellationToken) {
        var current = await (from head in context.DecisionHeads.AsNoTracking()
            join generation in context.DecisionGenerations.AsNoTracking() on new { head.DecisionId, Generation = head.CurrentGeneration }
                equals new { generation.DecisionId, generation.Generation }
            join step in context.RuntimeSteps.AsNoTracking() on new { head.RunId, head.StepInstanceId } equals new { step.RunId, step.StepInstanceId }
            join runtime in context.RuntimeStates.AsNoTracking() on head.RunId equals runtime.RunId
            join root in context.RuntimeStates.AsNoTracking() on runtime.RootRunId equals root.RunId
            where head.DecisionId == capture.DecisionId && head.CurrentGeneration == capture.DecisionGeneration &&
                head.RunId == capture.RunId && head.RootRunId == capture.RootRunId && root.RootRunId == root.RunId &&
                generation.RefreshCommandId == capture.AdmissionCommandId
            select new { Root = root.Status, Run = runtime.Status, Step = step.Status, generation.Phase, generation.UnclosedChildCount })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return current is not null && current.Phase == ProcessDecisionPhase.Reconciling && current.UnclosedChildCount == 0 &&
            current.Root != ProcessRuntimeStatus.CancelRequested && !ProcessRuntimeTerminalStates.IsRunTerminal(current.Root) &&
            current.Run != ProcessRuntimeStatus.CancelRequested && !ProcessRuntimeTerminalStates.IsRunTerminal(current.Run) &&
            !ProcessRuntimeTerminalStates.IsStepTerminal(current.Step);
    }
}
