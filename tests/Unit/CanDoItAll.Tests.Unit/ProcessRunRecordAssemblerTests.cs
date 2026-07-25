using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRunRecordAssemblerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ProcessRuntimeStatus.Completed, ProcessRunDisposition.Succeeded)]
    [InlineData(ProcessRuntimeStatus.Failed, ProcessRunDisposition.Failed)]
    [InlineData(ProcessRuntimeStatus.Cancelled, ProcessRunDisposition.Cancelled)]
    [InlineData(ProcessRuntimeStatus.Escalated, ProcessRunDisposition.Escalated)]
    public async Task AssembleAsync_complete_terminal_subtree_rolls_up_hard_facts_in_batches(
        ProcessRuntimeStatus runtimeStatus,
        ProcessRunDisposition disposition)
    {
        var scenario = CompleteScenario.Create(runtimeStatus, disposition);
        var harness = scenario.CreateHarness();

        var result = await harness.Assembler.AssembleAsync(
            scenario.CreateClaim(),
            scenario.CreateRecord());

        Assert.Equal(ProcessRunRecordCompleteness.Complete, result.Completeness);
        Assert.Equal(ProcessRunEvidenceSource.All, result.AvailableEvidenceSources);
        Assert.Equal(ProcessRunEvidenceSource.None, result.MissingEvidenceSources);
        Assert.Empty(result.CompletenessWarnings);
        Assert.Equal(3, result.Metrics.TotalStepCount);
        Assert.Equal(3, result.Metrics.CompletedStepCount);
        Assert.Equal(1, result.Metrics.RepetitionCount);
        Assert.Equal(3, result.Metrics.ExecutionCount);
        Assert.Equal(1, result.Metrics.ReworkCount);
        Assert.Equal(1, result.Metrics.IncidentCount);
        Assert.Equal(1, result.Metrics.EscalationCount);
        Assert.Equal(30, result.Metrics.InputTokenCount);
        Assert.Equal(6, result.Metrics.CachedInputTokenCount);
        Assert.Equal(15, result.Metrics.OutputTokenCount);
        Assert.Equal(3, result.Metrics.ReasoningTokenCount);
        Assert.Equal(54, result.Metrics.TotalTokenCount);
        Assert.Equal(0.36m, result.Metrics.EstimatedCost);
        Assert.Equal(0.33m, result.Metrics.ActualCost);
        Assert.Equal(3, result.Metrics.ToolCallCount);
        Assert.Equal(1, result.Metrics.ArtifactCount);
        Assert.Equal(2, result.Metrics.SubprocessCount);
        Assert.Equal(3, result.Facts.Steps.Count);
        Assert.Equal(
            new HashSet<ProcessRunId>
            {
                scenario.RootRunId,
                scenario.ChildRunId,
                scenario.GrandchildRunId
            },
            result.Facts.Steps.Select(step => step.OwningRunId).ToHashSet());
        Assert.Equal(
            new HashSet<ProcessRunId>
            {
                scenario.ChildRunId,
                scenario.GrandchildRunId
            },
            result.Facts.SubprocessRunIds.ToHashSet());
        Assert.Equal(3, result.Facts.WorkflowIds.Count);
        Assert.Equal(3, result.Facts.ExecutionRunIds.Count);
        Assert.Contains(scenario.ArtifactId, result.Facts.ArtifactIds);
        Assert.Equal(scenario.ProjectId, result.Identity.ProjectId);
        Assert.Equal(
            scenario.Plans.Single(plan =>
                plan.Header.PlanId == scenario.RootPlanId).Definition.DefinitionId,
            result.Identity.DefinitionId);

        Assert.Equal(1, harness.HierarchyStore.CallCount);
        Assert.Equal(1, harness.AssignmentStore.LoadByRunsCallCount);
        Assert.Equal(1, harness.StateStore.LoadManyCallCount);
        Assert.Equal(1, harness.PlanStore.LoadManyCallCount);
        Assert.Equal(1, harness.ObservationReader.CallCount);
        Assert.Equal(1, harness.UsageReader.CallCount);
        var observationQuery = Assert.IsType<ProcessExecutionObservationQuery>(
            harness.ObservationReader.LastQuery);
        Assert.Equal(
            ProcessExecutionObservationDetailLevel.Summary,
            observationQuery.DetailLevel);
        Assert.Equal(
            new HashSet<ProcessRunId>
            {
                scenario.RootRunId,
                scenario.ChildRunId,
                scenario.GrandchildRunId
            },
            observationQuery.RunIds.ToHashSet());
    }

    [Fact]
    public async Task AssembleAsync_captures_exact_bounded_runtime_event_aggregates_without_event_details()
    {
        var scenario = CompleteScenario.Create(
            ProcessRuntimeStatus.Completed,
            ProcessRunDisposition.Succeeded);
        scenario.SourceGlobalSequence = 7;
        scenario.SourceRootSequence = 7;
        scenario.RuntimeEvents.Clear();
        scenario.RuntimeEvents.AddRange(
        [
            CreateStoredEvent(
                1,
                1,
                scenario.RootRunId,
                scenario.RootRunId,
                ProcessRuntimeEventTypes.ProcessRunActivated,
                scenario.EndedAtUtc.AddMinutes(-4)),
            CreateStoredEvent(
                2,
                2,
                scenario.RootRunId,
                scenario.RootRunId,
                ProcessRuntimeEventTypes.StepRunning,
                scenario.EndedAtUtc.AddMinutes(-3).AddSeconds(10)),
            CreateStoredEvent(
                3,
                3,
                scenario.RootRunId,
                scenario.RootRunId,
                ProcessRuntimeEventTypes.StepCompleted,
                scenario.EndedAtUtc.AddMinutes(-3).AddSeconds(50)),
            CreateStoredEvent(
                4,
                4,
                scenario.RootRunId,
                scenario.ChildRunId,
                ProcessRuntimeEventTypes.DispatchClaimCreated,
                scenario.EndedAtUtc.AddMinutes(-2).AddSeconds(15)),
            CreateStoredEvent(
                5,
                5,
                scenario.RootRunId,
                scenario.ChildRunId,
                ProcessRuntimeEventTypes.ManagerIncidentRaised,
                scenario.EndedAtUtc.AddMinutes(-2).AddSeconds(45)),
            CreateStoredEvent(
                6,
                6,
                scenario.RootRunId,
                scenario.GrandchildRunId,
                new ProcessEventType("FutureRuntimeSignal"),
                scenario.EndedAtUtc.AddMinutes(-1).AddSeconds(30)),
            CreateStoredEvent(
                7,
                7,
                scenario.RootRunId,
                scenario.RootRunId,
                ProcessRuntimeEventTypes.ProcessRunCompleted,
                scenario.EndedAtUtc)
        ]);
        var harness = scenario.CreateHarness();

        var result = await harness.Assembler.AssembleAsync(
            scenario.CreateClaim(),
            scenario.CreateRecord());

        Assert.Equal(7, result.Facts.TotalRuntimeEventCount);
        Assert.Equal(1, result.Facts.ManagerRuntimeEventCount);
        Assert.Equal(5, result.Facts.RuntimeEventMinuteBuckets.Count);
        var stepBucket = Assert.Single(
            result.Facts.RuntimeEventMinuteBuckets,
            bucket => bucket.MinuteUtc == scenario.EndedAtUtc.AddMinutes(-3));
        Assert.Equal(2, stepBucket.EventCount);
        Assert.Equal(0, stepBucket.ManagerEventCount);
        Assert.Equal(40_000, stepBucket.DurationMilliseconds);
        var mixedBucket = Assert.Single(
            result.Facts.RuntimeEventMinuteBuckets,
            bucket => bucket.MinuteUtc == scenario.EndedAtUtc.AddMinutes(-2));
        Assert.Equal(2, mixedBucket.EventCount);
        Assert.Equal(1, mixedBucket.ManagerEventCount);
        Assert.Equal(30_000, mixedBucket.DurationMilliseconds);

        var categoryCounts = result.Facts.RuntimeEventCategories
            .ToDictionary(category => category.Category, category => category.EventCount);
        Assert.Equal(2, categoryCounts[ProcessRunRuntimeEventCategory.RunLifecycle]);
        Assert.Equal(2, categoryCounts[ProcessRunRuntimeEventCategory.Step]);
        Assert.Equal(1, categoryCounts[ProcessRunRuntimeEventCategory.Dispatch]);
        Assert.Equal(1, categoryCounts[ProcessRunRuntimeEventCategory.Manager]);
        Assert.Equal(1, categoryCounts[ProcessRunRuntimeEventCategory.Other]);

        var factsJson = JsonSerializer.Serialize(result.Facts);
        Assert.DoesNotContain("sha256:event", factsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("unit-test", factsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("FutureRuntimeSignal", factsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssembleAsync_child_record_rolls_up_only_its_reachable_descendants()
    {
        var scenario = CompleteScenario.Create(
            ProcessRuntimeStatus.Completed,
            ProcessRunDisposition.Succeeded);
        const long childSourceSequence = 5;
        scenario.RuntimeEvents.Add(CreateStoredEvent(
            childSourceSequence,
            childSourceSequence,
            scenario.RootRunId,
            scenario.ChildRunId,
            ProcessRuntimeEventTypes.ProcessRunCompleted,
            scenario.EndedAtUtc.AddMinutes(1)));
        var harness = scenario.CreateHarness();

        var result = await harness.Assembler.AssembleAsync(
            scenario.CreateClaim(
                scenario.ChildRunId,
                childSourceSequence),
            scenario.CreateRecord(
                scenario.ChildRunId,
                scenario.ChildPlanId,
                scenario.RootRunId,
                childSourceSequence,
                childSourceSequence,
                scenario.EndedAtUtc.AddMinutes(1)));

        Assert.Equal(ProcessRunRecordCompleteness.Complete, result.Completeness);
        Assert.Equal(2, result.Metrics.TotalStepCount);
        Assert.Equal(
            new HashSet<ProcessRunId>
            {
                scenario.ChildRunId,
                scenario.GrandchildRunId
            },
            result.Facts.Steps.Select(step => step.OwningRunId).ToHashSet());
        Assert.Equal(
            [scenario.GrandchildRunId],
            result.Facts.SubprocessRunIds);
        Assert.Equal(scenario.RootRunId, result.Identity.ParentRunId);
        var query = Assert.IsType<ProcessExecutionObservationQuery>(
            harness.ObservationReader.LastQuery);
        Assert.DoesNotContain(scenario.RootRunId, query.RunIds);
        Assert.Equal(
            new HashSet<ProcessRunId>
            {
                scenario.ChildRunId,
                scenario.GrandchildRunId
            },
            query.RunIds.ToHashSet());
    }

    [Theory]
    [InlineData(
        MissingEvidenceCase.RuntimeState,
        ProcessRunEvidenceSource.RuntimeState,
        ProcessRunRecordWarningCode.MissingSubprocessEvidence)]
    [InlineData(
        MissingEvidenceCase.InstancePlan,
        ProcessRunEvidenceSource.InstancePlan,
        ProcessRunRecordWarningCode.MissingInstancePlan)]
    [InlineData(
        MissingEvidenceCase.StepAssignments,
        ProcessRunEvidenceSource.StepAssignments,
        ProcessRunRecordWarningCode.MissingStepAssignments)]
    [InlineData(
        MissingEvidenceCase.ExecutionObservations,
        ProcessRunEvidenceSource.ExecutionObservations,
        ProcessRunRecordWarningCode.MissingExecutionObservations)]
    [InlineData(
        MissingEvidenceCase.UsageTelemetry,
        ProcessRunEvidenceSource.UsageTelemetry,
        ProcessRunRecordWarningCode.MissingUsageTelemetry)]
    [InlineData(
        MissingEvidenceCase.Pricing,
        ProcessRunEvidenceSource.Pricing,
        ProcessRunRecordWarningCode.MissingPricing)]
    [InlineData(
        MissingEvidenceCase.RuntimeEvents,
        ProcessRunEvidenceSource.RuntimeEvents,
        ProcessRunRecordWarningCode.MissingRuntimeEvents)]
    [InlineData(
        MissingEvidenceCase.ArtifactLineage,
        ProcessRunEvidenceSource.ArtifactLineage,
        ProcessRunRecordWarningCode.MissingArtifactLineage)]
    [InlineData(
        MissingEvidenceCase.Subprocesses,
        ProcessRunEvidenceSource.Subprocesses,
        ProcessRunRecordWarningCode.SubprocessDiscoveryFailed)]
    public async Task AssembleAsync_missing_evidence_is_explicitly_partial(
        MissingEvidenceCase missingCase,
        ProcessRunEvidenceSource expectedMissingSource,
        ProcessRunRecordWarningCode expectedWarning)
    {
        var scenario = CompleteScenario.Create(
            ProcessRuntimeStatus.Completed,
            ProcessRunDisposition.Succeeded);
        scenario.RemoveEvidence(missingCase);
        var harness = scenario.CreateHarness();

        var result = await harness.Assembler.AssembleAsync(
            scenario.CreateClaim(),
            scenario.CreateRecord());

        Assert.Equal(ProcessRunRecordCompleteness.Partial, result.Completeness);
        Assert.True(result.MissingEvidenceSources.HasFlag(expectedMissingSource));
        Assert.Contains(expectedWarning, result.CompletenessWarnings);
    }

    [Fact]
    public async Task AssembleAsync_truncated_execution_observation_read_is_explicitly_partial()
    {
        var scenario = CompleteScenario.Create(
            ProcessRuntimeStatus.Completed,
            ProcessRunDisposition.Succeeded);
        var harness = scenario.CreateHarness();
        harness.ObservationReader.IsComplete = false;

        var result = await harness.Assembler.AssembleAsync(
            scenario.CreateClaim(),
            scenario.CreateRecord());

        Assert.Equal(ProcessRunRecordCompleteness.Partial, result.Completeness);
        Assert.True(result.MissingEvidenceSources.HasFlag(
            ProcessRunEvidenceSource.ExecutionObservations));
        Assert.False(result.AvailableEvidenceSources.HasFlag(
            ProcessRunEvidenceSource.ExecutionObservations));
        Assert.Contains(
            ProcessRunRecordWarningCode.MissingExecutionObservations,
            result.CompletenessWarnings);
    }

    [Fact]
    public async Task AssembleAsync_truncated_usage_read_is_explicitly_partial()
    {
        var scenario = CompleteScenario.Create(
            ProcessRuntimeStatus.Completed,
            ProcessRunDisposition.Succeeded);
        var harness = scenario.CreateHarness();
        harness.UsageReader.IsComplete = false;

        var result = await harness.Assembler.AssembleAsync(
            scenario.CreateClaim(),
            scenario.CreateRecord());

        Assert.Equal(ProcessRunRecordCompleteness.Partial, result.Completeness);
        Assert.True(result.MissingEvidenceSources.HasFlag(
            ProcessRunEvidenceSource.UsageTelemetry));
        Assert.True(result.MissingEvidenceSources.HasFlag(
            ProcessRunEvidenceSource.Pricing));
        Assert.Contains(
            ProcessRunRecordWarningCode.MissingUsageTelemetry,
            result.CompletenessWarnings);
        Assert.Contains(
            ProcessRunRecordWarningCode.MissingPricing,
            result.CompletenessWarnings);
    }

    [Fact]
    public async Task AssembleAsync_missing_primary_state_fails_explicitly()
    {
        var scenario = CompleteScenario.Create(
            ProcessRuntimeStatus.Completed,
            ProcessRunDisposition.Succeeded);
        scenario.States.RemoveAll(state => state.RunId == scenario.RootRunId);
        var harness = scenario.CreateHarness();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Assembler.AssembleAsync(
                scenario.CreateClaim(),
                scenario.CreateRecord()));

        Assert.Contains("was not found", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, harness.HierarchyStore.CallCount);
    }

    [Fact]
    public async Task AssembleAsync_non_escalated_reactivated_state_fails_explicitly()
    {
        var scenario = CompleteScenario.Create(
            ProcessRuntimeStatus.Active,
            ProcessRunDisposition.Succeeded);
        var harness = scenario.CreateHarness();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Assembler.AssembleAsync(
                scenario.CreateClaim(),
                scenario.CreateRecord()));

        Assert.Contains("reactivated or is not terminal", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssembleAsync_stale_claim_fails_before_loading_runtime_state()
    {
        var scenario = CompleteScenario.Create(
            ProcessRuntimeStatus.Completed,
            ProcessRunDisposition.Succeeded);
        var harness = scenario.CreateHarness();
        var staleClaim = scenario.CreateClaim() with
        {
            SourceGlobalSequence = scenario.SourceGlobalSequence - 1
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Assembler.AssembleAsync(
                staleClaim,
                scenario.CreateRecord()));

        Assert.Contains("is stale", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, harness.StateStore.LoadCallCount);
    }

    [Fact]
    public async Task AssembleAsync_nonterminal_escalation_seed_is_partial_but_assembled()
    {
        var scenario = CompleteScenario.Create(
            ProcessRuntimeStatus.Blocked,
            ProcessRunDisposition.Escalated);
        scenario.RuntimeEvents[^1] = CreateStoredEvent(
            scenario.SourceGlobalSequence,
            scenario.SourceRootSequence,
            scenario.RootRunId,
            scenario.RootRunId,
            ProcessRuntimeEventTypes.ManagerLoopBudgetEscalated,
            scenario.EndedAtUtc);
        var harness = scenario.CreateHarness();

        var result = await harness.Assembler.AssembleAsync(
            scenario.CreateClaim(),
            scenario.CreateRecord());

        Assert.Equal(ProcessRunRecordCompleteness.Partial, result.Completeness);
        Assert.True(result.MissingEvidenceSources.HasFlag(
            ProcessRunEvidenceSource.RuntimeState));
        Assert.False(result.MissingEvidenceSources.HasFlag(
            ProcessRunEvidenceSource.InstancePlan));
        Assert.False(result.MissingEvidenceSources.HasFlag(
            ProcessRunEvidenceSource.StepAssignments));
        Assert.Contains(
            ProcessRunRecordWarningCode.PrimaryRunNonTerminalAtEscalation,
            result.CompletenessWarnings);
    }

    [Fact]
    public async Task AssembleAsync_newer_subtree_event_rejects_claim()
    {
        var scenario = CompleteScenario.Create(
            ProcessRuntimeStatus.Completed,
            ProcessRunDisposition.Succeeded);
        scenario.RuntimeEvents.Add(CreateStoredEvent(
            scenario.SourceGlobalSequence + 1,
            scenario.SourceRootSequence + 1,
            scenario.RootRunId,
            scenario.ChildRunId,
            ProcessRuntimeEventTypes.ProcessRunReactivated,
            scenario.EndedAtUtc.AddSeconds(1)));
        var harness = scenario.CreateHarness();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Assembler.AssembleAsync(
                scenario.CreateClaim(),
                scenario.CreateRecord()));

        Assert.Contains("newer subtree runtime events", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssembleAsync_runtime_event_cap_marks_runtime_events_missing()
    {
        const long eventCap = 100_000;
        var scenario = CompleteScenario.Create(
            ProcessRuntimeStatus.Completed,
            ProcessRunDisposition.Succeeded);
        scenario.SourceGlobalSequence = eventCap;
        scenario.SourceRootSequence = eventCap;
        var harness = scenario.CreateHarness();
        harness.EventStore.SynthesizeFullPages = true;

        var result = await harness.Assembler.AssembleAsync(
            scenario.CreateClaim(),
            scenario.CreateRecord());

        Assert.Equal(100, harness.EventStore.ReadByRootCallCount);
        Assert.Equal(ProcessRunRecordCompleteness.Partial, result.Completeness);
        Assert.True(result.MissingEvidenceSources.HasFlag(
            ProcessRunEvidenceSource.RuntimeEvents));
        Assert.Contains(
            ProcessRunRecordWarningCode.MissingRuntimeEvents,
            result.CompletenessWarnings);
    }

    [Fact]
    public async Task AssembleAsync_subprocess_family_cap_is_bounded_and_partial()
    {
        var scenario = CompleteScenario.Create(
            ProcessRuntimeStatus.Completed,
            ProcessRunDisposition.Succeeded);
        scenario.RootFamilyRunIds.Clear();
        scenario.RootFamilyRunIds.AddRange(
            Enumerable.Range(
                    0,
                    ProcessRunRecordPayloadLimits.MaximumSubprocessRunIds + 1)
                .Select(_ => ProcessRunId.New()));
        var harness = scenario.CreateHarness();

        var result = await harness.Assembler.AssembleAsync(
            scenario.CreateClaim(),
            scenario.CreateRecord());

        Assert.Equal(ProcessRunRecordCompleteness.Partial, result.Completeness);
        Assert.True(result.MissingEvidenceSources.HasFlag(
            ProcessRunEvidenceSource.Subprocesses));
        Assert.Contains(
            ProcessRunRecordWarningCode.SubprocessDepthLimitReached,
            result.CompletenessWarnings);
        Assert.Equal(
            IProcessRuntimeStepAssignmentStore.MaximumBatchRunCount,
            harness.AssignmentStore.LastBatchRunIds.Count);
    }

    [Fact]
    public void Aggregator_omits_generated_summaries_prompts_logs_and_tool_arguments()
    {
        var scenario = CompleteScenario.Create(
            ProcessRuntimeStatus.Completed,
            ProcessRunDisposition.Succeeded);
        var state = scenario.States.Single(item =>
            item.RunId == scenario.RootRunId);
        var plan = scenario.Plans.Single(item =>
            item.Header.PlanId == scenario.RootPlanId);
        var assignments = scenario.Assignments
            .Where(item => item.RunId == scenario.RootRunId)
            .ToArray();
        var source = new ProcessRunAssemblySource(
            state,
            plan,
            assignments,
            assignments.ToDictionary(item => item.StepInstanceId),
            true);
        var longSummary = new string('x', 2_048) +
            "😀summary-secret-tail";
        var observation = scenario.ExecutionObservations.Single(item =>
            item.RunId == scenario.RootRunId) with
        {
            InputSummary = "input-secret",
            ResultSummary = longSummary,
            RecentActivities =
            [
                new ProcessExecutionActivityObservation(
                    Now,
                    "Running",
                    "Execution",
                    "log-secret")
            ],
            RecentTools =
            [
                new ProcessExecutionToolObservation(
                    "tool",
                    "provider",
                    "tool-argument-secret",
                    "tool-output-secret",
                    Now.AddMinutes(-2),
                    Now.AddMinutes(-1))
            ],
            LastError = "error-secret"
        };
        var subtree = new ProcessRunSubtree(
            [source],
            [scenario.RootRunId],
            [],
            true,
            true,
            true,
            []);
        var result = new ProcessRunFactsAggregator().Aggregate(
            new ProcessRunAggregationInput(
                scenario.CreateRecord().Summary.Identity,
                scenario.EndedAtUtc,
                subtree,
                new ProcessExecutionObservationReadResult(
                    [observation],
                    IsComplete: true),
                new ProcessRuntimeUsageTelemetryReadResult(
                    scenario.UsageObservations
                        .Where(item => item.RunId == scenario.RootRunId)
                        .ToArray(),
                    IsComplete: true),
                new ProcessRunRuntimeEventEvidence(
                    true,
                    true,
                    Now.AddMinutes(-20),
                    scenario.EndedAtUtc,
                    0,
                    0,
                    0)));

        Assert.Single(result.Facts.Steps);
        var serializedFacts = JsonSerializer.Serialize(result.Facts);
        Assert.DoesNotContain(longSummary, serializedFacts, StringComparison.Ordinal);
        Assert.DoesNotContain("summary-secret-tail", serializedFacts, StringComparison.Ordinal);
        Assert.DoesNotContain("input-secret", serializedFacts, StringComparison.Ordinal);
        Assert.DoesNotContain("log-secret", serializedFacts, StringComparison.Ordinal);
        Assert.DoesNotContain("tool-argument-secret", serializedFacts, StringComparison.Ordinal);
        Assert.DoesNotContain("tool-output-secret", serializedFacts, StringComparison.Ordinal);
        Assert.DoesNotContain("error-secret", serializedFacts, StringComparison.Ordinal);
        Assert.DoesNotContain("prompt-secret", serializedFacts, StringComparison.Ordinal);
    }

    public enum MissingEvidenceCase
    {
        RuntimeState,
        InstancePlan,
        StepAssignments,
        ExecutionObservations,
        UsageTelemetry,
        Pricing,
        RuntimeEvents,
        ArtifactLineage,
        Subprocesses
    }

    private sealed class CompleteScenario
    {
        private CompleteScenario()
        {
        }

        public ProcessRunId RootRunId { get; } = ProcessRunId.New();

        public ProcessRunId ChildRunId { get; } = ProcessRunId.New();

        public ProcessRunId GrandchildRunId { get; } = ProcessRunId.New();

        public ProcessInstancePlanId RootPlanId { get; } =
            ProcessInstancePlanId.New();

        public ProcessInstancePlanId ChildPlanId { get; } =
            ProcessInstancePlanId.New();

        public ProcessInstancePlanId GrandchildPlanId { get; } =
            ProcessInstancePlanId.New();

        public Guid ProjectId { get; } = Guid.NewGuid();

        public ArtifactSlotId ArtifactSlotId { get; } =
            new(Guid.NewGuid());

        public ArtifactInstanceId ArtifactId { get; } =
            new(Guid.NewGuid());

        public DateTimeOffset EndedAtUtc { get; } = Now.AddMinutes(-5);

        public long SourceGlobalSequence { get; set; } = 4;

        public long SourceRootSequence { get; set; } = 4;

        public ProcessRunDisposition Disposition { get; private init; }

        public List<ProcessRuntimeStateSnapshot> States { get; } = [];

        public List<ProcessInstancePlan> Plans { get; } = [];

        public List<ProcessRuntimeStepAssignment> Assignments { get; } = [];

        public List<ProcessExecutionObservation> ExecutionObservations { get; } = [];

        public List<ProcessRuntimeUsageObservation> UsageObservations { get; } = [];

        public List<ProcessStoredRuntimeEvent> RuntimeEvents { get; } = [];

        public List<ProcessRunId> RootFamilyRunIds { get; } = [];

        public bool ThrowHierarchyRead { get; set; }

        public static CompleteScenario Create(
            ProcessRuntimeStatus primaryStatus,
            ProcessRunDisposition disposition)
        {
            var scenario = new CompleteScenario
            {
                Disposition = disposition
            };
            var rootStepId = ProcessStepInstanceId.New();
            var childStepId = ProcessStepInstanceId.New();
            var grandchildStepId = ProcessStepInstanceId.New();
            var rootStepDefinitionId = ProcessStepDefinitionId.New();
            var childStepDefinitionId = ProcessStepDefinitionId.New();
            var grandchildStepDefinitionId = ProcessStepDefinitionId.New();
            scenario.Plans.AddRange(
            [
                CreatePlan(
                    scenario.RootPlanId,
                    scenario.RootPlanId,
                    null,
                    null,
                    rootStepId,
                    rootStepDefinitionId,
                    "root-step"),
                CreatePlan(
                    scenario.ChildPlanId,
                    scenario.RootPlanId,
                    scenario.RootPlanId,
                    rootStepId,
                    childStepId,
                    childStepDefinitionId,
                    "child-step"),
                CreatePlan(
                    scenario.GrandchildPlanId,
                    scenario.RootPlanId,
                    scenario.ChildPlanId,
                    childStepId,
                    grandchildStepId,
                    grandchildStepDefinitionId,
                    "grandchild-step")
            ]);
            scenario.States.AddRange(
            [
                CreateState(
                    scenario.RootRunId,
                    scenario.RootRunId,
                    scenario.RootPlanId,
                    rootStepId,
                    rootStepDefinitionId,
                    primaryStatus,
                    attemptNumber: 2,
                    scenario.EndedAtUtc,
                    new HashSet<ArtifactSlotId>
                    {
                        scenario.ArtifactSlotId
                    },
                    [
                        new ProcessRuntimeInputArtifactReceipt(
                            rootStepId,
                            scenario.ArtifactSlotId,
                            ProcessArtifactInputAvailability.Available,
                            rootStepId,
                            scenario.ArtifactId,
                            "sha256:artifact",
                            "sha256:connection")
                    ]),
                CreateState(
                    scenario.RootRunId,
                    scenario.ChildRunId,
                    scenario.ChildPlanId,
                    childStepId,
                    childStepDefinitionId,
                    ProcessRuntimeStatus.Completed,
                    attemptNumber: 1,
                    scenario.EndedAtUtc.AddMinutes(-2),
                    new HashSet<ArtifactSlotId>(),
                    []),
                CreateState(
                    scenario.RootRunId,
                    scenario.GrandchildRunId,
                    scenario.GrandchildPlanId,
                    grandchildStepId,
                    grandchildStepDefinitionId,
                    ProcessRuntimeStatus.Completed,
                    attemptNumber: 1,
                    scenario.EndedAtUtc.AddMinutes(-3),
                    new HashSet<ArtifactSlotId>(),
                    [])
            ]);
            var rootExecutorId = Guid.NewGuid();
            var childExecutorId = Guid.NewGuid();
            var grandchildExecutorId = Guid.NewGuid();
            scenario.Assignments.AddRange(
            [
                CreateAssignment(
                    scenario.RootRunId,
                    scenario.RootPlanId,
                    rootStepId,
                    "root-step",
                    rootExecutorId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [ProcessRuntimeLaunchVariables.ProjectId] =
                            scenario.ProjectId.ToString("D")
                    }),
                CreateAssignment(
                    scenario.ChildRunId,
                    scenario.ChildPlanId,
                    childStepId,
                    "child-step",
                    childExecutorId,
                    ProcessRuntimeLaunchVariables.CreateParentRunLookup(
                        scenario.RootRunId)),
                CreateAssignment(
                    scenario.GrandchildRunId,
                    scenario.GrandchildPlanId,
                    grandchildStepId,
                    "grandchild-step",
                    grandchildExecutorId,
                    ProcessRuntimeLaunchVariables.CreateParentRunLookup(
                        scenario.ChildRunId))
            ]);
            scenario.ExecutionObservations.AddRange(
            [
                CreateObservation(
                    scenario.RootRunId,
                    rootStepId,
                    rootExecutorId,
                    Now.AddMinutes(-12),
                    "Root work completed."),
                CreateObservation(
                    scenario.ChildRunId,
                    childStepId,
                    childExecutorId,
                    Now.AddMinutes(-11),
                    "Child work completed."),
                CreateObservation(
                    scenario.GrandchildRunId,
                    grandchildStepId,
                    grandchildExecutorId,
                    Now.AddMinutes(-10),
                    "Grandchild work completed.")
            ]);
            scenario.UsageObservations.AddRange(
                scenario.ExecutionObservations.Select(CreateUsage));
            scenario.RuntimeEvents.AddRange(
            [
                CreateStoredEvent(
                    1,
                    1,
                    scenario.RootRunId,
                    scenario.RootRunId,
                    ProcessRuntimeEventTypes.StepReworkRequested,
                    Now.AddMinutes(-20)),
                CreateStoredEvent(
                    2,
                    2,
                    scenario.RootRunId,
                    scenario.ChildRunId,
                    ProcessRuntimeEventTypes.ManagerIncidentRaised,
                    Now.AddMinutes(-15)),
                CreateStoredEvent(
                    3,
                    3,
                    scenario.RootRunId,
                    scenario.GrandchildRunId,
                    ProcessRuntimeEventTypes.ManagerLoopBudgetEscalated,
                    Now.AddMinutes(-10)),
                CreateStoredEvent(
                    scenario.SourceGlobalSequence,
                    scenario.SourceRootSequence,
                    scenario.RootRunId,
                    scenario.RootRunId,
                    ProcessRuntimeEventTypes.ProcessRunCompleted,
                    scenario.EndedAtUtc)
            ]);
            scenario.RootFamilyRunIds.AddRange(
            [
                scenario.ChildRunId,
                scenario.GrandchildRunId
            ]);
            return scenario;
        }

        public ProcessRunRecord CreateRecord(
            ProcessRunId? runId = null,
            ProcessInstancePlanId? planId = null,
            ProcessRunId? parentRunId = null,
            long? sourceGlobalSequence = null,
            long? sourceRootSequence = null,
            DateTimeOffset? endedAtUtc = null)
        {
            var resolvedRunId = runId ?? RootRunId;
            var resolvedPlanId = planId ?? RootPlanId;
            var resolvedEndedAtUtc = endedAtUtc ?? EndedAtUtc;
            var identity = new ProcessRunRecordIdentity(
                resolvedRunId,
                RootRunId,
                parentRunId,
                resolvedPlanId,
                null,
                null,
                null);
            var metrics = new ProcessRunRecordMetrics(
                null,
                resolvedEndedAtUtc,
                null,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0m,
                0m,
                0,
                0,
                0);
            return new ProcessRunRecord(
                new ProcessRunRecordSummary(
                    identity,
                    Disposition,
                    ProcessRunRecordLifecycleState.Current,
                    ProcessRunRecordCompleteness.SeedOnly,
                    ProcessRunEvidenceSource.None,
                    ProcessRunEvidenceSource.All,
                    [],
                    ProcessRunFactsStatus.Assembling,
                    1,
                    null,
                    null,
                    null,
                    ProcessRunNarrativeStatus.Pending,
                    0,
                    null,
                    null,
                    null,
                    metrics,
                    [],
                    null,
                    sourceGlobalSequence ?? SourceGlobalSequence,
                    sourceRootSequence ?? SourceRootSequence,
                    ProcessRunRecordSchema.CurrentVersion,
                    Now.AddMinutes(-4)),
                null);
        }

        public ProcessRunFactsClaim CreateClaim(
            ProcessRunId? runId = null,
            long? sourceGlobalSequence = null)
            => new(
                runId ?? RootRunId,
                sourceGlobalSequence ?? SourceGlobalSequence,
                ProcessRunRecordClaimToken.New(),
                Now.AddMinutes(5),
                1);

        public TestHarness CreateHarness()
        {
            var stateStore = new RecordingStateStore(States);
            var hierarchyStore = new RecordingHierarchyStore(RootFamilyRunIds)
            {
                ThrowOnRead = ThrowHierarchyRead
            };
            var planStore = new RecordingPlanStore(Plans);
            var assignmentStore = new RecordingAssignmentStore(Assignments);
            var observationReader = new RecordingObservationReader(
                ExecutionObservations);
            var usageReader = new RecordingUsageReader(UsageObservations);
            var eventStore = new RecordingEventStore(
                RuntimeEvents,
                RootRunId,
                Now.AddMinutes(-20));
            return new TestHarness(
                new ProcessRunRecordAssembler(
                    stateStore,
                    hierarchyStore,
                    planStore,
                    assignmentStore,
                    observationReader,
                    usageReader,
                    eventStore,
                    new FixedTimeProvider(Now)),
                stateStore,
                hierarchyStore,
                planStore,
                assignmentStore,
                observationReader,
                usageReader,
                eventStore);
        }

        public void RemoveEvidence(MissingEvidenceCase missingCase)
        {
            switch (missingCase)
            {
                case MissingEvidenceCase.RuntimeState:
                    States.RemoveAll(state =>
                        state.RunId == GrandchildRunId);
                    break;
                case MissingEvidenceCase.InstancePlan:
                    Plans.RemoveAll(plan =>
                        plan.Header.PlanId == RootPlanId);
                    break;
                case MissingEvidenceCase.StepAssignments:
                    Assignments.RemoveAll(assignment =>
                        assignment.RunId == RootRunId);
                    break;
                case MissingEvidenceCase.ExecutionObservations:
                    ExecutionObservations.RemoveAll(observation =>
                        observation.RunId == RootRunId);
                    break;
                case MissingEvidenceCase.UsageTelemetry:
                    UsageObservations.Clear();
                    break;
                case MissingEvidenceCase.Pricing:
                    UsageObservations[0] = UsageObservations[0] with
                    {
                        IsKnownUsage = false
                    };
                    break;
                case MissingEvidenceCase.RuntimeEvents:
                    RuntimeEvents.RemoveAll(runtimeEvent =>
                        runtimeEvent.GlobalSequence == SourceGlobalSequence);
                    break;
                case MissingEvidenceCase.ArtifactLineage:
                    var stateIndex = States.FindIndex(state =>
                        state.RunId == RootRunId);
                    States[stateIndex] = States[stateIndex] with
                    {
                        ConnectedInputArtifacts = []
                    };
                    break;
                case MissingEvidenceCase.Subprocesses:
                    ThrowHierarchyRead = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(missingCase),
                        missingCase,
                        "Missing-evidence case is not defined.");
            }
        }
    }

    private sealed record TestHarness(
        ProcessRunRecordAssembler Assembler,
        RecordingStateStore StateStore,
        RecordingHierarchyStore HierarchyStore,
        RecordingPlanStore PlanStore,
        RecordingAssignmentStore AssignmentStore,
        RecordingObservationReader ObservationReader,
        RecordingUsageReader UsageReader,
        RecordingEventStore EventStore);

    private static ProcessRuntimeStateSnapshot CreateState(
        ProcessRunId rootRunId,
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId stepId,
        ProcessStepDefinitionId stepDefinitionId,
        ProcessRuntimeStatus status,
        int attemptNumber,
        DateTimeOffset updatedAtUtc,
        IReadOnlySet<ArtifactSlotId> availableArtifactSlots,
        IReadOnlyList<ProcessRuntimeInputArtifactReceipt> connectedInputArtifacts)
    {
        return new ProcessRuntimeStateSnapshot(
            rootRunId,
            runId,
            planId,
            $"sha256:plan:{planId}",
            status,
            [
                new ProcessRuntimeStepState(
                    stepId,
                    stepDefinitionId,
                    ProcessRuntimeStepStatus.Completed,
                    true,
                    attemptNumber,
                    new HashSet<ProcessStepInstanceId>(),
                    new HashSet<ArtifactSlotId>(),
                    null,
                    null)
            ],
            [],
            [],
            availableArtifactSlots,
            updatedAtUtc)
        {
            ConnectedInputArtifacts = connectedInputArtifacts
        };
    }

    private static ProcessInstancePlan CreatePlan(
        ProcessInstancePlanId planId,
        ProcessInstancePlanId rootPlanId,
        ProcessInstancePlanId? parentPlanId,
        ProcessStepInstanceId? parentStepId,
        ProcessStepInstanceId stepId,
        ProcessStepDefinitionId stepDefinitionId,
        string stepKey)
    {
        return new ProcessInstancePlan(
            new ProcessInstancePlanHeader(
                planId,
                rootPlanId,
                parentPlanId,
                parentStepId,
                "processes.instance-plan.v1",
                Now.AddHours(-1),
                parentPlanId.HasValue ? 1 : 0),
            new ResolvedProcessDefinitionSnapshot(
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                "sha256:definition",
                "template/1",
                "template/1",
                [],
                [],
                []),
            new DriverStackSnapshot([]),
            new StrategyBindingSet([], [], [], []),
            [
                new StepInstancePlan(
                    stepId,
                    stepDefinitionId,
                    stepKey,
                    ProcessStepKind.Activity,
                    true,
                    false,
                    null)
            ],
            new ArtifactPlan([], []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan("sha256:manager", null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(true, "sha256:monitoring"),
            new SecurityPlan("sha256:security", []),
            $"sha256:plan:{planId}");
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId stepId,
        string stepKey,
        Guid executorId,
        IReadOnlyDictionary<string, string> launchVariables)
    {
        return new ProcessRuntimeStepAssignment(
            runId,
            planId,
            stepId,
            stepKey,
            "worker",
            "worker",
            "Worker",
            ProcessLaunchExecutorKinds.Agent,
            executorId.ToString("D"),
            "Worker",
            $"prompt-secret:{stepKey}",
            "sha256:readiness",
            "Resolved for test.",
            [],
            [],
            [],
            "ReadOnly",
            launchVariables,
            null,
            Now.AddMinutes(-30))
        {
            WorkflowBinding = new ProcessWorkflowExecutorBinding(
                new ProcessWorkflowId(Guid.NewGuid()))
        };
    }

    private static ProcessExecutionObservation CreateObservation(
        ProcessRunId runId,
        ProcessStepInstanceId stepId,
        Guid agentId,
        DateTimeOffset startedAtUtc,
        string resultSummary)
    {
        var executionRunId = Guid.NewGuid();
        return new ProcessExecutionObservation(
            executionRunId,
            runId,
            stepId,
            agentId,
            "Worker",
            "provider",
            "model",
            "Completed",
            "Succeeded",
            startedAtUtc.AddMinutes(-1),
            startedAtUtc.AddMinutes(1),
            startedAtUtc,
            startedAtUtc.AddMinutes(1),
            "input-secret",
            resultSummary,
            [],
            [],
            [],
            "log-secret");
    }

    private static ProcessRuntimeUsageObservation CreateUsage(
        ProcessExecutionObservation observation)
    {
        return new ProcessRuntimeUsageObservation(
            Guid.NewGuid(),
            observation.ExecutionRunId,
            observation.RunId,
            observation.StepInstanceId,
            observation.UpdatedAtUtc,
            "provider",
            "model",
            "Execution",
            "Known",
            true,
            10,
            2,
            5,
            1,
            18,
            0.12m,
            0.11m)
        {
            ToolCallCount = 1
        };
    }

    private static ProcessStoredRuntimeEvent CreateStoredEvent(
        long globalSequence,
        long rootSequence,
        ProcessRunId rootRunId,
        ProcessRunId runId,
        ProcessEventType eventType,
        DateTimeOffset occurredAtUtc)
    {
        return new ProcessStoredRuntimeEvent(
            globalSequence,
            rootSequence,
            new ProcessRuntimeEventEnvelope(
                RuntimeEventId.New(),
                rootRunId,
                runId,
                new ProcessCorrelationId("assembler-test"),
                null,
                new ProcessEventActor(
                    ProcessEventActorKind.System,
                    new ProcessActorId("unit-test")),
                ProcessContractVersions.RuntimeEventEnvelopeV1,
                ProcessEventSensitivity.Normal,
                occurredAtUtc,
                eventType,
                $"sha256:event:{globalSequence}"));
    }

    private sealed class RecordingStateStore(
        IReadOnlyList<ProcessRuntimeStateSnapshot> states)
        : IProcessRuntimeStateStore
    {
        public int LoadCallCount { get; private set; }

        public int LoadManyCallCount { get; private set; }

        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
        {
            LoadCallCount++;
            return Task.FromResult<ProcessRuntimeStateSnapshot?>(
                states.SingleOrDefault(state => state.RunId == runId));
        }

        public Task<IReadOnlyList<ProcessRuntimeStateSnapshot>> LoadManyAsync(
            IReadOnlyList<ProcessRunId> runIds,
            CancellationToken cancellationToken = default)
        {
            LoadManyCallCount++;
            var requested = runIds.ToHashSet();
            return Task.FromResult<IReadOnlyList<ProcessRuntimeStateSnapshot>>(
                states
                    .Where(state => requested.Contains(state.RunId))
                    .OrderBy(state => state.RunId.Value)
                    .ToArray());
        }
    }

    private sealed class RecordingHierarchyStore(
        IReadOnlyList<ProcessRunId> runIds)
        : IProcessRuntimeRunHierarchyStore
    {
        public int CallCount { get; private set; }

        public bool ThrowOnRead { get; init; }

        public Task<IReadOnlyList<ProcessRunId>> FindDescendantRunIdsAsync(
            ProcessRunId rootRunId,
            int take,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("Simulated hierarchy read failure.");
            }

            return Task.FromResult<IReadOnlyList<ProcessRunId>>(
                runIds.OrderBy(runId => runId.Value).Take(take).ToArray());
        }

        public Task<IReadOnlyList<ProcessRunId>> FindCancellableDescendantRunIdsAsync(
            ProcessRunId rootRunId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingPlanStore(
        IReadOnlyList<ProcessInstancePlan> plans)
        : IProcessInstancePlanStore
    {
        public int LoadManyCallCount { get; private set; }

        public ValueTask<PersistedProcessInstancePlan> PersistAsync(
            ProcessInstancePlan plan,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<ProcessInstancePlan?> LoadAsync(
            ProcessInstancePlanId planId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<ProcessInstancePlan?>(
                plans.SingleOrDefault(plan => plan.Header.PlanId == planId));

        public ValueTask<IReadOnlyList<ProcessInstancePlan>> LoadManyAsync(
            IReadOnlyList<ProcessInstancePlanId> planIds,
            CancellationToken cancellationToken = default)
        {
            LoadManyCallCount++;
            var requested = planIds.ToHashSet();
            return ValueTask.FromResult<IReadOnlyList<ProcessInstancePlan>>(
                plans
                    .Where(plan => requested.Contains(plan.Header.PlanId))
                    .OrderBy(plan => plan.Header.PlanId.Value)
                    .ToArray());
        }
    }

    private sealed class RecordingAssignmentStore(
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments)
        : IProcessRuntimeStepAssignmentStore
    {
        public int LoadByRunsCallCount { get; private set; }

        public IReadOnlyList<ProcessRunId> LastBatchRunIds { get; private set; } = [];

        public ValueTask SaveAsync(
            IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>(
                assignments.Where(item => item.RunId == runId).ToArray());

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunsAsync(
            IReadOnlyList<ProcessRunId> runIds,
            CancellationToken cancellationToken = default)
        {
            LoadByRunsCallCount++;
            LastBatchRunIds = runIds;
            var requested = runIds.ToHashSet();
            return ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>(
                assignments
                    .Where(item => requested.Contains(item.RunId))
                    .OrderBy(item => item.RunId.Value)
                    .ThenBy(item => item.StepKey, StringComparer.Ordinal)
                    .ToArray());
        }

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>>
            FindByLaunchVariablesAsync(
                IReadOnlyDictionary<string, string> requiredVariables,
                CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "Assembler must use the bounded hierarchy and assignment batch ports.");

        public ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
            ProcessRunId runId,
            ProcessStepInstanceId stepInstanceId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(
                assignments.SingleOrDefault(item =>
                    item.RunId == runId &&
                    item.StepInstanceId == stepInstanceId));
    }

    private sealed class RecordingObservationReader(
        IReadOnlyList<ProcessExecutionObservation> observations)
        : IProcessExecutionObservationReader
    {
        public int CallCount { get; private set; }

        public ProcessExecutionObservationQuery? LastQuery { get; private set; }

        public bool IsComplete { get; set; } = true;

        public ValueTask<IReadOnlyList<ProcessExecutionObservation>> ListAsync(
            ProcessExecutionObservationQuery query,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastQuery = query;
            var runIds = query.RunIds.ToHashSet();
            var stepIds = query.StepInstanceIds.ToHashSet();
            return ValueTask.FromResult<IReadOnlyList<ProcessExecutionObservation>>(
                observations
                    .Where(observation =>
                        runIds.Contains(observation.RunId) &&
                        (stepIds.Count == 0 ||
                         stepIds.Contains(observation.StepInstanceId)) &&
                        observation.UpdatedAtUtc >= query.FromUtc &&
                        observation.UpdatedAtUtc <= query.ToUtc)
                    .OrderBy(observation => observation.RunId.Value)
                    .ThenBy(observation => observation.UpdatedAtUtc)
                    .ToArray());
        }

        public async ValueTask<ProcessExecutionObservationReadResult> ReadAsync(
            ProcessExecutionObservationQuery query,
            CancellationToken cancellationToken = default)
            => new(
                await ListAsync(query, cancellationToken).ConfigureAwait(false),
                IsComplete);
    }

    private sealed class RecordingUsageReader(
        IReadOnlyList<ProcessRuntimeUsageObservation> observations)
        : IProcessRuntimeUsageTelemetryReader
    {
        public int CallCount { get; private set; }

        public bool IsComplete { get; set; } = true;

        public ValueTask<IReadOnlyList<ProcessRuntimeUsageObservation>> ListAsync(
            ProcessRuntimeUsageTelemetryQuery query,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var runIds = query.RunIds.ToHashSet();
            return ValueTask.FromResult<IReadOnlyList<ProcessRuntimeUsageObservation>>(
                observations
                    .Where(observation =>
                        runIds.Contains(observation.RunId) &&
                        observation.CreatedAtUtc >= query.FromUtc &&
                        observation.CreatedAtUtc <= query.ToUtc)
                    .OrderBy(observation => observation.RunId.Value)
                    .ThenBy(observation => observation.CreatedAtUtc)
                    .ToArray());
        }

        public async ValueTask<ProcessRuntimeUsageTelemetryReadResult> ReadAsync(
            ProcessRuntimeUsageTelemetryQuery query,
            CancellationToken cancellationToken = default)
            => new(
                await ListAsync(query, cancellationToken).ConfigureAwait(false),
                IsComplete);
    }

    private sealed class RecordingEventStore(
        IReadOnlyList<ProcessStoredRuntimeEvent> events,
        ProcessRunId syntheticRootRunId,
        DateTimeOffset syntheticOccurredAtUtc)
        : IProcessRuntimeEventReplayStore
    {
        public int ReadByRootCallCount { get; private set; }

        public bool SynthesizeFullPages { get; set; }

        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>>
            ReadAfterGlobalSequenceAsync(
                long globalSequenceExclusive,
                int take,
                CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadByRootRunAsync(
            ProcessRunId rootRunId,
            long rootSequenceExclusive,
            int take,
            CancellationToken cancellationToken = default)
        {
            ReadByRootCallCount++;
            if (SynthesizeFullPages)
            {
                return Task.FromResult<IReadOnlyList<ProcessStoredRuntimeEvent>>(
                    Enumerable.Range(1, take)
                        .Select(offset =>
                        {
                            var sequence = rootSequenceExclusive + offset;
                            return CreateStoredEvent(
                                sequence,
                                sequence,
                                syntheticRootRunId,
                                syntheticRootRunId,
                                ProcessRuntimeEventTypes.ProcessRunCompleted,
                                syntheticOccurredAtUtc);
                        })
                        .ToArray());
            }

            return Task.FromResult<IReadOnlyList<ProcessStoredRuntimeEvent>>(
                events
                    .Where(runtimeEvent =>
                        runtimeEvent.Envelope.RootRunId == rootRunId &&
                        runtimeEvent.RootSequence > rootSequenceExclusive)
                    .OrderBy(runtimeEvent => runtimeEvent.RootSequence)
                    .Take(take)
                    .ToArray());
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
