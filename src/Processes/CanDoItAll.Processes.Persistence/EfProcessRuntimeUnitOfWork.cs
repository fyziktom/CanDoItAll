using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessRuntimeUnitOfWork(ProcessPersistenceDbContext dbContext) :
    IProcessRuntimeUnitOfWork,
    IProcessRuntimeStateStore,
    IProcessRuntimeActivityStore,
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

    public async Task<IReadOnlyList<ProcessRuntimeStateSnapshot>> LoadManyAsync(
        IReadOnlyList<ProcessRunId> runIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runIds);
        if (runIds.Count > IProcessRuntimeStateStore.MaximumBatchRunCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runIds),
                runIds.Count,
                $"Runtime state batch cannot exceed {IProcessRuntimeStateStore.MaximumBatchRunCount} runs.");
        }

        var values = runIds
            .Select(runId => runId.Value)
            .Distinct()
            .ToArray();
        if (values.Length == 0)
        {
            return [];
        }

        var entities = await BuildStateQuery(trackChanges: false)
            .Where(state => values.Contains(state.RunId))
            .OrderBy(state => state.RunId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities
            .Select(ProcessPersistenceMappers.ToSnapshot)
            .ToArray();
    }

    public async Task<ProcessRuntimeActivitySelection> QueryActivityAsync(
        ProcessRuntimeActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var states = dbContext.RuntimeStates.AsNoTracking();
        var selectedRuns = await OrderActivity(states.Where(state =>
                (state.Status != ProcessRuntimeStatus.Completed &&
                 state.Status != ProcessRuntimeStatus.Failed &&
                 state.Status != ProcessRuntimeStatus.Cancelled) ||
                !states.Any(candidate =>
                    candidate.Status != ProcessRuntimeStatus.Completed &&
                    candidate.Status != ProcessRuntimeStatus.Failed &&
                    candidate.Status != ProcessRuntimeStatus.Cancelled)))
            .Take(query.Take)
            .Select(MapActivityRow())
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var mode = selectedRuns.Any(run => !ProcessRuntimeTerminalStates.IsRunTerminal(run.Status))
            ? ProcessRuntimeActivitySelectionMode.Active
            : ProcessRuntimeActivitySelectionMode.RecentFallback;
        return new ProcessRuntimeActivitySelection(
            mode,
            selectedRuns);
    }

    private static IOrderedQueryable<ProcessRuntimeStateEntity> OrderActivity(
        IQueryable<ProcessRuntimeStateEntity> states)
        => states
            .OrderByDescending(state => state.UpdatedAtUtc)
            .ThenByDescending(state => state.RunId);

    private static System.Linq.Expressions.Expression<Func<ProcessRuntimeStateEntity, ProcessRuntimeActivityRow>> MapActivityRow()
        => state => new ProcessRuntimeActivityRow(
            new ProcessRunId(state.RootRunId),
            new ProcessRunId(state.RunId),
            state.Status,
            state.UpdatedAtUtc);

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

    public async Task<IReadOnlyList<ProcessRunId>> FindDescendantRunIdsAsync(
        ProcessRunId rootRunId,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > IProcessRuntimeRunHierarchyStore.MaximumBatchRunCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                take,
                $"Runtime hierarchy take must be between 1 and {IProcessRuntimeRunHierarchyStore.MaximumBatchRunCount}.");
        }

        return await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                state.RootRunId == rootRunId.Value &&
                state.RunId != rootRunId.Value)
            .OrderBy(state => state.RunId)
            .Take(take)
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

        ValidateInitialPlanMatchesState(request);

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
        if (await ValidateParentStepPreconditionAsync(
                request,
                isNewState: existing is null,
                cancellationToken).ConfigureAwait(false) is { } rejection)
        {
            dbContext.ChangeTracker.Clear();
            return rejection;
        }

        await StageInitialPlanAsync(
            request,
            isNewState: existing is null,
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

    private async Task StageInitialPlanAsync(
        ProcessRuntimeCommitRequest request,
        bool isNewState,
        CancellationToken cancellationToken)
    {
        if (request.InitialPlan is not { } plan)
        {
            return;
        }

        if (!isNewState)
        {
            throw new InvalidOperationException(
                $"Initial process instance plan '{plan.Header.PlanId}' can only be committed while creating runtime state '{request.Mutation.State.RunId}'.");
        }

        var existingPlan = await dbContext.InstancePlans
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.PlanId == plan.Header.PlanId.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (existingPlan is not null)
        {
            ProcessInstancePlanPersistenceMapper.EnsureSameIdentityAndHash(existingPlan, plan);
            _ = ProcessInstancePlanPersistenceMapper.ToPlan(existingPlan);
            return;
        }

        dbContext.InstancePlans.Add(ProcessInstancePlanPersistenceMapper.ToEntity(plan));
    }

    private static void ValidateInitialPlanMatchesState(ProcessRuntimeCommitRequest request)
    {
        if (request.InitialPlan is not { } plan)
        {
            return;
        }

        if (plan.Header.PlanId != request.OriginalState.PlanId ||
            plan.Header.PlanId != request.Mutation.State.PlanId)
        {
            throw new InvalidOperationException(
                $"Initial process instance plan '{plan.Header.PlanId}' does not match runtime state plan id '{request.Mutation.State.PlanId}'.");
        }

        if (!string.Equals(plan.PlanHash, request.OriginalState.PlanHash, StringComparison.Ordinal) ||
            !string.Equals(plan.PlanHash, request.Mutation.State.PlanHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Initial process instance plan '{plan.Header.PlanId}' does not match runtime state plan hash '{request.Mutation.State.PlanHash}'.");
        }
    }

    private async Task<ProcessRuntimeCommitResult?> ValidateParentStepPreconditionAsync(
        ProcessRuntimeCommitRequest request,
        bool isNewState,
        CancellationToken cancellationToken)
    {
        if (request.ParentStepPrecondition is not { } parentStepPrecondition)
        {
            if (isNewState &&
                request.Mutation.State.RunId != request.Mutation.State.RootRunId)
            {
                return RejectParentStepPrecondition(
                    request,
                    "Runtime.ParentStepPreconditionRequired",
                    $"New descendant process run '{request.Mutation.State.RunId}' cannot start without a typed parent-step precondition.");
            }

            return null;
        }

        var rootState = await LoadStateEntityAsync(
            request.Mutation.State.RootRunId.Value,
            trackChanges: false,
            cancellationToken).ConfigureAwait(false);
        if (rootState is null ||
            rootState.RootRunId != request.Mutation.State.RootRunId.Value)
        {
            return RejectParentStepPrecondition(
                request,
                "Runtime.ParentRootMissing",
                $"Child process run '{request.Mutation.State.RunId}' cannot start because root run '{request.Mutation.State.RootRunId}' does not exist.");
        }

        if (rootState.Status == ProcessRuntimeStatus.CancelRequested ||
            ProcessRuntimeTerminalStates.IsRunTerminal(rootState.Status))
        {
            return RejectParentStepPrecondition(
                request,
                "Runtime.ParentRootNotLaunchable",
                $"Child process run '{request.Mutation.State.RunId}' cannot start because root run '{request.Mutation.State.RootRunId}' has status '{rootState.Status}'.");
        }

        var parentState = parentStepPrecondition.RunId == request.Mutation.State.RootRunId
            ? rootState
            : await LoadStateEntityAsync(
                parentStepPrecondition.RunId.Value,
                trackChanges: false,
                cancellationToken).ConfigureAwait(false);
        if (parentState is null ||
            parentState.RootRunId != request.Mutation.State.RootRunId.Value)
        {
            return RejectParentStepPrecondition(
                request,
                "Runtime.ParentRunMissing",
                $"Child process run '{request.Mutation.State.RunId}' cannot start because parent run '{parentStepPrecondition.RunId}' is missing from root '{request.Mutation.State.RootRunId}'.");
        }

        if (parentState.Status != ProcessRuntimeStatus.Active)
        {
            return RejectParentStepPrecondition(
                request,
                "Runtime.ParentRunNotLaunchable",
                $"Child process run '{request.Mutation.State.RunId}' cannot start because parent run '{parentStepPrecondition.RunId}' has status '{parentState.Status}'.");
        }

        var parentStep = parentState.Steps.SingleOrDefault(step =>
            step.StepInstanceId == parentStepPrecondition.StepInstanceId.Value);
        if (parentStep is null ||
            parentStep.Status != ProcessRuntimeStepStatus.Running ||
            parentStep.ActiveClaimToken is not { } activeClaimToken ||
            !parentState.Claims.Any(claim =>
                claim.ClaimToken == activeClaimToken &&
                claim.StepInstanceId == parentStep.StepInstanceId &&
                (claim.Status is DispatchClaimStatus.Claimed or
                    DispatchClaimStatus.LeaseRenewed or
                    DispatchClaimStatus.Reclaimed) &&
                claim.ExpiresAtUtc > request.Mutation.State.UpdatedAtUtc))
        {
            return RejectParentStepPrecondition(
                request,
                "Runtime.ParentStepNotRunning",
                $"Child process run '{request.Mutation.State.RunId}' cannot start because parent step '{parentStepPrecondition.StepInstanceId}' is not running with an active dispatch claim.");
        }

        return null;
    }

    private static ProcessRuntimeCommitResult RejectParentStepPrecondition(
        ProcessRuntimeCommitRequest request,
        string code,
        string message)
        => ProcessRuntimeCommitResult.FromMutation(
            ProcessRuntimeMutation.Rejected(request.Mutation.State, code, message));

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
        return await BuildStateQuery(trackChanges)
            .SingleOrDefaultAsync(state => state.RunId == runId, cancellationToken)
            .ConfigureAwait(false);
    }

    private IQueryable<ProcessRuntimeStateEntity> BuildStateQuery(bool trackChanges)
    {
        var query = dbContext.RuntimeStates.AsQueryable();
        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return query
            .AsSplitQuery()
            .Include(state => state.Steps)
            .Include(state => state.Claims)
            .Include(state => state.ResultReceipts)
            .Include(state => state.AvailableArtifactSlots)
            .Include(state => state.ConnectedInputArtifacts);
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
        existing.BlockedRecoveryActionsJson = replacement.BlockedRecoveryActionsJson;
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
