using System.Data;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionRefreshCaptureQuery(IDbContextFactory<ProcessPersistenceDbContext> factory,
    ICanonicalRuntimeDatabase database) : IProcessDecisionRefreshCaptureQuery {
    public async Task<ProcessDecisionRefreshCaptureProgress> ReadProgressAsync(ProcessRunId runId, RuntimeCommandId admissionId,
        CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(runId.Value, Guid.Empty);
        ProcessDecisionRefreshCaptureIdentity identity = new(admissionId);
        Guid[] commands = [identity.RootReserve.Value, identity.RootComplete.Value, identity.RootAbort.Value,
            identity.LocalReserve.Value, identity.LocalAbort.Value, identity.Successor.Value];
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await context.IdempotencyKeys.AsNoTracking().Where(row => row.RunId == runId.Value && commands.Contains(row.CommandId))
            .Select(row => new { row.CommandId, row.Outcome, row.DecisionReceiptJson, row.DecisionBudgetReceiptJson, row.DecisionRunScopeReceiptJson })
            .ToDictionaryAsync(row => row.CommandId, cancellationToken).ConfigureAwait(false);
        return new(Read(identity.RootReserve), Read(identity.RootComplete), Read(identity.RootAbort),
            Read(identity.LocalReserve), Read(identity.LocalAbort), Read(identity.Successor));

        ProcessDecisionRefreshCaptureReceipt? Read(RuntimeCommandId command) => rows.TryGetValue(command.Value, out var row)
            ? new(row.Outcome, ProcessDecisionReceiptCodec.Read(row.DecisionReceiptJson),
                ProcessDecisionBudgetReceiptCodec.Read(row.DecisionBudgetReceiptJson),
                ProcessDecisionRunScopeReceiptCodec.Read(row.DecisionRunScopeReceiptJson)) : null;
    }

    public async Task<ProcessDecisionRefreshCaptureSelection> ReadAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandId admissionId,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfEqual(admissionId.Value, Guid.Empty);
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (!context.Database.IsNpgsql()) {
            throw new NotSupportedException("Coherent refresh preparation requires PostgreSQL.");
        }
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync(
            "SET TRANSACTION READ ONLY; SET LOCAL transaction_timeout = '5s'; SET LOCAL statement_timeout = '2s'; SET LOCAL lock_timeout = '1s'",
            cancellationToken).ConfigureAwait(false);
        var admission = await EfProcessDecisionRefreshReceiptReader.ReadCoreAsync(context, state.RunId, admissionId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Refresh capture requires its retained original admission.");
        var address = ((ProcessDecisionBeginRefresh)admission.Decision.Command).Intent.Request.Child;
        var selected = await EfProcessDecisionResultQuery.ReadCoreAsync(context, state.RunId, address.StepInstanceId,
            new ProcessExecutionRunId(address.ChildId.Value), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Refresh capture lost its original child and budget.");
        var input = await EfProcessAcceptedExecutionInputReader.ReadCoreAsync(context, database.Profile.Profile.Id,
            state.RunId, address.StepInstanceId, cancellationToken).ConfigureAwait(false);
        _ = ProcessDecisionRefreshAccountingPreparation.RequireOriginal(state, input, selected, admission);
        await EfProcessDecisionRefreshReconciliation.RequireAsync(context, state, admission, selected, cancellationToken).ConfigureAwait(false);
        var policy = input.Assignment.DecisionDataPolicy!;
        var row = await (from runtime in context.RuntimeStates.AsNoTracking()
            join budget in context.DecisionBudgets.AsNoTracking().Where(item => item.StepInstanceId == null)
                on runtime.RunId equals budget.RunId
            where runtime.RunId == state.RootRunId.Value
            select new { runtime.RootRunId, runtime.Status, Budget = budget })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Refresh capture lost its original root funding owner.");
        var root = ProcessDecisionRunAdmissionCodec.Deserialize(row.Budget.RunAdmissionJson
            ?? throw new InvalidDataException("Refresh capture lost its original root policy admission."));
        root.RequireChild(policy);
        var balance = ProcessDecisionRows.ReadBalance(row.Budget);
        if (root.RunId != state.RootRunId || row.RootRunId != state.RootRunId.Value || root.BudgetId != balance.BudgetId ||
                root.DatabaseProfileId != input.Source.DatabaseProfileId || root.DatabaseProfileId != database.Profile.Profile.Id ||
                balance.Ceiling != root.Policy.Budget || selected.Budget.Ceiling != policy.Policy.Budget) {
            throw new InvalidDataException("Refresh capture changed its original root, profile, funding or budget ceilings.");
        }
        ProcessDecisionRunScopeState? shared = null;
        if (policy.SharedRunScopeId is { } scopeId) {
            var loaded = await new EfProcessDecisionRunScopeStore(context, database).ReadPreparationAsync(scopeId, cancellationToken).ConfigureAwait(false);
            shared = loaded.Scope;
            if (shared.Admission != root.MaterialScope || selected.SharedScope != shared.Expectation ||
                    ProcessDecisionRunAdmissionCodec.Serialize(loaded.Root) != row.Budget.RunAdmissionJson) {
                throw new InvalidDataException("Refresh capture changed its original shared material scope.");
            }
        }
        var pending = await context.DecisionBudgetAllocations.AsNoTracking().AnyAsync(row =>
            row.BudgetId == balance.BudgetId.Value && row.OwnerWorkRevision == null, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(input, admission, selected, root, balance, row.Status, shared, pending);
    }
}
