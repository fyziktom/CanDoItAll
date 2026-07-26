using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRunRecordQueryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 24, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_maps_filters_and_round_trips_opaque_keyset_cursor()
    {
        var record = CreateRecord();
        var storeCursor = new ProcessRunRecordCursor(record.Summary.Metrics.EndedAtUtc, record.Summary.Identity.RunId);
        var store = new RecordingStore
        {
            Page = new ProcessRunRecordPage([record.Summary], storeCursor)
        };
        var service = new ProcessRunRecordQueryService(store);
        var definitionId = ProcessDefinitionId.New();
        var rootRunId = ProcessRunId.New();
        var projectId = Guid.NewGuid();
        var participantId = new ProcessRunParticipantId("agent-42");

        var firstPage = await service.ListAsync(new ProcessRunRecordSearchQuery(25)
        {
            ProjectId = projectId,
            DefinitionId = definitionId,
            RootRunId = rootRunId,
            Disposition = ProcessRunDisposition.Escalated,
            ParticipantId = participantId,
            EndedFromUtc = Now.AddDays(-2).ToOffset(TimeSpan.FromHours(-4)),
            EndedBeforeUtc = Now.AddDays(1).ToOffset(TimeSpan.FromHours(2))
        });

        var firstQuery = Assert.Single(store.ListQueries);
        Assert.Equal(25, firstQuery.Take);
        Assert.Equal(ProcessRunRecordListPayload.Compact, firstQuery.Payload);
        Assert.Equal(projectId, firstQuery.ProjectId);
        Assert.Equal(definitionId, firstQuery.DefinitionId);
        Assert.Equal(rootRunId, firstQuery.RootRunId);
        Assert.Equal(ProcessRunDisposition.Escalated, firstQuery.Disposition);
        Assert.Equal(participantId, firstQuery.ParticipantId);
        Assert.Equal(TimeSpan.Zero, firstQuery.EndedFromUtc?.Offset);
        Assert.Equal(TimeSpan.Zero, firstQuery.EndedBeforeUtc?.Offset);
        Assert.NotNull(firstPage.NextCursor);
        Assert.DoesNotContain(record.Summary.Identity.RunId.Value.ToString("D"), firstPage.NextCursor, StringComparison.OrdinalIgnoreCase);

        store.Page = new ProcessRunRecordPage([], null);
        await service.ListAsync(new ProcessRunRecordSearchQuery(25)
        {
            Cursor = firstPage.NextCursor
        });

        var secondQuery = store.ListQueries[1];
        Assert.Equal(storeCursor, secondQuery.Cursor);
        Assert.False(secondQuery.IncludeSuperseded);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(201, null)]
    [InlineData(10, "not-a-cursor")]
    [InlineData(10, "")]
    public async Task List_rejects_invalid_bounds_or_cursor_without_calling_store(
        int take,
        string? cursor)
    {
        var store = new RecordingStore();
        var service = new ProcessRunRecordQueryService(store);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.ListAsync(
            new ProcessRunRecordSearchQuery(take)
            {
                Cursor = cursor
            }));

        Assert.Empty(store.ListQueries);
    }

    [Fact]
    public async Task List_rejects_reversed_date_range_without_calling_store()
    {
        var store = new RecordingStore();
        var service = new ProcessRunRecordQueryService(store);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(
            new ProcessRunRecordSearchQuery
            {
                EndedFromUtc = Now,
                EndedBeforeUtc = Now
            }));

        Assert.Empty(store.ListQueries);
    }

    [Fact]
    public async Task List_never_exposes_more_records_than_requested()
    {
        var record = CreateRecord();
        var store = new RecordingStore
        {
            Page = new ProcessRunRecordPage(
                [record.Summary, record.Summary],
                null)
        };
        var service = new ProcessRunRecordQueryService(store);

        var result = await service.ListAsync(new ProcessRunRecordSearchQuery(1));

        Assert.Single(result.Records);
    }

    [Fact]
    public async Task List_rejects_default_typed_filters_without_calling_store()
    {
        var store = new RecordingStore();
        var service = new ProcessRunRecordQueryService(store);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(
            new ProcessRunRecordSearchQuery
            {
                DefinitionId = default(ProcessDefinitionId)
            }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(
            new ProcessRunRecordSearchQuery
            {
                RootRunId = default(ProcessRunId)
            }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(
            new ProcessRunRecordSearchQuery
            {
                ParticipantId = default(ProcessRunParticipantId)
            }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListAsync(
            new ProcessRunRecordSearchQuery
            {
                Disposition = (ProcessRunDisposition)int.MaxValue
            }));

        Assert.Empty(store.ListQueries);
    }

    [Fact]
    public async Task Analytics_maps_filters_and_rejects_unbounded_window()
    {
        var store = new RecordingStore
        {
            Analytics = EmptyAnalytics()
        };
        var service = new ProcessRunRecordQueryService(store);
        var projectId = Guid.NewGuid();
        var definitionId = ProcessDefinitionId.New();
        var rootRunId = ProcessRunId.New();
        var participantId = new ProcessRunParticipantId("manager-agent");

        var result = await service.ReadAnalyticsAsync(new ProcessRunRecordAnalyticsRequest(
            Now.AddDays(-10).ToOffset(TimeSpan.FromHours(-4)),
            Now.ToOffset(TimeSpan.FromHours(2)))
        {
            ProjectId = projectId,
            DefinitionId = definitionId,
            RootRunId = rootRunId,
            ParticipantId = participantId
        });

        Assert.Same(store.Analytics, result);
        var query = Assert.Single(store.AnalyticsQueries);
        Assert.Equal(TimeSpan.Zero, query.FromUtc.Offset);
        Assert.Equal(TimeSpan.Zero, query.ToUtc.Offset);
        Assert.Equal(projectId, query.ProjectId);
        Assert.Equal(definitionId, query.DefinitionId);
        Assert.Equal(rootRunId, query.RootRunId);
        Assert.Equal(participantId, query.ParticipantId);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ReadAnalyticsAsync(
            new ProcessRunRecordAnalyticsRequest(Now.AddDays(-367), Now)));
        Assert.Single(store.AnalyticsQueries);
    }

    [Fact]
    public async Task Graph_derives_dependency_edges_from_record_facts_and_missing_record_is_null()
    {
        var record = CreateRecord();
        var store = new RecordingStore
        {
            Record = record
        };
        var service = new ProcessRunRecordQueryService(store);

        var graph = await service.GetGraphAsync(record.Summary.Identity.RunId);

        Assert.NotNull(graph);
        Assert.Equal(2, graph.Nodes.Count);
        Assert.Equal(2, graph.TotalNodeCount);
        Assert.Equal(0, graph.StepOffset);
        Assert.Equal(ProcessRunRecordQueryService.DefaultGraphStepPageSize, graph.StepTake);
        Assert.False(graph.HasMoreNodes);
        Assert.All(
            graph.Nodes,
            node => Assert.Equal(record.Summary.Identity.RunId, node.OwningRunId));
        var edge = Assert.Single(graph.Edges);
        Assert.Equal(record.Facts!.Steps[0].StepInstanceId, edge.SourceStepInstanceId);
        Assert.Equal(record.Facts.Steps[1].StepInstanceId, edge.TargetStepInstanceId);
        Assert.Equal(ProcessRunRecordGraphEdgeKind.Dependency, edge.Kind);
        Assert.Equal(record.Facts.SubprocessRunIds, graph.SubprocessRunIds);
        Assert.Equal(1, store.GetCallCount);
        Assert.Empty(store.ListQueries);
        Assert.Empty(store.AnalyticsQueries);

        var secondNodePage = await service.GetGraphAsync(
            record.Summary.Identity.RunId,
            stepOffset: 1,
            stepTake: 1);

        Assert.NotNull(secondNodePage);
        Assert.Single(secondNodePage.Nodes);
        Assert.Equal(2, secondNodePage.TotalNodeCount);
        Assert.Equal(1, secondNodePage.StepOffset);
        Assert.Equal(1, secondNodePage.StepTake);
        Assert.False(secondNodePage.HasMoreNodes);
        Assert.Empty(secondNodePage.Edges);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetGraphAsync(
            record.Summary.Identity.RunId,
            stepOffset: -1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetGraphAsync(
            record.Summary.Identity.RunId,
            stepTake: ProcessRunRecordQueryService.MaximumGraphStepPageSize + 1));

        store.Record = null;
        Assert.Null(await service.GetGraphAsync(ProcessRunId.New()));
        Assert.Equal(3, store.GetCallCount);
    }

    private static ProcessRunRecord CreateRecord()
    {
        var runId = ProcessRunId.New();
        var rootRunId = ProcessRunId.New();
        var participantId = new ProcessRunParticipantId("manager-agent");
        var firstStepId = ProcessStepInstanceId.New();
        var secondStepId = ProcessStepInstanceId.New();
        var firstStep = CreateStep(runId, firstStepId, "prepare", participantId, []);
        var secondStep = CreateStep(runId, secondStepId, "finish", participantId, [firstStepId]);
        var narrative = new ProcessRunNarrative(
            "The run completed.",
            "Succeeded",
            ["Prepared inputs.", "Produced output."],
            [],
            ["Used the validated plan."],
            [],
            new ProcessRunNarrativeProvenance(
                participantId,
                Guid.NewGuid(),
                "manager-summary-v1",
                "test-model",
                Now));
        var summary = new ProcessRunRecordSummary(
            new ProcessRunRecordIdentity(
                runId,
                rootRunId,
                null,
                ProcessInstancePlanId.New(),
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                Guid.NewGuid()),
            ProcessRunDisposition.Succeeded,
            ProcessRunRecordLifecycleState.Current,
            ProcessRunRecordCompleteness.Complete,
            ProcessRunEvidenceSource.All,
            ProcessRunEvidenceSource.None,
            [],
            ProcessRunFactsStatus.Completed,
            1,
            null,
            null,
            null,
            ProcessRunNarrativeStatus.Completed,
            1,
            null,
            null,
            null,
            CreateMetrics(),
            [participantId],
            narrative,
            42,
            10,
            ProcessRunRecordSchema.CurrentVersion,
            Now);
        var facts = new ProcessRunHardFacts(
            [firstStep, secondStep],
            [participantId],
            [Guid.NewGuid()],
            [ProcessRunId.New()],
            [Guid.NewGuid()],
            [ArtifactInstanceId.New()]);
        return new ProcessRunRecord(summary, facts);
    }

    private static ProcessRunStepFact CreateStep(
        ProcessRunId owningRunId,
        ProcessStepInstanceId stepId,
        string stepKey,
        ProcessRunParticipantId participantId,
        IReadOnlyList<ProcessStepInstanceId> dependencies)
    {
        return new ProcessRunStepFact(
            owningRunId,
            stepId,
            ProcessStepDefinitionId.New(),
            stepKey,
            ProcessRunStepOutcome.Completed,
            1,
            participantId,
            Guid.NewGuid(),
            dependencies,
            [Guid.NewGuid()],
            Now.AddMinutes(-10),
            Now.AddMinutes(-5),
            300_000,
            100,
            20,
            30,
            10,
            160,
            0.25m,
            0.20m,
            2,
            1);
    }

    private static ProcessRunRecordMetrics CreateMetrics()
    {
        return new ProcessRunRecordMetrics(
            Now.AddMinutes(-20),
            Now,
            1_200_000,
            2,
            2,
            2,
            0,
            0,
            0,
            2,
            0,
            0,
            0,
            200,
            40,
            60,
            20,
            320,
            0.50m,
            0.40m,
            4,
            2,
            1);
    }

    private static ProcessRunRecordAnalytics EmptyAnalytics()
    {
        return new ProcessRunRecordAnalytics(
            MatchingRunCount: 0,
            FactsAvailableRunCount: 0,
            EvidenceCompleteRunCount: 0,
            EvidencePartialRunCount: 0,
            FactsUnavailableRunCount: 0,
            LatestEndedAtUtc: null,
            MaximumSourceGlobalSequence: null,
            DurationMilliseconds: 0,
            InputTokenCount: 0,
            CachedInputTokenCount: 0,
            OutputTokenCount: 0,
            ReasoningTokenCount: 0,
            TotalTokenCount: 0,
            EstimatedCost: 0,
            ActualCost: 0,
            RepetitionCount: 0,
            ExecutionCount: 0,
            ReworkCount: 0,
            IncidentCount: 0,
            EscalationCount: 0,
            ToolCallCount: 0,
            ArtifactCount: 0,
            Dispositions: []);
    }

    private sealed class RecordingStore : IProcessRunRecordStore
    {
        public ProcessRunRecordPage Page { get; set; } = new([], null);

        public ProcessRunRecordAnalytics Analytics { get; set; } = EmptyAnalytics();

        public ProcessRunRecord? Record { get; set; }

        public List<ProcessRunRecordListQuery> ListQueries { get; } = [];

        public List<ProcessRunRecordAnalyticsQuery> AnalyticsQueries { get; } = [];

        public int GetCallCount { get; private set; }

        public Task<ProcessRunRecord?> GetAsync(
            ProcessRunId runId,
            bool includeSuperseded = false,
            CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            return Task.FromResult(Record?.Summary.Identity.RunId == runId ? Record : null);
        }

        public Task<ProcessRunRecordPage> ListAsync(
            ProcessRunRecordListQuery query,
            CancellationToken cancellationToken = default)
        {
            ListQueries.Add(query);
            return Task.FromResult(Page);
        }

        public Task<ProcessRunRecordAnalytics> ReadAnalyticsAsync(
            ProcessRunRecordAnalyticsQuery query,
            CancellationToken cancellationToken = default)
        {
            AnalyticsQueries.Add(query);
            return Task.FromResult(Analytics);
        }

        public Task<bool> UpsertSeedAsync(
            ProcessRunRecordSeed seed,
            CancellationToken cancellationToken = default)
            => throw UnexpectedMutation();

        public Task<bool> SupersedeAsync(
            ProcessRunRecordSupersession supersession,
            CancellationToken cancellationToken = default)
            => throw UnexpectedMutation();

        public Task<IReadOnlyList<ProcessRunFactsClaim>> ClaimFactsAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default)
            => throw UnexpectedMutation();

        public Task<bool> CompleteFactsAsync(
            ProcessRunFactsCompletion completion,
            CancellationToken cancellationToken = default)
            => throw UnexpectedMutation();

        public Task<bool> FailFactsAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default)
            => throw UnexpectedMutation();

        public Task<IReadOnlyList<ProcessRunNarrativeClaim>> ClaimNarrativesAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default)
            => throw UnexpectedMutation();

        public Task<bool> CompleteNarrativeAsync(
            ProcessRunNarrativeCompletion completion,
            CancellationToken cancellationToken = default)
            => throw UnexpectedMutation();

        public Task<bool> FailNarrativeAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default)
            => throw UnexpectedMutation();

        private static InvalidOperationException UnexpectedMutation()
            => new("Run-record query tests must not invoke mutation or claim operations.");
    }
}
