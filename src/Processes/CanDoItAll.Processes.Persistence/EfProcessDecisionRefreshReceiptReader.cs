using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionRefreshReceiptReader(IDbContextFactory<ProcessPersistenceDbContext> factory)
    : IProcessDecisionRefreshReceiptReader {
    public async Task<ProcessDecisionRefreshReceipt?> ReadAsync(ProcessRunId runId, RuntimeCommandId commandId,
        CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(runId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(commandId.Value, Guid.Empty);
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ReadCoreAsync(context, runId, commandId, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<ProcessDecisionRefreshReceipt?> ReadCoreAsync(ProcessPersistenceDbContext context,
        ProcessRunId runId, RuntimeCommandId commandId, CancellationToken cancellationToken) {
        var row = await context.IdempotencyKeys.AsNoTracking().Where(item => item.RunId == runId.Value && item.CommandId == commandId.Value)
            .Select(item => new { item.DecisionReceiptJson, item.DecisionRunScopeReceiptJson })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? null : Read(runId, commandId, row.DecisionReceiptJson, row.DecisionRunScopeReceiptJson);
    }

    public async Task<ProcessDecisionRefreshReceipt?> ReadCurrentAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
        CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(runId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(stepId.Value, Guid.Empty);
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await (from head in context.DecisionHeads.AsNoTracking()
            join generation in context.DecisionGenerations.AsNoTracking()
                on new { head.DecisionId, Generation = head.CurrentGeneration } equals new { generation.DecisionId, generation.Generation }
            join receipt in context.IdempotencyKeys.AsNoTracking()
                on new { head.RunId, CommandId = generation.RefreshCommandId } equals new { receipt.RunId, CommandId = (Guid?)receipt.CommandId } into receipts
            from receipt in receipts.DefaultIfEmpty()
            where head.RunId == runId.Value && head.StepInstanceId == stepId.Value
            select new { head.RootRunId, head.DecisionId, head.CurrentGeneration, generation.RefreshCommandId,
                generation.MaterialId, generation.BudgetId, generation.Revision,
                Decision = receipt == null ? null : receipt.DecisionReceiptJson,
                Scope = receipt == null ? null : receipt.DecisionRunScopeReceiptJson })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row?.RefreshCommandId is not { } commandId) {
            return null;
        }
        var original = Read(runId, new(commandId), row.Decision, row.Scope);
        var begin = (ProcessDecisionBeginRefresh)original.Decision.Command;
        if (begin.StepInstanceId != stepId || begin.Intent.Request.Child.RootRunId.Value != row.RootRunId ||
                begin.Intent.Request.Decision.Reference != new ProcessDecisionReference(new(row.DecisionId), row.CurrentGeneration) ||
                original.Decision.Current.MaterialId.Value != row.MaterialId || original.Decision.Current.BudgetId.Value != row.BudgetId ||
                original.Decision.Current.Revision > row.Revision) {
            throw new InvalidDataException("The current generation refers to another original refresh admission.");
        }
        return original;
    }

    private static ProcessDecisionRefreshReceipt Read(ProcessRunId runId, RuntimeCommandId commandId, string? decisionJson, string? scopeJson) {
        var decision = ProcessDecisionReceiptCodec.Read(decisionJson);
        if (decision is not { Command: ProcessDecisionBeginRefresh begin } || begin.Intent.Request.Child.RunId != runId ||
                begin.Intent.Request.CommandId != commandId) {
            throw new InvalidDataException("The requested refresh command has no matching original admission receipt.");
        }
        ProcessDecisionRefreshReceipt receipt = new(decision, ProcessDecisionRunScopeReceiptCodec.Read(scopeJson));
        receipt.RequireOriginal(begin.Intent.Request, begin.Intent.Actor);
        return receipt;
    }
}
