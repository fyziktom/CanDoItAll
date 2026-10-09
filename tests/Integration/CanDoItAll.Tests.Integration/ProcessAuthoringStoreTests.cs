using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Templates;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessAuthoringStoreTests {
    [Fact]
    public async Task Additive_migration_preserves_prepared_and_accepted_runtime_payloads_and_is_idempotent() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("process-authoring-upgrade");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync("20261007193102_AddProjectExternalIdentity");
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>().UseNpgsql(lease.ConnectionString).Options;
        var store = new EfProcessPreparedLaunchStore(new ContextFactory(options), options);
        var saved = await store.PrepareAsync(ProcessPreparedLaunchFixture.Create());
        await using (var runtime = new ProcessPersistenceDbContext(options)) {
            await new EfProcessRuntimeUnitOfWork(runtime).CommitAsync(ProcessPreparedLaunchFixture.Commit(saved));
        }
        var before = await context.Database.SqlQueryRaw<string>("""
            SELECT to_jsonb(p)::text AS "Value" FROM process_prepared_launches p
            UNION ALL SELECT to_jsonb(p)::text AS "Value" FROM process_instance_plans p
            UNION ALL SELECT to_jsonb(p)::text AS "Value" FROM process_runtime_states p
            UNION ALL SELECT to_jsonb(p)::text AS "Value" FROM process_runtime_step_assignments p
            ORDER BY "Value"
            """).ToArrayAsync();
        Assert.NotEmpty(before);
        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();
        Assert.False(context.Database.HasPendingModelChanges());
        var after = await context.Database.SqlQueryRaw<string>("""
            SELECT to_jsonb(p)::text AS "Value" FROM process_prepared_launches p
            UNION ALL SELECT to_jsonb(p)::text AS "Value" FROM process_instance_plans p
            UNION ALL SELECT to_jsonb(p)::text AS "Value" FROM process_runtime_states p
            UNION ALL SELECT to_jsonb(p)::text AS "Value" FROM process_runtime_step_assignments p
            ORDER BY "Value"
            """).ToArrayAsync();
        Assert.Equal(before, after);
        Assert.Empty(await context.Set<ProcessAuthoringHeadEntity>().ToArrayAsync());
        Assert.Empty(await context.Set<ProcessAuthoringPublicationEntity>().ToArrayAsync());
        Assert.Empty(await context.Set<ProcessAuthoringReceiptEntity>().ToArrayAsync());
        Assert.Equal(saved.PreparationFingerprint, (await store.GetAsync(saved.Preparation.AdmissionId))!.PreparationFingerprint);
    }

    [Fact]
    public async Task Concurrent_first_writes_have_one_head_and_one_accepted_revision() {
        await using var app = await TestApplication.CreateAsync(new());
        await using var owned = app.Services.CreateAsyncScope();
        var services = owned.ServiceProvider;
        var command = Command(services);
        await using var left = services.CreateAsyncScope();
        await using var right = services.CreateAsyncScope();
        var results = await Task.WhenAll(Store(left.ServiceProvider).CommitAsync(command),
            Store(right.ServiceProvider).CommitAsync(command with { OperationId = Guid.NewGuid() }));
        Assert.Single(results, result => result.Outcome == ProcessAuthoringOutcome.Accepted);
        Assert.Single(results, result => result.Outcome == ProcessAuthoringOutcome.Conflict);
        Assert.All(results, result => Assert.Equal(1, result.Snapshot!.Revision));
        await using var context = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await context.AuthoringHeads.CountAsync());
        Assert.Equal(2, await context.AuthoringReceipts.CountAsync());
    }

    [Fact]
    public async Task Concurrent_operation_replay_and_new_scope_recovery_return_exact_original_result() {
        await using var app = await TestApplication.CreateAsync(new());
        await using var owned = app.Services.CreateAsyncScope();
        var services = owned.ServiceProvider;
        var command = Command(services) with { Publish = true, Lifecycle = ProcessAuthoringLifecycle.Published };
        await using var left = services.CreateAsyncScope();
        await using var right = services.CreateAsyncScope();
        var results = await Task.WhenAll(Store(left.ServiceProvider).CommitAsync(command), Store(right.ServiceProvider).CommitAsync(command));
        Assert.All(results, result => Assert.Equal(ProcessAuthoringOutcome.Accepted, result.Outcome));
        Assert.Equal(ProcessAuthoringCodec.WriteReceipt(results[0]), ProcessAuthoringCodec.WriteReceipt(results[1]));
        await using var fresh = services.CreateAsyncScope();
        var recovered = await Store(fresh.ServiceProvider).RecoverAsync(command.Address, command.CallerId, command.OperationId, command.RequestFingerprint);
        Assert.Equal(ProcessAuthoringCodec.WriteReceipt(results[0]), ProcessAuthoringCodec.WriteReceipt(recovered!));
        await using var context = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await context.AuthoringPublications.CountAsync());
        Assert.Equal(1, await context.AuthoringReceipts.CountAsync());
    }

    [Fact]
    public async Task Reused_operation_with_different_payload_is_rejected_without_changing_head() {
        await using var app = await TestApplication.CreateAsync(new());
        await using var owned = app.Services.CreateAsyncScope();
        var services = owned.ServiceProvider;
        var command = Command(services);
        var store = Store(services);
        await store.CommitAsync(command);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CommitAsync(command with { RequestFingerprint = ProcessAuthoringCodec.Hash("different intent") }));
        Assert.Equal(1, (await store.ReadAsync(command.Address))!.Revision);
    }

    [Fact]
    public async Task New_draft_preserves_immutable_publication_and_stale_whole_aggregate_write_conflicts() {
        await using var app = await TestApplication.CreateAsync(new());
        await using var owned = app.Services.CreateAsyncScope();
        var services = owned.ServiceProvider;
        var command = Command(services) with { Publish = true, Lifecycle = ProcessAuthoringLifecycle.Published };
        var store = Store(services);
        var published = (await store.CommitAsync(command)).Snapshot!;
        var publication = (await store.ReadPublicationAsync(published.PublishedId!.Value))!;
        var changed = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(command.Content));
        changed.Definition.DisplayName = "Next draft";
        var next = command with { OperationId = Guid.NewGuid(), ExpectedRevision = 1, Content = changed, Publish = false,
            Lifecycle = ProcessAuthoringLifecycle.Draft, RequestFingerprint = ProcessAuthoringCodec.Hash("next draft") };
        var saved = await store.CommitAsync(next);
        Assert.Equal(published.PublishedId, saved.Snapshot!.PublishedId);
        Assert.Equal(2, saved.Snapshot.Revision);
        Assert.Equal(ProcessAuthoringOutcome.Conflict, (await store.CommitAsync(next with { OperationId = Guid.NewGuid() })).Outcome);
        var retained = (await store.ReadPublicationAsync(publication.Id))!;
        Assert.Equal(publication.ContentHash, retained.ContentHash);
        Assert.Equal(command.Content.Definition.DisplayName, retained.Content.Definition.DisplayName);
        Assert.NotEqual("Next draft", retained.Content.Definition.DisplayName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_transaction_fault_distinguishes_rollback_from_committed_recoverable_receipt(bool afterCommit) {
        await using var app = await TestApplication.CreateAsync(new());
        await using var owned = app.Services.CreateAsyncScope();
        var services = owned.ServiceProvider;
        var command = Command(services) with { Publish = true, Lifecycle = ProcessAuthoringLifecycle.Published };
        var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        var failure = new InvalidOperationException("Injected authoring transaction fault.");
        var fault = new TransactionFault(afterCommit, failure);
        options.AddInterceptors(fault);
        var store = new EfProcessAuthoringStore(new ContextFactory(options.Options),
            services.GetRequiredService<CoordinatedDatabaseTransaction>(), services.GetRequiredService<IProcessAuthoringAdmissionPolicy>(), TimeProvider.System);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => store.CommitAsync(command)));
        Assert.True(fault.Reached);
        var recovered = await Store(services).RecoverAsync(command.Address, command.CallerId, command.OperationId, command.RequestFingerprint);
        Assert.Equal(afterCommit, recovered is not null);
        await using var context = new ProcessPersistenceDbContext(options.Options);
        Assert.Equal(afterCommit ? 1 : 0, await context.AuthoringHeads.CountAsync());
        Assert.Equal(afterCommit ? 1 : 0, await context.AuthoringPublications.CountAsync());
        Assert.Equal(afterCommit ? 1 : 0, await context.AuthoringReceipts.CountAsync());
    }

    [Fact]
    public async Task PostgreSql_primary_key_enforces_global_scope_uniqueness_without_null_escape() {
        await using var app = await TestApplication.CreateAsync(new());
        await using var owned = app.Services.CreateAsyncScope();
        var services = owned.ServiceProvider;
        var command = Command(services);
        await Store(services).CommitAsync(command);
        await using var factoryContext = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        var duplicate = await factoryContext.AuthoringHeads.AsNoTracking().SingleAsync();
        factoryContext.AuthoringHeads.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => factoryContext.SaveChangesAsync());
    }

    [Fact]
    public void Codec_preserves_complete_documents_and_materialized_guidance_and_rejects_future_schema() {
        var loader = new ProcessTemplatePackLoader();
        foreach (var template in loader.Load().Definitions) {
            var definition = loader.LoadDefinition(template.Key);
            var originalGuidance = definition.Steps.SelectMany(step => step.ResolvedExecutionGuidance).ToArray();
            var content = Content(definition);
            var json = ProcessAuthoringCodec.Write(content);
            var reloaded = ProcessAuthoringCodec.Read(json);
            Assert.Equal(json, ProcessAuthoringCodec.Write(reloaded));
            Assert.Equal(originalGuidance, reloaded.Definition.Steps.SelectMany(step => step.ResolvedExecutionGuidance));
            Assert.Throws<NotSupportedException>(() => ProcessAuthoringCodec.Write(content with { SchemaVersion = 999 }));
            Assert.Throws<System.Text.Json.JsonException>(() => ProcessAuthoringCodec.Read(json.Insert(1, "\"unknownSemanticField\":true,")));
        }
    }

    private static IProcessAuthoringStore Store(IServiceProvider services) => services.GetRequiredService<IProcessAuthoringStore>();

    private static ProcessAuthoringCommit Command(IServiceProvider services) {
        var loader = services.GetRequiredService<ProcessTemplatePackLoader>();
        var definition = loader.LoadDefinition(loader.Load().Definitions[0].Key);
        ProcessAuthoringAddress address = new(services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id, Guid.Empty, Guid.Empty, definition.Key);
        return new(address, ProcessAuthoringAdmissionPolicy.LocalCallerId, Guid.NewGuid(), ProcessAuthoringCodec.Hash("store test"),
            0, Content(definition), ProcessAuthoringLifecycle.Draft, false);
    }

    private static ProcessAuthoringContent Content(ProcessTemplateDefinitionDocument definition)
        => new(ProcessAuthoringContent.CurrentSchemaVersion, definition,
            definition.Steps.ToDictionary(step => step.Key, step => step.ResolvedExecutionGuidance),
            new Dictionary<string, ProcessTemplateRoleResourceDocument>(), new("test-template", "1", ProcessAuthoringCodec.Hash(definition.Key)), [], []);

    private sealed class ContextFactory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    private sealed class TransactionFault(bool afterCommit, Exception failure) : DbTransactionInterceptor {
        public bool Reached { get; private set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            if (!afterCommit) {
                Reached = true;
                throw failure;
            }
            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (afterCommit) {
                Reached = true;
                throw failure;
            }
            return Task.CompletedTask;
        }
    }
}
