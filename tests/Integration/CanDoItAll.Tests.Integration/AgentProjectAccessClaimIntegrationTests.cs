using System.Data.Common;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentProjectAccessClaimIntegrationTests {
    [Fact]
    public async Task Concurrent_owner_claims_admit_one_generation_and_active_lease_is_not_reported_as_completion() {
        await using var application = await TestApplication.CreateAsync(Harness());
        var record = await SeedAsync(application.Services);
        await using var firstScope = application.Services.CreateAsyncScope();
        await using var secondScope = application.Services.CreateAsyncScope();
        var first = Participant(firstScope.ServiceProvider);
        var second = Participant(secondScope.ServiceProvider);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<AgentProjectAccessRevocationClaim?> ClaimWhenReleased(AgentProjectStructureAccessDeletionParticipant participant) {
            await release.Task;
            return await participant.TryClaimAsync(record);
        }
        var attempts = new[] { ClaimWhenReleased(first), ClaimWhenReleased(second) };
        release.SetResult(true);
        var claims = await Task.WhenAll(attempts);
        var winner = Assert.Single(claims.OfType<AgentProjectAccessRevocationClaim>());
        Assert.Equal(1, winner.Generation);
        var pending = Assert.Single(await first.ListPendingRecoveriesAsync());
        Assert.Equal(record.Id, pending.RecoveryId);
        Assert.Equal(ProjectDeletionRecoveryStatus.Processing, pending.Status);
        Assert.False(pending.CanRetryNow);
        Assert.NotNull(pending.RetryAvailableAtUtc);
        await Assert.ThrowsAsync<ProjectDeletionParticipantCleanupException>(() => second.CompleteAsync(new(record.ProjectId, record.Id)));
        var afterRefusal = await ReadAsync(application.Services, record.Id);
        Assert.Equal(1, afterRefusal.AttemptCount);
        Assert.Equal(AgentProjectStructureAccessRevocationStatus.Processing, afterRefusal.Status);
        Assert.Null(afterRefusal.LastFailureCode);
        Assert.True(await first.TryCompleteClaimAsync(winner));
        Assert.Equal(record.Id, (await second.CompleteAsync(new(record.ProjectId, record.Id))).RecoveryId);
        Assert.Equal(1, (await ReadAsync(application.Services, record.Id)).AttemptCount);
    }

    [Fact]
    public async Task Expired_generation_cannot_renew_complete_or_fail_a_new_owner_and_database_time_controls_reclaim() {
        await using var application = await TestApplication.CreateAsync(Harness());
        var record = await SeedAsync(application.Services);
        await using var scope = application.Services.CreateAsyncScope();
        var current = Participant(scope.ServiceProvider);
        var skewed = Participant(scope.ServiceProvider, timeProvider: new FutureTimeProvider());
        var oldClaim = Assert.IsType<AgentProjectAccessRevocationClaim>(await current.TryClaimAsync(record));
        var processing = await ReadAsync(application.Services, record.Id);
        Assert.Null(await skewed.TryClaimAsync(processing));
        Assert.False(Assert.Single(await skewed.ListPendingRecoveriesAsync()).CanRetryNow);
        await ExpireAsync(application.Services, record.Id);
        Assert.True(Assert.Single(await current.ListPendingRecoveriesAsync()).CanRetryNow);
        Assert.False(await current.TryRenewClaimAsync(oldClaim));
        Assert.False(await current.TryCompleteClaimAsync(oldClaim));
        Assert.False(await current.TryFailClaimAsync(oldClaim, "expired-owner"));
        var nextClaim = Assert.IsType<AgentProjectAccessRevocationClaim>(
            await skewed.TryClaimAsync(await ReadAsync(application.Services, record.Id)));
        Assert.Equal(oldClaim.Generation + 1, nextClaim.Generation);
        Assert.False(await current.TryRenewClaimAsync(oldClaim));
        Assert.False(await current.TryCompleteClaimAsync(oldClaim));
        Assert.False(await current.TryFailClaimAsync(oldClaim, "superseded-owner"));
        var newer = await ReadAsync(application.Services, record.Id);
        Assert.Equal(nextClaim.Generation, newer.AttemptCount);
        Assert.Equal(AgentProjectStructureAccessRevocationStatus.Processing, newer.Status);
        Assert.Null(newer.LastFailureCode);
        Assert.True(newer.LastAttemptAtUtc < new DateTimeOffset(2090, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.True(await current.TryCompleteClaimAsync(nextClaim));
        var completed = await ReadAsync(application.Services, record.Id);
        Assert.Equal(AgentProjectStructureAccessRevocationStatus.Completed, completed.Status);
        Assert.NotNull(completed.CompletedAtUtc);
        Assert.True(completed.CompletedAtUtc < new DateTimeOffset(2090, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Heartbeat_preserves_attempt_time_and_ownership_loss_cancels_processing_without_rewriting_the_successor() {
        var options = AgentProjectAccessClaimOptions.Default with { HeartbeatInterval = TimeSpan.FromMilliseconds(40) };
        await using var application = await TestApplication.CreateAsync(Harness(options));
        var record = await SeedAsync(application.Services);
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, CancellableWorkspaceProxy>();
        var control = (CancellableWorkspaceProxy)(object)workspace;
        await using var scope = application.Services.CreateAsyncScope();
        var first = Participant(scope.ServiceProvider, workspace, options);
        var second = Participant(scope.ServiceProvider, options: options);
        var completion = first.CompleteAsync(new(record.ProjectId, record.Id));
        await control.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var entered = await ReadAsync(application.Services, record.Id);
        Assert.Equal(1, entered.AttemptCount);
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10))) {
            while (true) {
                var heartbeat = await ReadAsync(application.Services, record.Id, timeout.Token);
                Assert.Equal(entered.LastAttemptAtUtc, heartbeat.LastAttemptAtUtc);
                if (heartbeat.UpdatedAtUtc > entered.UpdatedAtUtc) {
                    break;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(25), timeout.Token);
            }
        }
        await ExpireAsync(application.Services, record.Id);
        var successor = Assert.IsType<AgentProjectAccessRevocationClaim>(
            await second.TryClaimAsync(await ReadAsync(application.Services, record.Id)));
        await control.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.ThrowsAsync<ProjectDeletionParticipantCleanupException>(() => completion);
        var afterLoss = await ReadAsync(application.Services, record.Id);
        Assert.Equal(successor.Generation, afterLoss.AttemptCount);
        Assert.Equal(AgentProjectStructureAccessRevocationStatus.Processing, afterLoss.Status);
        Assert.Null(afterLoss.LastFailureCode);
        Assert.Null(afterLoss.CompletedAtUtc);
        Assert.Equal(1, control.InvocationCount);
        Assert.True(await second.TryCompleteClaimAsync(successor));
    }

    [Theory]
    [InlineData(AgentProjectStructureAccessRevocationStatus.Processing)]
    [InlineData(AgentProjectStructureAccessRevocationStatus.Completed)]
    [InlineData(AgentProjectStructureAccessRevocationStatus.Failed)]
    public async Task A_transition_blocked_until_lease_expiry_cannot_renew_complete_or_fail_the_same_generation(
        AgentProjectStructureAccessRevocationStatus transitionStatus) {
        var options = AgentProjectAccessClaimOptions.Default with {
            LeaseDuration = TimeSpan.FromSeconds(3), HeartbeatInterval = TimeSpan.FromSeconds(1)
        };
        await using var application = await TestApplication.CreateAsync(Harness(options));
        var record = await SeedAsync(application.Services);
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var participant = Participant(services, options: options);
        var claim = Assert.IsType<AgentProjectAccessRevocationClaim>(await participant.TryClaimAsync(record));
        var before = await ReadAsync(services, record.Id);
        await using var blocker = await services.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>().CreateDbContextAsync();
        await using var heldTransaction = await blocker.Database.BeginTransactionAsync();
        Assert.Single(await blocker.Database.SqlQuery<Guid>($"""
            SELECT "Id" AS "Value" FROM "AgentFramework_ProjectAccessRevocations" WHERE "Id" = {record.Id} FOR UPDATE
            """).ToArrayAsync());
        var observer = new ClaimCommandObserver();
        var interceptedOptions = new DbContextOptionsBuilder<AgentProjectAccessDbContext>(
            services.GetRequiredService<DbContextOptions<AgentProjectAccessDbContext>>()).AddInterceptors(observer).Options;
        var delayed = Participant(services, options: options, factory: new ObservedContextFactory(interceptedOptions));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var transition = transitionStatus switch {
            AgentProjectStructureAccessRevocationStatus.Processing => delayed.TryRenewClaimAsync(claim, timeout.Token),
            AgentProjectStructureAccessRevocationStatus.Completed => delayed.TryCompleteClaimAsync(claim, timeout.Token),
            AgentProjectStructureAccessRevocationStatus.Failed => delayed.TryFailClaimAsync(claim, "delayed-owner", timeout.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(transitionStatus))
        };
        bool result;
        try {
            var backendId = await observer.Started.Task.WaitAsync(timeout.Token);
            await using var inspection = await services.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>().CreateDbContextAsync(timeout.Token);
            await WaitUntilAsync(async () => await inspection.Database.SqlQuery<bool>($"""
                SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid = {backendId} AND wait_event_type = 'Lock') AS "Value"
                """).SingleAsync(timeout.Token), timeout.Token);
            Assert.False(await LeaseExpiredAsync(inspection, record.Id, options.LeaseDuration, timeout.Token));
            Assert.False(transition.IsCompleted);
            await WaitUntilAsync(() => LeaseExpiredAsync(inspection, record.Id, options.LeaseDuration, timeout.Token), timeout.Token);
            Assert.False(transition.IsCompleted);
        } finally {
            await heldTransaction.RollbackAsync();
            result = await transition;
        }
        Assert.False(result);
        var after = await ReadAsync(services, record.Id);
        Assert.Equal(before.AttemptCount, after.AttemptCount);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc);
        Assert.Equal(before.LastAttemptAtUtc, after.LastAttemptAtUtc);
        Assert.Equal(before.CompletedAtUtc, after.CompletedAtUtc);
        Assert.Equal(before.LastFailureCode, after.LastFailureCode);
    }

    [Fact]
    public async Task Abandoned_claim_reclaims_the_same_recovery_after_host_restart_and_profiles_keep_independent_generations() {
        await using var environment = CanDoItAllTestEnvironment.Create("agent-project-access-claim-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var harness = Harness(environment: environment, profile: profile);
        AgentProjectStructureAccessRevocationRecord record;
        await using (var original = await TestApplication.CreateAsync(harness)) {
            record = await SeedAsync(original.Services);
            await using var scope = original.Services.CreateAsyncScope();
            Assert.Equal(1, (await Participant(scope.ServiceProvider).TryClaimAsync(record))?.Generation);
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var participant = Participant(restartedScope.ServiceProvider);
        Assert.Equal(record.Id, Assert.Single(await participant.ListPendingRecoveriesAsync()).RecoveryId);
        Assert.Null(await participant.TryClaimAsync(await ReadAsync(restarted.Services, record.Id)));
        await ExpireAsync(restarted.Services, record.Id);
        var next = Assert.IsType<AgentProjectAccessRevocationClaim>(await participant.TryClaimAsync(await ReadAsync(restarted.Services, record.Id)));
        Assert.Equal(record.Id, next.RecoveryId);
        Assert.Equal(2, next.Generation);
        await using var other = await TestApplication.CreateAsync(Harness(environment: environment, profile: otherProfile));
        await using var otherScope = other.Services.CreateAsyncScope();
        var otherParticipant = Participant(otherScope.ServiceProvider);
        Assert.Empty(await otherParticipant.ListPendingRecoveriesAsync());
        Assert.Null(await otherParticipant.TryClaimAsync(record));
        Assert.True(await participant.TryCompleteClaimAsync(next));
        Assert.Equal(2, (await ReadAsync(restarted.Services, record.Id)).AttemptCount);
    }

    private static AgentProjectStructureAccessDeletionParticipant Participant(IServiceProvider services,
        IAgentFrameworkWorkspaceService? workspace = null, AgentProjectAccessClaimOptions? options = null, TimeProvider? timeProvider = null,
        IDbContextFactory<AgentProjectAccessDbContext>? factory = null) => new(
        workspace!, factory ?? services.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>(), timeProvider ?? TimeProvider.System,
        NullLogger<AgentProjectStructureAccessDeletionParticipant>.Instance, services.GetRequiredService<DbContextOptions<AgentProjectAccessDbContext>>(),
        services.GetRequiredService<CoordinatedDatabaseTransaction>(), options ?? AgentProjectAccessClaimOptions.Default);

    private static TestHarnessOptions Harness(AgentProjectAccessClaimOptions? options = null,
        CanDoItAllTestEnvironment? environment = null, TestDatabaseProfile? profile = null) => new() {
        TestEnvironment = environment, ActiveProfile = profile,
        ConfigureServices = services => services.AddSingleton(options ?? AgentProjectAccessClaimOptions.Default)
    };

    private static async Task<AgentProjectStructureAccessRevocationRecord> SeedAsync(IServiceProvider services) {
        await using var context = await services.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>().CreateDbContextAsync();
        var now = await context.Database.SqlQueryRaw<DateTimeOffset>("SELECT CURRENT_TIMESTAMP AS \"Value\"").SingleAsync();
        var record = new AgentProjectStructureAccessRevocationRecord {
            Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Status = AgentProjectStructureAccessRevocationStatus.Pending,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        context.Add(record);
        await context.SaveChangesAsync();
        return record;
    }

    private static async Task<AgentProjectStructureAccessRevocationRecord> ReadAsync(IServiceProvider services, Guid id,
        CancellationToken cancellationToken = default) {
        await using var context = await services.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>().CreateDbContextAsync(cancellationToken);
        return await context.Set<AgentProjectStructureAccessRevocationRecord>().AsNoTracking().SingleAsync(item => item.Id == id, cancellationToken);
    }

    private static async Task ExpireAsync(IServiceProvider services, Guid id) {
        await using var context = await services.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>().CreateDbContextAsync();
        var now = await context.Database.SqlQueryRaw<DateTimeOffset>("SELECT CURRENT_TIMESTAMP AS \"Value\"").SingleAsync();
        var expired = now - TimeSpan.FromMinutes(20);
        Assert.Equal(1, await context.Set<AgentProjectStructureAccessRevocationRecord>().Where(item => item.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UpdatedAtUtc, expired).SetProperty(item => item.LastAttemptAtUtc, expired)));
    }

    private static Task<bool> LeaseExpiredAsync(AgentProjectAccessDbContext context, Guid recoveryId, TimeSpan lease,
        CancellationToken cancellationToken) => context.Database.SqlQuery<bool>($"""
            SELECT GREATEST("UpdatedAtUtc", "LastAttemptAtUtc") + {lease} <= clock_timestamp() AS "Value"
            FROM "AgentFramework_ProjectAccessRevocations" WHERE "Id" = {recoveryId}
            """).SingleAsync(cancellationToken);

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, CancellationToken cancellationToken) {
        while (!await condition()) {
            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }
    }

    private sealed class ObservedContextFactory(DbContextOptions<AgentProjectAccessDbContext> options)
        : IDbContextFactory<AgentProjectAccessDbContext> {
        public AgentProjectAccessDbContext CreateDbContext() => new(options);
    }

    private sealed class ClaimCommandObserver : DbCommandInterceptor {
        public TaskCompletionSource<int> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Observe(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Observe(command);
            return ValueTask.FromResult(result);
        }

        private void Observe(DbCommand command) {
            if (command.CommandText.Contains("AgentFramework_ProjectAccessRevocations", StringComparison.Ordinal) &&
                (command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal) || command.CommandText.Contains("UPDATE ", StringComparison.Ordinal))) {
                Started.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
            }
        }
    }

    private sealed class FutureTimeProvider : TimeProvider {
        public override DateTimeOffset GetUtcNow() => new(2099, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    public class CancellableWorkspaceProxy : DispatchProxy {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int InvocationCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name != nameof(IAgentFrameworkWorkspaceService.RevokeProjectStructureAccessFromAllAgentsAsync)) {
                throw new NotSupportedException(targetMethod?.Name);
            }
            return RunAsync((CancellationToken)args![1]!);
        }

        private async Task<int> RunAsync(CancellationToken cancellationToken) {
            InvocationCount++;
            Entered.TrySetResult(true);
            try {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("The test workspace should leave only by cancellation.");
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                Cancelled.TrySetResult(true);
                throw;
            }
        }
    }
}
