using System.Net;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessRunRecordApiIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 24, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Record_routes_page_filter_and_map_only_safe_record_data()
    {
        var record = CreateRecord();
        var store = new RecordingRunRecordStore
        {
            Record = record,
            Page = new ProcessRunRecordPage(
                [record.Summary],
                new ProcessRunRecordCursor(record.Summary.Metrics.EndedAtUtc, record.Summary.Identity.RunId)),
            Analytics = new ProcessRunRecordAnalytics(
                1,
                1,
                1,
                0,
                0,
                record.Summary.Metrics.EndedAtUtc,
                record.Summary.SourceGlobalSequence,
                record.Summary.Metrics.DurationMilliseconds ?? 0,
                100,
                10,
                20,
                5,
                135,
                0.4m,
                0.3m,
                1,
                2,
                0,
                0,
                0,
                3,
                1,
                [new ProcessRunDispositionAnalytics(ProcessRunDisposition.Succeeded, 1)])
        };
        await using var host = await CreateHostAsync(store);
        var projectId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var rootRunId = Guid.NewGuid();

        using var listResponse = await host.Client.GetAsync(
            $"/api/processes/runs?projectId={projectId:D}&definitionId={definitionId:D}&rootRunId={rootRunId:D}&disposition=failed&participantId=agent-7&fromUtc=2026-07-01T00:00:00Z&toUtc=2026-08-01T00:00:00Z&take=20");
        var listJson = await listResponse.Content.ReadAsStringAsync();

        Assert.True(listResponse.IsSuccessStatusCode, listJson);
        using var listDocument = JsonDocument.Parse(listJson);
        var listedRecord = Assert.Single(listDocument.RootElement.GetProperty("records").EnumerateArray());
        Assert.Equal(record.Summary.Identity.RunId.Value, listedRecord.GetProperty("identity").GetProperty("runId").GetGuid());
        Assert.Equal(ProcessRunRecordSchema.CurrentVersion, listedRecord.GetProperty("schemaVersion").GetString());
        Assert.Equal("Complete", listedRecord.GetProperty("completeness").GetString());
        Assert.Equal("Completed", listedRecord.GetProperty("factsStatus").GetString());
        Assert.Equal("Completed", listedRecord.GetProperty("narrativeStatus").GetString());
        Assert.Equal(
            record.Summary.UpdatedAtUtc,
            listedRecord.GetProperty("recordUpdatedAtUtc").GetDateTimeOffset());
        Assert.False(listedRecord.TryGetProperty("freshnessAtUtc", out _));
        Assert.False(listedRecord.TryGetProperty("participantIds", out _));
        Assert.False(listedRecord.TryGetProperty("narrativePreview", out _));
        Assert.DoesNotContain("Completed the requested process", listJson, StringComparison.Ordinal);
        Assert.DoesNotContain("manager-agent", listJson, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-diagnostic-reference", listJson, StringComparison.Ordinal);
        Assert.DoesNotContain("lease", listJson, StringComparison.OrdinalIgnoreCase);
        var nextCursor = listDocument.RootElement.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(nextCursor));

        var listQuery = Assert.Single(store.ListQueries);
        Assert.Equal(projectId, listQuery.ProjectId);
        Assert.Equal(definitionId, listQuery.DefinitionId?.Value);
        Assert.Equal(rootRunId, listQuery.RootRunId?.Value);
        Assert.Equal(ProcessRunDisposition.Failed, listQuery.Disposition);
        Assert.Equal("agent-7", listQuery.ParticipantId?.Value);
        Assert.Equal(20, listQuery.Take);
        Assert.False(listQuery.IncludeSuperseded);

        store.Page = new ProcessRunRecordPage([], null);
        using var secondPageResponse = await host.Client.GetAsync(
            $"/api/processes/runs?cursor={Uri.EscapeDataString(nextCursor!)}&take=20");
        Assert.True(secondPageResponse.IsSuccessStatusCode, await secondPageResponse.Content.ReadAsStringAsync());
        Assert.NotNull(store.ListQueries[1].Cursor);

        using var summaryResponse = await host.Client.GetAsync(
            $"/api/processes/runs/{record.Summary.Identity.RunId.Value:D}/summary");
        var summaryJson = await summaryResponse.Content.ReadAsStringAsync();
        Assert.True(summaryResponse.IsSuccessStatusCode, summaryJson);
        using var summaryDocument = JsonDocument.Parse(summaryJson);
        var factsElement = summaryDocument.RootElement.GetProperty("facts");
        Assert.Equal(2, factsElement.GetProperty("steps").GetArrayLength());
        Assert.Equal(2, factsElement.GetProperty("stepPage").GetProperty("totalCount").GetInt32());
        Assert.False(factsElement.GetProperty("stepPage").GetProperty("hasMore").GetBoolean());
        Assert.Equal(6, factsElement.GetProperty("totalRuntimeEventCount").GetInt32());
        Assert.Equal(2, factsElement.GetProperty("managerRuntimeEventCount").GetInt32());
        var runtimeEventMinuteBuckets = factsElement
            .GetProperty("runtimeEventMinuteBuckets")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(3, runtimeEventMinuteBuckets.Length);
        Assert.Equal(
            3,
            factsElement
                .GetProperty("runtimeEventMinuteBucketPage")
                .GetProperty("totalCount")
                .GetInt32());
        Assert.False(
            factsElement
                .GetProperty("runtimeEventMinuteBucketPage")
                .GetProperty("hasMore")
                .GetBoolean());
        var managerCategory = Assert.Single(
            factsElement
                .GetProperty("runtimeEventCategories")
                .EnumerateArray(),
            category => category.GetProperty("category").GetString() == "Manager");
        Assert.Equal(2, managerCategory.GetProperty("eventCount").GetInt32());
        Assert.All(
            runtimeEventMinuteBuckets,
            bucket => Assert.All(
                bucket.EnumerateObject(),
                property => Assert.True(
                    property.Name is
                        "minuteUtc" or
                        "eventCount" or
                        "managerEventCount" or
                        "durationMilliseconds")));
        Assert.All(
            factsElement.GetProperty("runtimeEventCategories").EnumerateArray(),
            category => Assert.All(
                category.EnumerateObject(),
                property => Assert.True(
                    property.Name is
                        "category" or
                        "eventCount" or
                        "firstOccurredAtUtc" or
                        "lastOccurredAtUtc")));
        Assert.Equal(
            "manager-agent",
            summaryDocument.RootElement
                .GetProperty("narrative")
                .GetProperty("provenance")
                .GetProperty("managerAgentId")
                .GetString());
        Assert.All(
            summaryDocument.RootElement.GetProperty("facts").GetProperty("steps").EnumerateArray(),
            step => Assert.Equal(
                record.Summary.Identity.RunId.Value,
                step.GetProperty("owningRunId").GetGuid()));
        Assert.DoesNotContain("secret-diagnostic-reference", summaryJson, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnostic", summaryJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "InternalFailure",
            summaryDocument.RootElement
                .GetProperty("summary")
                .GetProperty("factsLastErrorClass")
                .GetString());
        Assert.Equal(
            "InternalFailure",
            summaryDocument.RootElement
                .GetProperty("summary")
                .GetProperty("narrativeLastErrorClass")
                .GetString());
        Assert.DoesNotContain("\"resultSummary\"", summaryJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payloadHash", summaryJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eventName", summaryJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eventDetails", summaryJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eventActor", summaryJson, StringComparison.OrdinalIgnoreCase);

        using var pagedSummaryResponse = await host.Client.GetAsync(
            $"/api/processes/runs/{record.Summary.Identity.RunId.Value:D}/summary?stepOffset=1&stepTake=1&runtimeEventMinuteOffset=1&runtimeEventMinuteTake=1");
        using var pagedSummaryDocument = JsonDocument.Parse(
            await pagedSummaryResponse.Content.ReadAsStringAsync());
        Assert.True(pagedSummaryResponse.IsSuccessStatusCode);
        var pagedFacts = pagedSummaryDocument.RootElement.GetProperty("facts");
        Assert.Single(pagedFacts.GetProperty("steps").EnumerateArray());
        Assert.Equal(1, pagedFacts.GetProperty("stepPage").GetProperty("offset").GetInt32());
        Assert.Equal(1, pagedFacts.GetProperty("stepPage").GetProperty("take").GetInt32());
        Assert.False(pagedFacts.GetProperty("stepPage").GetProperty("hasMore").GetBoolean());
        Assert.Single(pagedFacts.GetProperty("runtimeEventMinuteBuckets").EnumerateArray());
        Assert.Equal(
            1,
            pagedFacts
                .GetProperty("runtimeEventMinuteBucketPage")
                .GetProperty("offset")
                .GetInt32());
        Assert.Equal(
            1,
            pagedFacts
                .GetProperty("runtimeEventMinuteBucketPage")
                .GetProperty("take")
                .GetInt32());
        Assert.True(
            pagedFacts
                .GetProperty("runtimeEventMinuteBucketPage")
                .GetProperty("hasMore")
                .GetBoolean());

        using var graphResponse = await host.Client.GetAsync(
            $"/api/processes/runs/{record.Summary.Identity.RunId.Value:D}/graph");
        using var graphDocument = JsonDocument.Parse(await graphResponse.Content.ReadAsStringAsync());
        Assert.True(graphResponse.IsSuccessStatusCode);
        Assert.Equal(2, graphDocument.RootElement.GetProperty("nodes").GetArrayLength());
        Assert.All(
            graphDocument.RootElement.GetProperty("nodes").EnumerateArray(),
            node => Assert.Equal(
                record.Summary.Identity.RunId.Value,
                node.GetProperty("owningRunId").GetGuid()));
        Assert.Single(graphDocument.RootElement.GetProperty("edges").EnumerateArray());
        Assert.Equal(
            2,
            graphDocument.RootElement.GetProperty("nodePage").GetProperty("totalCount").GetInt32());
        Assert.DoesNotContain(
            "\"resultSummary\"",
            graphDocument.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);

        using var analyticsResponse = await host.Client.GetAsync(
            "/api/processes/runs/analytics?fromUtc=2026-07-01T00:00:00Z&toUtc=2026-08-01T00:00:00Z&participantId=agent-7");
        using var analyticsDocument = JsonDocument.Parse(await analyticsResponse.Content.ReadAsStringAsync());
        Assert.True(analyticsResponse.IsSuccessStatusCode);
        Assert.Equal(
            ProcessRunRecordSchema.CurrentVersion,
            analyticsDocument.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(1, analyticsDocument.RootElement.GetProperty("matchingRunCount").GetInt32());
        Assert.Equal(1, analyticsDocument.RootElement.GetProperty("factsAvailableRunCount").GetInt32());
        Assert.Equal(1, analyticsDocument.RootElement.GetProperty("evidenceCompleteRunCount").GetInt32());
        Assert.Equal(0, analyticsDocument.RootElement.GetProperty("evidencePartialRunCount").GetInt32());
        Assert.Equal(0, analyticsDocument.RootElement.GetProperty("factsUnavailableRunCount").GetInt32());
        Assert.False(analyticsDocument.RootElement.TryGetProperty("runCount", out _));
        var disposition = Assert.Single(
            analyticsDocument.RootElement.GetProperty("dispositions").EnumerateArray());
        Assert.Equal(1, disposition.GetProperty("matchingRunCount").GetInt32());
        Assert.False(disposition.TryGetProperty("runCount", out _));
        Assert.Equal(
            record.Summary.Metrics.EndedAtUtc,
            analyticsDocument.RootElement.GetProperty("dataThroughUtc").GetDateTimeOffset());
        Assert.Equal(
            record.Summary.SourceGlobalSequence,
            analyticsDocument.RootElement
                .GetProperty("sourceGlobalSequenceWatermark")
                .GetInt64());
        Assert.False(analyticsDocument.RootElement.TryGetProperty("freshnessAtUtc", out _));
        Assert.Single(store.AnalyticsQueries);
        Assert.Equal("agent-7", store.AnalyticsQueries[0].ParticipantId?.Value);
        Assert.Equal(0, store.MutationCallCount);
    }

    [Fact]
    public async Task Record_routes_return_predictable_validation_and_not_found_responses()
    {
        var store = new RecordingRunRecordStore();
        await using var host = await CreateHostAsync(store);
        var missingRunId = Guid.NewGuid();

        using var invalidDisposition = await host.Client.GetAsync("/api/processes/runs?disposition=unknown");
        using var invalidCursor = await host.Client.GetAsync("/api/processes/runs?cursor=invalid");
        using var reversedRange = await host.Client.GetAsync(
            "/api/processes/runs?fromUtc=2026-08-01T00:00:00Z&toUtc=2026-07-01T00:00:00Z");
        using var emptyRunId = await host.Client.GetAsync(
            $"/api/processes/runs/{Guid.Empty:D}/summary");
        using var missingSummary = await host.Client.GetAsync(
            $"/api/processes/runs/{missingRunId:D}/summary");
        using var missingGraph = await host.Client.GetAsync(
            $"/api/processes/runs/{missingRunId:D}/graph");
        using var invalidSummaryPage = await host.Client.GetAsync(
            $"/api/processes/runs/{missingRunId:D}/summary?stepOffset=-1");
        using var invalidGraphPage = await host.Client.GetAsync(
            $"/api/processes/runs/{missingRunId:D}/graph?stepTake=201");
        using var invalidRuntimeEventMinutePage = await host.Client.GetAsync(
            $"/api/processes/runs/{missingRunId:D}/summary?runtimeEventMinuteTake=201");

        Assert.Equal(HttpStatusCode.BadRequest, invalidDisposition.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCursor.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reversedRange.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyRunId.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingSummary.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingGraph.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSummaryPage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidGraphPage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRuntimeEventMinutePage.StatusCode);
        Assert.Equal(
            "process.run_record_query_invalid",
            await ReadFirstErrorCodeAsync(invalidDisposition));
        Assert.Equal(
            "process.run_record_not_found",
            await ReadFirstErrorCodeAsync(missingSummary));
        Assert.Empty(store.ListQueries);
        Assert.Equal(2, store.GetCallCount);
        Assert.Equal(0, store.MutationCallCount);
    }

    private static async Task<string?> ReadFirstErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement
            .GetProperty("errors")[0]
            .GetProperty("code")
            .GetString();
    }

    private static Task<ApiTestHost> CreateHostAsync(RecordingRunRecordStore store)
    {
        return ApiTestHost.CreateAsync(
            jwtEnabled: false,
            services =>
            {
                services.RemoveAll<IProcessRunRecordStore>();
                services.RemoveAll<ProcessRunRecordQueryService>();
                services.AddSingleton<IProcessRunRecordStore>(store);
                services.AddScoped<ProcessRunRecordQueryService>();
            },
            useInMemoryDatabase: true);
    }

    private static ProcessRunRecord CreateRecord()
    {
        var runId = ProcessRunId.New();
        var participantId = new ProcessRunParticipantId("manager-agent");
        var firstStepId = ProcessStepInstanceId.New();
        var secondStepId = ProcessStepInstanceId.New();
        var firstStep = CreateStep(runId, firstStepId, "prepare", participantId, []);
        var secondStep = CreateStep(runId, secondStepId, "complete", participantId, [firstStepId]);
        var metrics = new ProcessRunRecordMetrics(
            Now.AddMinutes(-10),
            Now,
            600_000,
            2,
            2,
            2,
            0,
            0,
            1,
            2,
            0,
            0,
            0,
            100,
            10,
            20,
            5,
            135,
            0.4m,
            0.3m,
            3,
            1,
            0);
        var narrative = new ProcessRunNarrative(
            "Completed the requested process.",
            "Succeeded",
            ["Prepared the inputs.", "Completed the work."],
            [],
            ["Used validated evidence."],
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
                runId,
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
            "InternalFailure",
            "secret-diagnostic-reference",
            ProcessRunNarrativeStatus.Completed,
            1,
            null,
            "InternalFailure",
            "secret-diagnostic-reference",
            metrics,
            [participantId],
            narrative,
            10,
            10,
            ProcessRunRecordSchema.CurrentVersion,
            Now);
        return new ProcessRunRecord(
            summary,
            new ProcessRunHardFacts(
                [firstStep, secondStep],
                [participantId],
                [Guid.NewGuid()],
                [],
                [Guid.NewGuid()],
                [ArtifactInstanceId.New()])
            {
                TotalRuntimeEventCount = 6,
                ManagerRuntimeEventCount = 2,
                RuntimeEventMinuteBuckets =
                [
                    new ProcessRunRuntimeEventMinuteBucket(
                        Now.AddMinutes(-3),
                        2,
                        0,
                        0),
                    new ProcessRunRuntimeEventMinuteBucket(
                        Now.AddMinutes(-2),
                        3,
                        1,
                        60_000),
                    new ProcessRunRuntimeEventMinuteBucket(
                        Now.AddMinutes(-1),
                        1,
                        1,
                        60_000)
                ],
                RuntimeEventCategories =
                [
                    new ProcessRunRuntimeEventCategoryAggregate(
                        ProcessRunRuntimeEventCategory.RunLifecycle,
                        2,
                        Now.AddMinutes(-3),
                        Now.AddMinutes(-1)),
                    new ProcessRunRuntimeEventCategoryAggregate(
                        ProcessRunRuntimeEventCategory.Step,
                        2,
                        Now.AddMinutes(-2),
                        Now.AddMinutes(-2)),
                    new ProcessRunRuntimeEventCategoryAggregate(
                        ProcessRunRuntimeEventCategory.Manager,
                        2,
                        Now.AddMinutes(-2),
                        Now.AddMinutes(-1))
                ]
            });
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
            null,
            dependencies,
            [Guid.NewGuid()],
            Now.AddMinutes(-10),
            Now,
            600_000,
            50,
            5,
            10,
            2,
            67,
            0.2m,
            0.15m,
            1,
            1);
    }

    private sealed class RecordingRunRecordStore : IProcessRunRecordStore
    {
        public ProcessRunRecordPage Page { get; set; } = new([], null);

        public ProcessRunRecordAnalytics Analytics { get; set; } = new(
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

        public ProcessRunRecord? Record { get; set; }

        public List<ProcessRunRecordListQuery> ListQueries { get; } = [];

        public List<ProcessRunRecordAnalyticsQuery> AnalyticsQueries { get; } = [];

        public int GetCallCount { get; private set; }

        public int MutationCallCount { get; private set; }

        public Task<ProcessRunRecord?> GetAsync(
            ProcessRunId runId,
            bool includeSuperseded = false,
            CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            return Task.FromResult(
                !includeSuperseded && Record?.Summary.Identity.RunId == runId
                    ? Record
                    : null);
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
            => Mutation<bool>();

        public Task<bool> SupersedeAsync(
            ProcessRunRecordSupersession supersession,
            CancellationToken cancellationToken = default)
            => Mutation<bool>();

        public Task<IReadOnlyList<ProcessRunFactsClaim>> ClaimFactsAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default)
            => Mutation<IReadOnlyList<ProcessRunFactsClaim>>();

        public Task<bool> CompleteFactsAsync(
            ProcessRunFactsCompletion completion,
            CancellationToken cancellationToken = default)
            => Mutation<bool>();

        public Task<bool> FailFactsAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default)
            => Mutation<bool>();

        public Task<IReadOnlyList<ProcessRunNarrativeClaim>> ClaimNarrativesAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default)
            => Mutation<IReadOnlyList<ProcessRunNarrativeClaim>>();

        public Task<bool> CompleteNarrativeAsync(
            ProcessRunNarrativeCompletion completion,
            CancellationToken cancellationToken = default)
            => Mutation<bool>();

        public Task<bool> FailNarrativeAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default)
            => Mutation<bool>();

        private Task<T> Mutation<T>()
        {
            MutationCallCount++;
            throw new InvalidOperationException("Record-backed HTTP reads cannot invoke store mutations or claims.");
        }
    }
}
