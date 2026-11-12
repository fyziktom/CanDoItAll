using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowSettingsConcurrencyPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admission_revoked_after_sql_rolls_back_settings_before_physical_commit(bool existing) {
        await using var database = PostgresTestDatabaseLease.Create("workflowsettingsphysical");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var original = existing ? await catalog.SaveSettingsAsync(WorkflowSettings.Default) : await catalog.GetSettingsAsync();
        var allowed = true;
        var checks = 0;
        var flush = new SettingsFlushRevocation(() => allowed = false);
        var heldFactory = new WorkflowUsagePostgresDbContextFactory(new DbContextOptionsBuilder<AppDbContext>(
            database.CreateAppDbContextOptions()).AddInterceptors(flush).Options);
        var changes = WorkflowObservationTestSupport.Create();
        var writer = WorkflowOwnerPersistenceTestFactory.CreateCatalog(changes, heldFactory, new WorkflowDefinitionValidator(), gallery);
        using (changes.EnterUiWrite(_ => {
            checks++;
            return allowed ? ValueTask.CompletedTask : throw new UnauthorizedAccessException("Revoked after SQL");
        })) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => writer.SaveSettingsAsync(Changed(original, 512)));
        }
        Assert.Equal(1, flush.Writes);
        Assert.Equal(2, checks);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await catalog.GetSettingsAsync()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_peer_write_invalidates_the_original_even_when_values_are_unchanged(bool unchanged) {
        await using var database = PostgresTestDatabaseLease.Create("workflowsettingspeer");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var peer = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var original = await catalog.SaveSettingsAsync(WorkflowSettings.Default);
        var winner = await peer.SaveSettingsAsync(unchanged ? original : Changed(original, 800));
        Assert.NotEqual(original.UpdatedAtUtc, winner.UpdatedAtUtc);
        await Assert.ThrowsAsync<WorkflowSettingsConcurrencyException>(() => catalog.SaveSettingsAsync(Changed(original, 512)));
        Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize(await catalog.GetSettingsAsync()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Two_independent_contexts_that_pass_preflight_commit_only_one_original(bool existing) {
        await using var database = PostgresTestDatabaseLease.Create("workflowsettingsflushrace");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var original = existing ? await catalog.SaveSettingsAsync(WorkflowSettings.Default) : await catalog.GetSettingsAsync();
        var barrier = new SettingsFlushBarrier();
        var racingFactory = new WorkflowUsagePostgresDbContextFactory(new DbContextOptionsBuilder<AppDbContext>(
            database.CreateAppDbContextOptions()).AddInterceptors(barrier).Options);
        var first = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(racingFactory, gallery, new WorkflowDefinitionValidator());
        var second = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(racingFactory, gallery, new WorkflowDefinitionValidator());
        var results = await Task.WhenAll(Attempt(first, Changed(original, 512)), Attempt(second, Changed(original, 1024)))
            .WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(2, barrier.Arrivals);
        var winner = Assert.Single(results.OfType<WorkflowSettings>());
        Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize(await catalog.GetSettingsAsync()));
    }

    [Fact]
    public async Task An_original_cannot_create_an_absent_settings_row() {
        await using var database = PostgresTestDatabaseLease.Create("workflowsettingsmissing");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        await Assert.ThrowsAsync<WorkflowSettingsConcurrencyException>(() => catalog.SaveSettingsAsync(
            WorkflowSettings.Default with { UpdatedAtUtc = DateTimeOffset.UtcNow }));
        Assert.Null((await catalog.GetSettingsAsync()).UpdatedAtUtc);
    }

    [Fact]
    public async Task The_acknowledged_revision_round_trips_and_authorizes_the_next_write() {
        await using var database = PostgresTestDatabaseLease.Create("workflowsettingsexact");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var accepted = await catalog.SaveSettingsAsync(WorkflowSettings.Default);
        Assert.NotNull(accepted.UpdatedAtUtc);
        var read = await catalog.GetSettingsAsync();
        Assert.Equal(JsonSerializer.Serialize(accepted), JsonSerializer.Serialize(read));
        var next = await catalog.SaveSettingsAsync(Changed(read, 512));
        Assert.True(next.UpdatedAtUtc > accepted.UpdatedAtUtc);
        Assert.Equal(0, next.UpdatedAtUtc!.Value.Ticks % TimeSpan.TicksPerMicrosecond);
    }

    [Fact]
    public async Task Revoked_native_commit_admission_retains_the_original_settings() {
        await using var database = PostgresTestDatabaseLease.Create("workflowsettingsadmission");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var changes = WorkflowObservationTestSupport.Create();
        var catalog = WorkflowOwnerPersistenceTestFactory.CreateCatalog(changes, factory, new WorkflowDefinitionValidator(), gallery);
        var original = await catalog.SaveSettingsAsync(WorkflowSettings.Default);
        using (changes.EnterUiWrite(_ => throw new UnauthorizedAccessException("Revoked"))) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => catalog.SaveSettingsAsync(Changed(original, 512)));
        }
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await catalog.GetSettingsAsync()));
    }

    [Fact]
    public async Task Existing_settings_cannot_be_replaced_without_the_original_observation() {
        await using var database = PostgresTestDatabaseLease.Create("workflowsettingsoriginal");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var accepted = await catalog.SaveSettingsAsync(WorkflowSettings.Default);
        var stale = WorkflowSettings.Default with {
            ArtifactPolicy = WorkflowSettings.Default.ArtifactPolicy with { MaxInlinePayloadCharacters = 512 }
        };
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => catalog.SaveSettingsAsync(stale));
        Assert.Equal(JsonSerializer.Serialize(accepted), JsonSerializer.Serialize(await catalog.GetSettingsAsync()));
    }

    private static WorkflowSettings Changed(WorkflowSettings original, int limit)
        => original with { ArtifactPolicy = original.ArtifactPolicy with { MaxInlinePayloadCharacters = limit } };

    private static async Task<WorkflowSettings?> Attempt(PersistentWorkflowCatalogService catalog, WorkflowSettings candidate) {
        try {
            return await catalog.SaveSettingsAsync(candidate);
        } catch (WorkflowSettingsConcurrencyException) {
            return null;
        }
    }

    private sealed class SettingsFlushBarrier : SaveChangesInterceptor {
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        public int Arrivals => arrivals;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (eventData.Context!.ChangeTracker.Entries<WorkflowSettingsRecord>().Any(entry => entry.State is EntityState.Added or EntityState.Modified)) {
                if (Interlocked.Increment(ref arrivals) == 2) {
                    released.TrySetResult();
                }
                await released.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }

    private sealed class SettingsFlushRevocation(Action revoke) : SaveChangesInterceptor {
        public int Writes { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            Writes++;
            revoke();
            return ValueTask.FromResult(result);
        }
    }
}
