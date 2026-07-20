using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkflowCatalogConcurrencyPersistenceIntegrationTests
{
    private const string PreviousMigration = "20260719161437_AddPromptGalleryFavoritesAndPreferences";

    [Fact]
    public async Task PostgreSql_ConcurrentDefinitionAndStatusWrites_FromSameHead_AppendExactlyOneVersion()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("workflowcatalogconcurrency");
        var factory = new WorkflowUsagePostgresDbContextFactory(database.CreateAppDbContextOptions());
        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        var gallery = CreateGallery(factory);
        var verificationCatalog = CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var firstCreateId = WorkflowId.New();
        using (var validator = new SynchronizingWorkflowDefinitionValidator())
        {
            var concurrentCatalog = CreateCatalog(factory, gallery, validator);
            var outcomes = await Task.WhenAll(
                Task.Run(() => CaptureAsync(() => concurrentCatalog.SaveDefinitionAsync(CreateSaveRequest(
                    firstCreateId,
                    expectedVersionId: null,
                    "First creator")))),
                Task.Run(() => CaptureAsync(() => concurrentCatalog.SaveDefinitionAsync(CreateSaveRequest(
                    firstCreateId,
                    expectedVersionId: null,
                    "Second creator")))));
            await AssertSinglePersistedWinnerAsync(
                factory,
                verificationCatalog,
                firstCreateId,
                outcomes,
                expectedRevisions: [1L]);
        }

        var savedHead = await verificationCatalog.SaveDefinitionAsync(CreateSaveRequest(name: "Save head"));
        using (var validator = new SynchronizingWorkflowDefinitionValidator())
        {
            var concurrentCatalog = CreateCatalog(factory, gallery, validator);
            var outcomes = await Task.WhenAll(
                Task.Run(() => CaptureAsync(() => concurrentCatalog.SaveDefinitionAsync(CreateSaveRequest(
                    savedHead.Id,
                    savedHead.VersionId,
                    "Writer one")))),
                Task.Run(() => CaptureAsync(() => concurrentCatalog.SaveDefinitionAsync(CreateSaveRequest(
                    savedHead.Id,
                    savedHead.VersionId,
                    "Writer two")))));
            var winner = await AssertSinglePersistedWinnerAsync(
                factory,
                verificationCatalog,
                savedHead.Id,
                outcomes,
                expectedRevisions: [1L, 2L]);
            var summary = Assert.Single(
                await verificationCatalog.ListDefinitionsAsync(),
                item => item.Id == savedHead.Id);
            Assert.Equal(winner.VersionId, summary.VersionId);
            var latestDraft = await verificationCatalog.GetLatestDefinitionByStatusAsync(
                savedHead.Id,
                WorkflowLifecycleStatus.Draft);
            Assert.Equal(winner.VersionId, latestDraft?.Definition.VersionId);
        }

        var statusHead = await verificationCatalog.SaveDefinitionAsync(CreateSaveRequest(name: "Status head"));
        using (var validator = new SynchronizingWorkflowDefinitionValidator())
        {
            var concurrentCatalog = CreateCatalog(factory, gallery, validator);
            var outcomes = await Task.WhenAll(
                Task.Run(() => CaptureAsync(() => concurrentCatalog.ChangeDefinitionStatusAsync(
                    new WorkflowDefinitionStatusChangeRequest(
                        statusHead.Id,
                        statusHead.VersionId,
                        WorkflowLifecycleStatus.Suspended)))),
                Task.Run(() => CaptureAsync(() => concurrentCatalog.ChangeDefinitionStatusAsync(
                    new WorkflowDefinitionStatusChangeRequest(
                        statusHead.Id,
                        statusHead.VersionId,
                        WorkflowLifecycleStatus.Archived)))));
            await AssertSinglePersistedWinnerAsync(
                factory,
                verificationCatalog,
                statusHead.Id,
                outcomes,
                expectedRevisions: [1L, 2L]);
            var latestDraft = await verificationCatalog.GetLatestDefinitionByStatusAsync(
                statusHead.Id,
                WorkflowLifecycleStatus.Draft);
            Assert.Equal(statusHead.VersionId, latestDraft?.Definition.VersionId);
        }
    }

    [Theory]
    [InlineData(WorkflowLifecycleStatus.Suspended)]
    [InlineData(WorkflowLifecycleStatus.Archived)]
    public async Task PostgreSql_InactiveCurrentHead_HidesHistoricalActiveVersion(
        WorkflowLifecycleStatus inactiveStatus)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("workflowcataloglifecycle");
        var factory = new WorkflowUsagePostgresDbContextFactory(database.CreateAppDbContextOptions());
        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        var gallery = CreateGallery(factory);
        var catalog = CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var active = await catalog.SaveDefinitionAsync(CreateSaveRequest(name: "Lifecycle workflow") with
        {
            Status = WorkflowLifecycleStatus.Active
        });
        var inactive = await catalog.ChangeDefinitionStatusAsync(new WorkflowDefinitionStatusChangeRequest(
            active.Id,
            active.VersionId,
            inactiveStatus));

        var current = await catalog.GetDefinitionAsync(active.Id);
        var historicalActive = await catalog.GetDefinitionAsync(active.Id, active.VersionId);
        var latestActive = await catalog.GetLatestDefinitionByStatusAsync(
            active.Id,
            WorkflowLifecycleStatus.Active);

        Assert.Equal(inactive.VersionId, current?.Definition.VersionId);
        Assert.Equal(inactiveStatus, current?.Definition.Status);
        Assert.Equal(active.VersionId, historicalActive?.Definition.VersionId);
        Assert.Equal(WorkflowLifecycleStatus.Active, historicalActive?.Definition.Status);
        Assert.Null(latestActive);
    }

    [Fact]
    public async Task PostgreSql_MigrationBackfill_UsesPreviousLatestOrderingAndSequentialRevisions()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("workflowcatalogheadmigration");
        var factory = new WorkflowUsagePostgresDbContextFactory(database.CreateAppDbContextOptions());
        var workflowId = Guid.NewGuid();
        var oldestVersionId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var lowerTiedVersionId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var latestVersionId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var createdAtUtc = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
        var latestUpdatedAtUtc = createdAtUtc.AddMinutes(1);

        await using var dbContext = factory.CreateDbContext();
        var migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AgentFramework_WorkflowDefinitions"
                ("VersionId", "WorkflowId", "Name", "Description", "Status", "PreferredBackend",
                 "DefinitionJson", "InstructionSnapshotSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES
                ({oldestVersionId}, {workflowId}, {"Oldest"}, {"Oldest version"},
                 {(int)WorkflowLifecycleStatus.Draft}, {(int)WorkflowRuntimeBackendKind.InProcess},
                 {"{}"}, {2}, {createdAtUtc}, {createdAtUtc}),
                ({lowerTiedVersionId}, {workflowId}, {"Lower tie"}, {"Lower tied version"},
                 {(int)WorkflowLifecycleStatus.Active}, {(int)WorkflowRuntimeBackendKind.InProcess},
                 {"{}"}, {2}, {createdAtUtc}, {latestUpdatedAtUtc}),
                ({latestVersionId}, {workflowId}, {"Latest tie"}, {"Latest tied version"},
                 {(int)WorkflowLifecycleStatus.Suspended}, {(int)WorkflowRuntimeBackendKind.InProcess},
                 {"{}"}, {2}, {createdAtUtc}, {latestUpdatedAtUtc});
            """);

        await migrator.MigrateAsync();

        var revisions = await dbContext.Set<WorkflowDefinitionRecord>()
            .Where(record => record.WorkflowId == workflowId)
            .ToDictionaryAsync(record => record.VersionId, record => record.Revision);
        Assert.Equal(1, revisions[oldestVersionId]);
        Assert.Equal(2, revisions[lowerTiedVersionId]);
        Assert.Equal(3, revisions[latestVersionId]);
        var head = await dbContext.Set<WorkflowDefinitionHeadRecord>()
            .SingleAsync(record => record.WorkflowId == workflowId);
        Assert.Equal(latestVersionId, head.VersionId);
    }

    private static async Task<WorkflowDefinition> AssertSinglePersistedWinnerAsync(
        WorkflowUsagePostgresDbContextFactory factory,
        PersistentWorkflowCatalogService verificationCatalog,
        WorkflowId workflowId,
        IReadOnlyCollection<WriteOutcome> outcomes,
        IReadOnlyList<long> expectedRevisions)
    {
        var winner = Assert.Single(outcomes, outcome => outcome.Definition is not null).Definition!;
        var conflict = Assert.IsType<InvalidOperationException>(
            Assert.Single(outcomes, outcome => outcome.Exception is not null).Exception);
        Assert.Contains("updated by another request", conflict.Message, StringComparison.OrdinalIgnoreCase);

        var detail = await verificationCatalog.GetDefinitionAsync(workflowId);
        Assert.NotNull(detail);
        Assert.Equal(winner.VersionId, detail.Definition.VersionId);

        await using var dbContext = factory.CreateDbContext();
        var versions = await dbContext.Set<WorkflowDefinitionRecord>()
            .Where(record => record.WorkflowId == workflowId.Value)
            .OrderBy(record => record.Revision)
            .ToArrayAsync();
        Assert.Equal(expectedRevisions, versions.Select(record => record.Revision));
        var head = await dbContext.Set<WorkflowDefinitionHeadRecord>()
            .SingleAsync(record => record.WorkflowId == workflowId.Value);
        Assert.Equal(winner.VersionId.Value, head.VersionId);
        return winner;
    }

    private static PersistentWorkflowCatalogService CreateCatalog(
        WorkflowUsagePostgresDbContextFactory factory,
        PromptsService gallery,
        IWorkflowDefinitionValidator validator)
        => new(factory, validator, gallery, gallery);

    private static PromptsService CreateGallery(WorkflowUsagePostgresDbContextFactory factory)
        => new(
            factory,
            new SystemClock(),
            new NullActivityStream(),
            new EfPromptGallerySearchDriver(factory),
            new PromptGalleryProjectionCoordinator(factory, new DisabledPromptGalleryProjectionDriver()),
            new PromptGalleryCompatibilityEvaluator(),
            NullLogger<PromptsService>.Instance);

    private static WorkflowDefinitionSaveRequest CreateSaveRequest(
        WorkflowId? workflowId = null,
        WorkflowVersionId? expectedVersionId = null,
        string name = "Concurrent workflow")
        => new(
            workflowId,
            expectedVersionId,
            name,
            "Atomic workflow definition save.",
            WorkflowLifecycleStatus.Draft,
            CreateGraph(),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false));

    private static WorkflowGraph CreateGraph()
    {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        return new WorkflowGraph(
            start,
            [CreateNode(start, WorkflowNodeKind.Start), CreateNode(end, WorkflowNodeKind.End)],
            [
                new WorkflowEdge(
                    new WorkflowEdgeId("start-end"),
                    start,
                    SourcePortId: null,
                    end,
                    TargetPortId: null,
                    WorkflowEdgeKind.Direct,
                    ConditionExpression: string.Empty)
            ]);
    }

    private static WorkflowNode CreateNode(WorkflowNodeId id, WorkflowNodeKind kind)
        => new(
            id,
            kind,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static async Task<WriteOutcome> CaptureAsync(Func<Task<WorkflowDefinition>> write)
    {
        try
        {
            return new WriteOutcome(await write(), Exception: null);
        }
        catch (Exception exception)
        {
            return new WriteOutcome(Definition: null, exception);
        }
    }

    private sealed record WriteOutcome(WorkflowDefinition? Definition, Exception? Exception);

    private sealed class SynchronizingWorkflowDefinitionValidator : IWorkflowDefinitionValidator, IDisposable
    {
        private readonly Barrier barrier = new(participantCount: 2);
        private readonly WorkflowDefinitionValidator inner = new();

        public WorkflowValidationResult Validate(
            WorkflowDefinition definition,
            IReadOnlyList<LlmCallComponent> components)
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15)))
            {
                throw new TimeoutException("Concurrent workflow writes did not reach validation together.");
            }

            return inner.Validate(definition, components);
        }

        public void Dispose() => barrier.Dispose();
    }
}
