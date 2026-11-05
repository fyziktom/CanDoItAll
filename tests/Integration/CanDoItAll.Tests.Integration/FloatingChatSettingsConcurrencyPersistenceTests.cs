using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class FloatingChatSettingsConcurrencyPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Independent_contexts_flush_one_original_but_only_one_settings_replacement_commits(bool existing) {
        await using var database = PostgresTestDatabaseLease.Create("floatingsettingsrace");
        var canonical = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var reader = new FloatingAgentChatSettingsService(WorkflowOwnerPersistenceTestFactory.FromCanonical(canonical),
            TimeProvider.System, WorkflowObservationTestSupport.Create());
        var original = existing ? await reader.SaveSettingsAsync(FloatingAgentChatSettings.Default) : await reader.GetSettingsAsync();
        var barrier = new SettingsFlushBarrier();
        var heldCanonical = new WorkflowUsagePostgresDbContextFactory(new DbContextOptionsBuilder<AppDbContext>(
            database.CreateAppDbContextOptions()).AddInterceptors(barrier).Options);
        var held = WorkflowOwnerPersistenceTestFactory.FromCanonical(heldCanonical);
        var first = new FloatingAgentChatSettingsService(held, TimeProvider.System, WorkflowObservationTestSupport.Create());
        var second = new FloatingAgentChatSettingsService(held, TimeProvider.System, WorkflowObservationTestSupport.Create());

        var results = await Task.WhenAll(Attempt(first, original with { MaximumActiveChats = 8 }),
            Attempt(second, original with { MaximumActiveChats = 7 })).WaitAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(2, barrier.Arrivals);
        Assert.Equal(Assert.Single(results.OfType<FloatingAgentChatSettings>()), await reader.GetSettingsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authority_revoked_after_SQL_rolls_back_the_settings_change(bool existing) {
        await using var database = PostgresTestDatabaseLease.Create("floatingsettingsauthority");
        var canonical = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var reader = new FloatingAgentChatSettingsService(WorkflowOwnerPersistenceTestFactory.FromCanonical(canonical),
            TimeProvider.System, WorkflowObservationTestSupport.Create());
        var original = existing ? await reader.SaveSettingsAsync(FloatingAgentChatSettings.Default) : await reader.GetSettingsAsync();
        var allowed = true;
        var flush = new SettingsFlushRevocation(() => allowed = false);
        var heldCanonical = new WorkflowUsagePostgresDbContextFactory(new DbContextOptionsBuilder<AppDbContext>(
            database.CreateAppDbContextOptions()).AddInterceptors(flush).Options);
        var changes = WorkflowObservationTestSupport.Create();
        var writer = new FloatingAgentChatSettingsService(WorkflowOwnerPersistenceTestFactory.FromCanonical(heldCanonical), TimeProvider.System, changes);
        var checks = 0;
        using (changes.EnterUiWrite(_ => {
            checks++;
            return allowed ? ValueTask.CompletedTask : throw new UnauthorizedAccessException("Revoked after SQL");
        })) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => writer.SaveSettingsAsync(original with { MaximumActiveChats = 8 }));
        }

        Assert.True(checks >= 2);
        Assert.Equal(1, flush.Writes);
        Assert.Equal(original, await reader.GetSettingsAsync());
    }

    [Fact]
    public async Task The_exact_accepted_stamp_round_trips_and_advances_even_when_the_clock_does_not() {
        await using var database = PostgresTestDatabaseLease.Create("floatingsettingsexact");
        var canonical = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero).AddTicks(1234));
        var service = new FloatingAgentChatSettingsService(WorkflowOwnerPersistenceTestFactory.FromCanonical(canonical), clock,
            WorkflowObservationTestSupport.Create());

        var accepted = await service.SaveSettingsAsync(FloatingAgentChatSettings.Default);
        var read = await service.GetSettingsAsync();
        var next = await service.SaveSettingsAsync(read with { MaximumPreparedAgents = 3 });

        Assert.Equal(accepted, read);
        Assert.NotNull(accepted.UpdatedAtUtc);
        Assert.Equal(0, accepted.UpdatedAtUtc.Value.Ticks % TimeSpan.TicksPerMicrosecond);
        Assert.Equal(accepted.UpdatedAtUtc.Value.AddTicks(TimeSpan.TicksPerMicrosecond), next.UpdatedAtUtc);
        Assert.Equal(next, await service.GetSettingsAsync());
    }

    [Fact]
    public async Task An_observed_settings_row_cannot_be_recreated_after_its_removal() {
        await using var database = PostgresTestDatabaseLease.Create("floatingsettingsremoved");
        var canonical = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var factory = WorkflowOwnerPersistenceTestFactory.FromCanonical(canonical);
        var service = new FloatingAgentChatSettingsService(factory, TimeProvider.System, WorkflowObservationTestSupport.Create());
        var original = await service.SaveSettingsAsync(FloatingAgentChatSettings.Default);
        await using (var other = await factory.CreateDbContextAsync()) {
            other.Remove(await other.Set<WorkflowSettingsRecord>().SingleAsync());
            await other.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<WorkflowSettingsConcurrencyException>(() => service.SaveSettingsAsync(original));

        Assert.Null((await service.GetSettingsAsync()).UpdatedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_stale_floating_settings_replacement_cannot_overwrite_a_peer(bool existing) {
        await using var database = PostgresTestDatabaseLease.Create("floatingchatsettingspeer");
        var canonical = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var factory = WorkflowOwnerPersistenceTestFactory.FromCanonical(canonical);
        var first = new FloatingAgentChatSettingsService(factory, TimeProvider.System, WorkflowObservationTestSupport.Create());
        var second = new FloatingAgentChatSettingsService(factory, TimeProvider.System, WorkflowObservationTestSupport.Create());
        var original = existing ? await first.SaveSettingsAsync(FloatingAgentChatSettings.Default) : await first.GetSettingsAsync();
        var peer = await second.SaveSettingsAsync(original with { MaximumActiveChats = 8 });

        await Assert.ThrowsAsync<WorkflowSettingsConcurrencyException>(() => first.SaveSettingsAsync(
            original with { MaximumActiveChats = 7 }));

        Assert.Equal(peer, await first.GetSettingsAsync());
    }

    private static async Task<FloatingAgentChatSettings?> Attempt(FloatingAgentChatSettingsService service, FloatingAgentChatSettings candidate) {
        try {
            return await service.SaveSettingsAsync(candidate);
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

    private sealed class FixedTimeProvider(DateTimeOffset instant) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
