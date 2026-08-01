using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRunRecordAssembler(
    IProcessRuntimeStateStore runtimeStateStore,
    IProcessRuntimeRunHierarchyStore runHierarchyStore,
    IProcessInstancePlanStore planStore,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessExecutionObservationReader executionObservationReader,
    IProcessRuntimeUsageTelemetryReader usageTelemetryReader,
    IProcessRuntimeEventReplayStore eventReplayStore,
    TimeProvider timeProvider)
{
    private const int RuntimeEventPageSize = 1_000;
    private const int MaximumRuntimeEventCount = 100_000;
    private static readonly HashSet<ProcessEventType> RunLifecycleEventTypes =
    [
        ProcessRuntimeEventTypes.ProcessRunCreated,
        ProcessRuntimeEventTypes.ProcessRunActivated,
        ProcessRuntimeEventTypes.ProcessRunCancelRequested,
        ProcessRuntimeEventTypes.ProcessRunCancelled,
        ProcessRuntimeEventTypes.ProcessRunCompleted,
        ProcessRuntimeEventTypes.ProcessRunFailed,
        ProcessRuntimeEventTypes.ProcessRunBlocked,
        ProcessRuntimeEventTypes.ProcessRunReactivated
    ];
    private static readonly HashSet<ProcessEventType> StepEventTypes =
    [
        ProcessRuntimeEventTypes.StepReady,
        ProcessRuntimeEventTypes.StepWaiting,
        ProcessRuntimeEventTypes.StepClaimed,
        ProcessRuntimeEventTypes.StepRunning,
        ProcessRuntimeEventTypes.StepCompleted,
        ProcessRuntimeEventTypes.StepFailed,
        ProcessRuntimeEventTypes.StepBlocked,
        ProcessRuntimeEventTypes.StepCancelled,
        ProcessRuntimeEventTypes.StepSkipped,
        ProcessRuntimeEventTypes.StepReworkRequested
    ];
    private static readonly HashSet<ProcessEventType> DispatchEventTypes =
    [
        ProcessRuntimeEventTypes.DispatchClaimCreated,
        ProcessRuntimeEventTypes.DispatchLeaseRenewed,
        ProcessRuntimeEventTypes.DispatchClaimExpired,
        ProcessRuntimeEventTypes.DispatchClaimReleased,
        ProcessRuntimeEventTypes.DispatchClaimReclaimed,
        ProcessRuntimeEventTypes.DispatchClaimCompleted
    ];
    private static readonly HashSet<ProcessEventType> ManagerEventTypes =
    [
        ProcessRuntimeEventTypes.ManagerIncidentRaised,
        ProcessRuntimeEventTypes.ManagerRecoveryApproved,
        ProcessRuntimeEventTypes.ManagerRecoveryDenied,
        ProcessRuntimeEventTypes.ManagerBranchDecisionRecorded,
        ProcessRuntimeEventTypes.ManagerBranchDecisionRejected,
        ProcessRuntimeEventTypes.ManagerLoopBudgetEscalated,
        ProcessRuntimeEventTypes.ManagerSubprocessMessageQueued
    ];

    private readonly ProcessRunFactsAggregator aggregator = new();

    public async Task<ProcessRunFactsCompletion> AssembleAsync(
        ProcessRunFactsClaim claim,
        ProcessRunRecord currentRecord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(currentRecord);

        var nowUtc = timeProvider.GetUtcNow();
        ValidateClaim(claim, currentRecord, nowUtc);

        var primaryState = await runtimeStateStore
            .LoadAsync(claim.RunId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Runtime state for process run '{claim.RunId}' was not found.");
        var primaryRuntimeStateEvidenceComplete = ValidatePrimaryRuntimeState(
            primaryState,
            currentRecord.Summary);

        var subtree = await LoadSubtreeAsync(
            primaryState,
            primaryRuntimeStateEvidenceComplete,
            cancellationToken).ConfigureAwait(false);
        var targetRunIds = subtree.AllRunIds.ToHashSet();
        var runtimeEvents = await ReadRuntimeEventsAsync(
            primaryState.RootRunId,
            targetRunIds,
            currentRecord.Summary,
            cancellationToken).ConfigureAwait(false);

        var observationFromUtc = runtimeEvents.FirstTargetEventAtUtc ??
            currentRecord.Summary.Metrics.StartedAtUtc ??
            DateTimeOffset.MinValue;
        var observationToUtc = nowUtc > currentRecord.Summary.Metrics.EndedAtUtc
            ? nowUtc
            : currentRecord.Summary.Metrics.EndedAtUtc;
        var knownStepIds = subtree.Sources
            .SelectMany(source => source.State.Steps)
            .Select(step => step.StepInstanceId)
            .Distinct()
            .Take(ProcessRunRecordPayloadLimits.MaximumSteps + 1)
            .ToArray();
        var filterStepIds = subtree.RuntimeStateEvidenceComplete &&
            knownStepIds.Length <= ProcessRunRecordPayloadLimits.MaximumSteps
                ? knownStepIds
                : [];
        var executionObservationRead = await executionObservationReader
            .ReadAsync(
                new ProcessExecutionObservationQuery(
                    subtree.AllRunIds,
                    observationFromUtc,
                    observationToUtc,
                    ProcessRunRecordPayloadLimits.MaximumExecutionRunIds)
                {
                    StepInstanceIds = filterStepIds,
                    DetailLevel = ProcessExecutionObservationDetailLevel.Summary
                },
                cancellationToken)
            .ConfigureAwait(false);
        var usageTelemetryRead = await usageTelemetryReader
            .ReadAsync(
                new ProcessRuntimeUsageTelemetryQuery(
                    subtree.AllRunIds,
                    observationFromUtc,
                    observationToUtc,
                    ProcessRunRecordPayloadLimits.MaximumExecutionRunIds),
                cancellationToken)
            .ConfigureAwait(false);

        ValidateEvidenceScope(
            targetRunIds,
            subtree.Sources,
            executionObservationRead.Items,
            usageTelemetryRead.Items);
        var result = aggregator.Aggregate(
            new ProcessRunAggregationInput(
                currentRecord.Summary.Identity,
                currentRecord.Summary.Metrics.EndedAtUtc,
                subtree,
                executionObservationRead,
                usageTelemetryRead,
                runtimeEvents));

        return new ProcessRunFactsCompletion(
            result.Identity,
            claim.SourceGlobalSequence,
            claim.ClaimToken,
            result.Completeness,
            result.AvailableEvidenceSources,
            result.MissingEvidenceSources,
            result.Warnings,
            result.Metrics,
            result.Facts,
            nowUtc);
    }

    private static void ValidateClaim(
        ProcessRunFactsClaim claim,
        ProcessRunRecord currentRecord,
        DateTimeOffset nowUtc)
    {
        var summary = currentRecord.Summary;
        if (summary.Identity.RunId != claim.RunId)
        {
            throw new InvalidOperationException(
                $"Facts claim run '{claim.RunId}' does not match record run '{summary.Identity.RunId}'.");
        }

        if (summary.SourceGlobalSequence != claim.SourceGlobalSequence)
        {
            throw new InvalidOperationException(
                $"Facts claim source sequence '{claim.SourceGlobalSequence}' is stale for run '{claim.RunId}'.");
        }

        if (summary.LifecycleState != ProcessRunRecordLifecycleState.Current)
        {
            throw new InvalidOperationException(
                $"Facts cannot be assembled for superseded process run '{claim.RunId}'.");
        }

        if (summary.FactsStatus != ProcessRunFactsStatus.Assembling)
        {
            throw new InvalidOperationException(
                $"Process run '{claim.RunId}' is not currently claimed for facts assembly.");
        }

        if (claim.LeaseExpiresAtUtc <= nowUtc)
        {
            throw new InvalidOperationException(
                $"Facts claim for process run '{claim.RunId}' expired before assembly began.");
        }
    }

    private static bool ValidatePrimaryRuntimeState(
        ProcessRuntimeStateSnapshot state,
        ProcessRunRecordSummary summary)
    {
        if (state.RunId != summary.Identity.RunId)
        {
            throw new InvalidOperationException(
                $"Runtime state run '{state.RunId}' does not match seeded run '{summary.Identity.RunId}'.");
        }

        if (state.RootRunId != summary.Identity.RootRunId)
        {
            throw new InvalidOperationException(
                $"Runtime root run '{state.RootRunId}' does not match seeded root '{summary.Identity.RootRunId}'.");
        }

        if (summary.Identity.PlanId is { } seededPlanId && seededPlanId != state.PlanId)
        {
            throw new InvalidOperationException(
                $"Runtime plan '{state.PlanId}' does not match seeded plan '{seededPlanId}'.");
        }

        var currentDisposition = state.Status switch
        {
            ProcessRuntimeStatus.Completed => ProcessRunDisposition.Succeeded,
            ProcessRuntimeStatus.Failed => ProcessRunDisposition.Failed,
            ProcessRuntimeStatus.Cancelled => ProcessRunDisposition.Cancelled,
            ProcessRuntimeStatus.Blocked => ProcessRunDisposition.Blocked,
            ProcessRuntimeStatus.Escalated => ProcessRunDisposition.Escalated,
            _ => throw new InvalidOperationException(
                $"Process run '{state.RunId}' was reactivated or is not terminal; current status is '{state.Status}'.")
        };
        if (currentDisposition != summary.Disposition)
        {
            throw new InvalidOperationException(
                $"Runtime disposition '{currentDisposition}' does not match seeded disposition '{summary.Disposition}'.");
        }

        return state.Status != ProcessRuntimeStatus.Blocked;
    }

    private async Task<ProcessRunSubtree> LoadSubtreeAsync(
        ProcessRuntimeStateSnapshot primaryState,
        bool primaryRuntimeStateEvidenceComplete,
        CancellationToken cancellationToken)
    {
        var family = await ReadRootFamilyAsync(
            primaryState.RootRunId,
            cancellationToken).ConfigureAwait(false);
        var assignmentRunIds = new[]
            {
                primaryState.RunId
            }
            .Concat(family.RunIds.Where(runId => runId != primaryState.RunId))
            .ToArray();
        var assignments = await assignmentStore
            .LoadByRunsAsync(assignmentRunIds, cancellationToken)
            .ConfigureAwait(false);
        ValidateAssignmentBatchScope(assignmentRunIds, assignments);
        var assignmentsByRun = assignments
            .GroupBy(assignment => assignment.RunId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessRuntimeStepAssignment>)group
                    .OrderBy(assignment => assignment.StepKey, StringComparer.Ordinal)
                    .ThenBy(assignment => assignment.StepInstanceId.Value)
                    .ToArray());
        var warnings = new List<ProcessRunRecordWarningCode>(family.Warnings);
        var warningSet = new HashSet<ProcessRunRecordWarningCode>(warnings);
        if (!primaryRuntimeStateEvidenceComplete)
        {
            AddWarning(
                warnings,
                warningSet,
                ProcessRunRecordWarningCode.PrimaryRunBlocked);
        }

        var subprocessEvidenceComplete = family.Complete;
        var rootFamilyIds = family.RunIds
            .Append(primaryState.RootRunId)
            .ToHashSet();
        if (primaryState.RunId != primaryState.RootRunId)
        {
            var primaryParent = ResolveParentLink(
                primaryState.RunId,
                assignmentsByRun.GetValueOrDefault(primaryState.RunId) ?? []);
            ValidateParentInRootFamily(
                primaryState.RunId,
                primaryParent.ParentRunId,
                rootFamilyIds);
            if (!primaryParent.Complete)
            {
                subprocessEvidenceComplete = false;
                AddWarning(
                    warnings,
                    warningSet,
                    ProcessRunRecordWarningCode.MissingSubprocessParentMetadata);
            }
        }

        var parentByRun = new Dictionary<ProcessRunId, ProcessRunId>();
        foreach (var runId in family.RunIds.Where(runId =>
            runId != primaryState.RunId))
        {
            var parent = ResolveParentLink(
                runId,
                assignmentsByRun.GetValueOrDefault(runId) ?? []);
            if (!parent.Complete)
            {
                subprocessEvidenceComplete = false;
                AddWarning(
                    warnings,
                    warningSet,
                    ProcessRunRecordWarningCode.MissingSubprocessParentMetadata);
            }

            if (!parent.ParentRunId.HasValue)
            {
                continue;
            }

            ValidateParentInRootFamily(
                runId,
                parent.ParentRunId.Value,
                rootFamilyIds);
            parentByRun.Add(runId, parent.ParentRunId.Value);
        }

        var descendantRunIds = ResolveReachableDescendants(
            primaryState.RunId,
            parentByRun);
        if (primaryState.RunId == primaryState.RootRunId &&
            descendantRunIds.Count != parentByRun.Count)
        {
            throw new InvalidOperationException(
                $"Root process run '{primaryState.RunId}' contains a disconnected or cyclic subprocess hierarchy.");
        }

        var descendantStates = await runtimeStateStore
            .LoadManyAsync(descendantRunIds, cancellationToken)
            .ConfigureAwait(false);
        ValidateStateBatchScope(descendantRunIds, descendantStates);
        var stateByRun = descendantStates.ToDictionary(state => state.RunId);
        var planIds = descendantStates
            .Select(state => state.PlanId)
            .Append(primaryState.PlanId)
            .Distinct()
            .OrderBy(planId => planId.Value)
            .ToArray();
        var plans = await planStore
            .LoadManyAsync(planIds, cancellationToken)
            .ConfigureAwait(false);
        ValidatePlanBatchScope(planIds, plans);
        var planById = plans.ToDictionary(plan => plan.Header.PlanId);

        var sources = new List<ProcessRunAssemblySource>(
            capacity: descendantRunIds.Count + 1)
        {
            CreateRunSource(
                primaryState,
                primaryState.RootRunId,
                planById.GetValueOrDefault(primaryState.PlanId),
                assignmentsByRun.GetValueOrDefault(primaryState.RunId) ?? [])
        };
        var allDiscoveredStatesLoaded = true;
        var runtimeStateEvidenceComplete = primaryRuntimeStateEvidenceComplete;
        foreach (var descendantRunId in descendantRunIds)
        {
            if (!stateByRun.TryGetValue(descendantRunId, out var state))
            {
                allDiscoveredStatesLoaded = false;
                runtimeStateEvidenceComplete = false;
                AddWarning(
                    warnings,
                    warningSet,
                    ProcessRunRecordWarningCode.MissingSubprocessEvidence);
                continue;
            }

            var source = CreateRunSource(
                state,
                primaryState.RootRunId,
                planById.GetValueOrDefault(state.PlanId),
                assignmentsByRun.GetValueOrDefault(state.RunId) ?? []);
            sources.Add(source);
            if (!source.IsTerminal)
            {
                runtimeStateEvidenceComplete = false;
                AddWarning(
                    warnings,
                    warningSet,
                    ProcessRunRecordWarningCode.SubprocessNonTerminal);
            }
        }

        return new ProcessRunSubtree(
            sources,
            [primaryState.RunId, .. descendantRunIds],
            descendantRunIds,
            allDiscoveredStatesLoaded,
            runtimeStateEvidenceComplete,
            subprocessEvidenceComplete,
            warnings);
    }

    private async Task<RootFamilyRead> ReadRootFamilyAsync(
        ProcessRunId rootRunId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ProcessRunId> result;
        try
        {
            result = await runHierarchyStore
                .FindDescendantRunIdsAsync(
                    rootRunId,
                    ProcessRunRecordPayloadLimits.MaximumSubprocessRunIds + 1,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new RootFamilyRead(
                [],
                false,
                [ProcessRunRecordWarningCode.SubprocessDiscoveryFailed]);
        }

        if (result.Count >
            ProcessRunRecordPayloadLimits.MaximumSubprocessRunIds + 1)
        {
            throw new InvalidOperationException(
                $"Runtime hierarchy store exceeded the requested root-family page size for '{rootRunId}'.");
        }

        var ordered = result
            .OrderBy(runId => runId.Value)
            .ToArray();
        if (ordered.Distinct().Count() != ordered.Length ||
            ordered.Any(runId => runId == rootRunId))
        {
            throw new InvalidOperationException(
                $"Runtime hierarchy store returned invalid root-family identifiers for '{rootRunId}'.");
        }

        var capped = ordered.Length >
            ProcessRunRecordPayloadLimits.MaximumSubprocessRunIds;
        return new RootFamilyRead(
            ordered
                .Take(ProcessRunRecordPayloadLimits.MaximumSubprocessRunIds)
                .ToArray(),
            !capped,
            capped
                ? [ProcessRunRecordWarningCode.SubprocessDepthLimitReached]
                : []);
    }

    private static ProcessRunAssemblySource CreateRunSource(
        ProcessRuntimeStateSnapshot state,
        ProcessRunId expectedRootRunId,
        ProcessInstancePlan? plan,
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments)
    {
        if (state.RootRunId != expectedRootRunId)
        {
            throw new InvalidOperationException(
                $"Runtime state '{state.RunId}' belongs to root '{state.RootRunId}' instead of '{expectedRootRunId}'.");
        }

        ValidatePlan(state, plan);
        var assignmentsByStep = ValidateAndIndexAssignments(state, assignments);
        return new ProcessRunAssemblySource(
            state,
            plan,
            assignments,
            assignmentsByStep,
            IsTerminalStatus(state.Status));
    }

    private static ParentLinkResolution ResolveParentLink(
        ProcessRunId runId,
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments)
    {
        ProcessRunId? parentRunId = null;
        var complete = assignments.Count > 0;
        foreach (var assignment in assignments)
        {
            if (assignment.RunId != runId)
            {
                throw new InvalidOperationException(
                    $"Step assignment '{assignment.StepInstanceId}' does not belong to subprocess run '{runId}'.");
            }

            if (!ProcessRuntimeLaunchVariables.TryReadParentRunId(
                    assignment.LaunchVariables,
                    out var candidateParentRunId))
            {
                complete = false;
                continue;
            }

            if (candidateParentRunId == runId)
            {
                throw new InvalidOperationException(
                    $"Process run '{runId}' cannot be its own subprocess parent.");
            }

            if (parentRunId.HasValue &&
                parentRunId.Value != candidateParentRunId)
            {
                throw new InvalidOperationException(
                    $"Process run '{runId}' has conflicting subprocess parent identifiers.");
            }

            parentRunId = candidateParentRunId;
        }

        return new ParentLinkResolution(parentRunId, complete && parentRunId.HasValue);
    }

    private static void ValidateParentInRootFamily(
        ProcessRunId runId,
        ProcessRunId? parentRunId,
        IReadOnlySet<ProcessRunId> rootFamilyIds)
    {
        if (parentRunId.HasValue && !rootFamilyIds.Contains(parentRunId.Value))
        {
            throw new InvalidOperationException(
                $"Subprocess run '{runId}' references parent '{parentRunId}' outside its persisted root family.");
        }
    }

    private static IReadOnlyList<ProcessRunId> ResolveReachableDescendants(
        ProcessRunId primaryRunId,
        IReadOnlyDictionary<ProcessRunId, ProcessRunId> parentByRun)
    {
        var visited = new HashSet<ProcessRunId> { primaryRunId };
        var frontier = new List<ProcessRunId> { primaryRunId };
        var result = new List<ProcessRunId>();
        while (frontier.Count > 0)
        {
            var frontierSet = frontier.ToHashSet();
            var nextFrontier = parentByRun
                .Where(pair =>
                    frontierSet.Contains(pair.Value) &&
                    !visited.Contains(pair.Key))
                .Select(pair => pair.Key)
                .OrderBy(runId => runId.Value)
                .ToArray();
            foreach (var runId in nextFrontier)
            {
                if (!visited.Add(runId))
                {
                    continue;
                }

                result.Add(runId);
            }

            frontier = [.. nextFrontier];
        }

        return result;
    }

    private static void ValidateAssignmentBatchScope(
        IReadOnlyList<ProcessRunId> requestedRunIds,
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments)
    {
        var requested = requestedRunIds.ToHashSet();
        if (assignments.Any(assignment => !requested.Contains(assignment.RunId)))
        {
            throw new InvalidOperationException(
                "Step-assignment batch returned evidence outside the requested root family.");
        }
    }

    private static void ValidateStateBatchScope(
        IReadOnlyList<ProcessRunId> requestedRunIds,
        IReadOnlyList<ProcessRuntimeStateSnapshot> states)
    {
        var requested = requestedRunIds.ToHashSet();
        if (states.Any(state => !requested.Contains(state.RunId)) ||
            states.Select(state => state.RunId).Distinct().Count() != states.Count)
        {
            throw new InvalidOperationException(
                "Runtime state batch returned duplicate or out-of-scope process runs.");
        }
    }

    private static void ValidatePlanBatchScope(
        IReadOnlyList<ProcessInstancePlanId> requestedPlanIds,
        IReadOnlyList<ProcessInstancePlan> plans)
    {
        var requested = requestedPlanIds.ToHashSet();
        if (plans.Any(plan => !requested.Contains(plan.Header.PlanId)) ||
            plans.Select(plan => plan.Header.PlanId).Distinct().Count() != plans.Count)
        {
            throw new InvalidOperationException(
                "Instance-plan batch returned duplicate or out-of-scope plans.");
        }
    }

    private async Task<ProcessRunRuntimeEventEvidence> ReadRuntimeEventsAsync(
        ProcessRunId rootRunId,
        IReadOnlySet<ProcessRunId> targetRunIds,
        ProcessRunRecordSummary summary,
        CancellationToken cancellationToken)
    {
        var rootSequence = 0L;
        var readCount = 0;
        var completed = false;
        var containsSeedEvent = false;
        DateTimeOffset? firstTargetEventAtUtc = null;
        DateTimeOffset? lastTargetEventAtUtc = null;
        var reworkCount = 0;
        var incidentCount = 0;
        var escalationCount = 0;
        var totalTargetEventCount = 0;
        var managerTargetEventCount = 0;
        var minuteAccumulators =
            new Dictionary<DateTimeOffset, RuntimeEventMinuteAccumulator>();
        var categoryAccumulators =
            new Dictionary<ProcessRunRuntimeEventCategory, RuntimeEventCategoryAccumulator>();
        while (readCount < MaximumRuntimeEventCount)
        {
            var take = Math.Min(RuntimeEventPageSize, MaximumRuntimeEventCount - readCount);
            var page = await eventReplayStore
                .ReadByRootRunAsync(
                    rootRunId,
                    rootSequence,
                    take,
                    cancellationToken)
                .ConfigureAwait(false);
            ValidateRuntimeEventPage(rootRunId, rootSequence, take, page);
            readCount += page.Count;
            foreach (var runtimeEvent in page)
            {
                if (!targetRunIds.Contains(runtimeEvent.Envelope.RunId))
                {
                    continue;
                }

                if (runtimeEvent.GlobalSequence > summary.SourceGlobalSequence &&
                    !IsExpectedDescendantCancellationClosure(runtimeEvent, summary))
                {
                    throw new InvalidOperationException(
                        $"Process run '{summary.Identity.RunId}' has newer subtree runtime events than its claimed record seed.");
                }

                var occurredAtUtc = runtimeEvent.Envelope.OccurredAtUtc.ToUniversalTime();
                if (!firstTargetEventAtUtc.HasValue ||
                    occurredAtUtc < firstTargetEventAtUtc.Value)
                {
                    firstTargetEventAtUtc = occurredAtUtc;
                }

                if (!lastTargetEventAtUtc.HasValue ||
                    occurredAtUtc > lastTargetEventAtUtc.Value)
                {
                    lastTargetEventAtUtc = occurredAtUtc;
                }

                containsSeedEvent |=
                    runtimeEvent.GlobalSequence == summary.SourceGlobalSequence &&
                    runtimeEvent.RootSequence == summary.SourceRootSequence;
                var category = ClassifyRuntimeEvent(runtimeEvent.Envelope.EventType);
                totalTargetEventCount++;
                if (category == ProcessRunRuntimeEventCategory.Manager)
                {
                    managerTargetEventCount++;
                }

                var minuteUtc = TruncateToMinute(occurredAtUtc);
                if (!minuteAccumulators.TryGetValue(minuteUtc, out var minuteAccumulator))
                {
                    minuteAccumulator = new RuntimeEventMinuteAccumulator(minuteUtc);
                    minuteAccumulators.Add(minuteUtc, minuteAccumulator);
                }

                minuteAccumulator.Add(
                    occurredAtUtc,
                    category == ProcessRunRuntimeEventCategory.Manager);
                if (!categoryAccumulators.TryGetValue(category, out var categoryAccumulator))
                {
                    categoryAccumulator = new RuntimeEventCategoryAccumulator(category);
                    categoryAccumulators.Add(category, categoryAccumulator);
                }

                categoryAccumulator.Add(occurredAtUtc);
                if (runtimeEvent.Envelope.EventType ==
                    ProcessRuntimeEventTypes.StepReworkRequested)
                {
                    reworkCount++;
                }
                else if (runtimeEvent.Envelope.EventType ==
                    ProcessRuntimeEventTypes.ManagerIncidentRaised)
                {
                    incidentCount++;
                }
                else if (runtimeEvent.Envelope.EventType ==
                    ProcessRuntimeEventTypes.ManagerLoopBudgetEscalated)
                {
                    escalationCount++;
                }
            }

            if (page.Count < take)
            {
                completed = true;
                break;
            }

            rootSequence = page[^1].RootSequence;
        }

        var minuteBucketsTruncated =
            minuteAccumulators.Count >
            ProcessRunRecordPayloadLimits.MaximumRuntimeEventMinuteBuckets;
        var minuteBuckets = minuteAccumulators.Values
            .OrderByDescending(accumulator => accumulator.MinuteUtc)
            .Take(ProcessRunRecordPayloadLimits.MaximumRuntimeEventMinuteBuckets)
            .OrderBy(accumulator => accumulator.MinuteUtc)
            .Select(accumulator => accumulator.ToAggregate())
            .ToArray();
        var categories = categoryAccumulators.Values
            .OrderBy(accumulator => accumulator.Category)
            .Select(accumulator => accumulator.ToAggregate())
            .ToArray();
        return new ProcessRunRuntimeEventEvidence(
            completed,
            containsSeedEvent,
            firstTargetEventAtUtc,
            lastTargetEventAtUtc,
            reworkCount,
            incidentCount,
            escalationCount)
        {
            TotalEventCount = totalTargetEventCount,
            ManagerEventCount = managerTargetEventCount,
            MinuteBuckets = minuteBuckets,
            Categories = categories,
            MinuteBucketsTruncated = minuteBucketsTruncated
        };
    }

    private static ProcessRunRuntimeEventCategory ClassifyRuntimeEvent(
        ProcessEventType eventType)
    {
        if (RunLifecycleEventTypes.Contains(eventType))
        {
            return ProcessRunRuntimeEventCategory.RunLifecycle;
        }

        if (StepEventTypes.Contains(eventType))
        {
            return ProcessRunRuntimeEventCategory.Step;
        }

        if (DispatchEventTypes.Contains(eventType))
        {
            return ProcessRunRuntimeEventCategory.Dispatch;
        }

        return ManagerEventTypes.Contains(eventType)
            ? ProcessRunRuntimeEventCategory.Manager
            : ProcessRunRuntimeEventCategory.Other;
    }

    private static DateTimeOffset TruncateToMinute(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            utc.Minute,
            0,
            TimeSpan.Zero);
    }

    private sealed class RuntimeEventMinuteAccumulator(DateTimeOffset minuteUtc)
    {
        private DateTimeOffset firstOccurredAtUtc;
        private DateTimeOffset lastOccurredAtUtc;

        public DateTimeOffset MinuteUtc { get; } = minuteUtc;

        public int EventCount { get; private set; }

        public int ManagerEventCount { get; private set; }

        public void Add(DateTimeOffset occurredAtUtc, bool isManagerEvent)
        {
            if (EventCount == 0 || occurredAtUtc < firstOccurredAtUtc)
            {
                firstOccurredAtUtc = occurredAtUtc;
            }

            if (EventCount == 0 || occurredAtUtc > lastOccurredAtUtc)
            {
                lastOccurredAtUtc = occurredAtUtc;
            }

            EventCount++;
            if (isManagerEvent)
            {
                ManagerEventCount++;
            }
        }

        public ProcessRunRuntimeEventMinuteBucket ToAggregate()
        {
            var durationMilliseconds = EventCount < 2
                ? 0
                : checked((long)Math.Max(
                    0,
                    (lastOccurredAtUtc - firstOccurredAtUtc).TotalMilliseconds));
            return new ProcessRunRuntimeEventMinuteBucket(
                MinuteUtc,
                EventCount,
                ManagerEventCount,
                durationMilliseconds);
        }
    }

    private sealed class RuntimeEventCategoryAccumulator(
        ProcessRunRuntimeEventCategory category)
    {
        private DateTimeOffset firstOccurredAtUtc;
        private DateTimeOffset lastOccurredAtUtc;

        public ProcessRunRuntimeEventCategory Category { get; } = category;

        public int EventCount { get; private set; }

        public void Add(DateTimeOffset occurredAtUtc)
        {
            if (EventCount == 0 || occurredAtUtc < firstOccurredAtUtc)
            {
                firstOccurredAtUtc = occurredAtUtc;
            }

            if (EventCount == 0 || occurredAtUtc > lastOccurredAtUtc)
            {
                lastOccurredAtUtc = occurredAtUtc;
            }

            EventCount++;
        }

        public ProcessRunRuntimeEventCategoryAggregate ToAggregate()
        {
            return new ProcessRunRuntimeEventCategoryAggregate(
                Category,
                EventCount,
                firstOccurredAtUtc,
                lastOccurredAtUtc);
        }
    }

    private static void ValidatePlan(
        ProcessRuntimeStateSnapshot state,
        ProcessInstancePlan? plan)
    {
        if (plan is null)
        {
            return;
        }

        if (plan.Header.PlanId != state.PlanId)
        {
            throw new InvalidOperationException(
                $"Loaded plan '{plan.Header.PlanId}' does not match runtime plan '{state.PlanId}'.");
        }

        var planSteps = plan.Steps.ToDictionary(step => step.StepInstanceId);
        foreach (var stateStep in state.Steps)
        {
            if (!planSteps.TryGetValue(stateStep.StepInstanceId, out var planStep))
            {
                throw new InvalidOperationException(
                    $"Runtime step '{stateStep.StepInstanceId}' is not present in plan '{state.PlanId}'.");
            }

            if (planStep.StepDefinitionId != stateStep.StepDefinitionId ||
                planStep.IsExecutable != stateStep.IsExecutable)
            {
                throw new InvalidOperationException(
                    $"Runtime step '{stateStep.StepInstanceId}' does not match its persisted plan definition.");
            }
        }
    }

    private static IReadOnlyDictionary<ProcessStepInstanceId, ProcessRuntimeStepAssignment>
        ValidateAndIndexAssignments(
            ProcessRuntimeStateSnapshot state,
            IReadOnlyList<ProcessRuntimeStepAssignment> assignments)
    {
        var stateStepIds = state.Steps
            .Select(step => step.StepInstanceId)
            .ToHashSet();
        var result = new Dictionary<ProcessStepInstanceId, ProcessRuntimeStepAssignment>();
        foreach (var assignment in assignments)
        {
            if (assignment.RunId != state.RunId ||
                assignment.PlanId != state.PlanId ||
                !stateStepIds.Contains(assignment.StepInstanceId))
            {
                throw new InvalidOperationException(
                    $"Step assignment '{assignment.StepInstanceId}' does not belong to runtime run '{state.RunId}'.");
            }

            if (!result.TryAdd(assignment.StepInstanceId, assignment))
            {
                throw new InvalidOperationException(
                    $"Process run '{state.RunId}' has duplicate assignments for step '{assignment.StepInstanceId}'.");
            }
        }

        return result;
    }

    private static void ValidateRuntimeEventPage(
        ProcessRunId rootRunId,
        long rootSequenceExclusive,
        int requestedTake,
        IReadOnlyList<ProcessStoredRuntimeEvent> page)
    {
        if (page.Count > requestedTake)
        {
            throw new InvalidOperationException(
                $"Runtime event replay for root '{rootRunId}' exceeded the requested page size.");
        }

        var previousSequence = rootSequenceExclusive;
        foreach (var runtimeEvent in page)
        {
            if (runtimeEvent.Envelope.RootRunId != rootRunId ||
                runtimeEvent.RootSequence <= previousSequence)
            {
                throw new InvalidOperationException(
                    $"Runtime event replay for root '{rootRunId}' returned an invalid sequence.");
            }

            previousSequence = runtimeEvent.RootSequence;
        }
    }

    private static void ValidateEvidenceScope(
        IReadOnlySet<ProcessRunId> targetRunIds,
        IReadOnlyList<ProcessRunAssemblySource> sources,
        IReadOnlyList<ProcessExecutionObservation> executionObservations,
        IReadOnlyList<ProcessRuntimeUsageObservation> usageObservations)
    {
        var stepIdsByRun = sources.ToDictionary(
            source => source.State.RunId,
            source => source.State.Steps
                .Select(step => step.StepInstanceId)
                .ToHashSet());
        foreach (var observation in executionObservations)
        {
            if (!targetRunIds.Contains(observation.RunId))
            {
                throw new InvalidOperationException(
                    $"Execution observation reader returned evidence outside the requested process subtree.");
            }

            if (stepIdsByRun.TryGetValue(observation.RunId, out var stepIds) &&
                !stepIds.Contains(observation.StepInstanceId))
            {
                throw new InvalidOperationException(
                    $"Execution observation '{observation.ExecutionRunId:D}' references an unknown runtime step.");
            }
        }

        foreach (var observation in usageObservations)
        {
            if (!targetRunIds.Contains(observation.RunId))
            {
                throw new InvalidOperationException(
                    $"Usage telemetry reader returned evidence outside the requested process subtree.");
            }

            if (observation.StepInstanceId is { } stepInstanceId &&
                stepIdsByRun.TryGetValue(observation.RunId, out var stepIds) &&
                !stepIds.Contains(stepInstanceId))
            {
                throw new InvalidOperationException(
                    $"Usage observation '{observation.UsageObservationId:D}' references an unknown runtime step.");
            }
        }
    }

    private static bool IsTerminalStatus(ProcessRuntimeStatus status)
        => status is ProcessRuntimeStatus.Completed or
            ProcessRuntimeStatus.Failed or
            ProcessRuntimeStatus.Cancelled or
            ProcessRuntimeStatus.Escalated;

    private static bool IsExpectedDescendantCancellationClosure(
        ProcessStoredRuntimeEvent runtimeEvent,
        ProcessRunRecordSummary summary)
    {
        return summary.Disposition == ProcessRunDisposition.Cancelled &&
               runtimeEvent.Envelope.RunId != summary.Identity.RunId &&
               runtimeEvent.Envelope.EventType == ProcessRuntimeEventTypes.ProcessRunCancelled;
    }

    private static void AddWarning(
        List<ProcessRunRecordWarningCode> warnings,
        HashSet<ProcessRunRecordWarningCode> warningSet,
        ProcessRunRecordWarningCode warning)
    {
        if (warnings.Count >= ProcessRunRecordPayloadLimits.MaximumCompletenessWarnings ||
            !warningSet.Add(warning))
        {
            return;
        }

        warnings.Add(warning);
    }

    private sealed record RootFamilyRead(
        IReadOnlyList<ProcessRunId> RunIds,
        bool Complete,
        IReadOnlyList<ProcessRunRecordWarningCode> Warnings);

    private sealed record ParentLinkResolution(
        ProcessRunId? ParentRunId,
        bool Complete);
}
