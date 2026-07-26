using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

internal sealed class ProcessRunFactsAggregator
{
    public ProcessRunAggregationResult Aggregate(ProcessRunAggregationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Subtree.Sources.Count == 0)
        {
            throw new InvalidOperationException(
                "Process run aggregation requires the primary runtime state.");
        }

        var warnings = new ProcessRunWarningCollector(input.Subtree.Warnings);
        var primarySource = input.Subtree.Sources[0];
        var identity = ResolveIdentity(
            input.SeededIdentity,
            primarySource,
            warnings);
        var artifacts = ResolveArtifacts(input.Subtree, warnings);
        var facts = BuildHardFacts(
            input.Subtree,
            input.ExecutionObservationRead.Items,
            input.UsageTelemetryRead.Items,
            input.RuntimeEvents,
            artifacts,
            warnings);
        var evidence = AssessEvidence(
            input.Subtree,
            input.ExecutionObservationRead,
            input.UsageTelemetryRead,
            input.RuntimeEvents,
            artifacts.LineageComplete,
            warnings);
        var metrics = BuildMetrics(
            input.EndedAtUtc,
            input.Subtree,
            input.ExecutionObservationRead.Items,
            input.UsageTelemetryRead.Items,
            input.RuntimeEvents,
            artifacts.TotalCount,
            warnings);
        var completeness = evidence.Missing == ProcessRunEvidenceSource.None &&
            warnings.Count == 0
                ? ProcessRunRecordCompleteness.Complete
                : ProcessRunRecordCompleteness.Partial;

