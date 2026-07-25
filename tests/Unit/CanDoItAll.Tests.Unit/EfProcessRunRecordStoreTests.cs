using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Unit;

public sealed class EfProcessRunRecordStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ProcessRunDisposition.Succeeded)]
    [InlineData(ProcessRunDisposition.Failed)]
    [InlineData(ProcessRunDisposition.Cancelled)]
    [InlineData(ProcessRunDisposition.Escalated)]
    public async Task Seed_round_trips_every_reportable_disposition(ProcessRunDisposition disposition)
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRunRecordStore(dbContext);
        var runId = ProcessRunId.New();

        var inserted = await store.UpsertSeedAsync(NewSeed(runId, disposition, sourceSequence: 10));

        Assert.True(inserted);
        var record = await store.GetAsync(runId);
        Assert.NotNull(record);
        Assert.Equal(disposition, record.Summary.Disposition);
        Assert.Equal(ProcessRunFactsStatus.Pending, record.Summary.FactsStatus);
        Assert.Equal(ProcessRunNarrativeStatus.Pending, record.Summary.NarrativeStatus);
        Assert.Equal(ProcessRunEvidenceSource.All, record.Summary.MissingEvidenceSources);
    }

    [Fact]
    public async Task Duplicate_seed_preserves_completed_projection_and_reactivation_requires_new_closure_revision()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRunRecordStore(dbContext);
        var runId = ProcessRunId.New();
        await store.UpsertSeedAsync(NewSeed(runId, ProcessRunDisposition.Failed, sourceSequence: 10));
        var factsClaim = Assert.Single(await store.ClaimFactsAsync(
            new ProcessRunRecordClaimRequest(Now.AddMinutes(1), TimeSpan.FromMinutes(5), 10)));
        Assert.True(await store.CompleteFactsAsync(NewFactsCompletion(runId, factsClaim)));
        var narrativeClaim = Assert.Single(await store.ClaimNarrativesAsync(
            new ProcessRunRecordClaimRequest(Now.AddMinutes(2), TimeSpan.FromMinutes(5), 10)));
        Assert.True(await store.CompleteNarrativeAsync(NewNarrativeCompletion(runId, narrativeClaim)));

        var duplicateApplied = await store.UpsertSeedAsync(
            NewSeed(runId, ProcessRunDisposition.Failed, sourceSequence: 11));

        Assert.False(duplicateApplied);
        var completed = Assert.IsType<ProcessRunRecord>(await store.GetAsync(runId));
        Assert.Equal(10, completed.Summary.SourceGlobalSequence);
        Assert.Equal(ProcessRunFactsStatus.Completed, completed.Summary.FactsStatus);
        Assert.Equal(ProcessRunNarrativeStatus.Completed, completed.Summary.NarrativeStatus);
        Assert.NotNull(completed.Facts);
        Assert.NotNull(completed.Summary.Narrative);

        Assert.True(await store.SupersedeAsync(
            new ProcessRunRecordSupersession(runId, 12, 12, Now.AddMinutes(3))));
        Assert.Null(await store.GetAsync(runId));
        var superseded = Assert.IsType<ProcessRunRecord>(await store.GetAsync(runId, includeSuperseded: true));
        Assert.Equal(ProcessRunRecordLifecycleState.Superseded, superseded.Summary.LifecycleState);
        Assert.Equal(ProcessRunNarrativeStatus.Completed, superseded.Summary.NarrativeStatus);

        Assert.False(await store.UpsertSeedAsync(
            NewSeed(runId, ProcessRunDisposition.Failed, sourceSequence: 11)));
        Assert.True(await store.UpsertSeedAsync(
            NewSeed(runId, ProcessRunDisposition.Failed, sourceSequence: 13)));
        var reopened = Assert.IsType<ProcessRunRecord>(await store.GetAsync(runId));
        Assert.Equal(ProcessRunRecordLifecycleState.Current, reopened.Summary.LifecycleState);
        Assert.Equal(ProcessRunFactsStatus.Pending, reopened.Summary.FactsStatus);
        Assert.Equal(ProcessRunNarrativeStatus.Pending, reopened.Summary.NarrativeStatus);
        Assert.Null(reopened.Facts);
        Assert.Null(reopened.Summary.Narrative);
    }

    [Fact]
    public async Task Claims_are_exclusive_and_completions_are_lease_and_source_guarded()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString("N");
        await using var firstContext = CreateDbContext(databaseName, databaseRoot);
        await using var secondContext = CreateDbContext(databaseName, databaseRoot);
        var firstStore = new EfProcessRunRecordStore(firstContext);
        var secondStore = new EfProcessRunRecordStore(secondContext);
        var runId = ProcessRunId.New();
        await firstStore.UpsertSeedAsync(NewSeed(runId, ProcessRunDisposition.Succeeded, sourceSequence: 20));
        var request = new ProcessRunRecordClaimRequest(Now.AddMinutes(1), TimeSpan.FromMinutes(5), 1);

        var batches = await Task.WhenAll(
            firstStore.ClaimFactsAsync(request),
            secondStore.ClaimFactsAsync(request));

        var claim = Assert.Single(batches.SelectMany(batch => batch));
        Assert.Equal(1, claim.AttemptCount);
        var staleCompletion = NewFactsCompletion(runId, claim) with
        {
            ClaimToken = ProcessRunRecordClaimToken.New()
        };
        Assert.False(await secondStore.CompleteFactsAsync(staleCompletion));
        Assert.True(await secondStore.CompleteFactsAsync(NewFactsCompletion(runId, claim)));
        Assert.Empty(await firstStore.ClaimFactsAsync(request with { NowUtc = Now.AddMinutes(2) }));
    }

    [Fact]
    public async Task Non_consuming_failure_releases_lease_without_inflating_attempt_count()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRunRecordStore(dbContext);
        var runId = ProcessRunId.New();
        await store.UpsertSeedAsync(NewSeed(runId, ProcessRunDisposition.Succeeded, sourceSequence: 21));
        var claim = Assert.Single(await store.ClaimFactsAsync(
            new ProcessRunRecordClaimRequest(Now, TimeSpan.FromMinutes(5), 1)));
        Assert.Equal(1, claim.AttemptCount);

        Assert.True(await store.FailFactsAsync(new ProcessRunStageFailure(
            runId,
            claim.SourceGlobalSequence,
            claim.ClaimToken,
            "WorkStillActive",
            "process-run-facts:work-still-active",
            Now.AddMinutes(1),
            Now.AddMinutes(2),
            ConsumesAttempt: false)));

        var deferred = Assert.IsType<ProcessRunRecord>(await store.GetAsync(runId));
        Assert.Equal(0, deferred.Summary.FactsAttemptCount);
        Assert.Equal(ProcessRunFactsStatus.Failed, deferred.Summary.FactsStatus);
        Assert.Equal(Now.AddMinutes(2), deferred.Summary.FactsNextAttemptAtUtc);
        var retry = Assert.Single(await store.ClaimFactsAsync(
            new ProcessRunRecordClaimRequest(Now.AddMinutes(2), TimeSpan.FromMinutes(5), 1)));
        Assert.Equal(1, retry.AttemptCount);
        Assert.True(await store.CompleteFactsAsync(NewFactsCompletion(runId, retry)));
        var narrativeClaim = Assert.Single(await store.ClaimNarrativesAsync(
            new ProcessRunRecordClaimRequest(Now.AddMinutes(3), TimeSpan.FromMinutes(5), 1)));
        Assert.Equal(1, narrativeClaim.AttemptCount);
        Assert.True(await store.FailNarrativeAsync(new ProcessRunStageFailure(
            runId,
            narrativeClaim.SourceGlobalSequence,
            narrativeClaim.ClaimToken,
            "ExecutionStillActive",
            "process-run-narrative:execution-still-active",
            Now.AddMinutes(4),
            Now.AddMinutes(5),
            ConsumesAttempt: false)));

        var narrativeDeferred = Assert.IsType<ProcessRunRecord>(await store.GetAsync(runId));
        Assert.Equal(0, narrativeDeferred.Summary.NarrativeAttemptCount);
        var narrativeRetry = Assert.Single(await store.ClaimNarrativesAsync(
            new ProcessRunRecordClaimRequest(Now.AddMinutes(5), TimeSpan.FromMinutes(5), 1)));
        Assert.Equal(1, narrativeRetry.AttemptCount);
    }

    [Fact]
    public async Task Stage_failures_require_the_lease_and_respect_explicit_retry_schedule()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRunRecordStore(dbContext);
        var runId = ProcessRunId.New();
        await store.UpsertSeedAsync(NewSeed(runId, ProcessRunDisposition.Failed, sourceSequence: 25));
        var firstClaim = Assert.Single(await store.ClaimFactsAsync(
            new ProcessRunRecordClaimRequest(Now, TimeSpan.FromMinutes(5), 1)));
        Assert.True(await store.FailFactsAsync(new ProcessRunStageFailure(
            runId,
            firstClaim.SourceGlobalSequence,
            firstClaim.ClaimToken,
            "MissingObservationEvidence",
            "process-run-facts:missing-observations",
            Now.AddMinutes(1),
            Now.AddMinutes(10))));
        Assert.Empty(await store.ClaimFactsAsync(
            new ProcessRunRecordClaimRequest(Now.AddMinutes(9), TimeSpan.FromMinutes(5), 1)));
        var retryClaim = Assert.Single(await store.ClaimFactsAsync(
            new ProcessRunRecordClaimRequest(Now.AddMinutes(10), TimeSpan.FromMinutes(5), 1)));
        Assert.Equal(2, retryClaim.AttemptCount);
        Assert.True(await store.CompleteFactsAsync(NewFactsCompletion(runId, retryClaim)));
        var narrativeClaim = Assert.Single(await store.ClaimNarrativesAsync(
            new ProcessRunRecordClaimRequest(Now.AddMinutes(11), TimeSpan.FromMinutes(5), 1)));
        Assert.True(await store.FailNarrativeAsync(new ProcessRunStageFailure(
            runId,
            narrativeClaim.SourceGlobalSequence,
            narrativeClaim.ClaimToken,
            "ManagerSummaryFailed",
            "process-run-narrative:provider-failure",
            Now.AddMinutes(12),
            NextAttemptAtUtc: null)));

        Assert.Empty(await store.ClaimNarrativesAsync(
            new ProcessRunRecordClaimRequest(Now.AddDays(1), TimeSpan.FromMinutes(5), 1)));
        var failed = Assert.IsType<ProcessRunRecord>(await store.GetAsync(runId));
        Assert.Equal(ProcessRunNarrativeStatus.Failed, failed.Summary.NarrativeStatus);
        Assert.Equal("ManagerSummaryFailed", failed.Summary.NarrativeLastErrorClass);
    }

    [Fact]
    public async Task List_uses_bounded_exact_ids_participant_index_and_keyset_while_analytics_uses_scalars()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRunRecordStore(dbContext);
        var firstRunId = new ProcessRunId(new Guid("10000000-0000-0000-0000-000000000001"));
        var secondRunId = new ProcessRunId(new Guid("10000000-0000-0000-0000-000000000002"));
        var thirdRunId = new ProcessRunId(new Guid("10000000-0000-0000-0000-000000000003"));
        await store.UpsertSeedAsync(NewSeed(firstRunId, ProcessRunDisposition.Succeeded, 31, Now.AddMinutes(-3)));
        await store.UpsertSeedAsync(NewSeed(secondRunId, ProcessRunDisposition.Failed, 32, Now.AddMinutes(-2)));
        await store.UpsertSeedAsync(NewSeed(thirdRunId, ProcessRunDisposition.Cancelled, 33, Now.AddMinutes(-1)));
        var firstClaim = Assert.Single(await store.ClaimFactsAsync(
            new ProcessRunRecordClaimRequest(Now, TimeSpan.FromMinutes(5), 1)));
        Assert.True(await store.CompleteFactsAsync(NewFactsCompletion(firstClaim.RunId, firstClaim)));
        var narrativeClaim = Assert.Single(await store.ClaimNarrativesAsync(
            new ProcessRunRecordClaimRequest(Now.AddMinutes(1), TimeSpan.FromMinutes(5), 1)));
        Assert.True(await store.CompleteNarrativeAsync(
            NewNarrativeCompletion(firstClaim.RunId, narrativeClaim)));

        var firstPage = await store.ListAsync(new ProcessRunRecordListQuery(1)
        {
            RunIds = [firstRunId, secondRunId, thirdRunId]
        });
        Assert.Single(firstPage.Records);
        Assert.NotNull(firstPage.NextCursor);
        var secondPage = await store.ListAsync(new ProcessRunRecordListQuery(2)
        {
            RunIds = [firstRunId, secondRunId, thirdRunId],
            Cursor = firstPage.NextCursor
        });
        Assert.Equal(2, secondPage.Records.Count);
        Assert.DoesNotContain(
            secondPage.Records,
            record => record.Identity.RunId == firstPage.Records[0].Identity.RunId);

        var compactParticipantPage = await store.ListAsync(new ProcessRunRecordListQuery
        {
            ParticipantId = new ProcessRunParticipantId("agent:manager")
        });
        var compactParticipantRecord = Assert.Single(compactParticipantPage.Records);
        Assert.Equal(firstClaim.RunId, compactParticipantRecord.Identity.RunId);
        Assert.Empty(compactParticipantRecord.ParticipantIds);
        Assert.Null(compactParticipantRecord.Narrative);

        var participantPage = await store.ListAsync(new ProcessRunRecordListQuery
        {
            ParticipantId = new ProcessRunParticipantId("agent:manager"),
            Payload = ProcessRunRecordListPayload.Full
        });
        var participantRecord = Assert.Single(participantPage.Records);
        Assert.Equal(firstClaim.RunId, participantRecord.Identity.RunId);
        Assert.Contains(
            new ProcessRunParticipantId("agent:manager"),
            participantRecord.ParticipantIds);
        Assert.NotNull(participantRecord.Narrative);

        var analytics = await store.ReadAnalyticsAsync(
            new ProcessRunRecordAnalyticsQuery(Now.AddHours(-1), Now.AddHours(1)));
        Assert.Equal(3, analytics.MatchingRunCount);
        Assert.Equal(1, analytics.FactsAvailableRunCount);
        Assert.Equal(0, analytics.EvidenceCompleteRunCount);
        Assert.Equal(1, analytics.EvidencePartialRunCount);
        Assert.Equal(2, analytics.FactsUnavailableRunCount);
        Assert.Equal(Now, analytics.LatestEndedAtUtc);
        Assert.Equal(33, analytics.MaximumSourceGlobalSequence);
        var stageUpdatedRecord = Assert.IsType<ProcessRunRecord>(await store.GetAsync(firstClaim.RunId));
        Assert.True(stageUpdatedRecord.Summary.UpdatedAtUtc > analytics.LatestEndedAtUtc!.Value);
        Assert.Equal(42, analytics.TotalTokenCount);
        Assert.Equal(1.25m, analytics.ActualCost);
        Assert.Contains(
            analytics.Dispositions,
            item => item.Disposition == ProcessRunDisposition.Succeeded && item.MatchingRunCount == 1);
    }

    [Fact]
    public void Model_has_keyset_covering_process_run_record_indexes()
    {
        using var dbContext = CreateDbContext();
        var entityType = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IEntityType>(
            dbContext.Model.FindEntityType(typeof(ProcessRunRecordEntity)));
        var indexes = entityType.GetIndexes()
            .Select(index => string.Join(",", index.Properties.Select(property => property.Name)))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("LifecycleState,EndedAtUtc,RunId", indexes);
        Assert.Contains("LifecycleState,ProjectId,EndedAtUtc,RunId", indexes);
        Assert.Contains("LifecycleState,DefinitionId,EndedAtUtc,RunId", indexes);
        Assert.Contains("LifecycleState,RootRunId,EndedAtUtc,RunId", indexes);
        Assert.Contains("LifecycleState,ParentRunId,EndedAtUtc,RunId", indexes);
        Assert.Contains("LifecycleState,Disposition,EndedAtUtc,RunId", indexes);
    }

    [Fact]
    public async Task List_root_runs_only_excludes_descendant_records()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRunRecordStore(dbContext);
        var rootRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        await store.UpsertSeedAsync(NewSeed(rootRunId, ProcessRunDisposition.Succeeded, 35));
        var childSeed = NewSeed(childRunId, ProcessRunDisposition.Succeeded, 36) with
        {
            Identity = NewIdentity(childRunId) with
            {
                RootRunId = rootRunId,
                ParentRunId = rootRunId
            }
        };
        await store.UpsertSeedAsync(childSeed);

        var page = await store.ListAsync(new ProcessRunRecordListQuery
        {
            RootRunsOnly = true
        });

        var record = Assert.Single(page.Records);
        Assert.Equal(rootRunId, record.Identity.RunId);
    }

    [Fact]
    public async Task Backfill_selects_latest_compatible_terminal_event_excludes_current_records_and_is_idempotent()
    {
        await using var dbContext = CreateDbContext();
        var completedRunId = ProcessRunId.New();
        var excludedRunId = ProcessRunId.New();
        AddRuntimeState(dbContext, completedRunId, ProcessRuntimeStatus.Completed, Now);
        AddRuntimeState(dbContext, excludedRunId, ProcessRuntimeStatus.Failed, Now.AddMinutes(-1));
        AddRuntimeEvent(
            dbContext,
            completedRunId,
            ProcessRuntimeEventTypes.ProcessRunFailed.Value,
            globalSequence: 40,
            Now.AddMinutes(-5));
        AddRuntimeEvent(
            dbContext,
            completedRunId,
            ProcessRuntimeEventTypes.ProcessRunReactivated.Value,
            globalSequence: 41,
            Now.AddMinutes(-4));
        AddRuntimeEvent(
            dbContext,
            completedRunId,
            ProcessRuntimeEventTypes.ProcessRunCompleted.Value,
            globalSequence: 42,
            Now.AddMinutes(-3));
        AddRuntimeEvent(
            dbContext,
            completedRunId,
            ProcessRuntimeEventTypes.ProcessRunCompleted.Value,
            globalSequence: 43,
            Now.AddMinutes(-2));
        AddRuntimeEvent(
            dbContext,
            excludedRunId,
            ProcessRuntimeEventTypes.ProcessRunFailed.Value,
            globalSequence: 44,
            Now.AddMinutes(-1));
        await dbContext.SaveChangesAsync();
        var store = new EfProcessRunRecordStore(dbContext);
        await store.UpsertSeedAsync(NewSeed(excludedRunId, ProcessRunDisposition.Failed, 44));
        var observedAtUtc = Now.AddHours(2);
        var source = new EfProcessRunRecordBackfillSource(
            dbContext,
            new FixedTimeProvider(observedAtUtc));
        var processor = new ProcessRunRecordBackfillProcessor(source, store);

        var first = await processor.RunBatchAsync(1);
        var second = await processor.RunBatchAsync(1);

        Assert.Equal(1, first.CandidateCount);
        Assert.Equal(1, first.InsertedOrRevisedCount);
        Assert.Equal(0, second.CandidateCount);
        var record = Assert.IsType<ProcessRunRecord>(await store.GetAsync(completedRunId));
        Assert.Equal(ProcessRunDisposition.Succeeded, record.Summary.Disposition);
        Assert.Equal(43, record.Summary.SourceGlobalSequence);
        Assert.Equal(Now.AddMinutes(-2), record.Summary.Metrics.EndedAtUtc);
        Assert.Equal(observedAtUtc, record.Summary.UpdatedAtUtc);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => source.ListMissingTerminalSeedsAsync(101));
    }

    [Fact]
    public async Task Stale_backfill_seed_is_rejected_after_run_reactivation()
    {
        await using var dbContext = CreateDbContext();
        var runId = ProcessRunId.New();
        AddRuntimeState(dbContext, runId, ProcessRuntimeStatus.Completed, Now);
        AddRuntimeEvent(
            dbContext,
            runId,
            ProcessRuntimeEventTypes.ProcessRunCompleted.Value,
            globalSequence: 50,
            Now);
        await dbContext.SaveChangesAsync();
        var source = new EfProcessRunRecordBackfillSource(
            dbContext,
            new FixedTimeProvider(Now.AddMinutes(1)));
        var staleSeed = Assert.Single(await source.ListMissingTerminalSeedsAsync(1));
        Assert.Equal(ProcessRunRecordSeedValidation.CurrentTerminalSource, staleSeed.Validation);

        var state = await dbContext.RuntimeStates.SingleAsync(item => item.RunId == runId.Value);
        state.Status = ProcessRuntimeStatus.Active;
        state.UpdatedAtUtc = Now.AddMinutes(2);
        AddRuntimeEvent(
            dbContext,
            runId,
            ProcessRuntimeEventTypes.ProcessRunReactivated.Value,
            globalSequence: 51,
            Now.AddMinutes(2));
        await dbContext.SaveChangesAsync();
        var store = new EfProcessRunRecordStore(dbContext);
        Assert.False(await store.SupersedeAsync(
            new ProcessRunRecordSupersession(
                runId,
                SourceGlobalSequence: 51,
                SourceRootSequence: 51,
                SupersededAtUtc: Now.AddMinutes(2))));

        var applied = await store.UpsertSeedAsync(staleSeed);

        Assert.False(applied);
        Assert.Null(await store.GetAsync(runId, includeSuperseded: true));
    }

    [Fact]
    public void PostgreSql_keyset_and_latest_terminal_event_queries_translate()
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=test;Password=test")
            .Options;
        using var dbContext = new ProcessPersistenceDbContext(options);
        var cursorRunId = Guid.NewGuid();
        var cursorEndedAtUtc = Now;

        var keysetSql = dbContext.RunRecords
            .Where(record =>
                record.EndedAtUtc < cursorEndedAtUtc ||
                (record.EndedAtUtc == cursorEndedAtUtc &&
                 record.RunId.CompareTo(cursorRunId) < 0))
            .OrderByDescending(record => record.EndedAtUtc)
            .ThenByDescending(record => record.RunId)
            .Take(10)
            .ToQueryString();
        var latestEventSql = dbContext.RuntimeEvents
            .Where(runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunCompleted.Value)
            .GroupBy(runtimeEvent => new
            {
                runtimeEvent.RunId,
                runtimeEvent.EventType
            })
            .Select(group => group
                .OrderByDescending(runtimeEvent => runtimeEvent.GlobalSequence)
                .First())
            .ToQueryString();

        Assert.Contains("ORDER BY", keysetSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROW_NUMBER", latestEventSql, StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessRunRecordSeed NewSeed(
        ProcessRunId runId,
        ProcessRunDisposition disposition,
        long sourceSequence,
        DateTimeOffset? endedAtUtc = null)
    {
        return new ProcessRunRecordSeed(
            NewIdentity(runId),
            disposition,
            endedAtUtc ?? Now,
            sourceSequence,
            sourceSequence,
            endedAtUtc ?? Now);
    }

    private static ProcessRunRecordIdentity NewIdentity(ProcessRunId runId)
    {
        return new ProcessRunRecordIdentity(
            runId,
            runId,
            ParentRunId: null,
            new ProcessInstancePlanId(new Guid("20000000-0000-0000-0000-000000000001")),
            new ProcessDefinitionId(new Guid("30000000-0000-0000-0000-000000000001")),
            new ProcessDefinitionVersionId(new Guid("40000000-0000-0000-0000-000000000001")),
            new Guid("50000000-0000-0000-0000-000000000001"));
    }

    private static ProcessRunFactsCompletion NewFactsCompletion(
        ProcessRunId runId,
        ProcessRunFactsClaim claim)
    {
        var participantId = new ProcessRunParticipantId("agent:manager");
        var step = new ProcessRunStepFact(
            runId,
            new ProcessStepInstanceId(new Guid("60000000-0000-0000-0000-000000000001")),
            new ProcessStepDefinitionId(new Guid("70000000-0000-0000-0000-000000000001")),
            "test-step",
            ProcessRunStepOutcome.Completed,
            2,
            participantId,
            new Guid("80000000-0000-0000-0000-000000000001"),
            [],
            [new Guid("90000000-0000-0000-0000-000000000001")],
            Now.AddMinutes(-5),
            Now,
            300_000,
            20,
            5,
            15,
            2,
            42,
            1.5m,
            1.25m,
            3,
            1);
        var metrics = new ProcessRunRecordMetrics(
            Now.AddMinutes(-5),
            Now,
            300_000,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            20,
            5,
            15,
            2,
            42,
            1.5m,
            1.25m,
            3,
            1,
            0);
        var facts = new ProcessRunHardFacts(
            [step],
            [participantId],
            [new Guid("80000000-0000-0000-0000-000000000001")],
            [],
            [new Guid("90000000-0000-0000-0000-000000000001")],
            [new ArtifactInstanceId(new Guid("a0000000-0000-0000-0000-000000000001"))]);
        return new ProcessRunFactsCompletion(
            NewIdentity(runId),
            claim.SourceGlobalSequence,
            claim.ClaimToken,
            ProcessRunRecordCompleteness.Partial,
            ProcessRunEvidenceSource.RuntimeState |
            ProcessRunEvidenceSource.InstancePlan |
            ProcessRunEvidenceSource.StepAssignments |
            ProcessRunEvidenceSource.ExecutionObservations |
            ProcessRunEvidenceSource.UsageTelemetry,
            ProcessRunEvidenceSource.Pricing |
            ProcessRunEvidenceSource.RuntimeEvents |
            ProcessRunEvidenceSource.ArtifactLineage |
            ProcessRunEvidenceSource.Subprocesses,
            [ProcessRunRecordWarningCode.MissingPricing],
            metrics,
            facts,
            Now.AddMinutes(2));
    }

    private static ProcessRunNarrativeCompletion NewNarrativeCompletion(
        ProcessRunId runId,
        ProcessRunNarrativeClaim claim)
    {
        return new ProcessRunNarrativeCompletion(
            runId,
            claim.SourceGlobalSequence,
            claim.ClaimToken,
            new ProcessRunNarrative(
                "The run was reviewed.",
                "The expected result was produced.",
                ["Completed the test step."],
                ["Pricing evidence was unavailable."],
                ["Accepted the partial cost evidence."],
                ["Refresh pricing evidence."],
                new ProcessRunNarrativeProvenance(
                    new ProcessRunParticipantId("agent:manager"),
                    new Guid("b0000000-0000-0000-0000-000000000001"),
                    "process-run-summary:v1",
                    "test-model",
                    Now.AddMinutes(3))),
            Now.AddMinutes(3));
    }

    private static void AddRuntimeState(
        ProcessPersistenceDbContext dbContext,
        ProcessRunId runId,
        ProcessRuntimeStatus status,
        DateTimeOffset updatedAtUtc)
    {
        dbContext.RuntimeStates.Add(new ProcessRuntimeStateEntity
        {
            RunId = runId.Value,
            RootRunId = runId.Value,
            PlanId = Guid.NewGuid(),
            PlanHash = "hash:plan",
            Status = status,
            UpdatedAtUtc = updatedAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        });
    }

    private static void AddRuntimeEvent(
        ProcessPersistenceDbContext dbContext,
        ProcessRunId runId,
        string eventType,
        long globalSequence,
        DateTimeOffset occurredAtUtc)
    {
        dbContext.RuntimeEvents.Add(new ProcessRuntimeEventEntity
        {
            GlobalSequence = globalSequence,
            RootSequence = globalSequence,
            EventId = Guid.NewGuid(),
            RootRunId = runId.Value,
            RunId = runId.Value,
            CorrelationId = $"backfill:{runId}",
            ActorKind = "System",
            ActorId = "system",
            SchemaVersion = "1.0",
            Sensitivity = "Normal",
            OccurredAtUtc = occurredAtUtc,
            EventType = eventType,
            PayloadHash = $"hash:{globalSequence}"
        });
    }

    private static ProcessPersistenceDbContext CreateDbContext()
    {
        return CreateDbContext(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot());
    }

    private static ProcessPersistenceDbContext CreateDbContext(
        string databaseName,
        InMemoryDatabaseRoot databaseRoot)
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot)
            .Options;
        return new ProcessPersistenceDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
