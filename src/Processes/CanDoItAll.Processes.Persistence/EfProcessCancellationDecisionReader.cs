using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessCancellationDecisionReader(IDbContextFactory<ProcessPersistenceDbContext> factory) : IProcessCancellationDecisionReader {
    public async Task<ProcessDecisionReference?> NextAsync(ProcessRunId rootRunId, ProcessDecisionId? after, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(rootRunId.Value, Guid.Empty);
        if (after?.Value == Guid.Empty) {
            throw new ArgumentOutOfRangeException(nameof(after));
        }
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));
        var query = context.DecisionHeads.AsNoTracking().Where(head => head.RootRunId == rootRunId.Value);
        if (after is { } cursor) {
            query = query.Where(head => head.DecisionId.CompareTo(cursor.Value) > 0);
        }
        return await query.OrderBy(head => head.DecisionId).Select(head => new ProcessDecisionReference(new(head.DecisionId), head.CurrentGeneration))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProcessCancellationDecisionSelection?> ReadAsync(ProcessRunId rootRunId,
        ProcessDecisionReference decision, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(rootRunId.Value, Guid.Empty);
        decision.Validate();
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));
        return await context.DecisionHeads.AsNoTracking().Where(head => head.RootRunId == rootRunId.Value &&
                head.DecisionId == decision.DecisionId.Value && head.CurrentGeneration == decision.Generation &&
                context.DecisionGenerations.Any(generation => generation.DecisionId == head.DecisionId && generation.Generation == head.CurrentGeneration))
            .Select(head => new ProcessCancellationDecisionSelection(new(head.RunId), new(head.StepInstanceId), decision,
                context.DecisionGenerations.Any(generation => generation.DecisionId == head.DecisionId && generation.Generation != head.CurrentGeneration &&
                    (generation.UnclosedChildCount != 0 || generation.Phase != ProcessDecisionPhase.Completed &&
                    generation.Phase != ProcessDecisionPhase.Cancelled && generation.Phase != ProcessDecisionPhase.Superseded)) ||
                context.DecisionChildren.Any(child => child.DecisionId == head.DecisionId && child.Generation != head.CurrentGeneration && child.OwnerRevision == null)))
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProcessDecisionChildId?> NextChildAsync(ProcessRunId rootRunId, ProcessDecisionReference decision,
        ProcessDecisionChildId? after, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(rootRunId.Value, Guid.Empty);
        decision.Validate();
        if (after?.Value == Guid.Empty) {
            throw new ArgumentOutOfRangeException(nameof(after));
        }
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));
        var query = context.DecisionChildren.AsNoTracking().Where(child => child.DecisionId == decision.DecisionId.Value &&
            child.Generation == decision.Generation && context.DecisionHeads.Any(head => head.DecisionId == child.DecisionId && head.RootRunId == rootRunId.Value));
        if (after is { } cursor) {
            query = query.Where(child => child.ChildId.CompareTo(cursor.Value) > 0);
        }
        var selected = await query.OrderBy(child => child.ChildId).Select(child => (Guid?)child.ChildId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return selected is { } id ? new(id) : null;
    }
}
