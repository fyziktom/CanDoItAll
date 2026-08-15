using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectStructureProcessRunRecordIntegrationTests
{
    private static readonly DateTimeOffset EndedAtUtc = new(2026, 7, 24, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetStructureAsync_projects_completed_record_after_runtime_details_are_purged()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("process-run-record-project-structure");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = activeProfile
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var runRecordStore = scope.ServiceProvider.GetRequiredService<IProcessRunRecordStore>();
        var processDbContext = scope.ServiceProvider.GetRequiredService<ProcessPersistenceDbContext>();
        var projectId = await CreateProjectAsync(projects, "Durable archived process projection");
        var definitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("software-delivery"))
            .Value;
        var runId = ProcessRunId.New();
        var identity = CreateIdentity(runId, projectId, definitionId);

        await SeedCompletedRecordAsync(runRecordStore, identity);

        Assert.False(await processDbContext.RuntimeStates.AnyAsync(state => state.RunId == runId.Value));
        Assert.False(await processDbContext.RuntimeStepAssignments.AnyAsync(assignment => assignment.RunId == runId.Value));

        var surface = await workbench.GetStructureAsync(projectId);
        var definitionNodeId = ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(definitionId);
        var runNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId.Value);
        var summaryNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunSummaryNodeKey(runId.Value);
        var runNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, runNodeId, StringComparison.Ordinal));
        var summaryNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, summaryNodeId, StringComparison.Ordinal));

        Assert.Contains(surface.Nodes, node => string.Equals(node.Id, definitionNodeId, StringComparison.Ordinal));
        Assert.Equal(definitionNodeId, runNode.ParentId);
        Assert.Equal("Succeeded", runNode.Status);
        Assert.Equal(100, runNode.ProgressPercent);
        Assert.Contains("4/4 steps", runNode.Subtitle, StringComparison.Ordinal);
        Assert.Contains("1,525 tokens", runNode.Subtitle, StringComparison.Ordinal);
        Assert.Contains("agent:archive-manager", runNode.Notes, StringComparison.Ordinal);
        Assert.Equal(runNodeId, summaryNode.ParentId);
        Assert.Equal("Succeeded", summaryNode.Status);
        Assert.Contains("facts Completed", summaryNode.Notes, StringComparison.Ordinal);
        Assert.Contains("agent:archive-manager", summaryNode.Notes, StringComparison.Ordinal);

        using var metadata = JsonDocument.Parse(summaryNode.MetadataJson);
        var durableSummary = metadata.RootElement.GetProperty("processRunSummary");
        Assert.Equal("Completed", durableSummary.GetProperty("FactsStatus").GetString());
        Assert.Equal(
            JsonValueKind.Null,
            durableSummary.GetProperty("Identity").GetProperty("PlanId").ValueKind);
    }

    [Fact]
    public async Task ReadAnalyticsAsync_groups_completed_costs_by_utc_day_in_postgresql()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("process-run-record-daily-cost");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = activeProfile
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var runRecordStore = scope.ServiceProvider.GetRequiredService<IProcessRunRecordStore>();
        var projectId = await CreateProjectAsync(projects, "Daily process cost analytics");
        var definitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("software-delivery"))
            .Value;
        var midnightUtc = new DateTimeOffset(2026, 7, 24, 0, 0, 0, TimeSpan.Zero);

        await SeedCompletedRecordAsync(
            runRecordStore,
            CreateIdentity(ProcessRunId.New(), projectId, definitionId),
            midnightUtc.AddMinutes(-10),
            sourceSequence: 101,
            estimatedCost: 1.25m,
            actualCost: 1m);
        await SeedCompletedRecordAsync(
            runRecordStore,
            CreateIdentity(ProcessRunId.New(), projectId, definitionId),
            midnightUtc.AddMinutes(-1),
            sourceSequence: 102,
            estimatedCost: 2.75m,
            actualCost: 2m);
        await SeedCompletedRecordAsync(
            runRecordStore,
            CreateIdentity(ProcessRunId.New(), projectId, definitionId),
            midnightUtc.AddMinutes(5),
            sourceSequence: 103,
            estimatedCost: 4.5m,
            actualCost: 4m);

        var analytics = await runRecordStore.ReadAnalyticsAsync(
            new ProcessRunRecordAnalyticsQuery(
                midnightUtc.AddHours(-1),
                midnightUtc.AddHours(1))
            {
                ProjectId = projectId,
                RootRunsOnly = true,
                IncludeTotals = false,
                IncludeDailyCostTrend = true
            });

        Assert.Collection(
            analytics.DailyCostTrend,
            first =>
            {
                Assert.Equal(new DateOnly(2026, 7, 23), first.DayUtc);
                Assert.Equal(4m, first.EstimatedCost);
                Assert.Equal(3m, first.ActualCost);
            },
            second =>
            {
                Assert.Equal(new DateOnly(2026, 7, 24), second.DayUtc);
                Assert.Equal(4.5m, second.EstimatedCost);
                Assert.Equal(4m, second.ActualCost);
            });
    }

    [Fact]
    public async Task LoadAsync_caps_current_root_history_and_logs_older_records()
    {
        var projectId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var processDbContext = new ProcessPersistenceDbContext(options);
        var rootRecords = Enumerable
            .Range(0, 1001)
            .Select(index =>
            {
                var runId = Guid.NewGuid();
                return new ProcessRunRecordEntity
                {
                    RunId = runId,
                    RootRunId = runId,
                    ProjectId = projectId,
                    Disposition = ProcessRunDisposition.Succeeded,
                    LifecycleState = ProcessRunRecordLifecycleState.Current,
                    Completeness = ProcessRunRecordCompleteness.SeedOnly,
                    EndedAtUtc = EndedAtUtc.AddMinutes(-index),
                    MissingEvidenceSources = ProcessRunEvidenceSource.All,
                    FactsStatus = ProcessRunFactsStatus.Pending,
                    NarrativeStatus = ProcessRunNarrativeStatus.Pending,
                    SourceGlobalSequence = index + 1,
                    SourceRootSequence = index + 1,
                    SchemaVersion = ProcessRunRecordSchema.CurrentVersion,
                    UpdatedAtUtc = EndedAtUtc.AddMinutes(-index)
                };
            })
            .ToArray();
        var differentProjectRunId = Guid.NewGuid();
        var childRunId = Guid.NewGuid();
        processDbContext.RunRecords.AddRange(rootRecords);
        processDbContext.RunRecords.Add(new ProcessRunRecordEntity
        {
            RunId = differentProjectRunId,
            RootRunId = differentProjectRunId,
            ProjectId = Guid.NewGuid(),
            LifecycleState = ProcessRunRecordLifecycleState.Current,
            EndedAtUtc = EndedAtUtc.AddMinutes(2),
            FactsStatus = ProcessRunFactsStatus.Pending,
            NarrativeStatus = ProcessRunNarrativeStatus.Pending,
            SourceGlobalSequence = 2000,
            SourceRootSequence = 2000,
            SchemaVersion = ProcessRunRecordSchema.CurrentVersion,
            UpdatedAtUtc = EndedAtUtc.AddMinutes(2)
        });
        processDbContext.RunRecords.Add(new ProcessRunRecordEntity
        {
            RunId = childRunId,
            RootRunId = rootRecords[0].RunId,
            ProjectId = projectId,
            LifecycleState = ProcessRunRecordLifecycleState.Current,
            EndedAtUtc = EndedAtUtc.AddMinutes(1),
            FactsStatus = ProcessRunFactsStatus.Pending,
            NarrativeStatus = ProcessRunNarrativeStatus.Pending,
            SourceGlobalSequence = 2001,
            SourceRootSequence = 2001,
            SchemaVersion = ProcessRunRecordSchema.CurrentVersion,
            UpdatedAtUtc = EndedAtUtc.AddMinutes(1)
        });
        await processDbContext.SaveChangesAsync();
        var logger = new RecordingLogger<ProjectStructureProcessRunRecordProjector>();
        var projector = new ProjectStructureProcessRunRecordProjector(
            new ProcessRunRecordReader(new EfProcessRunRecordStore(processDbContext)),
            logger);

        var projections = await projector.LoadAsync(projectId, CancellationToken.None);

        Assert.Equal(1000, projections.Count);
        Assert.Contains(rootRecords[0].RunId, projections.Keys);
        Assert.DoesNotContain(rootRecords[^1].RunId, projections.Keys);
        Assert.DoesNotContain(differentProjectRunId, projections.Keys);
        Assert.DoesNotContain(childRunId, projections.Keys);
        Assert.Contains(
            logger.Messages,
            message =>
                message.Contains("1000 current-root record limit", StringComparison.Ordinal) &&
                message.Contains("process run history API", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ProcessRunFactsStatus.Pending)]
    [InlineData(ProcessRunFactsStatus.Failed)]
    public async Task GetStructureAsync_renders_pending_and_failed_durable_fact_status(
        ProcessRunFactsStatus factsStatus)
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("process-run-record-project-structure");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = activeProfile
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var runRecordStore = scope.ServiceProvider.GetRequiredService<IProcessRunRecordStore>();
        var projectId = await CreateProjectAsync(projects, $"Durable {factsStatus} process projection");
        var definitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("software-delivery"))
            .Value;
        var runId = ProcessRunId.New();
        var identity = CreateIdentity(runId, projectId, definitionId);

        Assert.True(await runRecordStore.UpsertSeedAsync(CreateSeed(identity, ProcessRunDisposition.Failed)));
        if (factsStatus == ProcessRunFactsStatus.Failed)
        {
            var claim = Assert.Single(
                await runRecordStore.ClaimFactsAsync(
                    new ProcessRunRecordClaimRequest(
                        EndedAtUtc.AddMinutes(1),
                        TimeSpan.FromMinutes(5),
                        10)),
                candidate => candidate.RunId == runId);
            Assert.True(await runRecordStore.FailFactsAsync(
                new ProcessRunStageFailure(
                    runId,
                    claim.SourceGlobalSequence,
                    claim.ClaimToken,
                    "ProjectionTestFailure",
                    "projection-test-diagnostic",
                    EndedAtUtc.AddMinutes(2),
                    NextAttemptAtUtc: null)));
        }

        var surface = await workbench.GetStructureAsync(projectId);
        var runNode = Assert.Single(
            surface.Nodes,
            node => string.Equals(
                node.Id,
                ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId.Value),
                StringComparison.Ordinal));
        var summaryNode = Assert.Single(
            surface.Nodes,
            node => string.Equals(
                node.Id,
                ProjectStructureProcessNodeKeys.BuildProcessRunSummaryNodeKey(runId.Value),
                StringComparison.Ordinal));

        Assert.Equal("Failed", runNode.Status);
        Assert.Equal("Failed", summaryNode.Status);
        Assert.Equal(
            $"Failed · durable facts {factsStatus.ToString().ToLowerInvariant()}",
            summaryNode.Subtitle);
        Assert.Contains($"Facts status: {factsStatus};", summaryNode.Notes, StringComparison.Ordinal);
        if (factsStatus == ProcessRunFactsStatus.Failed)
        {
            Assert.Contains("ProjectionTestFailure", summaryNode.Notes, StringComparison.Ordinal);
            Assert.Contains("projection-test-diagnostic", summaryNode.Notes, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("Background assembly is pending.", summaryNode.Notes, StringComparison.Ordinal);
        }
    }

    private static ProcessRunRecordIdentity CreateIdentity(
        ProcessRunId runId,
        Guid projectId,
        Guid definitionId)
    {
        return new ProcessRunRecordIdentity(
            runId,
            runId,
            ParentRunId: null,
            PlanId: null,
            new ProcessDefinitionId(definitionId),
            DefinitionVersionId: null,
            projectId);
    }

    private static ProcessRunRecordSeed CreateSeed(
        ProcessRunRecordIdentity identity,
        ProcessRunDisposition disposition)
    {
        return new ProcessRunRecordSeed(
            identity,
            disposition,
            EndedAtUtc,
            SourceGlobalSequence: 10,
            SourceRootSequence: 10,
            ObservedAtUtc: EndedAtUtc);
    }

    private static async Task SeedCompletedRecordAsync(
        IProcessRunRecordStore runRecordStore,
        ProcessRunRecordIdentity identity,
        DateTimeOffset? endedAtUtc = null,
        long sourceSequence = 10,
        decimal estimatedCost = 1.5m,
        decimal actualCost = 1.25m)
    {
        var completedAtUtc = endedAtUtc ?? EndedAtUtc;
        Assert.True(await runRecordStore.UpsertSeedAsync(
            new ProcessRunRecordSeed(
                identity,
                ProcessRunDisposition.Succeeded,
                completedAtUtc,
                sourceSequence,
                sourceSequence,
                completedAtUtc)));
        var claim = Assert.Single(
            await runRecordStore.ClaimFactsAsync(
                new ProcessRunRecordClaimRequest(
                    completedAtUtc.AddMinutes(1),
                    TimeSpan.FromMinutes(5),
                    10)),
            candidate => candidate.RunId == identity.RunId);
        var participantId = new ProcessRunParticipantId("agent:archive-manager");
        var metrics = new ProcessRunRecordMetrics(
            StartedAtUtc: completedAtUtc.AddMinutes(-12),
            completedAtUtc,
            DurationMilliseconds: 720_000,
            TotalStepCount: 4,
            ExecutableStepCount: 4,
            CompletedStepCount: 4,
            FailedStepCount: 0,
            CancelledStepCount: 0,
            RepetitionCount: 2,
            ExecutionCount: 5,
            ReworkCount: 1,
            IncidentCount: 0,
            EscalationCount: 0,
            InputTokenCount: 1_000,
            CachedInputTokenCount: 100,
            OutputTokenCount: 400,
            ReasoningTokenCount: 25,
            TotalTokenCount: 1_525,
            EstimatedCost: estimatedCost,
            ActualCost: actualCost,
            ToolCallCount: 7,
            ArtifactCount: 3,
            SubprocessCount: 1);
        var facts = new ProcessRunHardFacts(
            Steps: [],
            ParticipantIds: [participantId],
            WorkflowIds: [],
            SubprocessRunIds: [],
            ExecutionRunIds: [],
            ArtifactIds: []);

        Assert.True(await runRecordStore.CompleteFactsAsync(
            new ProcessRunFactsCompletion(
                identity,
                claim.SourceGlobalSequence,
                claim.ClaimToken,
                ProcessRunRecordCompleteness.Partial,
                ProcessRunEvidenceSource.None,
                ProcessRunEvidenceSource.All,
                [ProcessRunRecordWarningCode.MissingRuntimeEvents],
                metrics,
                facts,
                completedAtUtc.AddMinutes(2))));
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Execution"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