        return new ProcessRunAggregationResult(
            identity,
            completeness,
            evidence.Available,
            evidence.Missing,
            warnings.Values,
            metrics,
            facts);
    }

    private static ProcessRunRecordIdentity ResolveIdentity(
        ProcessRunRecordIdentity seededIdentity,
        ProcessRunAssemblySource source,
        ProcessRunWarningCollector warnings)
    {
        var state = source.State;
        ProcessRunId? parentRunId = seededIdentity.ParentRunId;
        var assignmentParentRunIds = source.Assignments
            .Select(assignment =>
                ProcessRuntimeLaunchVariables.TryReadParentRunId(
                    assignment.LaunchVariables,
                    out var candidate)
                    ? candidate
                    : (ProcessRunId?)null)
            .Where(candidate => candidate.HasValue)
            .Select(candidate => candidate!.Value)
            .Distinct()
            .ToArray();
        if (assignmentParentRunIds.Length > 1)
        {
            throw new InvalidOperationException(
                $"Process run '{state.RunId}' has conflicting parent run identifiers.");
        }

        if (assignmentParentRunIds.Length == 1)
        {
            if (parentRunId.HasValue && parentRunId.Value != assignmentParentRunIds[0])
            {
                throw new InvalidOperationException(
                    $"Assignment parent run '{assignmentParentRunIds[0]}' does not match seeded parent '{parentRunId}'.");
            }

            parentRunId ??= assignmentParentRunIds[0];
        }

        Guid? projectId = seededIdentity.ProjectId;
        var assignmentProjectIds = new HashSet<Guid>();
        foreach (var assignment in source.Assignments)
        {
            if (!assignment.LaunchVariables.TryGetValue(
                    ProcessRuntimeLaunchVariables.ProjectId,
                    out var projectIdValue))
            {
                continue;
            }

            if (!Guid.TryParse(projectIdValue, out var parsedProjectId) ||
                parsedProjectId == Guid.Empty)
            {
                warnings.Add(ProcessRunRecordWarningCode.InvalidProjectId);
                continue;
            }

            assignmentProjectIds.Add(parsedProjectId);
        }

        if (assignmentProjectIds.Count > 1)
        {
            throw new InvalidOperationException(
                $"Process run '{state.RunId}' has conflicting project identifiers.");
        }

        if (assignmentProjectIds.Count == 1)
        {
            var assignmentProjectId = assignmentProjectIds.Single();
            if (projectId.HasValue && projectId.Value != assignmentProjectId)
            {
                throw new InvalidOperationException(
                    $"Assignment project '{assignmentProjectId:D}' does not match seeded project '{projectId:D}'.");
            }

            projectId ??= assignmentProjectId;
        }

        var definitionId = seededIdentity.DefinitionId;
        var definitionVersionId = seededIdentity.DefinitionVersionId;
        if (source.Plan is not null)
        {
            if (definitionId.HasValue &&
                definitionId.Value != source.Plan.Definition.DefinitionId)
            {
                throw new InvalidOperationException(
                    $"Plan definition '{source.Plan.Definition.DefinitionId}' does not match seeded definition '{definitionId}'.");
            }

            if (definitionVersionId.HasValue &&
                definitionVersionId.Value != source.Plan.Definition.VersionId)
            {
                throw new InvalidOperationException(
                    $"Plan definition version '{source.Plan.Definition.VersionId}' does not match seeded version '{definitionVersionId}'.");
            }

            definitionId ??= source.Plan.Definition.DefinitionId;
            definitionVersionId ??= source.Plan.Definition.VersionId;
        }

        return new ProcessRunRecordIdentity(
            state.RunId,
            state.RootRunId,
            parentRunId,
            state.PlanId,
            definitionId,
            definitionVersionId,
            projectId);
    }

    private static EvidenceAssessment AssessEvidence(
        ProcessRunSubtree subtree,
        ProcessExecutionObservationReadResult executionObservationRead,
        ProcessRuntimeUsageTelemetryReadResult usageTelemetryRead,
        ProcessRunRuntimeEventEvidence runtimeEvents,
        bool artifactLineageComplete,
        ProcessRunWarningCollector warnings)
    {
        var available = ProcessRunEvidenceSource.None;
        var missing = ProcessRunEvidenceSource.None;
        var executionObservations = executionObservationRead.Items;
        var usageObservations = usageTelemetryRead.Items;

        SetEvidence(
            ProcessRunEvidenceSource.RuntimeState,
            subtree.RuntimeStateEvidenceComplete,
            ref available,
            ref missing);

        var planEvidenceComplete = subtree.AllDiscoveredStatesLoaded &&
            subtree.Sources.All(source => source.Plan is not null);
        SetEvidence(
            ProcessRunEvidenceSource.InstancePlan,
            planEvidenceComplete,
            ref available,
            ref missing);
        if (!planEvidenceComplete)
        {
            warnings.Add(ProcessRunRecordWarningCode.MissingInstancePlan);
        }

        var assignmentEvidenceComplete = subtree.AllDiscoveredStatesLoaded &&
            subtree.Sources.All(source =>
                source.State.Steps.Count == source.AssignmentsByStep.Count);
        SetEvidence(
            ProcessRunEvidenceSource.StepAssignments,
            assignmentEvidenceComplete,
            ref available,
            ref missing);
        if (!assignmentEvidenceComplete)
        {
            warnings.Add(ProcessRunRecordWarningCode.MissingStepAssignments);
        }

        var attemptedSteps = subtree.Sources
            .SelectMany(source => source.State.Steps.Select(step => (source, step)))
            .Where(item => item.step.AttemptNumber > 0)
            .Where(item =>
                item.source.AssignmentsByStep.TryGetValue(
                    item.step.StepInstanceId,
                    out var assignment) &&
                ProcessLaunchExecutorKinds.CanResolveAsAgent(assignment.ExecutorKind))
            .Select(item => new RunStepKey(
                item.source.State.RunId,
                item.step.StepInstanceId))
            .ToHashSet();
        var observedSteps = executionObservations
            .Select(observation => new RunStepKey(
                observation.RunId,
                observation.StepInstanceId))
            .ToHashSet();
        var observationEvidenceComplete = executionObservationRead.IsComplete &&
            attemptedSteps.IsSubsetOf(observedSteps);
        SetEvidence(
            ProcessRunEvidenceSource.ExecutionObservations,
            observationEvidenceComplete,
            ref available,
            ref missing);
        if (!observationEvidenceComplete)
        {
            warnings.Add(ProcessRunRecordWarningCode.MissingExecutionObservations);
        }

        var knownSteps = subtree.Sources
            .SelectMany(source => source.State.Steps.Select(step =>
                new RunStepKey(source.State.RunId, step.StepInstanceId)))
            .ToHashSet();
        var hasUnallocatedUsage = usageObservations.Any(observation =>
            !observation.StepInstanceId.HasValue ||
            !knownSteps.Contains(new RunStepKey(
                observation.RunId,
                observation.StepInstanceId.Value)));
        var usageRequired = executionObservations.Count > 0;
        var usageEvidenceComplete = usageTelemetryRead.IsComplete &&
            !hasUnallocatedUsage &&
            (!usageRequired || usageObservations.Count > 0);
        SetEvidence(
            ProcessRunEvidenceSource.UsageTelemetry,
            usageEvidenceComplete,
            ref available,
            ref missing);
        if (!usageTelemetryRead.IsComplete ||
            usageRequired && usageObservations.Count == 0)
        {
            warnings.Add(ProcessRunRecordWarningCode.MissingUsageTelemetry);
        }

        if (hasUnallocatedUsage)
        {
            warnings.Add(ProcessRunRecordWarningCode.UnallocatedUsage);
        }

        var pricingEvidenceComplete = usageEvidenceComplete &&
            usageObservations.All(observation => observation.IsKnownUsage);
        SetEvidence(
            ProcessRunEvidenceSource.Pricing,
            pricingEvidenceComplete,
            ref available,
            ref missing);
        if (!pricingEvidenceComplete)
        {
            warnings.Add(ProcessRunRecordWarningCode.MissingPricing);
        }

        var runtimeEventsComplete = runtimeEvents.Completed &&
            runtimeEvents.ContainsSeedEvent;
        SetEvidence(
            ProcessRunEvidenceSource.RuntimeEvents,
            runtimeEventsComplete,
            ref available,
            ref missing);
        if (!runtimeEvents.Completed)
        {
            warnings.Add(ProcessRunRecordWarningCode.MissingRuntimeEvents);
        }

        if (runtimeEvents.Completed && !runtimeEvents.ContainsSeedEvent)
        {
            warnings.Add(ProcessRunRecordWarningCode.MissingRuntimeEvents);
        }

        SetEvidence(
            ProcessRunEvidenceSource.ArtifactLineage,
            artifactLineageComplete,
            ref available,
            ref missing);
        if (!artifactLineageComplete)
        {
            warnings.Add(ProcessRunRecordWarningCode.MissingArtifactLineage);
        }

        SetEvidence(
            ProcessRunEvidenceSource.Subprocesses,
            subtree.SubprocessEvidenceComplete,
            ref available,
            ref missing);

        return new EvidenceAssessment(available, missing);
    }

    private static void SetEvidence(
        ProcessRunEvidenceSource source,
        bool isComplete,
        ref ProcessRunEvidenceSource available,
        ref ProcessRunEvidenceSource missing)
    {
        if (isComplete)
        {
            available |= source;
            return;
        }

        missing |= source;
    }

    private static ArtifactAggregation ResolveArtifacts(
        ProcessRunSubtree subtree,
        ProcessRunWarningCollector warnings)
    {
        var allArtifactIds = new HashSet<ArtifactInstanceId>();
        var byStep = new Dictionary<RunStepKey, HashSet<ArtifactInstanceId>>();
        var lineageComplete = subtree.AllDiscoveredStatesLoaded;
        foreach (var source in subtree.Sources)
        {
            var knownArtifactSlots = new HashSet<ArtifactSlotId>();
            foreach (var result in source.State.AppliedResults)
            {
                var key = new RunStepKey(
                    source.State.RunId,
                    result.StepInstanceId);
                if (!byStep.TryGetValue(key, out var stepArtifactIds))
                {
                    stepArtifactIds = [];
                    byStep.Add(key, stepArtifactIds);
                }

                foreach (var artifact in result.ProducedArtifacts)
                {
                    allArtifactIds.Add(artifact.ArtifactId);
                    stepArtifactIds.Add(artifact.ArtifactId);
                    knownArtifactSlots.Add(artifact.SlotId);
                }
            }

            if (source.Plan is not null)
            {
                foreach (var entry in source.Plan.ArtifactPlan.InitialLedgerEntries)
                {
                    allArtifactIds.Add(entry.ArtifactId);
                    knownArtifactSlots.Add(entry.SlotId);
                }
            }

            foreach (var receipt in source.State.ConnectedInputArtifacts)
            {
                if (receipt.ArtifactId is not { } artifactId)
                {
                    continue;
                }

                allArtifactIds.Add(artifactId);
                knownArtifactSlots.Add(receipt.RequiredSlotId);
            }

            lineageComplete &= source.State.AvailableArtifactSlots
                .IsSubsetOf(knownArtifactSlots);
        }

        var boundedIds = BoundCollection(
            allArtifactIds.OrderBy(artifactId => artifactId.Value),
            ProcessRunRecordPayloadLimits.MaximumArtifactIds,
            warnings,
            ProcessRunRecordWarningCode.ArtifactIdsTruncated);
        return new ArtifactAggregation(
            boundedIds,
            byStep.ToDictionary(pair => pair.Key, pair => pair.Value.Count),
            allArtifactIds.Count,
            lineageComplete);
    }

    private static ProcessRunHardFacts BuildHardFacts(
        ProcessRunSubtree subtree,
        IReadOnlyList<ProcessExecutionObservation> executionObservations,
        IReadOnlyList<ProcessRuntimeUsageObservation> usageObservations,
        ProcessRunRuntimeEventEvidence runtimeEvents,
        ArtifactAggregation artifacts,
        ProcessRunWarningCollector warnings)
    {
        var observationsByStep = executionObservations
            .GroupBy(observation => new RunStepKey(
                observation.RunId,
                observation.StepInstanceId))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var usageByStep = usageObservations
            .Where(observation => observation.StepInstanceId.HasValue)
            .GroupBy(observation => new RunStepKey(
                observation.RunId,
                observation.StepInstanceId!.Value))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var participants = new Dictionary<string, ProcessRunParticipantId>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var source in subtree.Sources)
        {
            foreach (var assignment in source.Assignments)
            {
                TryAddParticipant(assignment.ExecutorId, participants, warnings);
            }
        }

        foreach (var observation in executionObservations)
        {
            TryAddParticipant(
                observation.AgentId.ToString("D"),
                participants,
                warnings);
        }

        var workflowIds = subtree.Sources
            .SelectMany(source => source.Assignments)
            .Select(assignment => assignment.WorkflowBinding?.WorkflowId.Value)
            .Where(workflowId => workflowId.HasValue)
            .Select(workflowId => workflowId!.Value)
            .ToHashSet();
        var executionRunIds = executionObservations
            .Select(observation => observation.ExecutionRunId)
            .ToHashSet();
        var stepFacts = new List<ProcessRunStepFact>(
            ProcessRunRecordPayloadLimits.MaximumSteps);
        var stepCount = 0;
        foreach (var source in subtree.Sources)
        {
            var planStepsById = source.Plan?.Steps.ToDictionary(
                step => step.StepInstanceId) ??
                new Dictionary<ProcessStepInstanceId, StepInstancePlan>();
            var planStepOrder = source.Plan?.Steps
                .Select((step, index) => (step.StepInstanceId, index))
                .ToDictionary(pair => pair.StepInstanceId, pair => pair.index) ??
                new Dictionary<ProcessStepInstanceId, int>();
            var claimsByStep = source.State.Claims
                .GroupBy(claim => claim.StepInstanceId)
                .ToDictionary(group => group.Key, group => group.ToArray());
            foreach (var step in source.State.Steps
                .OrderBy(item => planStepOrder.GetValueOrDefault(
                    item.StepInstanceId,
                    int.MaxValue))
                .ThenBy(item => item.StepInstanceId.Value))
            {
                stepCount++;
                if (stepFacts.Count >= ProcessRunRecordPayloadLimits.MaximumSteps)
                {
                    continue;
                }

                var key = new RunStepKey(
                    source.State.RunId,
                    step.StepInstanceId);
                source.AssignmentsByStep.TryGetValue(
                    step.StepInstanceId,
                    out var assignment);
                observationsByStep.TryGetValue(key, out var stepObservations);
                usageByStep.TryGetValue(key, out var stepUsage);
                claimsByStep.TryGetValue(step.StepInstanceId, out var stepClaims);
                stepObservations ??= [];
                stepUsage ??= [];
                stepClaims ??= [];

                var participantId = TryAddParticipant(
                    assignment?.ExecutorId,
                    participants,
                    warnings);
                if (!participantId.HasValue && stepObservations.Length > 0)
                {
                    var latestObservation = stepObservations
                        .OrderByDescending(observation => observation.UpdatedAtUtc)
                        .ThenByDescending(observation => observation.ExecutionRunId)
                        .First();
                    participantId = TryAddParticipant(
                        latestObservation.AgentId.ToString("D"),
                        participants,
                        warnings);
                }

                var workflowId = assignment?.WorkflowBinding?.WorkflowId.Value;
                var startedAtUtc = stepObservations
                    .Where(observation => observation.StartedAtUtc.HasValue)
                    .Select(observation => observation.StartedAtUtc!.Value)
                    .Concat(stepClaims.Select(claim => claim.CreatedAtUtc))
                    .Cast<DateTimeOffset?>()
                    .Min();
                var endedAtUtc = stepObservations
                    .Where(observation => observation.CompletedAtUtc.HasValue)
                    .Select(observation => observation.CompletedAtUtc!.Value)
                    .Cast<DateTimeOffset?>()
                    .Max();
                var duration = CalculateDuration(
                    startedAtUtc,
                    endedAtUtc,
                    warnings,
                    ProcessRunRecordWarningCode.MissingStepTiming);
                var stepKey = ResolveStepKey(
                    step,
                    planStepsById,
                    assignment,
                    warnings);
                var attemptCount = ResolveAttemptCount(
                    step,
                    stepClaims,
                    stepObservations);
                var stepExecutionRunIds = BoundCollection(
                    stepObservations
                        .Select(observation => observation.ExecutionRunId)
                        .Distinct()
                        .OrderBy(executionRunId => executionRunId),
                    ProcessRunRecordPayloadLimits.MaximumExecutionRunIds,
                    warnings,
                    ProcessRunRecordWarningCode.ExecutionRunIdsTruncated);

                stepFacts.Add(new ProcessRunStepFact(
                    source.State.RunId,
                    step.StepInstanceId,
                    step.StepDefinitionId,
                    stepKey,
                    MapStepOutcome(step.Status),
                    attemptCount,
                    participantId,
                    workflowId,
                    BoundCollection(
                        step.DependencyStepIds.OrderBy(stepId => stepId.Value),
                        ProcessRunRecordPayloadLimits.MaximumStepDependencyIds,
                        warnings,
                        ProcessRunRecordWarningCode.StepDependenciesTruncated),
                    stepExecutionRunIds,
                    startedAtUtc,
                    endedAtUtc,
                    duration,
                    SumLong(stepUsage, usage => usage.InputTokens),
                    SumLong(stepUsage, usage => usage.CachedInputTokens),
                    SumLong(stepUsage, usage => usage.OutputTokens),
                    SumLong(stepUsage, usage => usage.ReasoningTokens),
                    SumLong(stepUsage, usage => usage.TotalTokens),
                    SumCost(stepUsage, usage => usage.EstimatedCostUsd),
                    SumCost(stepUsage, usage => usage.ActualCostUsd),
                    stepUsage.Sum(usage => Math.Max(0, usage.ToolCallCount)),
                    artifacts.CountByStep.GetValueOrDefault(key)));
            }
        }

        if (stepCount > ProcessRunRecordPayloadLimits.MaximumSteps)
        {
            warnings.Add(ProcessRunRecordWarningCode.StepFactsTruncated);
        }

        if (runtimeEvents.MinuteBucketsTruncated)
        {
            warnings.Add(ProcessRunRecordWarningCode.RuntimeEventMinuteBucketsTruncated);
        }

        return new ProcessRunHardFacts(
            stepFacts,
            BoundCollection(
                participants.Values.OrderBy(
                    participant => participant.Value,
                    StringComparer.OrdinalIgnoreCase),
                ProcessRunRecordPayloadLimits.MaximumParticipants,
                warnings,
                ProcessRunRecordWarningCode.ParticipantIdsTruncated),
            BoundCollection(
                workflowIds.OrderBy(workflowId => workflowId),
                ProcessRunRecordPayloadLimits.MaximumWorkflowIds,
                warnings,
                ProcessRunRecordWarningCode.WorkflowIdsTruncated),
            BoundCollection(
                subtree.DescendantRunIds,
                ProcessRunRecordPayloadLimits.MaximumSubprocessRunIds,
                warnings,
                ProcessRunRecordWarningCode.SubprocessRunIdsTruncated),
            BoundCollection(
                executionRunIds.OrderBy(executionRunId => executionRunId),
                ProcessRunRecordPayloadLimits.MaximumExecutionRunIds,
                warnings,
                ProcessRunRecordWarningCode.ExecutionRunIdsTruncated),
            artifacts.ArtifactIds)
        {
            TotalRuntimeEventCount = runtimeEvents.TotalEventCount,
            ManagerRuntimeEventCount = runtimeEvents.ManagerEventCount,
            RuntimeEventMinuteBuckets = runtimeEvents.MinuteBuckets,
            RuntimeEventCategories = runtimeEvents.Categories
        };
    }

    private static ProcessRunRecordMetrics BuildMetrics(
        DateTimeOffset endedAtUtc,
        ProcessRunSubtree subtree,
        IReadOnlyList<ProcessExecutionObservation> executionObservations,
        IReadOnlyList<ProcessRuntimeUsageObservation> usageObservations,
        ProcessRunRuntimeEventEvidence runtimeEvents,
        int artifactCount,
        ProcessRunWarningCollector warnings)
    {
        var observationsByStep = executionObservations
            .GroupBy(observation => new RunStepKey(
                observation.RunId,
                observation.StepInstanceId))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var repetitionCount = 0;
        foreach (var source in subtree.Sources)
        {
            var claimsByStep = source.State.Claims
                .GroupBy(claim => claim.StepInstanceId)
                .ToDictionary(group => group.Key, group => group.ToArray());
            foreach (var step in source.State.Steps)
            {
                observationsByStep.TryGetValue(
                    new RunStepKey(source.State.RunId, step.StepInstanceId),
                    out var stepObservations);
                claimsByStep.TryGetValue(step.StepInstanceId, out var stepClaims);
                repetitionCount += Math.Max(
                    0,
                    ResolveAttemptCount(
                        step,
                        stepClaims ?? [],
                        stepObservations ?? []) - 1);
            }
        }

        var firstStepActivityAtUtc = executionObservations
            .Where(observation => observation.StartedAtUtc.HasValue)
            .Select(observation => observation.StartedAtUtc!.Value)
            .Concat(
                subtree.Sources
                    .SelectMany(source => source.State.Claims)
                    .Select(claim => claim.CreatedAtUtc))
            .Cast<DateTimeOffset?>()
            .Min();
        var startedAtUtc = runtimeEvents.FirstTargetEventAtUtc ??
            firstStepActivityAtUtc;
        var duration = CalculateDuration(
            startedAtUtc,
            endedAtUtc,
            warnings,
            ProcessRunRecordWarningCode.InvalidRunTiming);
        var allSteps = subtree.Sources
            .SelectMany(source => source.State.Steps)
            .ToArray();
        return new ProcessRunRecordMetrics(
            startedAtUtc,
            endedAtUtc,
            duration,
            allSteps.Length,
            allSteps.Count(step => step.IsExecutable),
            allSteps.Count(step => step.Status == ProcessRuntimeStepStatus.Completed),
            allSteps.Count(step => step.Status == ProcessRuntimeStepStatus.Failed),
            allSteps.Count(step => step.Status == ProcessRuntimeStepStatus.Cancelled),
            repetitionCount,
            executionObservations
                .Select(observation => observation.ExecutionRunId)
                .Distinct()
                .Count(),
            runtimeEvents.ReworkCount,
            runtimeEvents.IncidentCount,
            runtimeEvents.EscalationCount,
            SumLong(usageObservations, usage => usage.InputTokens),
            SumLong(usageObservations, usage => usage.CachedInputTokens),
            SumLong(usageObservations, usage => usage.OutputTokens),
            SumLong(usageObservations, usage => usage.ReasoningTokens),
            SumLong(usageObservations, usage => usage.TotalTokens),
            SumCost(usageObservations, usage => usage.EstimatedCostUsd),
            SumCost(usageObservations, usage => usage.ActualCostUsd),
            usageObservations.Sum(usage => Math.Max(0, usage.ToolCallCount)),
            artifactCount,
            subtree.DescendantRunIds.Count);
    }

    private static string ResolveStepKey(
        ProcessRuntimeStepState step,
        IReadOnlyDictionary<ProcessStepInstanceId, StepInstancePlan> planStepsById,
        ProcessRuntimeStepAssignment? assignment,
        ProcessRunWarningCollector warnings)
    {
        var value = planStepsById.TryGetValue(step.StepInstanceId, out var planStep)
            ? planStep.StepKey
            : assignment?.StepKey;
        if (string.IsNullOrWhiteSpace(value))
        {
            warnings.Add(ProcessRunRecordWarningCode.MissingStepKey);
            value = step.StepDefinitionId.ToString();
        }

        return BoundText(
            value,
            ProcessRunRecordPayloadLimits.MaximumStepKeyLength,
            warnings,
            ProcessRunRecordWarningCode.StepKeyTruncated);
    }

    private static ProcessRunParticipantId? TryAddParticipant(
        string? value,
        IDictionary<string, ProcessRunParticipantId> participants,
        ProcessRunWarningCollector warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > 256)
        {
            warnings.Add(ProcessRunRecordWarningCode.ParticipantIdsTruncated);
            return null;
        }

        var participantId = new ProcessRunParticipantId(normalized);
        participants.TryAdd(normalized, participantId);
        return participantId;
    }

    private static int ResolveAttemptCount(
        ProcessRuntimeStepState step,
        IReadOnlyList<DispatchClaimState> claims,
        IReadOnlyList<ProcessExecutionObservation> observations)
        => Math.Max(
            Math.Max(0, step.AttemptNumber),
            Math.Max(
                claims.Select(claim => claim.AttemptNumber).DefaultIfEmpty(0).Max(),
                observations.Count));

    private static ProcessRunStepOutcome MapStepOutcome(ProcessRuntimeStepStatus status)
        => status switch
        {
            ProcessRuntimeStepStatus.Planned or
                ProcessRuntimeStepStatus.Pending or
                ProcessRuntimeStepStatus.Ready => ProcessRunStepOutcome.Pending,
            ProcessRuntimeStepStatus.Claimed or
                ProcessRuntimeStepStatus.Running => ProcessRunStepOutcome.Running,
            ProcessRuntimeStepStatus.Waiting or
                ProcessRuntimeStepStatus.WaitingApproval => ProcessRunStepOutcome.Waiting,
            ProcessRuntimeStepStatus.Blocked => ProcessRunStepOutcome.Blocked,
            ProcessRuntimeStepStatus.Completed => ProcessRunStepOutcome.Completed,
            ProcessRuntimeStepStatus.Failed => ProcessRunStepOutcome.Failed,
            ProcessRuntimeStepStatus.Cancelled => ProcessRunStepOutcome.Cancelled,
            ProcessRuntimeStepStatus.Skipped => ProcessRunStepOutcome.Skipped,
            _ => ProcessRunStepOutcome.Unknown
        };

    private static long SumLong<T>(
        IEnumerable<T> values,
        Func<T, int> selector)
        => values.Sum(value => (long)Math.Max(0, selector(value)));

    private static decimal SumCost<T>(
        IEnumerable<T> values,
        Func<T, decimal> selector)
        => decimal.Round(
            values.Sum(value => Math.Max(0m, selector(value))),
            6,
            MidpointRounding.AwayFromZero);

    private static long? CalculateDuration(
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? endedAtUtc,
        ProcessRunWarningCollector warnings,
        ProcessRunRecordWarningCode invalidTimingWarning)
    {
        if (!startedAtUtc.HasValue || !endedAtUtc.HasValue)
        {
            return null;
        }

        if (endedAtUtc.Value < startedAtUtc.Value)
        {
            warnings.Add(invalidTimingWarning);
            return null;
        }

        return (endedAtUtc.Value - startedAtUtc.Value).Ticks /
            TimeSpan.TicksPerMillisecond;
    }

    private static string BoundText(
        string? value,
        int maximumLength,
        ProcessRunWarningCollector warnings,
        ProcessRunRecordWarningCode truncationWarning)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length <= maximumLength)
        {
            return normalized;
        }

        var length = maximumLength;
        if (char.IsHighSurrogate(normalized[length - 1]) &&
            char.IsLowSurrogate(normalized[length]))
        {
            length--;
        }

        warnings.Add(truncationWarning);
        return normalized[..length];
    }

    private static IReadOnlyList<T> BoundCollection<T>(
        IEnumerable<T> values,
        int maximumCount,
        ProcessRunWarningCollector warnings,
        ProcessRunRecordWarningCode truncationWarning)
    {
        var bounded = values
            .Take(maximumCount + 1)
            .ToArray();
        if (bounded.Length <= maximumCount)
        {
            return bounded;
        }

        warnings.Add(truncationWarning);
        return bounded[..maximumCount];
    }

    private readonly record struct RunStepKey(
        ProcessRunId RunId,
        ProcessStepInstanceId StepInstanceId);

    private sealed record ArtifactAggregation(
        IReadOnlyList<ArtifactInstanceId> ArtifactIds,
        IReadOnlyDictionary<RunStepKey, int> CountByStep,
        int TotalCount,
        bool LineageComplete);

    private sealed record EvidenceAssessment(
        ProcessRunEvidenceSource Available,
        ProcessRunEvidenceSource Missing);
}

