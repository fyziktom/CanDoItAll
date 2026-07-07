using System.Globalization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessRuntimeEvidenceSourceProvider(
    IDbContextFactory<ProcessPersistenceDbContext> dbContextFactory,
    IProcessExecutionObservationReader executionObservationReader) : IProcessRuntimeEvidenceSourceProvider
{
    private const string FeedbackHookMetadataKey = "feedbackHook";
    private const string ProcessCompletionFeedbackHook = "process-runtime-completion";
    private const string SourceProviderName = "process-runtime";
    private static readonly DateTimeOffset MinimumObservationUtc = DateTimeOffset.UnixEpoch;
    private static readonly DateTimeOffset MaximumObservationUtc = DateTimeOffset.Parse("9999-12-31T23:59:59Z", CultureInfo.InvariantCulture);

    public async Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ProcessRuntimeEvidenceSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProcessRunId == Guid.Empty)
        {
            throw new ArgumentException("Process runtime evidence source requests must use null for global scope or a non-empty process run id.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var runId = request.ProcessRunId;
        var scopeId = runId ?? Guid.Empty;
        var runIds = await ReadRunIdsAsync(dbContext, runId, cancellationToken);
        var agentSessionItems = await ReadAgentSessionItemsAsync(runIds, cancellationToken);
        var runtimeStates = FilterByRunId(dbContext.RuntimeStates.AsNoTracking(), runId);
        var runtimeStatesWithChildren = runtimeStates
            .Include(state => state.Steps)
            .Include(state => state.Claims)
            .Include(state => state.ResultReceipts)
            .Include(state => state.AvailableArtifactSlots);
        var runtimeEvents = FilterByRunId(dbContext.RuntimeEvents.AsNoTracking(), runId);
        var artifactRows = dbContext.ArtifactLedgerEvents
            .AsNoTracking()
            .Join(
                runtimeEvents,
                artifact => artifact.EventId,
                runtimeEvent => runtimeEvent.EventId,
                (artifact, runtimeEvent) => new ProcessArtifactLedgerSnapshotRow(
                    runtimeEvent.RunId,
                    runtimeEvent.RootRunId,
                    artifact.LedgerEventId,
                    artifact.EventId,
                    artifact.SlotId,
                    artifact.ArtifactId,
                    artifact.ContentHash,
                    runtimeEvent.OccurredAtUtc));
        var deadLetterRows = dbContext.ProjectionDeadLetters
            .AsNoTracking()
            .Join(
                runtimeEvents,
                deadLetter => deadLetter.EventId,
                runtimeEvent => runtimeEvent.EventId,
                (deadLetter, runtimeEvent) => new ProcessProjectionDeadLetterSnapshotRow(
                    runtimeEvent.RunId,
                    runtimeEvent.RootRunId,
                    deadLetter.DeadLetterId,
                    deadLetter.ProjectorName,
                    deadLetter.ShardKey,
                    deadLetter.EventId,
                    deadLetter.GlobalSequence,
                    deadLetter.ErrorClass,
                    deadLetter.DiagnosticReference,
                    deadLetter.RetryPolicy,
                    deadLetter.DeadLetteredAtUtc));
        var planRows = dbContext.InstancePlans
            .AsNoTracking()
            .Join(
                runtimeStates,
                plan => plan.PlanId,
                state => state.PlanId,
                (plan, state) => new ProcessDefinitionPlanSnapshotRow(
                    state.RunId,
                    state.RootRunId,
                    plan.PlanId,
                    plan.RootPlanId,
                    plan.ParentPlanId,
                    plan.ParentStepId,
                    plan.DefinitionId,
                    plan.DefinitionVersionId,
                    plan.PlanHash,
                    plan.PlanSchemaVersion,
                    plan.DefinitionContentHash,
                    plan.PayloadJson,
                    plan.CreatedAtUtc));

        var sources = new[]
            {
                CreateSource(
                    MemorySourceEntityKind.ProcessDefinition,
                    planRows,
                    query => query.OrderBy(item => item.RunId).ThenBy(item => item.PlanId),
                    MapDefinition),
                CreateSource(
                    MemorySourceEntityKind.ProcessRun,
                    runtimeStatesWithChildren,
                    query => query.OrderBy(item => item.RunId),
                    MapRun),
                CreateSource(
                    MemorySourceEntityKind.ProcessStepEvidence,
                    FilterByRunId(dbContext.RuntimeSteps.AsNoTracking(), runId),
                    query => query.OrderBy(item => item.RunId).ThenBy(item => item.StepInstanceId),
                    MapStep),
                CreateSource(
                    MemorySourceEntityKind.ProcessRunAssignment,
                    FilterByRunId(dbContext.RuntimeStepAssignments.AsNoTracking(), runId),
                    query => query.OrderBy(item => item.RunId).ThenBy(item => item.StepInstanceId),
                    MapAssignment),
                CreateSource(
                    MemorySourceEntityKind.ProcessAgentSession,
                    FilterByRunId(dbContext.DispatchClaims.AsNoTracking(), runId),
                    query => query.OrderBy(item => item.RunId).ThenBy(item => item.ClaimToken),
                    MapDispatchClaim),
                CreateInMemorySource(
                    MemorySourceEntityKind.ProcessAgentSession,
                    agentSessionItems),
                CreateSource(
                    MemorySourceEntityKind.ProcessDecision,
                    FilterByRunId(dbContext.StrategyResultReceipts.AsNoTracking(), runId),
                    query => query.OrderBy(item => item.RunId).ThenBy(item => item.StepInstanceId).ThenBy(item => item.StrategyId).ThenBy(item => item.IdempotencyKey),
                    MapResultReceipt),
                CreateSource(
                    MemorySourceEntityKind.ProcessArtifact,
                    FilterByRunId(dbContext.AvailableArtifactSlots.AsNoTracking(), runId),
                    query => query.OrderBy(item => item.RunId).ThenBy(item => item.SlotId),
                    MapAvailableArtifactSlot),
                CreateSource(
                    MemorySourceEntityKind.ProcessArtifact,
                    artifactRows,
                    query => query.OrderBy(item => item.RunId).ThenBy(item => item.LedgerEventId),
                    MapArtifactLedger),
                CreateSource(
                    MemorySourceEntityKind.ProcessJournal,
                    runtimeEvents,
                    query => query.OrderBy(item => item.RunId).ThenBy(item => item.GlobalSequence),
                    MapRuntimeEvent),
                CreateSource(
                    MemorySourceEntityKind.ProcessJournal,
                    FilterByRunId(dbContext.ProjectionHistory.AsNoTracking(), runId),
                    query => query.OrderBy(item => item.RunId).ThenBy(item => item.GlobalSequence),
                    MapProjectionHistory),
                CreateSource(
                    MemorySourceEntityKind.ProcessConformanceObservation,
                    deadLetterRows,
                    query => query.OrderBy(item => item.RunId).ThenBy(item => item.GlobalSequence),
                    MapDeadLetter),
                CreateSource(
                    MemorySourceEntityKind.ProcessCompletionOutcome,
                    runtimeStatesWithChildren.Where(state =>
                        state.Status == ProcessRuntimeStatus.Completed ||
                        state.Status == ProcessRuntimeStatus.Failed ||
                        state.Status == ProcessRuntimeStatus.Cancelled),
                    query => query.OrderBy(item => item.RunId),
                    MapCompletionOutcome)
            }
            .OrderBy(source => source.EntityKind.ToString(), StringComparer.Ordinal)
            .ThenBy(source => source.Ordinal)
            .ToList();

        var page = await ReadPageAsync(
            sources,
            request.Cursor,
            request.Take,
            scopeId,
            cancellationToken);

        return new MemorySourceSnapshot(
            new MemorySourceSnapshotManifest(
                MemorySourceSnapshotId.Create(MemorySourceKind.ProcessRuntime, scopeId, page.SnapshotHash),
                MemorySourceKind.ProcessRuntime,
                scopeId,
                DateTimeOffset.UtcNow,
                page.TotalItemCount,
                page.NextCursor,
                page.HasMore,
                page.HasMore ? MemorySourceSnapshotPageStatus.PageReturned : MemorySourceSnapshotPageStatus.EndOfSource,
                MemorySourceSnapshotHashScope.PageScoped,
                MemorySourceSnapshotProviderVersions.ProcessRuntime),
            page.Items);
    }

    private async Task<IReadOnlyList<MemorySourceItem>> ReadAgentSessionItemsAsync(
        IReadOnlyList<ProcessRunId> runIds,
        CancellationToken cancellationToken)
    {
        if (runIds.Count == 0)
        {
            return [];
        }

        var observations = await executionObservationReader.ListAsync(
            new ProcessExecutionObservationQuery(
                runIds,
                MinimumObservationUtc,
                MaximumObservationUtc,
                TakePerRun: 100),
            cancellationToken);

        return observations
            .OrderBy(item => item.RunId.Value)
            .ThenBy(item => item.ExecutionRunId)
            .Select(MapExecutionObservation)
            .ToArray();
    }

    private static async Task<IReadOnlyList<ProcessRunId>> ReadRunIdsAsync(
        ProcessPersistenceDbContext dbContext,
        Guid? requestedRunId,
        CancellationToken cancellationToken)
    {
        if (requestedRunId.HasValue)
        {
            return [new ProcessRunId(requestedRunId.Value)];
        }

        var ids = await dbContext.RuntimeStates
            .AsNoTracking()
            .OrderBy(item => item.RunId)
            .Select(item => item.RunId)
            .ToListAsync(cancellationToken);
        return ids
            .Select(item => new ProcessRunId(item))
            .ToArray();
    }

    private static async Task<MemorySourcePageSlice> ReadPageAsync(
        IReadOnlyList<ProcessSourcePage> sources,
        MemorySourceSnapshotCursor? cursor,
        int? take,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var descriptor = MemorySourceSnapshotCursor.ReadDescriptorOrThrow(
            cursor,
            MemorySourceKind.ProcessRuntime,
            scopeId,
            MemorySourceSnapshotProviderVersions.ProcessRuntime);
        var sourceCounts = new List<ProcessSourcePageCount>(sources.Count);
        foreach (var source in sources)
        {
            sourceCounts.Add(new ProcessSourcePageCount(source, await source.CountAsync(cancellationToken)));
        }

        var totalItemCount = sourceCounts.Sum(item => item.Count);
        var startPosition = descriptor?.Position ?? 0;
        if (descriptor is not null)
        {
            var anchor = await ReadItemIdAtPositionAsync(sourceCounts, descriptor.Position - 1, cancellationToken);
            if (anchor is null || anchor.Value != descriptor.LastItemId)
            {
                MemorySourceSnapshotCursor.ThrowStaleAnchor(
                    cursor!.Value,
                    MemorySourceKind.ProcessRuntime,
                    scopeId,
                    MemorySourceSnapshotProviderVersions.ProcessRuntime,
                    "Process runtime source cursor anchor is stale or no longer matches the ordered source item at the recorded position.");
            }
        }

        var pageSize = MemorySourceSnapshotPage.NormalizeTake(take);
        var pageItems = new List<MemorySourceItem>(pageSize);
        var remainingSkip = startPosition;
        foreach (var sourceCount in sourceCounts)
        {
            if (pageItems.Count == pageSize)
            {
                break;
            }

            if (remainingSkip >= sourceCount.Count)
            {
                remainingSkip -= sourceCount.Count;
                continue;
            }

            var sourceSkip = remainingSkip;
            remainingSkip = 0;
            var sourceTake = Math.Min(pageSize - pageItems.Count, sourceCount.Count - sourceSkip);
            if (sourceTake <= 0)
            {
                continue;
            }

            pageItems.AddRange(await sourceCount.Source.ReadPageAsync(sourceSkip, sourceTake, cancellationToken));
        }

        var hasMore = startPosition + pageItems.Count < totalItemCount;
        MemorySourceSnapshotCursor? nextCursor = hasMore && pageItems.Count > 0
            ? MemorySourceSnapshotCursor.Create(
                MemorySourceKind.ProcessRuntime,
                scopeId,
                MemorySourceSnapshotProviderVersions.ProcessRuntime,
                startPosition + pageItems.Count,
                pageItems[^1].Id)
            : null;
        var snapshotHash = MemorySourceSnapshotHasher.Compute(
            MemorySourceSnapshotProviderVersions.ProcessRuntime,
            scopeId.ToString("D"),
            startPosition.ToString(CultureInfo.InvariantCulture),
            string.Join("|", pageItems.Select(item => item.ContentHash)));
        return new MemorySourcePageSlice(pageItems, totalItemCount, nextCursor, hasMore, snapshotHash);
    }

    private static async Task<MemorySourceItemId?> ReadItemIdAtPositionAsync(
        IReadOnlyList<ProcessSourcePageCount> sourceCounts,
        int position,
        CancellationToken cancellationToken)
    {
        if (position < 0)
        {
            return null;
        }

        var remaining = position;
        foreach (var sourceCount in sourceCounts)
        {
            if (remaining >= sourceCount.Count)
            {
                remaining -= sourceCount.Count;
                continue;
            }

            return await sourceCount.Source.ReadItemIdAsync(remaining, cancellationToken);
        }

        return null;
    }

    private static ProcessSourcePage CreateSource<T>(
        MemorySourceEntityKind entityKind,
        IQueryable<T> query,
        Func<IQueryable<T>, IOrderedQueryable<T>> order,
        Func<T, MemorySourceItem> map)
    {
        var ordinal = ProcessSourcePage.NextOrdinal();
        return new ProcessSourcePage(
            entityKind,
            ordinal,
            cancellationToken => query.CountAsync(cancellationToken),
            async (skip, take, cancellationToken) => (await order(query)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync(cancellationToken))
                .Select(map)
                .ToArray(),
            async (index, cancellationToken) => (await order(query)
                    .Skip(index)
                    .Take(1)
                    .ToListAsync(cancellationToken))
                .Select(map)
                .FirstOrDefault()
                ?.Id);
    }

    private static ProcessSourcePage CreateInMemorySource(
        MemorySourceEntityKind entityKind,
        IReadOnlyList<MemorySourceItem> items)
    {
        var orderedItems = items
            .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .ToArray();
        var ordinal = ProcessSourcePage.NextOrdinal();
        return new ProcessSourcePage(
            entityKind,
            ordinal,
            cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(orderedItems.Length);
            },
            (skip, take, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<MemorySourceItem>>(orderedItems.Skip(skip).Take(take).ToArray());
            },
            (index, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(index >= 0 && index < orderedItems.Length
                    ? orderedItems[index].Id
                    : (MemorySourceItemId?)null);
            });
    }

    private static IQueryable<ProcessRuntimeStateEntity> FilterByRunId(
        IQueryable<ProcessRuntimeStateEntity> query,
        Guid? runId)
        => runId.HasValue ? query.Where(item => item.RunId == runId.Value) : query;

    private static IQueryable<ProcessRuntimeStepEntity> FilterByRunId(
        IQueryable<ProcessRuntimeStepEntity> query,
        Guid? runId)
        => runId.HasValue ? query.Where(item => item.RunId == runId.Value) : query;

    private static IQueryable<ProcessRuntimeStepAssignmentEntity> FilterByRunId(
        IQueryable<ProcessRuntimeStepAssignmentEntity> query,
        Guid? runId)
        => runId.HasValue ? query.Where(item => item.RunId == runId.Value) : query;

    private static IQueryable<ProcessDispatchClaimEntity> FilterByRunId(
        IQueryable<ProcessDispatchClaimEntity> query,
        Guid? runId)
        => runId.HasValue ? query.Where(item => item.RunId == runId.Value) : query;

    private static IQueryable<ProcessStrategyResultReceiptEntity> FilterByRunId(
        IQueryable<ProcessStrategyResultReceiptEntity> query,
        Guid? runId)
        => runId.HasValue ? query.Where(item => item.RunId == runId.Value) : query;

    private static IQueryable<ProcessRuntimeAvailableArtifactSlotEntity> FilterByRunId(
        IQueryable<ProcessRuntimeAvailableArtifactSlotEntity> query,
        Guid? runId)
        => runId.HasValue ? query.Where(item => item.RunId == runId.Value) : query;

    private static IQueryable<ProcessRuntimeEventEntity> FilterByRunId(
        IQueryable<ProcessRuntimeEventEntity> query,
        Guid? runId)
        => runId.HasValue ? query.Where(item => item.RunId == runId.Value) : query;

    private static IQueryable<ProcessProjectionHistoryEntity> FilterByRunId(
        IQueryable<ProcessProjectionHistoryEntity> query,
        Guid? runId)
        => runId.HasValue ? query.Where(item => item.RunId == runId.Value) : query;

    private static MemorySourceItem MapDefinition(ProcessDefinitionPlanSnapshotRow plan)
    {
        var sourceEntityId = $"definition:{plan.DefinitionId:D}:plan:{plan.PlanId:D}";
        var itemId = BuildItemId(plan.RunId, MemorySourceEntityKind.ProcessDefinition, sourceEntityId);
        var hasPayload = HasPayload(plan.PayloadJson);
        var content = BuildContent(
            ("Definition id", plan.DefinitionId.ToString("D")),
            ("Definition version id", plan.DefinitionVersionId.ToString("D")),
            ("Plan id", plan.PlanId.ToString("D")),
            ("Root plan id", plan.RootPlanId.ToString("D")),
            ("Parent plan id", plan.ParentPlanId?.ToString("D")),
            ("Parent step id", plan.ParentStepId?.ToString("D")),
            ("Plan schema version", plan.PlanSchemaVersion),
            ("Definition content hash", plan.DefinitionContentHash),
            ("Plan payload", RedactJson(plan.PayloadJson)));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            plan.RunId.ToString("D"),
            plan.PlanId.ToString("D"),
            plan.DefinitionId.ToString("D"),
            plan.DefinitionVersionId.ToString("D"),
            plan.PlanHash,
            plan.PlanSchemaVersion,
            plan.DefinitionContentHash,
            plan.PayloadJson,
            plan.CreatedAtUtc.ToString("O"));

        return RestrictedIfNeeded(
            new MemorySourceItem(
                itemId,
                MemorySourceKind.ProcessRuntime,
                MemorySourceEntityKind.ProcessDefinition,
                $"Process definition {plan.DefinitionId:D}",
                content,
                contentHash,
                plan.CreatedAtUtc,
                plan.CreatedAtUtc,
                BuildProvenance(plan.RunId, MemorySourceEntityKind.ProcessDefinition, sourceEntityId, $"/processes/runs/{plan.RunId:D}/plans/{plan.PlanId:D}"),
                InternalRedactedPermission(
                    hasPayload,
                    "Process definition plan snapshots redact plan payload JSON before exposure."),
                Layout: null,
                Links: BuildLinks(plan.RunId, itemId, [new LinkTarget(MemorySourceEntityKind.ProcessRun, plan.RunId.ToString("D"), "DefinesRun")]),
                References:
                [
                    Reference("process-definition", plan.DefinitionId, 0),
                    Reference("process-definition-version", plan.DefinitionVersionId, 1),
                    Reference("process-plan", plan.PlanId, 2)
                ],
                StorageReference: null,
                Metadata(
                    ("planId", plan.PlanId.ToString("D")),
                    ("definitionId", plan.DefinitionId.ToString("D")),
                    ("definitionVersionId", plan.DefinitionVersionId.ToString("D")),
                    ("planSchemaVersion", plan.PlanSchemaVersion))),
            hasPayload,
            "Process definition hash includes raw plan payload JSON. Use only for non-exportable source integrity checks.");
    }

    private static MemorySourceItem MapRun(ProcessRuntimeStateEntity state)
    {
        var content = BuildContent(
            ("Run id", state.RunId.ToString("D")),
            ("Root run id", state.RootRunId.ToString("D")),
            ("Plan id", state.PlanId.ToString("D")),
            ("Status", state.Status.ToString()),
            ("Step count", state.Steps.Count.ToString(CultureInfo.InvariantCulture)),
            ("Claim count", state.Claims.Count.ToString(CultureInfo.InvariantCulture)),
            ("Result count", state.ResultReceipts.Count.ToString(CultureInfo.InvariantCulture)),
            ("Available artifact slot count", state.AvailableArtifactSlots.Count.ToString(CultureInfo.InvariantCulture)));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            state.RunId.ToString("D"),
            state.RootRunId.ToString("D"),
            state.PlanId.ToString("D"),
            state.PlanHash,
            state.Status.ToString(),
            state.UpdatedAtUtc.ToString("O"),
            state.ConcurrencyToken.ToString("D"),
            state.Steps.Count.ToString(CultureInfo.InvariantCulture),
            state.Claims.Count.ToString(CultureInfo.InvariantCulture),
            state.ResultReceipts.Count.ToString(CultureInfo.InvariantCulture));
        var itemId = BuildItemId(state.RunId, MemorySourceEntityKind.ProcessRun, state.RunId.ToString("D"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessRun,
            $"Process run {state.RunId:D}",
            content,
            contentHash,
            CreatedAtUtc: null,
            state.UpdatedAtUtc,
            BuildProvenance(state.RunId, MemorySourceEntityKind.ProcessRun, state.RunId.ToString("D"), $"/processes/runs/{state.RunId:D}"),
            InternalReadOnlyPermission("Process run snapshots expose runtime status and identifiers only."),
            Layout: null,
            Links: [],
            References:
            [
                Reference("root-process-run", state.RootRunId, 0),
                Reference("process-plan", state.PlanId, 1)
            ],
            StorageReference: null,
            Metadata(
                ("status", state.Status.ToString()),
                ("rootRunId", state.RootRunId.ToString("D")),
                ("planId", state.PlanId.ToString("D"))));
    }

    private static MemorySourceItem MapStep(ProcessRuntimeStepEntity step)
    {
        var sourceEntityId = StepSourceEntityId(step.StepInstanceId);
        var itemId = BuildItemId(step.RunId, MemorySourceEntityKind.ProcessStepEvidence, sourceEntityId);
        var content = BuildContent(
            ("Step instance id", step.StepInstanceId.ToString("D")),
            ("Step definition id", step.StepDefinitionId.ToString("D")),
            ("Status", step.Status.ToString()),
            ("Executable", step.IsExecutable.ToString()),
            ("Attempt", step.AttemptNumber.ToString(CultureInfo.InvariantCulture)),
            ("Dependency step ids", step.DependencyStepIds),
            ("Required artifact slot ids", step.RequiredArtifactSlotIds),
            ("Active claim token", step.ActiveClaimToken?.ToString("D")),
            ("Completed result key", step.CompletedResultKey?.ToString("D")));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            step.RunId.ToString("D"),
            step.StepInstanceId.ToString("D"),
            step.StepDefinitionId.ToString("D"),
            step.Status.ToString(),
            step.IsExecutable.ToString(),
            step.AttemptNumber.ToString(CultureInfo.InvariantCulture),
            step.DependencyStepIds,
            step.RequiredArtifactSlotIds,
            step.ActiveClaimToken?.ToString("D"),
            step.CompletedResultKey?.ToString("D"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessStepEvidence,
            $"Process step {step.StepInstanceId:D}",
            content,
            contentHash,
            CreatedAtUtc: null,
            UpdatedAtUtc: null,
            BuildProvenance(step.RunId, MemorySourceEntityKind.ProcessStepEvidence, sourceEntityId, $"/processes/runs/{step.RunId:D}/steps/{step.StepInstanceId:D}"),
            InternalReadOnlyPermission("Process step snapshots expose scheduler state and dependency identifiers only."),
            Layout: null,
            Links: BuildLinks(step.RunId, itemId, [new LinkTarget(MemorySourceEntityKind.ProcessRun, step.RunId.ToString("D"), "BelongsToRun")]),
            References:
            [
                Reference("process-step", step.StepInstanceId, 0),
                Reference("process-step-definition", step.StepDefinitionId, 1)
            ],
            StorageReference: null,
            Metadata(
                ("status", step.Status.ToString()),
                ("attemptNumber", step.AttemptNumber.ToString(CultureInfo.InvariantCulture))));
    }

    private static MemorySourceItem MapAssignment(ProcessRuntimeStepAssignmentEntity assignment)
    {
        var sourceEntityId = $"assignment:{assignment.StepInstanceId:D}";
        var itemId = BuildItemId(assignment.RunId, MemorySourceEntityKind.ProcessRunAssignment, sourceEntityId);
        var hasSensitivePayload = HasPayload(assignment.Prompt) || HasPayload(assignment.LaunchVariablesJson);
        var content = BuildContent(
            ("Step key", assignment.StepKey),
            ("Role key", assignment.RoleKey),
            ("Role resource key", assignment.RoleResourceKey),
            ("Role display name", assignment.RoleDisplayName),
            ("Executor kind", assignment.ExecutorKind),
            ("Executor id", assignment.ExecutorId),
            ("Executor display name", assignment.ExecutorDisplayName),
            ("Assignment reason", assignment.AssignmentReason),
            ("Allowed operations", assignment.AllowedOperations),
            ("Operation target scope", assignment.OperationTargetScope),
            ("Produced artifact slot ids", assignment.ProducedArtifactSlotIds),
            ("Required artifact slot ids", assignment.RequiredArtifactSlotIds),
            ("Branch gate source step key", assignment.BranchGateSourceStepKey),
            ("Branch gate required outcome key", assignment.BranchGateRequiredOutcomeKey),
            ("Prompt", assignment.Prompt),
            ("Launch variables", RedactJson(assignment.LaunchVariablesJson)));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            assignment.RunId.ToString("D"),
            assignment.StepInstanceId.ToString("D"),
            assignment.PlanId.ToString("D"),
            assignment.StepKey,
            assignment.RoleKey,
            assignment.RoleResourceKey,
            assignment.ExecutorKind,
            assignment.ExecutorId,
            assignment.Prompt,
            assignment.ReadinessHash,
            assignment.AssignmentReason,
            assignment.ProducedArtifactSlotIds,
            assignment.RequiredArtifactSlotIds,
            assignment.AllowedOperations,
            assignment.OperationTargetScope,
            assignment.LaunchVariablesJson,
            assignment.BranchGateSourceStepKey,
            assignment.BranchGateRequiredOutcomeKey,
            assignment.CreatedAtUtc.ToString("O"));

        return RestrictedIfNeeded(
            new MemorySourceItem(
                itemId,
                MemorySourceKind.ProcessRuntime,
                MemorySourceEntityKind.ProcessRunAssignment,
                $"Process assignment {assignment.StepKey}",
                content,
                contentHash,
                assignment.CreatedAtUtc,
                assignment.CreatedAtUtc,
                BuildProvenance(assignment.RunId, MemorySourceEntityKind.ProcessRunAssignment, sourceEntityId, $"/processes/runs/{assignment.RunId:D}/assignments/{assignment.StepInstanceId:D}"),
                InternalRedactedPermission(
                    hasSensitivePayload,
                    "Process assignment snapshots redact prompts and launch-variable JSON before exposure."),
                Layout: null,
                Links: BuildLinks(
                    assignment.RunId,
                    itemId,
                    [
                        new LinkTarget(MemorySourceEntityKind.ProcessRun, assignment.RunId.ToString("D"), "BelongsToRun"),
                        new LinkTarget(MemorySourceEntityKind.ProcessStepEvidence, StepSourceEntityId(assignment.StepInstanceId), "DescribesStep")
                    ]),
                References:
                [
                    Reference("process-step", assignment.StepInstanceId, 0),
                    Reference("process-plan", assignment.PlanId, 1),
                    Reference("process-role", assignment.RoleKey, 2),
                    Reference("process-executor", assignment.ExecutorId, 3)
                ],
                StorageReference: null,
                Metadata(
                    ("stepKey", assignment.StepKey),
                    ("roleKey", assignment.RoleKey),
                    ("executorKind", assignment.ExecutorKind),
                    ("executorId", assignment.ExecutorId),
                    ("operationTargetScope", assignment.OperationTargetScope))),
            hasSensitivePayload,
            "Process assignment hash includes raw prompt or launch-variable JSON. Use only for non-exportable source integrity checks.");
    }

    private static MemorySourceItem MapDispatchClaim(ProcessDispatchClaimEntity claim)
    {
        var sourceEntityId = $"claim:{claim.ClaimToken:D}";
        var itemId = BuildItemId(claim.RunId, MemorySourceEntityKind.ProcessAgentSession, sourceEntityId);
        var content = BuildContent(
            ("Claim token", claim.ClaimToken.ToString("D")),
            ("Step instance id", claim.StepInstanceId.ToString("D")),
            ("Owner id", claim.OwnerId),
            ("Status", claim.Status.ToString()),
            ("Attempt", claim.AttemptNumber.ToString(CultureInfo.InvariantCulture)),
            ("Expires at", claim.ExpiresAtUtc.ToString("O")),
            ("Renewed at", claim.RenewedAtUtc?.ToString("O")),
            ("Result idempotency key", claim.ResultIdempotencyKey?.ToString("D")));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            claim.RunId.ToString("D"),
            claim.ClaimToken.ToString("D"),
            claim.StepInstanceId.ToString("D"),
            claim.OwnerId,
            claim.Status.ToString(),
            claim.AttemptNumber.ToString(CultureInfo.InvariantCulture),
            claim.CreatedAtUtc.ToString("O"),
            claim.ExpiresAtUtc.ToString("O"),
            claim.RenewedAtUtc?.ToString("O"),
            claim.ResultIdempotencyKey?.ToString("D"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessAgentSession,
            $"Process dispatch session {claim.OwnerId}",
            content,
            contentHash,
            claim.CreatedAtUtc,
            claim.RenewedAtUtc ?? claim.CreatedAtUtc,
            BuildProvenance(claim.RunId, MemorySourceEntityKind.ProcessAgentSession, sourceEntityId, $"/processes/runs/{claim.RunId:D}/claims/{claim.ClaimToken:D}"),
            InternalReadOnlyPermission("Process dispatch claim snapshots expose claim metadata, not agent transcript content."),
            Layout: null,
            Links: BuildLinks(
                claim.RunId,
                itemId,
                [
                    new LinkTarget(MemorySourceEntityKind.ProcessRun, claim.RunId.ToString("D"), "BelongsToRun"),
                    new LinkTarget(MemorySourceEntityKind.ProcessStepEvidence, StepSourceEntityId(claim.StepInstanceId), "ExecutesStep")
                ]),
            References:
            [
                Reference("process-step", claim.StepInstanceId, 0),
                Reference("dispatch-owner", claim.OwnerId, 1),
                Reference("dispatch-claim", claim.ClaimToken, 2)
            ],
            StorageReference: null,
            Metadata(
                ("ownerId", claim.OwnerId),
                ("status", claim.Status.ToString()),
                ("attemptNumber", claim.AttemptNumber.ToString(CultureInfo.InvariantCulture))));
    }

    private static MemorySourceItem MapExecutionObservation(ProcessExecutionObservation observation)
    {
        var runId = observation.RunId.Value;
        var sourceEntityId = $"execution:{observation.ExecutionRunId:D}";
        var itemId = BuildItemId(runId, MemorySourceEntityKind.ProcessAgentSession, sourceEntityId);
        var artifacts = string.Join(
            Environment.NewLine,
            observation.Artifacts.Select(artifact =>
                $"{artifact.ArtifactKind}: {artifact.DisplayName} ({artifact.RelativePath}) {artifact.Summary}"));
        var activities = string.Join(
            Environment.NewLine,
            observation.RecentActivities.Select(activity =>
                $"{activity.CreatedAtUtc:O} {activity.State} {activity.Phase}: {activity.Message}"));
        var tools = string.Join(
            Environment.NewLine,
            observation.RecentTools.Select(tool =>
                $"{tool.StartedAtUtc:O} {tool.ToolName}: {tool.RequestSummary} => {tool.ExitSummary}"));
        var hasSensitivePayload =
            HasPayload(observation.InputSummary) ||
            HasPayload(observation.ResultSummary) ||
            HasPayload(observation.LastError) ||
            HasPayload(activities) ||
            HasPayload(tools) ||
            HasPayload(artifacts);
        var content = BuildContent(
            ("Execution run id", observation.ExecutionRunId.ToString("D")),
            ("Agent id", observation.AgentId.ToString("D")),
            ("Agent name", observation.AgentName),
            ("Provider", observation.ProviderName),
            ("Model", observation.Model),
            ("State", observation.State),
            ("Outcome", observation.Outcome),
            ("Input summary", observation.InputSummary),
            ("Result summary", observation.ResultSummary),
            ("Recent activities", activities),
            ("Recent tools", tools),
            ("Artifact references", artifacts),
            ("Last error", observation.LastError));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            observation.ExecutionRunId.ToString("D"),
            runId.ToString("D"),
            observation.StepInstanceId.Value.ToString("D"),
            observation.AgentId.ToString("D"),
            observation.ProviderName,
            observation.Model,
            observation.State,
            observation.Outcome,
            observation.CreatedAtUtc.ToString("O"),
            observation.UpdatedAtUtc.ToString("O"),
            observation.StartedAtUtc?.ToString("O"),
            observation.CompletedAtUtc?.ToString("O"),
            observation.InputSummary,
            observation.ResultSummary,
            activities,
            tools,
            artifacts,
            observation.LastError);

        return RestrictedIfNeeded(
            new MemorySourceItem(
                itemId,
                MemorySourceKind.ProcessRuntime,
                MemorySourceEntityKind.ProcessAgentSession,
                $"Agent session {observation.AgentName}",
                content,
                contentHash,
                observation.CreatedAtUtc,
                observation.UpdatedAtUtc,
                BuildProvenance(runId, MemorySourceEntityKind.ProcessAgentSession, sourceEntityId, $"/processes/runs/{runId:D}/agent-executions/{observation.ExecutionRunId:D}"),
                InternalRedactedPermission(
                    hasSensitivePayload,
                    "Process agent session snapshots redact execution summaries, activities, tool summaries, artifact summaries, and errors before exposure."),
                Layout: null,
                Links: BuildLinks(
                    runId,
                    itemId,
                    [
                        new LinkTarget(MemorySourceEntityKind.ProcessRun, runId.ToString("D"), "BelongsToRun"),
                        new LinkTarget(MemorySourceEntityKind.ProcessStepEvidence, StepSourceEntityId(observation.StepInstanceId.Value), "ExecutesStep")
                    ]),
                References:
                [
                    Reference("execution-run", observation.ExecutionRunId, 0),
                    Reference("agent", observation.AgentId, 1),
                    Reference("process-step", observation.StepInstanceId.Value, 2)
                ],
                StorageReference: null,
                Metadata(
                    ("agentId", observation.AgentId.ToString("D")),
                    ("agentName", observation.AgentName),
                    ("state", observation.State),
                    ("outcome", observation.Outcome),
                    ("providerName", observation.ProviderName),
                    ("model", observation.Model))),
            hasSensitivePayload,
            "Process agent session hash includes raw execution summaries and activity/tool/artifact summaries. Use only for non-exportable source integrity checks.");
    }

    private static MemorySourceItem MapResultReceipt(ProcessStrategyResultReceiptEntity receipt)
    {
        var sourceEntityId = $"result:{receipt.StepInstanceId:D}:{receipt.StrategyId}:{receipt.IdempotencyKey:D}";
        var itemId = BuildItemId(receipt.RunId, MemorySourceEntityKind.ProcessDecision, sourceEntityId);
        var content = BuildContent(
            ("Step instance id", receipt.StepInstanceId.ToString("D")),
            ("Strategy id", receipt.StrategyId),
            ("Idempotency key", receipt.IdempotencyKey.ToString("D")),
            ("Outcome", receipt.Outcome),
            ("Applied step status", receipt.AppliedStepStatus.ToString()),
            ("Result hash", receipt.ResultHash),
            ("Diagnostics", receipt.DiagnosticsJson),
            ("Produced artifacts", receipt.ProducedArtifactsJson),
            ("Recovery decision", receipt.RecoveryDecisionJson));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            receipt.RunId.ToString("D"),
            receipt.StepInstanceId.ToString("D"),
            receipt.StrategyId,
            receipt.IdempotencyKey.ToString("D"),
            receipt.Outcome,
            receipt.AppliedStepStatus.ToString(),
            receipt.ResultHash,
            receipt.DiagnosticsJson,
            receipt.ProducedArtifactsJson,
            receipt.RecoveryDecisionJson);

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessDecision,
            $"Process result {receipt.Outcome}",
            content,
            contentHash,
            CreatedAtUtc: null,
            UpdatedAtUtc: null,
            BuildProvenance(receipt.RunId, MemorySourceEntityKind.ProcessDecision, sourceEntityId, $"/processes/runs/{receipt.RunId:D}/results/{receipt.IdempotencyKey:D}"),
            InternalReadOnlyPermission("Process strategy result snapshots expose outcome metadata and source hashes, not result payload bytes."),
            Layout: null,
            Links: BuildLinks(
                receipt.RunId,
                itemId,
                [
                    new LinkTarget(MemorySourceEntityKind.ProcessRun, receipt.RunId.ToString("D"), "BelongsToRun"),
                    new LinkTarget(MemorySourceEntityKind.ProcessStepEvidence, StepSourceEntityId(receipt.StepInstanceId), "CompletesStep")
                ]),
            References:
            [
                Reference("process-step", receipt.StepInstanceId, 0),
                Reference("strategy", receipt.StrategyId, 1),
                Reference("result-idempotency-key", receipt.IdempotencyKey, 2)
            ],
            StorageReference: null,
            Metadata(
                ("strategyId", receipt.StrategyId),
                ("outcome", receipt.Outcome),
                ("appliedStepStatus", receipt.AppliedStepStatus.ToString()),
                ("diagnosticsJson", receipt.DiagnosticsJson),
                ("producedArtifactsJson", receipt.ProducedArtifactsJson),
                ("recoveryDecisionJson", receipt.RecoveryDecisionJson ?? string.Empty)));
    }

    private static MemorySourceItem MapAvailableArtifactSlot(ProcessRuntimeAvailableArtifactSlotEntity slot)
    {
        var sourceEntityId = $"slot:{slot.SlotId:D}";
        var itemId = BuildItemId(slot.RunId, MemorySourceEntityKind.ProcessArtifact, sourceEntityId);
        var content = BuildContent(
            ("Available artifact slot id", slot.SlotId.ToString("D")),
            ("Run id", slot.RunId.ToString("D")));
        var contentHash = MemorySourceSnapshotHasher.Compute(slot.RunId.ToString("D"), slot.SlotId.ToString("D"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessArtifact,
            $"Available artifact slot {slot.SlotId:D}",
            content,
            contentHash,
            CreatedAtUtc: null,
            UpdatedAtUtc: null,
            BuildProvenance(slot.RunId, MemorySourceEntityKind.ProcessArtifact, sourceEntityId, $"/processes/runs/{slot.RunId:D}/artifact-slots/{slot.SlotId:D}"),
            InternalReadOnlyPermission("Process artifact slot snapshots expose slot availability only."),
            Layout: null,
            Links: BuildLinks(slot.RunId, itemId, [new LinkTarget(MemorySourceEntityKind.ProcessRun, slot.RunId.ToString("D"), "BelongsToRun")]),
            References: [Reference("artifact-slot", slot.SlotId, 0)],
            StorageReference: null,
            Metadata(("slotId", slot.SlotId.ToString("D"))));
    }

    private static MemorySourceItem MapArtifactLedger(ProcessArtifactLedgerSnapshotRow artifact)
    {
        var sourceEntityId = $"artifact:{artifact.LedgerEventId:D}";
        var itemId = BuildItemId(artifact.RunId, MemorySourceEntityKind.ProcessArtifact, sourceEntityId);
        var content = BuildContent(
            ("Ledger event id", artifact.LedgerEventId.ToString("D")),
            ("Event id", artifact.EventId.ToString("D")),
            ("Slot id", artifact.SlotId.ToString("D")),
            ("Artifact id", artifact.ArtifactId.ToString("D")),
            ("Content hash", artifact.ContentHash));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            artifact.RunId.ToString("D"),
            artifact.LedgerEventId.ToString("D"),
            artifact.EventId.ToString("D"),
            artifact.SlotId.ToString("D"),
            artifact.ArtifactId.ToString("D"),
            artifact.ContentHash,
            artifact.OccurredAtUtc.ToString("O"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessArtifact,
            $"Process artifact {artifact.ArtifactId:D}",
            content,
            contentHash,
            artifact.OccurredAtUtc,
            artifact.OccurredAtUtc,
            BuildProvenance(artifact.RunId, MemorySourceEntityKind.ProcessArtifact, sourceEntityId, $"/processes/runs/{artifact.RunId:D}/artifacts/{artifact.ArtifactId:D}"),
            InternalReadOnlyPermission("Process artifact snapshots expose artifact references and content hashes, not artifact payload bytes."),
            Layout: null,
            Links: BuildLinks(
                artifact.RunId,
                itemId,
                [
                    new LinkTarget(MemorySourceEntityKind.ProcessRun, artifact.RunId.ToString("D"), "BelongsToRun"),
                    new LinkTarget(MemorySourceEntityKind.ProcessJournal, EventSourceEntityId(artifact.EventId), "ProducedByEvent")
                ]),
            References:
            [
                Reference("artifact-slot", artifact.SlotId, 0),
                Reference("artifact", artifact.ArtifactId, 1),
                Reference("process-event", artifact.EventId, 2)
            ],
            StorageReference: new MemorySourceStorageReference(
                SourceProviderName,
                "artifact-id",
                artifact.ArtifactId.ToString("D"),
                "application/octet-stream",
                $"process-artifact-{artifact.ArtifactId:D}"),
            Metadata(
                ("slotId", artifact.SlotId.ToString("D")),
                ("artifactId", artifact.ArtifactId.ToString("D")),
                ("contentHash", artifact.ContentHash)));
    }

    private static MemorySourceItem MapRuntimeEvent(ProcessRuntimeEventEntity runtimeEvent)
    {
        var sourceEntityId = EventSourceEntityId(runtimeEvent.EventId);
        var itemId = BuildItemId(runtimeEvent.RunId, MemorySourceEntityKind.ProcessJournal, sourceEntityId);
        var content = BuildContent(
            ("Event type", runtimeEvent.EventType),
            ("Global sequence", runtimeEvent.GlobalSequence.ToString(CultureInfo.InvariantCulture)),
            ("Root sequence", runtimeEvent.RootSequence.ToString(CultureInfo.InvariantCulture)),
            ("Correlation id", runtimeEvent.CorrelationId),
            ("Causation id", runtimeEvent.CausationId?.ToString("D")),
            ("Actor kind", runtimeEvent.ActorKind),
            ("Actor id", runtimeEvent.ActorId),
            ("Schema version", runtimeEvent.SchemaVersion),
            ("Sensitivity", runtimeEvent.Sensitivity),
            ("Payload hash", runtimeEvent.PayloadHash));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            runtimeEvent.GlobalSequence.ToString(CultureInfo.InvariantCulture),
            runtimeEvent.RootSequence.ToString(CultureInfo.InvariantCulture),
            runtimeEvent.EventId.ToString("D"),
            runtimeEvent.RootRunId.ToString("D"),
            runtimeEvent.RunId.ToString("D"),
            runtimeEvent.CorrelationId,
            runtimeEvent.CausationId?.ToString("D"),
            runtimeEvent.ActorKind,
            runtimeEvent.ActorId,
            runtimeEvent.SchemaVersion,
            runtimeEvent.Sensitivity,
            runtimeEvent.OccurredAtUtc.ToString("O"),
            runtimeEvent.EventType,
            runtimeEvent.PayloadHash);

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessJournal,
            runtimeEvent.EventType,
            content,
            contentHash,
            runtimeEvent.OccurredAtUtc,
            runtimeEvent.OccurredAtUtc,
            BuildProvenance(runtimeEvent.RunId, MemorySourceEntityKind.ProcessJournal, sourceEntityId, $"/processes/runs/{runtimeEvent.RunId:D}/events/{runtimeEvent.EventId:D}"),
            new MemorySourcePermissionContext(
                MemorySourceAccessMode.ReadOnly,
                ResolveSensitivity(runtimeEvent.Sensitivity),
                ContainsSensitivePayload: false,
                "Process runtime event snapshots expose event metadata and payload hashes only.",
                "Source-grounded process runtime evidence."),
            Layout: null,
            Links: BuildLinks(runtimeEvent.RunId, itemId, [new LinkTarget(MemorySourceEntityKind.ProcessRun, runtimeEvent.RunId.ToString("D"), "BelongsToRun")]),
            References:
            [
                Reference("process-event", runtimeEvent.EventId, 0),
                Reference("root-process-run", runtimeEvent.RootRunId, 1)
            ],
            StorageReference: null,
            Metadata(
                ("eventType", runtimeEvent.EventType),
                ("actorKind", runtimeEvent.ActorKind),
                ("actorId", runtimeEvent.ActorId),
                ("schemaVersion", runtimeEvent.SchemaVersion)));
    }

    private static MemorySourceItem MapProjectionHistory(ProcessProjectionHistoryEntity history)
    {
        var sourceEntityId = $"projection:{history.ProjectorName}:{history.ProjectionKey}:{history.GlobalSequence.ToString(CultureInfo.InvariantCulture)}";
        var itemId = BuildItemId(history.RunId, MemorySourceEntityKind.ProcessJournal, sourceEntityId);
        var hasPayload = HasPayload(history.PayloadJson);
        var content = BuildContent(
            ("Projector", history.ProjectorName),
            ("Projection key", history.ProjectionKey),
            ("Global sequence", history.GlobalSequence.ToString(CultureInfo.InvariantCulture)),
            ("Event type", history.EventType),
            ("Schema version", history.SchemaVersion),
            ("Sensitivity", history.Sensitivity),
            ("Payload hash", history.PayloadHash),
            ("Payload", RedactJson(history.PayloadJson)));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            history.ProjectorName,
            history.ProjectionKey,
            history.GlobalSequence.ToString(CultureInfo.InvariantCulture),
            history.RootRunId.ToString("D"),
            history.RunId.ToString("D"),
            history.OccurredAtUtc.ToString("O"),
            history.EventType,
            history.SchemaVersion,
            history.PayloadJson,
            history.PayloadHash,
            history.Sensitivity);

        return RestrictedIfNeeded(
            new MemorySourceItem(
                itemId,
                MemorySourceKind.ProcessRuntime,
                MemorySourceEntityKind.ProcessJournal,
                $"Projection {history.ProjectorName}",
                content,
                contentHash,
                history.OccurredAtUtc,
                history.OccurredAtUtc,
                BuildProvenance(history.RunId, MemorySourceEntityKind.ProcessJournal, sourceEntityId, $"/processes/runs/{history.RunId:D}/projections/{history.ProjectorName}/{history.GlobalSequence.ToString(CultureInfo.InvariantCulture)}"),
                new MemorySourcePermissionContext(
                    hasPayload ? MemorySourceAccessMode.Redacted : MemorySourceAccessMode.ReadOnly,
                    ResolveSensitivity(history.Sensitivity),
                    hasPayload,
                    "Process projection history snapshots redact projection payload JSON before exposure.",
                    "Source-grounded process projection evidence."),
                Layout: null,
                Links: BuildLinks(history.RunId, itemId, [new LinkTarget(MemorySourceEntityKind.ProcessRun, history.RunId.ToString("D"), "BelongsToRun")]),
                References:
                [
                    Reference("projector", history.ProjectorName, 0),
                    Reference("projection-key", history.ProjectionKey, 1),
                    Reference("root-process-run", history.RootRunId, 2)
                ],
                StorageReference: null,
                Metadata(
                    ("projectorName", history.ProjectorName),
                    ("projectionKey", history.ProjectionKey),
                    ("eventType", history.EventType))),
            hasPayload,
            "Process projection history hash includes raw projection payload JSON. Use only for non-exportable source integrity checks.");
    }

    private static MemorySourceItem MapDeadLetter(ProcessProjectionDeadLetterSnapshotRow deadLetter)
    {
        var sourceEntityId = $"dead-letter:{deadLetter.DeadLetterId:D}";
        var itemId = BuildItemId(deadLetter.RunId, MemorySourceEntityKind.ProcessConformanceObservation, sourceEntityId);
        var content = BuildContent(
            ("Projector", deadLetter.ProjectorName),
            ("Shard key", deadLetter.ShardKey),
            ("Event id", deadLetter.EventId.ToString("D")),
            ("Global sequence", deadLetter.GlobalSequence.ToString(CultureInfo.InvariantCulture)),
            ("Error class", deadLetter.ErrorClass),
            ("Diagnostic reference", deadLetter.DiagnosticReference),
            ("Retry policy", deadLetter.RetryPolicy));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            deadLetter.RunId.ToString("D"),
            deadLetter.DeadLetterId.ToString("D"),
            deadLetter.ProjectorName,
            deadLetter.ShardKey,
            deadLetter.EventId.ToString("D"),
            deadLetter.GlobalSequence.ToString(CultureInfo.InvariantCulture),
            deadLetter.ErrorClass,
            deadLetter.DiagnosticReference,
            deadLetter.RetryPolicy,
            deadLetter.DeadLetteredAtUtc.ToString("O"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessConformanceObservation,
            $"Projection dead letter {deadLetter.ErrorClass}",
            content,
            contentHash,
            deadLetter.DeadLetteredAtUtc,
            deadLetter.DeadLetteredAtUtc,
            BuildProvenance(deadLetter.RunId, MemorySourceEntityKind.ProcessConformanceObservation, sourceEntityId, $"/processes/runs/{deadLetter.RunId:D}/projection-dead-letters/{deadLetter.DeadLetterId:D}"),
            InternalReadOnlyPermission("Process conformance observations expose diagnostics and retry policy, not failed payload bytes."),
            Layout: null,
            Links: BuildLinks(
                deadLetter.RunId,
                itemId,
                [
                    new LinkTarget(MemorySourceEntityKind.ProcessRun, deadLetter.RunId.ToString("D"), "BelongsToRun"),
                    new LinkTarget(MemorySourceEntityKind.ProcessJournal, EventSourceEntityId(deadLetter.EventId), "ObservedEvent")
                ]),
            References:
            [
                Reference("projector", deadLetter.ProjectorName, 0),
                Reference("process-event", deadLetter.EventId, 1)
            ],
            StorageReference: null,
            Metadata(
                ("projectorName", deadLetter.ProjectorName),
                ("errorClass", deadLetter.ErrorClass),
                ("retryPolicy", deadLetter.RetryPolicy)));
    }

    private static MemorySourceItem MapCompletionOutcome(ProcessRuntimeStateEntity state)
    {
        var sourceEntityId = $"completion:{state.RunId:D}";
        var itemId = BuildItemId(state.RunId, MemorySourceEntityKind.ProcessCompletionOutcome, sourceEntityId);
        var completedSteps = state.Steps.Count(step => step.Status == ProcessRuntimeStepStatus.Completed);
        var failedSteps = state.Steps.Count(step => step.Status == ProcessRuntimeStepStatus.Failed);
        var blockedSteps = state.Steps.Count(step => step.Status == ProcessRuntimeStepStatus.Blocked);
        var outcomes = string.Join(
            ", ",
            state.ResultReceipts
                .GroupBy(receipt => receipt.Outcome, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}:{group.Count().ToString(CultureInfo.InvariantCulture)}"));
        var content = BuildContent(
            ("Run id", state.RunId.ToString("D")),
            ("Status", state.Status.ToString()),
            ("Updated at", state.UpdatedAtUtc.ToString("O")),
            ("Completed steps", completedSteps.ToString(CultureInfo.InvariantCulture)),
            ("Failed steps", failedSteps.ToString(CultureInfo.InvariantCulture)),
            ("Blocked steps", blockedSteps.ToString(CultureInfo.InvariantCulture)),
            ("Result outcomes", outcomes),
            ("Feedback hook", ProcessCompletionFeedbackHook));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            state.RunId.ToString("D"),
            state.Status.ToString(),
            state.UpdatedAtUtc.ToString("O"),
            completedSteps.ToString(CultureInfo.InvariantCulture),
            failedSteps.ToString(CultureInfo.InvariantCulture),
            blockedSteps.ToString(CultureInfo.InvariantCulture),
            outcomes,
            ProcessCompletionFeedbackHook);

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.ProcessRuntime,
            MemorySourceEntityKind.ProcessCompletionOutcome,
            $"Process completion {state.Status}",
            content,
            contentHash,
            CreatedAtUtc: null,
            state.UpdatedAtUtc,
            BuildProvenance(state.RunId, MemorySourceEntityKind.ProcessCompletionOutcome, sourceEntityId, $"/processes/runs/{state.RunId:D}/completion"),
            InternalReadOnlyPermission("Process completion snapshots expose final outcome counts and feedback hook identity only."),
            Layout: null,
            Links: BuildLinks(state.RunId, itemId, [new LinkTarget(MemorySourceEntityKind.ProcessRun, state.RunId.ToString("D"), "SummarizesRun")]),
            References: [Reference("process-run", state.RunId, 0)],
            StorageReference: null,
            Metadata(
                ("status", state.Status.ToString()),
                (FeedbackHookMetadataKey, ProcessCompletionFeedbackHook),
                ("completedSteps", completedSteps.ToString(CultureInfo.InvariantCulture)),
                ("failedSteps", failedSteps.ToString(CultureInfo.InvariantCulture))));
    }

    private static MemorySourceItem RestrictedIfNeeded(
        MemorySourceItem item,
        bool restricted,
        string usageSummary)
        => restricted
            ? item with
            {
                HashPolicy = MemorySourceHashPolicy.RestrictedRawPayloadIntegrity(usageSummary)
            }
            : item;

    private static MemorySourceItemId BuildItemId(
        Guid scopeId,
        MemorySourceEntityKind entityKind,
        string sourceEntityId)
        => MemorySourceItemId.Create(
            MemorySourceKind.ProcessRuntime,
            scopeId,
            entityKind,
            sourceEntityId);

    private static MemorySourceProvenance BuildProvenance(
        Guid scopeId,
        MemorySourceEntityKind entityKind,
        string sourceEntityId,
        string sourceRoute)
        => new(
            MemorySourceKind.ProcessRuntime,
            scopeId,
            entityKind,
            sourceEntityId,
            sourceRoute);

    private static MemorySourcePermissionContext InternalReadOnlyPermission(string redactionPolicy)
        => new(
            MemorySourceAccessMode.ReadOnly,
            MemorySourceSensitivity.Internal,
            ContainsSensitivePayload: false,
            redactionPolicy,
            "Source-grounded process runtime evidence.");

    private static MemorySourcePermissionContext InternalRedactedPermission(
        bool containsSensitivePayload,
        string redactionPolicy)
        => new(
            containsSensitivePayload ? MemorySourceAccessMode.Redacted : MemorySourceAccessMode.ReadOnly,
            containsSensitivePayload ? MemorySourceSensitivity.Sensitive : MemorySourceSensitivity.Internal,
            containsSensitivePayload,
            redactionPolicy,
            "Source-grounded process runtime evidence.");

    private static MemorySourceSensitivity ResolveSensitivity(string value)
    {
        if (Enum.TryParse<MemorySourceSensitivity>(value, ignoreCase: true, out var sensitivity))
        {
            return sensitivity;
        }

        return MemorySourceSensitivity.Internal;
    }

    private static string BuildContent(params (string Label, string? Value)[] fields)
        => string.Join(
            Environment.NewLine,
            fields
                .Where(field => !string.IsNullOrWhiteSpace(field.Value))
                .Select(field => $"{field.Label}: {WorkflowExecutorRedaction.RedactText(field.Value)}"));

    private static string RedactJson(string? json)
        => HasPayload(json) ? WorkflowExecutorRedaction.RedactSettingsJson(json) : string.Empty;

    private static bool HasPayload(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           !string.Equals(value.Trim(), "{}", StringComparison.Ordinal);

    private static IReadOnlyList<MemorySourceLink> BuildLinks(
        Guid scopeId,
        MemorySourceItemId sourceId,
        IReadOnlyList<LinkTarget> targets)
        => targets
            .Select(target => new MemorySourceLink(
                sourceId,
                BuildItemId(scopeId, target.EntityKind, target.SourceEntityId),
                target.Kind,
                IsUserAuthored: false))
            .OrderBy(link => link.TargetId.Value, StringComparer.Ordinal)
            .ThenBy(link => link.Kind, StringComparer.Ordinal)
            .ToArray();

    private static MemorySourceReference Reference(string referenceKind, Guid referenceId, int orderIndex)
        => new(referenceKind, referenceId.ToString("D"), orderIndex);

    private static MemorySourceReference Reference(string referenceKind, string? referenceId, int orderIndex)
        => new(referenceKind, referenceId ?? string.Empty, orderIndex);

    private static string StepSourceEntityId(Guid stepInstanceId) => $"step:{stepInstanceId:D}";

    private static string EventSourceEntityId(Guid eventId) => $"event:{eventId:D}";

    private static IReadOnlyDictionary<string, string> Metadata(params (string Key, string Value)[] values)
        => values.ToDictionary(
            value => value.Key,
            value => value.Value,
            StringComparer.Ordinal);

    private sealed record MemorySourcePageSlice(
        IReadOnlyList<MemorySourceItem> Items,
        int TotalItemCount,
        MemorySourceSnapshotCursor? NextCursor,
        bool HasMore,
        string SnapshotHash);

    private sealed record ProcessSourcePage(
        MemorySourceEntityKind EntityKind,
        int Ordinal,
        Func<CancellationToken, Task<int>> CountAsync,
        Func<int, int, CancellationToken, Task<IReadOnlyList<MemorySourceItem>>> ReadPageAsync,
        Func<int, CancellationToken, Task<MemorySourceItemId?>> ReadItemIdAsync)
    {
        private static int nextOrdinal;

        public static int NextOrdinal() => Interlocked.Increment(ref nextOrdinal);
    }

    private sealed record ProcessSourcePageCount(
        ProcessSourcePage Source,
        int Count);

    private sealed record LinkTarget(
        MemorySourceEntityKind EntityKind,
        string SourceEntityId,
        string Kind);

    private sealed record ProcessDefinitionPlanSnapshotRow(
        Guid RunId,
        Guid RootRunId,
        Guid PlanId,
        Guid RootPlanId,
        Guid? ParentPlanId,
        Guid? ParentStepId,
        Guid DefinitionId,
        Guid DefinitionVersionId,
        string PlanHash,
        string PlanSchemaVersion,
        string DefinitionContentHash,
        string PayloadJson,
        DateTimeOffset CreatedAtUtc);

    private sealed record ProcessArtifactLedgerSnapshotRow(
        Guid RunId,
        Guid RootRunId,
        Guid LedgerEventId,
        Guid EventId,
        Guid SlotId,
        Guid ArtifactId,
        string ContentHash,
        DateTimeOffset OccurredAtUtc);

    private sealed record ProcessProjectionDeadLetterSnapshotRow(
        Guid RunId,
        Guid RootRunId,
        Guid DeadLetterId,
        string ProjectorName,
        string ShardKey,
        Guid EventId,
        long GlobalSequence,
        string ErrorClass,
        string DiagnosticReference,
        string RetryPolicy,
        DateTimeOffset DeadLetteredAtUtc);
}
