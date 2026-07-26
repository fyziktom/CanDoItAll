using System.Buffers.Binary;
using System.Data;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessRuntimeUnitOfWork(
    ProcessPersistenceDbContext dbContext,
    TimeProvider? timeProvider = null) :
    IProcessRuntimeUnitOfWork,
    IProcessRuntimeStateStore,
    IProcessRuntimeActivityStore,
    IProcessRuntimeRunHierarchyStore,
    IProcessIdempotencyStore
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

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

        ValidateCommitIdentity(request);
        ValidateInitialPlanMatchesState(request);
        ValidateAtomicMutation(request);

        using var rootSequenceLock = await ProcessRuntimeRootSequenceLocks
            .AcquireAsync([request.OriginalState.RootRunId.Value], cancellationToken)
            .ConfigureAwait(false);

        if (!dbContext.Database.IsRelational())
        {
            try
            {
                return await CommitCoreAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await AcquireRootMutationLockAsync(
                    request.OriginalState.RootRunId.Value,
                    cancellationToken)
                .ConfigureAwait(false);
            var result = await CommitCoreAsync(request, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<ProcessRuntimeCommitResult> CommitCoreAsync(
        ProcessRuntimeCommitRequest request,
        CancellationToken cancellationToken)
    {
        var duplicate = await FindCompletedCommandAsync(
            request.OriginalState.RunId,
            request.CommandId,
            cancellationToken).ConfigureAwait(false);
        if (duplicate is not null)
        {
            return duplicate;
        }

        dbContext.ChangeTracker.Clear();
        var existing = await LoadStateEntityAsync(
            request.Mutation.State.RunId.Value,
            trackChanges: true,
            cancellationToken).ConfigureAwait(false);
        if (await ValidateParentStepPreconditionAsync(
                request,
                isNewState: existing is null,
                commitTimeUtc: clock.GetUtcNow(),
                cancellationToken).ConfigureAwait(false) is { } rejection)
        {
            dbContext.ChangeTracker.Clear();
            return rejection;
        }

        await StageInitialPlanAsync(
            request,
            isNewState: existing is null,
            cancellationToken).ConfigureAwait(false);
        StageInitialAssignments(request, isNewState: existing is null);

        if (existing is null)
        {
            dbContext.RuntimeStates.Add(ProcessPersistenceMappers.ToEntity(request.Mutation.State));
        }
        else
        {
            EnsureCurrentStateMatchesOriginal(existing, request.OriginalState);
            if (await ValidateBlockedRecoveryChildLineageAsync(
                    request,
                    cancellationToken).ConfigureAwait(false) is { } childLineageRejection)
            {
                dbContext.ChangeTracker.Clear();
                return childLineageRejection;
            }

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

    private void StageInitialAssignments(
        ProcessRuntimeCommitRequest request,
        bool isNewState)
    {
        if (request.InitialAssignments is not { } assignments)
        {
            if (isNewState &&
                (request.ParentStepPrecondition is not null ||
                 request.InitialPlan is not null))
            {
                throw new InvalidOperationException(
                    "A new executable process run must commit its initial assignments atomically with its runtime state and immutable plan.");
            }

            return;
        }

        if (!isNewState || request.InitialPlan is null)
        {
            throw new InvalidOperationException(
                "Initial process assignments can only be committed atomically with a new runtime state and its immutable plan.");
        }

        var planStepsById = request.InitialPlan.Steps
            .Where(step => step.IsExecutable)
            .ToDictionary(step => step.StepInstanceId);
        var stateStepsById = request.Mutation.State.Steps
            .Where(step => step.IsExecutable)
            .ToDictionary(step => step.StepInstanceId);
        var duplicateStepIds = assignments
            .GroupBy(assignment => assignment.StepInstanceId)
            .FirstOrDefault(group => group.Count() > 1);
        var duplicateStepKeys = assignments
            .GroupBy(assignment => assignment.StepKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateStepIds is not null ||
            duplicateStepKeys is not null ||
            assignments.Count != planStepsById.Count ||
            assignments.Count != stateStepsById.Count ||
            assignments.Any(assignment =>
                assignment.RunId != request.Mutation.State.RunId ||
                 assignment.PlanId != request.Mutation.State.PlanId ||
                 !planStepsById.TryGetValue(assignment.StepInstanceId, out var planStep) ||
                 !stateStepsById.TryGetValue(assignment.StepInstanceId, out var stateStep) ||
                 stateStep.StepDefinitionId != planStep.StepDefinitionId ||
                 !string.Equals(
                     assignment.StepKey,
                     planStep.StepKey,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Initial process assignments must map exactly once to every executable step in the new runtime state and immutable plan.");
        }

        ValidateAssignmentParentLineage(request, assignments);

        dbContext.RuntimeStepAssignments.AddRange(
            assignments.Select(EfProcessRuntimeStepAssignmentStore.ToEntity));
    }

    private static void ValidateAssignmentParentLineage(
        ProcessRuntimeCommitRequest request,
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments)
    {
        if (request.ParentStepPrecondition is not { } expectedParent)
        {
            if (assignments.Any(HasParentLineageKey))
            {
                throw new InvalidOperationException(
                    "A root process assignment cannot carry parent-run lineage.");
            }

            return;
        }

        if (assignments.Count == 0 ||
            assignments.Any(assignment =>
                !ProcessRuntimeLaunchVariables.TryReadParentStep(
                    assignment.LaunchVariables,
                    out var actualParent) ||
                actualParent != expectedParent))
        {
            throw new InvalidOperationException(
                "Every child process assignment must carry the exact typed parent-step lineage used to authorize launch.");
        }
    }

    private static bool HasParentLineageKey(ProcessRuntimeStepAssignment assignment)
    {
        return assignment.LaunchVariables.ContainsKey(
                   ProcessRuntimeLaunchVariables.ParentProcessRunId) ||
               assignment.LaunchVariables.ContainsKey(
                   ProcessRuntimeLaunchVariables.ParentProcessStepId);
    }

    private async Task AcquireRootMutationLockAsync(
        Guid rootRunId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        var lockKey = CreateAdvisoryLockKey(rootRunId);
        await dbContext.Database
            .ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({lockKey})",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static long CreateAdvisoryLockKey(Guid rootRunId)
    {
        Span<byte> bytes = stackalloc byte[16];
        rootRunId.TryWriteBytes(bytes);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes[..8]) ^
               BinaryPrimitives.ReadInt64LittleEndian(bytes[8..]);
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
        DateTimeOffset commitTimeUtc,
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
                claim.ExpiresAtUtc > commitTimeUtc))
        {
            return RejectParentStepPrecondition(
                request,
                "Runtime.ParentStepNotRunning",
                $"Child process run '{request.Mutation.State.RunId}' cannot start because parent step '{parentStepPrecondition.StepInstanceId}' is not running with an active dispatch claim.");
        }

        return null;
    }

    private async Task<ProcessRuntimeCommitResult?> ValidateBlockedRecoveryChildLineageAsync(
        ProcessRuntimeCommitRequest request,
        CancellationToken cancellationToken)
    {
        var authorization = request.BlockedRecoveryAuthorization;
        if (authorization?.RecoveryRouteKind != ProcessRecoveryRouteKind.ChildRunPropagation)
        {
            return null;
        }

        if (authorization.RelatedChildRunId is not { } relatedChildRunId ||
            authorization.ExpectedRelatedChildUpdatedAtUtc is not { } relatedChildUpdatedAtUtc ||
            authorization.ExpectedChildLineageEvidence is not { } expectedEvidence)
        {
            return RejectBlockedRecoveryChildLineage(
                request,
                "The child-run recovery commit has incomplete typed lineage evidence.");
        }

        var expectedIssue = ProcessRuntimeChildLineageEvidenceRules.FindIssue(
            expectedEvidence,
            request.OriginalState.RunId,
            authorization.SourceBlockedStepInstanceId,
            request.OriginalState.RootRunId,
            relatedChildRunId,
            relatedChildUpdatedAtUtc);
        if (expectedIssue is not null)
        {
            return RejectBlockedRecoveryChildLineage(request, expectedIssue);
        }

        var parentRunSnippet = BuildLaunchVariableJsonSnippet(
            ProcessRuntimeLaunchVariables.ParentProcessRunId,
            expectedEvidence.ParentRunId.ToString());
        var parentStepSnippet = BuildLaunchVariableJsonSnippet(
            ProcessRuntimeLaunchVariables.ParentProcessStepId,
            expectedEvidence.ParentStepInstanceId.ToString());
        var matchingAssignments = dbContext.RuntimeStepAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.LaunchVariablesJson.Contains(parentRunSnippet) &&
                assignment.LaunchVariablesJson.Contains(parentStepSnippet));
        var linkedAssignmentRows = await matchingAssignments
            .Select(assignment => assignment.RunId)
            .Distinct()
            .Select(runId => new LinkedChildAssignmentRow(
                runId,
                matchingAssignments
                    .Where(assignment => assignment.RunId == runId)
                    .Select(assignment => assignment.LaunchVariablesJson)
                    .First(),
                matchingAssignments
                    .Where(assignment => assignment.RunId == runId)
                    .Max(assignment => assignment.CreatedAtUtc)))
            .OrderByDescending(child => child.CreatedAtUtc)
            .ThenByDescending(child => child.RunId)
            .Take(ProcessRuntimeChildLineageEvidenceRules.MaximumLinkedChildRunCount + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (linkedAssignmentRows.Length >
            ProcessRuntimeChildLineageEvidenceRules.MaximumLinkedChildRunCount)
        {
            return RejectBlockedRecoveryChildLineage(
                request,
                "The current linked-child set exceeds the bounded automatic recovery limit.");
        }

        var linkedChildren = linkedAssignmentRows
            .Where(row =>
                ProcessRuntimeLaunchVariables.TryReadParentStep(
                    row.LaunchVariablesJson,
                    out var parentStep) &&
                parentStep.RunId == expectedEvidence.ParentRunId &&
                parentStep.StepInstanceId == expectedEvidence.ParentStepInstanceId)
            .GroupBy(row => row.RunId)
            .Select(group => new LinkedChildRun(
                new ProcessRunId(group.Key),
                group.Max(row => row.CreatedAtUtc)))
            .OrderByDescending(child => child.LinkCreatedAtUtc)
            .ThenByDescending(child => child.RunId.Value)
            .ToArray();
        if (linkedChildren.Length is < 1 or
            > ProcessRuntimeChildLineageEvidenceRules.MaximumLinkedChildRunCount)
        {
            return RejectBlockedRecoveryChildLineage(
                request,
                "The current linked-child set has an invalid bounded child count.");
        }

        var linkedRunIds = linkedChildren
            .Select(child => child.RunId.Value)
            .ToArray();
        var childStateRows = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state => linkedRunIds.Contains(state.RunId))
            .Select(state => new LinkedChildStateRow(
                state.RunId,
                state.RootRunId,
                state.Status,
                state.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (childStateRows.Length != linkedChildren.Length)
        {
            return RejectBlockedRecoveryChildLineage(
                request,
                "A currently linked child run has no durable runtime state.");
        }

        var childStatesByRunId = childStateRows.ToDictionary(state => state.RunId);
        var currentEvidence = ProcessRuntimeChildLineageEvidence.Create(
            expectedEvidence.ParentRunId,
            expectedEvidence.ParentStepInstanceId,
            linkedChildren.Select(child =>
            {
                var state = childStatesByRunId[child.RunId.Value];
                return new ProcessRuntimeLinkedChildEvidence(
                    child.RunId,
                    new ProcessRunId(state.RootRunId),
                    state.Status,
                    state.UpdatedAtUtc,
                    child.LinkCreatedAtUtc);
            }));
        var currentIssue = ProcessRuntimeChildLineageEvidenceRules.FindIssue(
            currentEvidence,
            request.OriginalState.RunId,
            authorization.SourceBlockedStepInstanceId,
            request.OriginalState.RootRunId,
            relatedChildRunId,
            relatedChildUpdatedAtUtc);
        if (currentIssue is not null)
        {
            return RejectBlockedRecoveryChildLineage(request, currentIssue);
        }

        return expectedEvidence.Matches(currentEvidence)
            ? null
            : RejectBlockedRecoveryChildLineage(
                request,
                "The linked-child membership, order, status, or version changed before commit.");
    }

    private static string BuildLaunchVariableJsonSnippet(string key, string value)
    {
        return $"{JsonSerializer.Serialize(key)}:{JsonSerializer.Serialize(value)}";
    }

    private static ProcessRuntimeCommitResult RejectBlockedRecoveryChildLineage(
        ProcessRuntimeCommitRequest request,
        string issue)
    {
        return ProcessRuntimeCommitResult.FromMutation(
            ProcessRuntimeMutation.Rejected(
                request.OriginalState,
                "Runtime.BlockedRecoveryChildLineageChanged",
                issue));
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

    private static void ValidateCommitIdentity(ProcessRuntimeCommitRequest request)
    {
        if (request.OriginalState.RunId != request.Mutation.State.RunId ||
            request.OriginalState.RootRunId != request.Mutation.State.RootRunId ||
            request.OriginalState.PlanId != request.Mutation.State.PlanId ||
            !string.Equals(
                request.OriginalState.PlanHash,
                request.Mutation.State.PlanHash,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A runtime mutation cannot change its run, root-run, plan, or plan-hash identity.");
        }
    }

    private static void ValidateAtomicMutation(ProcessRuntimeCommitRequest request)
    {
        var mutation = request.Mutation;
        var eventIds = new HashSet<RuntimeEventId>();
        foreach (var runtimeEvent in mutation.Events)
        {
            if (runtimeEvent.RunId != mutation.State.RunId ||
                runtimeEvent.RootRunId != mutation.State.RootRunId)
            {
                throw new InvalidOperationException(
                    $"Runtime event '{runtimeEvent.EventId}' must belong to mutation run '{mutation.State.RunId}' and root '{mutation.State.RootRunId}'.");
            }

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

    private readonly record struct LinkedChildAssignmentRow(
        Guid RunId,
        string LaunchVariablesJson,
        DateTimeOffset CreatedAtUtc);

    private readonly record struct LinkedChildRun(
        ProcessRunId RunId,
        DateTimeOffset LinkCreatedAtUtc);

    private readonly record struct LinkedChildStateRow(
        Guid RunId,
        Guid RootRunId,
        ProcessRuntimeStatus Status,
        DateTimeOffset UpdatedAtUtc);
}