internal sealed record ProcessRunAssemblySource(
    ProcessRuntimeStateSnapshot State,
    ProcessInstancePlan? Plan,
    IReadOnlyList<ProcessRuntimeStepAssignment> Assignments,
    IReadOnlyDictionary<ProcessStepInstanceId, ProcessRuntimeStepAssignment> AssignmentsByStep,
    bool IsTerminal);

internal sealed record ProcessRunSubtree(
    IReadOnlyList<ProcessRunAssemblySource> Sources,
    IReadOnlyList<ProcessRunId> AllRunIds,
    IReadOnlyList<ProcessRunId> DescendantRunIds,
    bool AllDiscoveredStatesLoaded,
    bool RuntimeStateEvidenceComplete,
    bool SubprocessEvidenceComplete,
    IReadOnlyList<ProcessRunRecordWarningCode> Warnings);

internal sealed record ProcessRunRuntimeEventEvidence(
    bool Completed,
    bool ContainsSeedEvent,
    DateTimeOffset? FirstTargetEventAtUtc,
    DateTimeOffset? LastTargetEventAtUtc,
    int ReworkCount,
    int IncidentCount,
    int EscalationCount)
{
    public int TotalEventCount { get; init; }

    public int ManagerEventCount { get; init; }

    public IReadOnlyList<ProcessRunRuntimeEventMinuteBucket> MinuteBuckets { get; init; } = [];

    public IReadOnlyList<ProcessRunRuntimeEventCategoryAggregate> Categories { get; init; } = [];

    public bool MinuteBucketsTruncated { get; init; }
}

