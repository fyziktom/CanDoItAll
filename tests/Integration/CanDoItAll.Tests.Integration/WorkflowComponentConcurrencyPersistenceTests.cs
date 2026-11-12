using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Editing;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowComponentConcurrencyPersistenceTests {
    [Fact]
    public async Task Failed_followup_returns_the_committed_revision_and_refresh_warning_without_replaying_creation() {
        await using var database = PostgresTestDatabaseLease.Create("workflowcomponentfollowup");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        ComponentCompletionFailure? completion = null;
        var catalog = WorkflowOwnerPersistenceTestFactory.CreateCatalog(WorkflowObservationTestSupport.Create(), factory,
            new WorkflowDefinitionValidator(), gallery, inner => completion = new(inner));
        var request = CreateRequest();
        var accepted = await catalog.SaveComponentAsync(request);
        Assert.True(accepted.RefreshRequired);
        Assert.Equal(1, completion!.Calls);
        AssertSame(accepted with { RefreshRequired = false }, await catalog.GetComponentAsync(accepted.Id));
        var next = await catalog.SaveComponentAsync(request with { Id = accepted.Id, ExpectedRevision = accepted.Revision });
        Assert.False(next.RefreshRequired);
        Assert.NotEqual(accepted.Revision, next.Revision);
        Assert.Equal(1, completion.Calls);
        await using var verify = factory.CreateDbContext();
        Assert.Equal(1, await verify.Set<WorkflowComponentRecord>().CountAsync());
        Assert.Equal(1, await verify.Set<PromptArtifact>().CountAsync());
        Assert.Equal(1, await verify.Set<PromptVersion>().CountAsync());
    }

    [Fact]
    public async Task Component_hints_follow_physical_commit_preserve_origin_and_do_not_cross_scopes_or_follow_refusals() {
        await using var database = PostgresTestDatabaseLease.Create("workflowcomponenthints");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var request = CreateRequest();
        var original = await catalog.SaveComponentAsync(request);
        var prompt = (await gallery.GetItemAsync(original.PromptArtifactId!.Value)).Value!;
        var hold = new ComponentCommitHold();
        var held = new WorkflowUsagePostgresDbContextFactory(new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions()).AddInterceptors(hold).Options);
        var feed = new CanDoItAll.Infrastructure.Editing.EditingChangeFeed();
        var changes = WorkflowObservationTestSupport.Create(feed);
        using var own = changes.SubscribeComponents();
        using var unrelated = WorkflowObservationTestSupport.Create(feed).SubscribeComponents();
        var writer = WorkflowOwnerPersistenceTestFactory.CreateCatalog(changes, held, new WorkflowDefinitionValidator(), gallery);
        var origin = new CanDoItAll.Infrastructure.Editing.EditingOperationIdentity(Guid.NewGuid(), Guid.NewGuid());
        using var operation = changes.Enter(origin);
        var pending = writer.SaveComponentAsync(request with { Id = original.Id, ExpectedRevision = original.Revision, Instructions = "Pending actual commit" });
        try {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(0, feed.DeliveredHints);
            AssertSame(original, await catalog.GetComponentAsync(original.Id));
            AssertSame(prompt, (await gallery.GetItemAsync(prompt.Id)).Value);
        } finally {
            hold.Release.TrySetResult();
        }
        var accepted = await pending;
        Assert.Equal(1, feed.DeliveredHints);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var hint = Assert.Single((await own.ReadAsync(deadline.Token)).Changes);
        Assert.Equal(EditingOwner.WorkflowComponent, hint.Resource.Owner);
        Assert.Equal(accepted.Id.Value, hint.Resource.ResourceId);
        Assert.Equal(origin.EditorId, hint.OriginEditorId);
        Assert.Equal(origin.OperationId, hint.OperationId);
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => writer.SaveComponentAsync(request with { Id = original.Id, ExpectedRevision = original.Revision }));
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => writer.DeleteComponentAsync(new(original.Id, original.Revision)));
        Assert.Equal(1, feed.DeliveredHints);
        await writer.DeleteComponentAsync(new(accepted.Id, accepted.Revision));
        Assert.Equal(2, feed.DeliveredHints);
        Assert.Equal(EditingChangeKind.Deleted, Assert.Single((await own.ReadAsync(deadline.Token)).Changes).Kind);
    }

    [Fact]
    public async Task An_existing_component_requires_its_original_before_any_prompt_mutation() {
        await using var database = PostgresTestDatabaseLease.Create("workflowcomponentoriginal");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var request = CreateRequest();
        var original = await catalog.SaveComponentAsync(request);
        var prompt = (await gallery.GetItemAsync(original.PromptArtifactId!.Value)).Value!;
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.SaveComponentAsync(request with {
            Id = original.Id,
            Instructions = "Unobserved older replacement instructions."
        }));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await catalog.GetComponentAsync(original.Id)));
        Assert.Equal(JsonSerializer.Serialize(prompt), JsonSerializer.Serialize((await gallery.GetItemAsync(prompt.Id)).Value));
    }

    [Fact]
    public async Task A_prompt_peer_committing_after_review_produces_a_typed_conflict_and_rolls_back_the_component() {
        await using var database = PostgresTestDatabaseLease.Create("workflowcomponentpromptcas");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var request = CreateRequest();
        var original = await catalog.SaveComponentAsync(request);
        var item = (await gallery.GetItemAsync(original.PromptArtifactId!.Value)).Value!;
        ComponentStageHold? hold = null;
        var writer = WorkflowOwnerPersistenceTestFactory.CreateCatalog(WorkflowObservationTestSupport.Create(), factory,
            new WorkflowDefinitionValidator(), gallery, inner => hold = new(inner));
        var pending = writer.SaveComponentAsync(request with { Id = original.Id, ExpectedRevision = original.Revision, Instructions = "Held component intent" });
        try {
            await hold!.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True((await gallery.SaveDraftAsync(ToDraft(item, "Prompt peer after review"))).IsSuccess);
        } finally {
            hold!.Release.TrySetResult();
        }
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => pending);
        AssertSame(original, await catalog.GetComponentAsync(original.Id));
        var peer = (await gallery.GetItemAsync(item.Id)).Value!;
        Assert.Equal("Prompt peer after review", peer.DraftContent);
        Assert.Single(peer.Versions);
        Assert.Equal(PromptArtifactStatus.Draft, peer.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_and_stale_originals_cannot_mutate_the_component_or_prompt(bool stale) {
        await using var database = PostgresTestDatabaseLease.Create("workflowcomponentstale");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var request = CreateRequest();
        var original = await catalog.SaveComponentAsync(request);
        var winner = await catalog.SaveComponentAsync(request with { Id = original.Id, ExpectedRevision = original.Revision, Instructions = "Peer accepted content" });
        var prompt = (await gallery.GetItemAsync(winner.PromptArtifactId!.Value)).Value!;
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.SaveComponentAsync(request with {
            Id = original.Id, ExpectedRevision = stale ? original.Revision : Guid.Empty
        }));
        AssertSame(winner, await catalog.GetComponentAsync(original.Id));
        AssertSame(prompt, (await gallery.GetItemAsync(prompt.Id)).Value);
    }

    [Fact]
    public async Task Chosen_identity_is_create_only_and_recreation_has_an_independent_prompt_lifetime() {
        await using var database = PostgresTestDatabaseLease.Create("workflowcomponentlifetime");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var request = CreateRequest() with { Id = WorkflowComponentId.New() };
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.SaveComponentAsync(request with { ExpectedRevision = Guid.NewGuid() }));
        Assert.Null(await catalog.GetComponentAsync(request.Id!.Value));
        var original = await catalog.SaveComponentAsync(request);
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.SaveComponentAsync(request));
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.DeleteComponentAsync(new(original.Id, Guid.Empty)));
        await catalog.DeleteComponentAsync(new(original.Id, original.Revision));
        var recreated = await catalog.SaveComponentAsync(request with { Instructions = "New lifetime content" });
        Assert.NotEqual(original.Revision, recreated.Revision);
        Assert.NotEqual(original.LifetimeId, recreated.LifetimeId);
        Assert.NotEqual(original.PromptArtifactId, recreated.PromptArtifactId);
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.DeleteComponentAsync(new(original.Id, original.Revision)));
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.SaveComponentAsync(request with { ExpectedRevision = original.Revision }));
        AssertSame(recreated, await catalog.GetComponentAsync(recreated.Id));
        Assert.Equal(original.Instructions, (await gallery.GetVersionSnapshotAsync(original.PromptVersionId!.Value)).Value!.Content);
        var priorPrompt = (await gallery.GetItemAsync(original.PromptArtifactId!.Value)).Value!;
        var rebound = await catalog.SaveComponentAsync(request with {
            ExpectedRevision = recreated.Revision, PromptArtifactId = original.PromptArtifactId, PromptVersionId = original.PromptVersionId
        });
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.SaveComponentAsync(request with {
            ExpectedRevision = rebound.Revision, Instructions = "Cannot implicitly replace a prior lifetime's Prompt"
        }));
        AssertSame(priorPrompt, (await gallery.GetItemAsync(priorPrompt.Id)).Value);
    }

    [Fact]
    public async Task A_noop_advances_only_component_revision_and_returns_the_exact_receipt() {
        await using var database = PostgresTestDatabaseLease.Create("workflowcomponentnoop");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var request = CreateRequest();
        var original = await catalog.SaveComponentAsync(request);
        var prompt = (await gallery.GetItemAsync(original.PromptArtifactId!.Value)).Value!;
        var accepted = await catalog.SaveComponentAsync(request with { Id = original.Id, ExpectedRevision = original.Revision });
        Assert.NotEqual(Guid.Empty, accepted.Revision);
        Assert.NotEqual(original.Revision, accepted.Revision);
        Assert.NotEqual(Guid.Empty, accepted.LifetimeId);
        Assert.Equal(original.LifetimeId, accepted.LifetimeId);
        AssertSame(accepted, await catalog.GetComponentAsync(accepted.Id));
        AssertSame(accepted, Assert.Single(await catalog.ListComponentsAsync()));
        AssertSame(prompt, (await gallery.GetItemAsync(prompt.Id)).Value);
    }

    [Fact]
    public async Task Same_original_writers_commit_exactly_one_component_and_one_prompt_version() {
        await using var database = PostgresTestDatabaseLease.Create("workflowcomponentrace");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var first = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var second = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var request = CreateRequest();
        var original = await first.SaveComponentAsync(request);
        async Task<(LlmCallComponent? Value, Exception? Error)> Submit(PersistentWorkflowCatalogService catalog, string content) {
            try {
                return (await catalog.SaveComponentAsync(request with { Id = original.Id, ExpectedRevision = original.Revision, Instructions = content }), null);
            } catch (Exception error) {
                return (null, error);
            }
        }
        var results = await Task.WhenAll(Submit(first, "First contender"), Submit(second, "Second contender"));
        var accepted = Assert.Single(results, item => item.Value is not null).Value!;
        Assert.IsType<WorkflowComponentConcurrencyException>(Assert.Single(results, item => item.Error is not null).Error);
        AssertSame(accepted, await first.GetComponentAsync(original.Id));
        var prompt = (await gallery.GetItemAsync(accepted.PromptArtifactId!.Value)).Value!;
        Assert.Equal(2, prompt.Versions.Count);
        Assert.Equal(accepted.PromptUpdatedAtUtc, prompt.UpdatedAtUtc);
        Assert.Equal(accepted.Instructions, (await gallery.GetVersionSnapshotAsync(accepted.PromptVersionId!.Value)).Value!.Content);
    }

    [Fact]
    public async Task A_peer_prompt_draft_cannot_be_blessed_by_a_fresh_read_during_component_save() {
        await using var database = PostgresTestDatabaseLease.Create("workflowcomponentpromptpeer");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var request = CreateRequest();
        var original = await catalog.SaveComponentAsync(request);
        var item = (await gallery.GetItemAsync(original.PromptArtifactId!.Value)).Value!;
        Assert.True((await gallery.SaveDraftAsync(ToDraft(item, "Peer Prompt content"))).IsSuccess);
        var peer = (await gallery.GetItemAsync(item.Id)).Value!;
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.SaveComponentAsync(request with {
            Id = original.Id, ExpectedRevision = original.Revision, Instructions = "Older component intent"
        }));
        AssertSame(original, await catalog.GetComponentAsync(original.Id));
        AssertSame(peer, (await gallery.GetItemAsync(item.Id)).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admission_revoked_after_component_sql_rolls_back_both_owners_and_emits_no_hint(bool existing) {
        await using var database = PostgresTestDatabaseLease.Create("workflowcomponentphysical");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var request = CreateRequest() with { Id = WorkflowComponentId.New() };
        var original = existing ? await catalog.SaveComponentAsync(request) : null;
        var prompt = original is null ? null : (await gallery.GetItemAsync(original.PromptArtifactId!.Value)).Value!;
        var allowed = true;
        var flush = new ComponentFlush(() => allowed = false);
        var held = new WorkflowUsagePostgresDbContextFactory(new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions()).AddInterceptors(flush).Options);
        var feed = new CanDoItAll.Infrastructure.Editing.EditingChangeFeed();
        var changes = WorkflowObservationTestSupport.Create(feed);
        using var subscription = changes.SubscribeComponents();
        var writer = WorkflowOwnerPersistenceTestFactory.CreateCatalog(changes, held, new WorkflowDefinitionValidator(), gallery);
        using (changes.EnterUiWrite(_ => allowed ? ValueTask.CompletedTask : throw new UnauthorizedAccessException("Revoked after SQL"))) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => writer.SaveComponentAsync(request with {
                ExpectedRevision = original?.Revision, Instructions = "Uncommitted staged instructions"
            }));
        }
        Assert.Equal(1, flush.Writes);
        Assert.Equal(0, feed.DeliveredHints);
        AssertSame(original, await catalog.GetComponentAsync(request.Id!.Value));
        if (prompt is not null) {
            AssertSame(prompt, (await gallery.GetItemAsync(prompt.Id)).Value);
        }
        await using var verify = factory.CreateDbContext();
        Assert.Equal(existing ? 1 : 0, await verify.Set<PromptArtifact>().CountAsync());
        Assert.Equal(existing ? 1 : 0, await verify.Set<PromptVersion>().CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Commit_transport_failure_never_leaves_a_half_committed_component_and_prompt(bool afterCommit) {
        await using var database = PostgresTestDatabaseLease.Create("workflowcomponentcommit");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var request = CreateRequest();
        var original = await catalog.SaveComponentAsync(request);
        var failure = new ComponentCommitFault(afterCommit);
        var held = new WorkflowUsagePostgresDbContextFactory(new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions()).AddInterceptors(failure).Options);
        var writer = WorkflowOwnerPersistenceTestFactory.CreateCatalog(WorkflowObservationTestSupport.Create(), held, new WorkflowDefinitionValidator(), gallery);
        await Assert.ThrowsAsync<IOException>(() => writer.SaveComponentAsync(request with {
            Id = original.Id, ExpectedRevision = original.Revision, Instructions = "Possibly accepted instructions"
        }));
        Assert.True(failure.Thrown);
        var stored = (await catalog.GetComponentAsync(original.Id))!;
        var item = (await gallery.GetItemAsync(stored.PromptArtifactId!.Value)).Value!;
        Assert.Equal(afterCommit ? "Possibly accepted instructions" : original.Instructions, stored.Instructions);
        Assert.Equal(afterCommit ? 2 : 1, item.Versions.Count);
        Assert.Equal(stored.Instructions, (await gallery.GetVersionSnapshotAsync(stored.PromptVersionId!.Value)).Value!.Content);
        Assert.Equal(stored.PromptUpdatedAtUtc, item.UpdatedAtUtc);
    }

    private static PromptGalleryDraft ToDraft(PromptGalleryItemDetails item, string content) => new(item.Id,
        item.ProjectId, item.CollectionId, item.Title, item.Summary, item.Kind, item.Phase, content, item.Tags,
        item.SupportedModels, item.SupportedConsumers, item.Recommendations, item.UpdatedAtUtc);

    private static void AssertSame<T>(T expected, T actual) => Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));

    private sealed class ComponentFlush(Action revoke) : SaveChangesInterceptor {
        public int Writes { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (eventData.Context!.ChangeTracker.Entries<WorkflowComponentRecord>().Any()) {
                Writes++;
                revoke();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ComponentCommitHold : SaveChangesInterceptor {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (eventData.Context!.ChangeTracker.Entries<WorkflowComponentRecord>().Any()) {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class ComponentStageHold(IPromptGalleryMutationService inner) : IPromptGalleryMutationService {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<Result<PromptVersionPreparation>> StageUpdateAndVersionAsync(PromptGalleryDraft draft, PromptImportVersionRequest version,
            CancellationToken cancellationToken = default) {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return await inner.StageUpdateAndVersionAsync(draft, version, cancellationToken);
        }
        public Task<Result<PromptVersionPreparation>> StageCreateImportedVersionAsync(PromptGalleryImportRequest request, CancellationToken cancellationToken = default)
            => inner.StageCreateImportedVersionAsync(request, cancellationToken);
        public Task CompleteVersionAsync(PromptVersionPreparation preparation, CancellationToken cancellationToken = default)
            => inner.CompleteVersionAsync(preparation, cancellationToken);
        public Task<Result<PromptDraftCreationPreparation>> StageCreateDraftAsync(PromptGalleryDraft draft, CancellationToken cancellationToken = default)
            => inner.StageCreateDraftAsync(draft, cancellationToken);
        public Task CompleteDraftCreationAsync(PromptDraftCreationPreparation preparation, CancellationToken cancellationToken = default)
            => inner.CompleteDraftCreationAsync(preparation, cancellationToken);
    }

    private sealed class ComponentCompletionFailure(IPromptGalleryMutationService inner) : IPromptGalleryMutationService {
        public int Calls { get; private set; }
        public Task CompleteVersionAsync(PromptVersionPreparation preparation, CancellationToken cancellationToken = default) {
            Calls++;
            throw new InvalidOperationException("Injected private readback failure after physical commit");
        }
        public Task<Result<PromptVersionPreparation>> StageUpdateAndVersionAsync(PromptGalleryDraft draft, PromptImportVersionRequest version, CancellationToken cancellationToken = default)
            => inner.StageUpdateAndVersionAsync(draft, version, cancellationToken);
        public Task<Result<PromptVersionPreparation>> StageCreateImportedVersionAsync(PromptGalleryImportRequest request, CancellationToken cancellationToken = default)
            => inner.StageCreateImportedVersionAsync(request, cancellationToken);
        public Task<Result<PromptDraftCreationPreparation>> StageCreateDraftAsync(PromptGalleryDraft draft, CancellationToken cancellationToken = default)
            => inner.StageCreateDraftAsync(draft, cancellationToken);
        public Task CompleteDraftCreationAsync(PromptDraftCreationPreparation preparation, CancellationToken cancellationToken = default)
            => inner.CompleteDraftCreationAsync(preparation, cancellationToken);
    }

    private sealed class ComponentCommitFault(bool afterCommit) : DbTransactionInterceptor {
        public bool Thrown { get; private set; }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            if (!afterCommit) {
                Thrown = true;
                throw new IOException("Injected before physical commit");
            }
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (afterCommit) {
                Thrown = true;
                throw new IOException("Injected after physical commit");
            }
            return Task.CompletedTask;
        }
    }

    internal static LlmCallComponentSaveRequest CreateRequest() => new(null, "Concurrent reusable step", null,
        "gpt-6-luna", WorkflowModality.Text, new(0.2, 800, false, string.Empty),
        "Return the workflow payload unchanged.", WorkflowValueShape.Text, WorkflowValueShape.Text, AgentPermissionsPolicy.Default);
}
