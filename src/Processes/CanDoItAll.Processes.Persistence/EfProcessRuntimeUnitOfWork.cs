using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessRuntimeUnitOfWork(ProcessPersistenceDbContext dbContext) :
    IProcessRuntimeUnitOfWork,
    IProcessRuntimeStateStore,
    IProcessRuntimeRunHierarchyStore,
    IProcessIdempotencyStore
{
    public async Task<ProcessRuntimeStateSnapshot?> LoadAsync(
        ProcessRunId runId,
        CancellationToken cancellationToken = default)
    {
        var entity = await LoadStateEntityAsync(
            runId.Value,
            trackChanges: false,
            cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ProcessPersistenceMappers.ToSnapshot(entity);
    }

    public async Task<IReadOnlyList<ProcessRunId>> FindCancellableDescendantRunIdsAsync(
        ProcessRunId rootRunId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                state.RootRunId == rootRunId.Value &&
                state.RunId != rootRunId.Value &&
                state.Status != ProcessRuntimeStatus.Completed &&
                state.Status != ProcessRuntimeStatus.Failed &&
                state.Status != ProcessRuntimeStatus.Cancelled &&
                state.Status != ProcessRuntimeStatus.CancelRequested)
            .OrderByDescending(state => state.UpdatedAtUtc)
            .Select(state => new ProcessRunId(state.RunId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProcessRuntimeCommitResult> CommitAsync(
        ProcessRuntimeCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Mutation.Outcome != ProcessRuntimeTransitionOutcome.Applied)
        {
            return ProcessRuntimeCommitResult.FromMutation(request.Mutation);
        }

        using var rootSequenceLock = await ProcessRuntimeRootSequenceLocks
            .AcquireAsync([request.Mutation.State.RootRunId.Value], cancellationToken)
            .ConfigureAwait(false);

        var duplicate = await FindCompletedCommandAsync(
            request.OriginalState.RunId,
            request.CommandId,
            cancellationToken).ConfigureAwait(false);
        if (duplicate is not null)
        {
            return duplicate;
        }

        ValidateAtomicMutation(request.Mutation);

        dbContext.ChangeTracker.Clear();
        var existing = await LoadStateEntityAsync(
            request.Mutation.State.RunId.Value,
            trackChanges: true,
            cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            dbContext.RuntimeStates.Add(ProcessPersistenceMappers.ToEntity(request.Mutation.State));
        }
        else
        {
            EnsureCurrentStateMatchesOriginal(existing, request.OriginalState);
            ReplaceState(existing, request.Mutation.State);
        }

        await AppendEventsAsync(request.Mutation.Events, cancellationToken).ConfigureAwait(false);
        AddOutboxMessages(request.Mutation.OutboxMessages, request.Mutation.State.UpdatedAtUtc);
        AddArtifactLedgerEvents(request.Mutation.ArtifactLedgerEvents);

        dbContext.IdempotencyKeys.Add(new ProcessRuntimeIdempotencyEntity
        {
            RunId = request.Mutation.State.RunId.Value,
            CommandId = request.CommandId.Value,
            Outcome = request.Mutation.Outcome,
            CompletedAtUtc = request.Mutation.State.UpdatedAtUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();
        return ProcessRuntimeCommitResult.FromMutation(request.Mutation);
    }

    public async Task<ProcessRuntimeCommitResult?> FindCompletedCommandAsync(
        RuntimeCommandId commandId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.IdempotencyKeys
            .AsNoTracking()
            .SingleOrDefaultAsync(key => key.CommandId == commandId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        return await FindCompletedCommandAsync(
            new ProcessRunId(entity.RunId),
            commandId,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessRuntimeCommitResult?> FindCompletedCommandAsync(
        ProcessRunId runId,
        RuntimeCommandId commandId,
        CancellationToken cancellationToken)
    {
        var key = await dbContext.IdempotencyKeys
            .FindAsync(new object[] { runId.Value, commandId.Value }, cancellationToken)
            .ConfigureAwait(false);
        if (key is null)
        {
            return null;
        }

        var state = await LoadAsync(runId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            throw new InvalidOperationException($"Runtime idempotency key '{commandId}' references missing run '{runId}'.");
        }

        return new ProcessRuntimeCommitResult(
            key.Outcome,
            state,
            [],
            [],
            [],
            []);
    }

    private async Task<ProcessRuntimeStateEntity?> LoadStateEntityAsync(
        Guid runId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = dbContext.RuntimeStates.AsQueryable();
        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query
            .AsSplitQuery()
            .Include(state => state.Steps)
            .Include(state => state.Claims)
            .Include(state => state.ResultReceipts)
            .Include(state => state.AvailableArtifactSlots)
            .Include(state => state.ConnectedInputArtifacts)
            .SingleOrDefaultAsync(state => state.RunId == runId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task AppendEventsAsync(
        IReadOnlyList<ProcessRuntimeEventEnvelope> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var rootSequences = new Dictionary<Guid, long>(events.Count);
        foreach (var runtimeEvent in events)
        {
            if (!rootSequences.TryGetValue(runtimeEvent.RootRunId.Value, out var nextRootSequence))
            {
                nextRootSequence = await NextRootSequenceAsync(
                    runtimeEvent.RootRunId.Value,
                    cancellationToken).ConfigureAwait(false);
            }

            dbContext.RuntimeEvents.Add(ProcessPersistenceMappers.ToEventEntity(
                runtimeEvent,
                nextRootSequence));

            rootSequences[runtimeEvent.RootRunId.Value] = nextRootSequence + 1;
        }
    }

    private async Task<long> NextRootSequenceAsync(Guid rootRunId, CancellationToken cancellationToken)
    {
        var maxSequence = await dbContext.RuntimeEvents
            .Where(runtimeEvent => runtimeEvent.RootRunId == rootRunId)
            .MaxAsync(runtimeEvent => (long?)runtimeEvent.RootSequence, cancellationToken)
            .ConfigureAwait(false);
        return (maxSequence ?? 0) + 1;
    }

    private void ReplaceState(ProcessRuntimeStateEntity existing, ProcessRuntimeStateSnapshot state)
    {
        dbContext.RuntimeSteps.RemoveRange(existing.Steps);
        dbContext.DispatchClaims.RemoveRange(existing.Claims);
        dbContext.StrategyResultReceipts.RemoveRange(existing.ResultReceipts);
        dbContext.AvailableArtifactSlots.RemoveRange(existing.AvailableArtifactSlots);
        dbContext.RuntimeInputArtifacts.RemoveRange(existing.ConnectedInputArtifacts);

        existing.RootRunId = state.RootRunId.Value;
        existing.PlanId = state.PlanId.Value;
        existing.PlanHash = state.PlanHash;
        existing.Status = state.Status;
        existing.UpdatedAtUtc = state.UpdatedAtUtc;
        existing.ConcurrencyToken = Guid.NewGuid();

        existing.Steps.Clear();
        existing.Claims.Clear();
        existing.ResultReceipts.Clear();
        existing.AvailableArtifactSlots.Clear();
        existing.ConnectedInputArtifacts.Clear();

        var replacement = ProcessPersistenceMappers.ToEntity(state);
        existing.Steps.AddRange(replacement.Steps);
        existing.Claims.AddRange(replacement.Claims);
        existing.ResultReceipts.AddRange(replacement.ResultReceipts);
        existing.AvailableArtifactSlots.AddRange(replacement.AvailableArtifactSlots);
        existing.ConnectedInputArtifacts.AddRange(replacement.ConnectedInputArtifacts);
    }

    private static void EnsureCurrentStateMatchesOriginal(
        ProcessRuntimeStateEntity existing,
        ProcessRuntimeStateSnapshot originalState)
    {
        if (existing.RunId != originalState.RunId.Value ||
            existing.RootRunId != originalState.RootRunId.Value ||
            existing.PlanId != originalState.PlanId.Value ||
            !string.Equals(existing.PlanHash, originalState.PlanHash, StringComparison.Ordinal) ||
            existing.Status != originalState.Status ||
            existing.UpdatedAtUtc != originalState.UpdatedAtUtc ||
            existing.Steps.Count != originalState.Steps.Count ||
            existing.Claims.Count != originalState.Claims.Count ||
            existing.ResultReceipts.Count != originalState.AppliedResults.Count ||
            existing.AvailableArtifactSlots.Count != originalState.AvailableArtifactSlots.Count ||
            existing.ConnectedInputArtifacts.Count != originalState.ConnectedInputArtifacts.Count)
        {
            throw new ProcessRuntimeOptimisticConcurrencyException(
                originalState.RunId,
                originalState.UpdatedAtUtc);
        }
    }

    private void AddOutboxMessages(IReadOnlyList<ProcessOutboxMessage> messages, DateTimeOffset createdAtUtc)
    {
        foreach (var message in messages)
        {
            dbContext.OutboxMessages.Add(ProcessPersistenceMappers.ToOutboxEntity(message, createdAtUtc));
        }
    }

    private void AddArtifactLedgerEvents(IReadOnlyList<ProcessArtifactLedgerEvent> ledgerEvents)
    {
        foreach (var ledgerEvent in ledgerEvents)
        {
            dbContext.ArtifactLedgerEvents.Add(ProcessPersistenceMappers.ToLedgerEntity(ledgerEvent));
        }
    }

    private static void ValidateAtomicMutation(ProcessRuntimeMutation mutation)
    {
        var eventIds = new HashSet<RuntimeEventId>();
        foreach (var runtimeEvent in mutation.Events)
        {
            if (!eventIds.Add(runtimeEvent.EventId))
            {
                throw new InvalidOperationException($"Runtime event '{runtimeEvent.EventId}' appears more than once in the same mutation.");
            }
        }

        foreach (var outboxMessage in mutation.OutboxMessages)
        {
            if (!eventIds.Contains(outboxMessage.EventId))
            {
                throw new InvalidOperationException($"Outbox message '{outboxMessage.MessageId}' references event '{outboxMessage.EventId}' outside the same mutation.");
            }
        }

        foreach (var ledgerEvent in mutation.ArtifactLedgerEvents)
        {
            if (!eventIds.Contains(ledgerEvent.EventId))
            {
                throw new InvalidOperationException($"Artifact ledger event '{ledgerEvent.LedgerEventId}' references event '{ledgerEvent.EventId}' outside the same mutation.");
            }
        }
    }
}
