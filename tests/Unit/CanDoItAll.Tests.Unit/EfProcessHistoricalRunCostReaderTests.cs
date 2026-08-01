using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Tests.Unit;

public sealed class EfProcessHistoricalRunCostReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReadAsync_averages_persisted_actual_cost_without_loading_runtime_telemetry()
    {
        var definitionId = ProcessDefinitionId.New();
        var runOne = ProcessRunId.New();
        var runTwo = ProcessRunId.New();
        var store = new RecordingRunRecordStore(
            Summary(runOne, definitionId, Now.AddHours(-3), executionCount: 2, actualCost: 1.00m),
            Summary(runTwo, definitionId, Now.AddHours(-1), executionCount: 4, actualCost: 2.00m));
        var reader = new EfProcessHistoricalRunCostReader(store);

        var estimate = await reader.ReadAsync(new ProcessHistoricalRunCostQuery(
            definitionId,
            "software-delivery",
            Now,
            TakeRuns: 5,
            FromUtc: Now.AddDays(-1)));

        Assert.Equal(2, estimate.CompletedRunCount);
        Assert.Equal(2, estimate.PricedRunCount);
        Assert.Equal(1.50m, estimate.AverageActualCostUsd);
        Assert.Contains(
            estimate.Samples,
            sample =>
                sample.RunId == runOne &&
                sample.UsageObservationCount == 2 &&
                sample.ActualCostUsd == 1.00m);
        Assert.Contains(
            estimate.Samples,
            sample =>
                sample.RunId == runTwo &&
                sample.UsageObservationCount == 4 &&
                sample.ActualCostUsd == 2.00m);

        var query = Assert.IsType<ProcessRunRecordListQuery>(store.LastQuery);
        Assert.Equal(5, query.Take);
        Assert.Equal(ProcessRunRecordListPayload.Compact, query.Payload);
        Assert.Equal(definitionId, query.DefinitionId);
        Assert.Equal(ProcessRunDisposition.Succeeded, query.Disposition);
        Assert.True(query.RootRunsOnly);
        Assert.Equal(Now.AddDays(-1), query.EndedFromUtc);
        Assert.True(query.EndedBeforeUtc > Now);
    }

    [Fact]
    public async Task ReadAsync_counts_zero_cost_run_when_pricing_evidence_is_present()
    {
        var definitionId = ProcessDefinitionId.New();
        var runId = ProcessRunId.New();
        var store = new RecordingRunRecordStore(
            Summary(runId, definitionId, Now.AddHours(-1), executionCount: 1, actualCost: 0m));
        var reader = new EfProcessHistoricalRunCostReader(store);

        var estimate = await reader.ReadAsync(new ProcessHistoricalRunCostQuery(
            definitionId,
            "software-delivery",
            Now));

        Assert.Equal(1, estimate.CompletedRunCount);
        Assert.Equal(1, estimate.PricedRunCount);
        Assert.Equal(0m, estimate.AverageActualCostUsd);
        Assert.Equal(ProcessRunRecordListPayload.Compact, store.LastQuery?.Payload);
        Assert.True(store.LastQuery?.RootRunsOnly);
    }

    [Fact]
    public async Task ReadAsync_returns_empty_when_no_persisted_completed_run_matches_definition()
    {
        var definitionId = ProcessDefinitionId.New();
        var store = new RecordingRunRecordStore();
        var reader = new EfProcessHistoricalRunCostReader(store);

        var estimate = await reader.ReadAsync(new ProcessHistoricalRunCostQuery(
            definitionId,
            "software-delivery",
            Now));

        Assert.Equal(0, estimate.CompletedRunCount);
        Assert.Equal(0, estimate.PricedRunCount);
        Assert.Equal(0m, estimate.AverageActualCostUsd);
        Assert.Equal(definitionId, store.LastQuery?.DefinitionId);
    }

    private static ProcessRunRecordSummary Summary(
        ProcessRunId runId,
        ProcessDefinitionId definitionId,
        DateTimeOffset endedAtUtc,
        int executionCount,
        decimal actualCost)
    {
        return new ProcessRunRecordSummary(
            new ProcessRunRecordIdentity(
                runId,
                runId,
                ParentRunId: null,
                PlanId: null,
                DefinitionId: definitionId,
                DefinitionVersionId: null,
                ProjectId: null),
            ProcessRunDisposition.Succeeded,
            ProcessRunRecordLifecycleState.Current,
            ProcessRunRecordCompleteness.Complete,
            ProcessRunEvidenceSource.All,
            ProcessRunEvidenceSource.None,
            [],
            ProcessRunFactsStatus.Completed,
            FactsAttemptCount: 1,
            FactsNextAttemptAtUtc: null,
            FactsLastErrorClass: null,
            FactsLastErrorDiagnosticReference: null,
            ProcessRunNarrativeStatus.Completed,
            NarrativeAttemptCount: 1,
            NarrativeNextAttemptAtUtc: null,
            NarrativeLastErrorClass: null,
            NarrativeLastErrorDiagnosticReference: null,
            new ProcessRunRecordMetrics(
                endedAtUtc.AddMinutes(-10),
                endedAtUtc,
                DurationMilliseconds: 600_000,
                TotalStepCount: 2,
                ExecutableStepCount: 2,
                CompletedStepCount: 2,
                FailedStepCount: 0,
                CancelledStepCount: 0,
                RepetitionCount: 0,
                ExecutionCount: executionCount,
                ReworkCount: 0,
                IncidentCount: 0,
                EscalationCount: 0,
                InputTokenCount: 100,
                CachedInputTokenCount: 0,
                OutputTokenCount: 20,
                ReasoningTokenCount: 0,
                TotalTokenCount: 120,
                EstimatedCost: actualCost,
                ActualCost: actualCost,
                ToolCallCount: 0,
                ArtifactCount: 0,
                SubprocessCount: 0),
            ParticipantIds: [],
            Narrative: null,
            SourceGlobalSequence: 10,
            SourceRootSequence: 10,
            SchemaVersion: ProcessRunRecordSchema.CurrentVersion,
            UpdatedAtUtc: endedAtUtc);
    }

    private sealed class RecordingRunRecordStore(params ProcessRunRecordSummary[] summaries)
        : IProcessRunRecordStore
    {
        public ProcessRunRecordListQuery? LastQuery { get; private set; }

        public Task<ProcessRunRecordPage> ListAsync(
            ProcessRunRecordListQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new ProcessRunRecordPage(
                summaries.Take(query.Take).ToArray(),
                NextCursor: null));
        }

        public Task<bool> UpsertSeedAsync(
            ProcessRunRecordSeed seed,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> SupersedeAsync(
            ProcessRunRecordSupersession supersession,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProcessRunRecord?> GetAsync(
            ProcessRunId runId,
            bool includeSuperseded = false,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProcessRunRecordAnalytics> ReadAnalyticsAsync(
            ProcessRunRecordAnalyticsQuery query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ProcessRunFactsClaim>> ClaimFactsAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> CompleteFactsAsync(
            ProcessRunFactsCompletion completion,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> FailFactsAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ProcessRunNarrativeClaim>> ClaimNarrativesAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> CompleteNarrativeAsync(
            ProcessRunNarrativeCompletion completion,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> FailNarrativeAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