internal sealed record ProcessRunAggregationInput(
    ProcessRunRecordIdentity SeededIdentity,
    DateTimeOffset EndedAtUtc,
    ProcessRunSubtree Subtree,
    ProcessExecutionObservationReadResult ExecutionObservationRead,
    ProcessRuntimeUsageTelemetryReadResult UsageTelemetryRead,
    ProcessRunRuntimeEventEvidence RuntimeEvents);

internal sealed record ProcessRunAggregationResult(
    ProcessRunRecordIdentity Identity,
    ProcessRunRecordCompleteness Completeness,
    ProcessRunEvidenceSource AvailableEvidenceSources,
    ProcessRunEvidenceSource MissingEvidenceSources,
    IReadOnlyList<ProcessRunRecordWarningCode> Warnings,
    ProcessRunRecordMetrics Metrics,
    ProcessRunHardFacts Facts);

internal sealed class ProcessRunWarningCollector
{
    private readonly List<ProcessRunRecordWarningCode> warnings = [];
    private readonly HashSet<ProcessRunRecordWarningCode> warningSet = [];

    public ProcessRunWarningCollector(
        IEnumerable<ProcessRunRecordWarningCode> initialWarnings)
    {
        foreach (var warning in initialWarnings)
        {
            Add(warning);
        }
    }

    public int Count => warnings.Count;

    public IReadOnlyList<ProcessRunRecordWarningCode> Values => warnings;

    public void Add(ProcessRunRecordWarningCode warning)
    {
        if (warnings.Count >= ProcessRunRecordPayloadLimits.MaximumCompletenessWarnings ||
            !warningSet.Add(warning))
        {
            return;
        }

        warnings.Add(warning);
    }
}
