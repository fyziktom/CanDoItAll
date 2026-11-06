using System.Text.Json;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Authoring;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowCatalogConcurrencyPersistenceIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Definition_admission_revoked_after_sql_rolls_back_before_physical_commit(bool delete) {
        await using var database = PostgresTestDatabaseLease.Create("workflowdefinitionphysical");
        var factory = await CreateDatabaseFactoryAsync(database);
        var gallery = CreateGallery(factory);
        var catalog = CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var original = await catalog.SaveDefinitionAsync(CreateSaveRequest());
        var allowed = true;
        var checks = 0;
        var flush = new AuthoringFlushRevocation(() => allowed = false);
        var heldFactory = new WorkflowUsagePostgresDbContextFactory(new DbContextOptionsBuilder<AppDbContext>(
            database.CreateAppDbContextOptions()).AddInterceptors(flush).Options);
        var feed = new EditingChangeFeed();
        var changes = WorkflowObservationTestSupport.Create(feed);
        using var subscription = changes.Subscribe(original.Id);
        var writer = WorkflowOwnerPersistenceTestFactory.CreateCatalog(changes, heldFactory, new WorkflowDefinitionValidator(), gallery);
        using (changes.EnterUiWrite(_ => {
            checks++;
            return allowed ? ValueTask.CompletedTask : throw new UnauthorizedAccessException("Revoked after SQL");
        })) {
            if (delete) {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => writer.DeleteDefinitionAsync(new(original.Id, original.VersionId)));
            } else {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => writer.SaveDefinitionAsync(CreateSaveRequest(original.Id, original.VersionId, "Uncommitted")));
            }
        }
        Assert.Equal(1, flush.Writes);
        Assert.Equal(2, checks);
        Assert.Equal(0, feed.DeliveredHints);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize((await catalog.GetDefinitionAsync(original.Id))!.Definition));
        await using var context = factory.CreateDbContext();
        Assert.Equal(1, await context.Set<WorkflowDefinitionRecord>().CountAsync(item => item.WorkflowId == original.Id.Value));
    }

    [Fact]
    public async Task Native_ui_save_rechecks_workflow_access_after_validation_before_committing() {
        await using var database = PostgresTestDatabaseLease.Create("workflowuicommitaccess");
        var factory = await CreateDatabaseFactoryAsync(database);
        var gallery = CreateGallery(factory);
        var original = await CreateCatalog(factory, gallery, new WorkflowDefinitionValidator()).SaveDefinitionAsync(CreateSaveRequest());
        var feed = new EditingChangeFeed();
        var changes = WorkflowObservationTestSupport.Create(feed);
        using var subscription = changes.Subscribe(original.Id);
        var allowed = true;
        var checks = 0;
        var validating = WorkflowOwnerPersistenceTestFactory.CreateCatalog(changes, factory, new CallbackDefinitionValidator(() => allowed = false), gallery);
        var owner = new WorkflowDocumentOwner(validating, NullLogger.Instance, changes, (_, _) => {
            checks++;
            return allowed ? ValueTask.CompletedTask : ValueTask.FromException(new UnauthorizedAccessException("Test workflow grant revoked"));
        });
        var result = await owner.Operations.Save(CreateSaveRequest(original.Id, original.VersionId, "Must not commit"), default);
        Assert.Equal(WorkflowSaveRejection.Access, Assert.IsType<WorkflowDefinitionSaveOutcome.Rejected>(result).Reason);
        Assert.Equal(2, checks);
        Assert.Equal(0, feed.DeliveredHints);
        Assert.Null(owner.Accepted);
        Assert.Equal(original.VersionId, (await CreateCatalog(factory, gallery, new WorkflowDefinitionValidator()).GetDefinitionAsync(original.Id))!.Definition.VersionId);
        await using var context = factory.CreateDbContext();
        Assert.Equal(1, await context.Set<WorkflowDefinitionRecord>().CountAsync());
    }

    [Fact]
    public async Task Definition_hints_follow_commits_and_exclude_stale_refusals_and_other_scopes() {
        await using var database = PostgresTestDatabaseLease.Create("workflowhints");
        var factory = await CreateDatabaseFactoryAsync(database);
        var feed = new EditingChangeFeed();
        var changes = WorkflowObservationTestSupport.Create(feed);
        var catalog = WorkflowOwnerPersistenceTestFactory.CreateCatalog(changes, factory, new WorkflowDefinitionValidator(), CreateGallery(factory));
        var id = WorkflowId.New();
        using var resource = changes.Subscribe(id);
        using var otherScope = WorkflowObservationTestSupport.Create(feed).Subscribe(id);
        var operation = new EditingOperationIdentity(Guid.NewGuid(), Guid.NewGuid());
        WorkflowDefinition original;
        using (changes.Enter(operation)) {
            original = await catalog.SaveDefinitionAsync(CreateSaveRequest(id));
        }
        Assert.Equal(1, feed.DeliveredHints);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var created = Assert.Single((await resource.ReadAsync(deadline.Token)).Changes);
        Assert.Equal(EditingChangeKind.Created, created.Kind);
        Assert.Equal(changes.Scope, created.Resource.Scope);
        Assert.Equal(operation.EditorId, created.OriginEditorId);
        Assert.Equal(operation.OperationId, created.OperationId);
        var winner = await catalog.SaveDefinitionAsync(CreateSaveRequest(id, original.VersionId, "Peer"));
        Assert.Equal(2, feed.DeliveredHints);
        Assert.Equal(EditingChangeKind.EditableContentChanged, Assert.Single((await resource.ReadAsync(deadline.Token)).Changes).Kind);
        await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(() => catalog.SaveDefinitionAsync(CreateSaveRequest(id, original.VersionId)));
        await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(() => catalog.DeleteDefinitionAsync(new(id, original.VersionId)));
        Assert.Equal(2, feed.DeliveredHints);
        await catalog.DeleteDefinitionAsync(new(id, winner.VersionId));
        Assert.Equal(3, feed.DeliveredHints);
        Assert.Equal(EditingChangeKind.Deleted, Assert.Single((await resource.ReadAsync(deadline.Token)).Changes).Kind);
    }

    [Fact]
    public async Task A_pending_native_definition_commit_emits_no_hint_until_it_succeeds() {
        await using var database = PostgresTestDatabaseLease.Create("workflowhintcommit");
        var factory = await CreateDatabaseFactoryAsync(database);
        var gallery = CreateGallery(factory);
        var original = await CreateCatalog(factory, gallery, new WorkflowDefinitionValidator()).SaveDefinitionAsync(CreateSaveRequest());
        var feed = new EditingChangeFeed();
        var changes = WorkflowObservationTestSupport.Create(feed);
        using var subscription = changes.Subscribe(original.Id);
        var hold = new DefinitionCommitHold(EntityState.Modified);
        var heldFactory = new WorkflowUsagePostgresDbContextFactory(new DbContextOptionsBuilder<AppDbContext>(
            database.CreateAppDbContextOptions()).AddInterceptors(hold).Options);
        var catalog = WorkflowOwnerPersistenceTestFactory.CreateCatalog(changes, heldFactory, new WorkflowDefinitionValidator(), gallery);
        var pending = catalog.SaveDefinitionAsync(CreateSaveRequest(original.Id, original.VersionId, "After commit"));
        try {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(0, feed.DeliveredHints);
            Assert.Equal(original.VersionId, (await CreateCatalog(factory, gallery, new WorkflowDefinitionValidator()).GetDefinitionAsync(original.Id))!.Definition.VersionId);
        } finally {
            hold.Release.TrySetResult();
        }
        var accepted = await pending;
        Assert.Equal(1, feed.DeliveredHints);
        Assert.Equal(accepted.VersionId, (await catalog.GetDefinitionAsync(original.Id))!.Definition.VersionId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Conditional_delete_and_save_commit_only_one_original_head(bool deleteWins) {
        await using var database = PostgresTestDatabaseLease.Create("workflowdeletecas");
        var factory = await CreateDatabaseFactoryAsync(database);
        var gallery = CreateGallery(factory);
        var catalog = CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var original = await catalog.SaveDefinitionAsync(CreateSaveRequest());
        var hold = new DefinitionCommitHold(deleteWins ? EntityState.Modified : EntityState.Deleted);
        var heldFactory = new WorkflowUsagePostgresDbContextFactory(new DbContextOptionsBuilder<AppDbContext>(
            database.CreateAppDbContextOptions()).AddInterceptors(hold).Options);
        var heldCatalog = CreateCatalog(heldFactory, gallery, new WorkflowDefinitionValidator());
        var replacement = CreateSaveRequest(original.Id, original.VersionId, "Peer winner");
        Task pending = deleteWins
            ? heldCatalog.SaveDefinitionAsync(replacement)
            : heldCatalog.DeleteDefinitionAsync(new(original.Id, original.VersionId));
        WorkflowDefinition? winner = null;
        try {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (deleteWins) {
                await catalog.DeleteDefinitionAsync(new(original.Id, original.VersionId));
            } else {
                winner = await catalog.SaveDefinitionAsync(replacement);
            }
        } finally {
            hold.Release.TrySetResult();
        }
        await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(() => pending);
        await using var context = factory.CreateDbContext();
        if (deleteWins) {
            Assert.Null(await catalog.GetDefinitionAsync(original.Id));
            Assert.Empty(await context.Set<WorkflowDefinitionHeadRecord>().ToArrayAsync());
            Assert.Empty(await context.Set<WorkflowDefinitionRecord>().ToArrayAsync());
            return;
        }
        Assert.NotNull(winner);
        Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize((await catalog.GetDefinitionAsync(original.Id))!.Definition));
        Assert.Equal(winner.VersionId.Value, (await context.Set<WorkflowDefinitionHeadRecord>().SingleAsync()).VersionId);
        Assert.Equal(2, await context.Set<WorkflowDefinitionRecord>().CountAsync());
        Assert.NotNull(await catalog.GetDefinitionAsync(original.Id, original.VersionId));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Existing_definition_save_and_status_require_the_original_version(bool statusOnly, bool peerCommitted) {
        await using var database = PostgresTestDatabaseLease.Create("workfloworiginal");
        var factory = await CreateDatabaseFactoryAsync(database);
        var gallery = CreateGallery(factory);
        var editor = CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var peer = CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var original = await editor.SaveDefinitionAsync(CreateSaveRequest(name: "Original definition"));
        var winner = peerCommitted
            ? await peer.SaveDefinitionAsync(CreateSaveRequest(original.Id, original.VersionId, "Peer winner"))
            : original;

        Task<WorkflowDefinition> Submit() => statusOnly
            ? editor.ChangeDefinitionStatusAsync(new(original.Id, null, WorkflowLifecycleStatus.Suspended))
            : editor.SaveDefinitionAsync(CreateSaveRequest(original.Id, null, "Held replacement"));

        await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(Submit);
        var current = await peer.GetDefinitionAsync(original.Id);
        Assert.NotNull(current);
        Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize(current.Definition));
        await using var context = factory.CreateDbContext();
        Assert.Equal(peerCommitted ? 2 : 1, await context.Set<WorkflowDefinitionRecord>().CountAsync());
        Assert.Equal(winner.VersionId.Value, (await context.Set<WorkflowDefinitionHeadRecord>().SingleAsync()).VersionId);
    }

    [Fact]
    public async Task Chosen_definition_id_is_create_only_without_an_original_version() {
        await using var database = PostgresTestDatabaseLease.Create("workflowchosenid");
        var factory = await CreateDatabaseFactoryAsync(database);
        var gallery = CreateGallery(factory);
        var catalog = CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var chosenId = WorkflowId.New();
        var original = await catalog.SaveDefinitionAsync(CreateSaveRequest(chosenId, null, "Chosen original"));

        Assert.Equal(chosenId, original.Id);
        await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(() =>
            catalog.SaveDefinitionAsync(CreateSaveRequest(chosenId, null, "Colliding creator")));
        Assert.Equal(original.VersionId, (await catalog.GetDefinitionAsync(chosenId))!.Definition.VersionId);
        await using var context = factory.CreateDbContext();
        Assert.Equal(1, await context.Set<WorkflowDefinitionRecord>().CountAsync());
    }

    [Fact]
    public async Task An_original_version_cannot_create_a_missing_definition() {
        await using var database = PostgresTestDatabaseLease.Create("workflowmissingoriginal");
        var factory = await CreateDatabaseFactoryAsync(database);
        var gallery = CreateGallery(factory);
        var catalog = CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var missingId = WorkflowId.New();

        await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(() =>
            catalog.SaveDefinitionAsync(CreateSaveRequest(missingId, WorkflowVersionId.New(), "Missing original")));
        Assert.Null(await catalog.GetDefinitionAsync(missingId));
        await using var context = factory.CreateDbContext();
        Assert.Empty(await context.Set<WorkflowDefinitionHeadRecord>().ToArrayAsync());
        Assert.Empty(await context.Set<WorkflowDefinitionRecord>().ToArrayAsync());
    }

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

    private static async Task<WorkflowDefinition> AssertSinglePersistedWinnerAsync(
        WorkflowUsagePostgresDbContextFactory factory,
        PersistentWorkflowCatalogService verificationCatalog,
        WorkflowId workflowId,
        IReadOnlyCollection<WriteOutcome> outcomes,
        IReadOnlyList<long> expectedRevisions)
    {
        var winner = Assert.Single(outcomes, outcome => outcome.Definition is not null).Definition!;
        // The losing writer gets the typed concurrency rejection that callers such as the workflow curator tools
        // branch on; its transaction saved nothing.
        var conflict = Assert.IsType<WorkflowDefinitionConcurrencyException>(
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

    internal static async Task<WorkflowUsagePostgresDbContextFactory> CreateDatabaseFactoryAsync(PostgresTestDatabaseLease database) {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var factory = new WorkflowUsagePostgresDbContextFactory(database.CreateAppDbContextOptions());
        await using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        return factory;
    }

    internal static PersistentWorkflowCatalogService CreateCatalog(
        WorkflowUsagePostgresDbContextFactory factory,
        PromptsService gallery,
        IWorkflowDefinitionValidator validator)
        => WorkflowOwnerPersistenceTestFactory.CreateCatalog(WorkflowObservationTestSupport.Create(), factory, validator, gallery);

    internal static PromptsService CreateGallery(WorkflowUsagePostgresDbContextFactory canonicalFactory) {
        var factory = PromptsPersistenceTestFactory.FromCanonical(canonicalFactory);
        return new(
            factory,
            new SystemClock(),
            new NullActivityStream(),
            new EfPromptGallerySearchDriver(factory),
            new PromptGalleryProjectionCoordinator(factory, new DisabledPromptGalleryProjectionDriver()),
            new PromptGalleryCompatibilityEvaluator(),
            NullLogger<PromptsService>.Instance,
            PromptGalleryObservationTestSupport.Create());
    }

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

    private sealed class CallbackDefinitionValidator(Action validated) : IWorkflowDefinitionValidator {
        public WorkflowValidationResult Validate(WorkflowDefinition definition, IReadOnlyList<LlmCallComponent> components) {
            var result = new WorkflowDefinitionValidator().Validate(definition, components);
            validated();
            return result;
        }
    }

    private sealed class DefinitionCommitHold(EntityState state) : SaveChangesInterceptor {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (eventData.Context!.ChangeTracker.Entries<WorkflowDefinitionHeadRecord>().Any(entry => entry.State == state)) {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class AuthoringFlushRevocation(Action revoke) : SaveChangesInterceptor {
        public int Writes { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            Writes++;
            revoke();
            return ValueTask.FromResult(result);
        }
    }

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
