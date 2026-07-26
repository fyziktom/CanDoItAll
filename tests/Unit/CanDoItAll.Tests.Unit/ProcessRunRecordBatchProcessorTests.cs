using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRunRecordBatchProcessorTests
{
    [Fact]
    public async Task Process_next_batch_runs_bounded_backfill_before_claims_and_reports_insertions()
    {
        const int batchSize = 2;
        var invocations = new List<Invocation>();
        var insertedRunId = ProcessRunId.New();
        var seeds = new[]
        {
            CreateSeed(insertedRunId, sourceGlobalSequence: 10),
            CreateSeed(ProcessRunId.New(), sourceGlobalSequence: 11)
        };
        var store = new RecordingStore(invocations, insertedRunId);
        var backfillSource = new RecordingBackfillSource(invocations, seeds);
        var processor = new ProcessRunRecordBatchProcessor(
            store,
            new ProcessRunRecordBackfillProcessor(backfillSource, store),
            assembler: null!,
            narrativeGenerator: null!,
            TimeProvider.System,
            Options.Create(new ProcessRunRecordProcessingOptions
            {
                BatchSize = batchSize
            }),
            NullLogger<ProcessRunRecordBatchProcessor>.Instance);

        var result = await processor.ProcessNextBatchAsync();

        Assert.Collection(
            invocations,
            invocation =>
            {
                Assert.Equal(InvocationKind.BackfillRead, invocation.Kind);
                Assert.Equal(batchSize, invocation.Take);
            },
            invocation =>
            {
                Assert.Equal(InvocationKind.SeedUpsert, invocation.Kind);
                Assert.Equal(seeds[0].Identity.RunId, invocation.RunId);
            },
            invocation =>
            {
                Assert.Equal(InvocationKind.SeedUpsert, invocation.Kind);
                Assert.Equal(seeds[1].Identity.RunId, invocation.RunId);
            },
            invocation =>
            {
                Assert.Equal(InvocationKind.FactsClaim, invocation.Kind);
                Assert.Equal(batchSize, invocation.Take);
            },
            invocation =>
            {
                Assert.Equal(InvocationKind.NarrativeClaim, invocation.Kind);
                Assert.Equal(batchSize, invocation.Take);
            });
        Assert.Equal(1, result.BackfilledCount);
        Assert.Equal(0, result.FactsCompletedCount);
        Assert.Equal(0, result.NarrativesCompletedCount);
        Assert.Equal(1, result.ProcessedCount);
    }

    [Fact]
    public async Task Process_next_batch_defers_active_same_source_narrative_without_consuming_attempt()
    {
        var now = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
        var retryBaseDelay = TimeSpan.FromSeconds(45);
        var runId = ProcessRunId.New();
        var claim = new ProcessRunNarrativeClaim(
            runId,
            SourceGlobalSequence: 42,
            ProcessRunRecordClaimToken.New(),
            now.AddMinutes(10),
            AttemptCount: 5);
        var executionRunId = Guid.NewGuid();
        var store = new NarrativeRaceStore(claim, CreateFactsCompletedRecord(runId, now));
        var processor = new ProcessRunRecordBatchProcessor(
            store,
            new ProcessRunRecordBackfillProcessor(new EmptyBackfillSource(), store),
            assembler: null!,
            new DeferredNarrativeGenerator(executionRunId),
            new FixedTimeProvider(now),
            Options.Create(new ProcessRunRecordProcessingOptions
            {
                BatchSize = 1,
                MaximumAttempts = claim.AttemptCount,
                RetryBaseDelay = retryBaseDelay,
                RetryMaximumDelay = TimeSpan.FromMinutes(5)
            }),
            NullLogger<ProcessRunRecordBatchProcessor>.Instance);

        var result = await processor.ProcessNextBatchAsync();

        Assert.Equal(0, result.NarrativesCompletedCount);
        var failure = Assert.IsType<ProcessRunStageFailure>(store.NarrativeDeferral);
        Assert.Equal(claim.RunId, failure.RunId);
        Assert.Equal(claim.SourceGlobalSequence, failure.SourceGlobalSequence);
        Assert.Equal(claim.ClaimToken, failure.ClaimToken);
        Assert.Equal(nameof(ProcessRunNarrativeGenerationDeferredException), failure.ErrorClass);
        Assert.Equal(now.Add(retryBaseDelay), failure.NextAttemptAtUtc);
        Assert.False(failure.ConsumesAttempt);
        Assert.Equal(0, store.NarrativeCompletionCount);
    }

    private static ProcessRunRecord CreateFactsCompletedRecord(
        ProcessRunId runId,
        DateTimeOffset now)
    {
        var identity = new ProcessRunRecordIdentity(
            runId,
            runId,
            ParentRunId: null,
            PlanId: null,
            DefinitionId: null,
            DefinitionVersionId: null,
            ProjectId: null);
        var metrics = new ProcessRunRecordMetrics(
            now.AddMinutes(-1),
            now,
            DurationMilliseconds: 60_000,
            TotalStepCount: 0,
            ExecutableStepCount: 0,
            CompletedStepCount: 0,
            FailedStepCount: 0,
            CancelledStepCount: 0,
            RepetitionCount: 0,
            ExecutionCount: 0,
            ReworkCount: 0,
            IncidentCount: 0,
            EscalationCount: 0,
            InputTokenCount: 0,
            CachedInputTokenCount: 0,
            OutputTokenCount: 0,
            ReasoningTokenCount: 0,
            TotalTokenCount: 0,
            EstimatedCost: 0,
            ActualCost: 0,
            ToolCallCount: 0,
            ArtifactCount: 0,
            SubprocessCount: 0);
        return new ProcessRunRecord(
            new ProcessRunRecordSummary(
                identity,
                ProcessRunDisposition.Succeeded,
                ProcessRunRecordLifecycleState.Current,
                ProcessRunRecordCompleteness.Complete,
                ProcessRunEvidenceSource.All,
                ProcessRunEvidenceSource.None,
                CompletenessWarnings: [],
                ProcessRunFactsStatus.Completed,
                FactsAttemptCount: 1,
                FactsNextAttemptAtUtc: null,
                FactsLastErrorClass: null,
                FactsLastErrorDiagnosticReference: null,
                ProcessRunNarrativeStatus.Pending,
                NarrativeAttemptCount: 4,
                NarrativeNextAttemptAtUtc: null,
                NarrativeLastErrorClass: null,
                NarrativeLastErrorDiagnosticReference: null,
                metrics,
                ParticipantIds: [],
                Narrative: null,
                SourceGlobalSequence: 42,
                SourceRootSequence: 7,
                ProcessRunRecordSchema.CurrentVersion,
                now),
            new ProcessRunHardFacts(
                Steps: [],
                ParticipantIds: [],
                WorkflowIds: [],
                SubprocessRunIds: [],
                ExecutionRunIds: [],
                ArtifactIds: []));
    }

    private static ProcessRunRecordSeed CreateSeed(
        ProcessRunId runId,
        long sourceGlobalSequence)
    {
        var endedAtUtc = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
        return new ProcessRunRecordSeed(
            new ProcessRunRecordIdentity(
                runId,
                runId,
                ParentRunId: null,
                PlanId: null,
                DefinitionId: null,
                DefinitionVersionId: null,
                ProjectId: null),
            ProcessRunDisposition.Succeeded,
            endedAtUtc,
            sourceGlobalSequence,
            sourceGlobalSequence,
            endedAtUtc);
    }

    private enum InvocationKind
    {
        BackfillRead,
        SeedUpsert,
        FactsClaim,
        NarrativeClaim
    }

    private sealed record Invocation(
        InvocationKind Kind,
        int? Take = null,
        ProcessRunId? RunId = null);

    private sealed class RecordingBackfillSource(
        List<Invocation> invocations,
        IReadOnlyList<ProcessRunRecordSeed> seeds) : IProcessRunRecordBackfillSource
    {
        public Task<IReadOnlyList<ProcessRunRecordSeed>> ListMissingReportableSeedsAsync(
            int take,
            CancellationToken cancellationToken = default)
        {
            invocations.Add(new Invocation(InvocationKind.BackfillRead, take));
            return Task.FromResult(seeds);
        }
    }

    private sealed class EmptyBackfillSource : IProcessRunRecordBackfillSource
    {
        public Task<IReadOnlyList<ProcessRunRecordSeed>> ListMissingReportableSeedsAsync(
            int take,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProcessRunRecordSeed>>([]);
    }

    private sealed class DeferredNarrativeGenerator(Guid executionRunId) : IProcessRunNarrativeGenerator
    {
        public Task<ProcessRunNarrative> GenerateAsync(
            ProcessRunRecord record,
            CancellationToken cancellationToken = default) =>
            throw new ProcessRunNarrativeGenerationDeferredException(
                executionRunId,
                ExecutionState.Running,
                AgentFrameworkProcessRunNarrativeGenerator.SourceKind,
                $"{record.Summary.Identity.RunId}:{record.Summary.SourceGlobalSequence}");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class NarrativeRaceStore(
        ProcessRunNarrativeClaim claim,
        ProcessRunRecord current) : IProcessRunRecordStore
    {
        public ProcessRunStageFailure? NarrativeDeferral { get; private set; }

        public int NarrativeCompletionCount { get; private set; }

        public Task<bool> UpsertSeedAsync(
            ProcessRunRecordSeed seed,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<ProcessRunFactsClaim>> ClaimFactsAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProcessRunFactsClaim>>([]);

        public Task<IReadOnlyList<ProcessRunNarrativeClaim>> ClaimNarrativesAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProcessRunNarrativeClaim>>([claim]);

        public Task<ProcessRunRecord?> GetAsync(
            ProcessRunId runId,
            bool includeSuperseded = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProcessRunRecord?>(runId == claim.RunId ? current : null);

        public Task<bool> CompleteNarrativeAsync(
            ProcessRunNarrativeCompletion completion,
            CancellationToken cancellationToken = default)
        {
            NarrativeCompletionCount++;
            return Task.FromResult(true);
        }

        public Task<bool> FailNarrativeAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default)
        {
            NarrativeDeferral = failure;
            return Task.FromResult(true);
        }

        public Task<bool> SupersedeAsync(
            ProcessRunRecordSupersession supersession,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProcessRunRecordPage> ListAsync(
            ProcessRunRecordListQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProcessRunRecordAnalytics> ReadAnalyticsAsync(
            ProcessRunRecordAnalyticsQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> CompleteFactsAsync(
            ProcessRunFactsCompletion completion,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> FailFactsAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingStore(
        List<Invocation> invocations,
        ProcessRunId insertedRunId) : IProcessRunRecordStore
    {
        public Task<bool> UpsertSeedAsync(
            ProcessRunRecordSeed seed,
            CancellationToken cancellationToken = default)
        {
            invocations.Add(new Invocation(InvocationKind.SeedUpsert, RunId: seed.Identity.RunId));
            return Task.FromResult(seed.Identity.RunId == insertedRunId);
        }

        public Task<IReadOnlyList<ProcessRunFactsClaim>> ClaimFactsAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default)
        {
            invocations.Add(new Invocation(InvocationKind.FactsClaim, request.Take));
            return Task.FromResult<IReadOnlyList<ProcessRunFactsClaim>>([]);
        }

        public Task<IReadOnlyList<ProcessRunNarrativeClaim>> ClaimNarrativesAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default)
        {
            invocations.Add(new Invocation(InvocationKind.NarrativeClaim, request.Take));
            return Task.FromResult<IReadOnlyList<ProcessRunNarrativeClaim>>([]);
        }

        public Task<bool> SupersedeAsync(
            ProcessRunRecordSupersession supersession,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProcessRunRecord?> GetAsync(
            ProcessRunId runId,
            bool includeSuperseded = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProcessRunRecordPage> ListAsync(
            ProcessRunRecordListQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProcessRunRecordAnalytics> ReadAnalyticsAsync(
            ProcessRunRecordAnalyticsQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> CompleteFactsAsync(
            ProcessRunFactsCompletion completion,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> FailFactsAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> CompleteNarrativeAsync(
            ProcessRunNarrativeCompletion completion,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> FailNarrativeAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
