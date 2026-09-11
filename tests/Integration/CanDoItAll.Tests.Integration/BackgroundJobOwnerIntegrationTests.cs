using CanDoItAll.Infrastructure.BackgroundJobs;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Persistence;

public sealed class BackgroundJobOwnerIntegrationTests {
    [Fact]
    public async Task Registered_owner_model_matches_legacy_table_without_foreign_entities() {
        await using var application = await TestApplication.CreateAsync();
        var factory = application.Services.GetRequiredService<IDbContextFactory<BackgroundJobsDbContext>>();
        await using var owner = await factory.CreateDbContextAsync();
        await using var legacy = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        Assert.True(owner.Database.IsNpgsql());
        var entity = Assert.Single(owner.Model.GetEntityTypes());
        Assert.Equal(typeof(BackgroundJobRecord), entity.ClrType);
        var original = legacy.Model.FindEntityType(typeof(BackgroundJobRecord))!;
        Assert.Equal(original.GetTableName(), entity.GetTableName());
        Assert.Equal(original.GetSchema(), entity.GetSchema());
        Assert.Equal(Properties(original), Properties(entity));
        Assert.Empty(entity.GetForeignKeys());
        Assert.DoesNotContain(entity.GetProperties(), property => property.IsConcurrencyToken);
    }

    [Fact]
    public async Task Registered_tracker_stays_on_canonical_profile_until_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("background-job-owner-profile");
        await using var services = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(environment);
        var profiles = services.GetRequiredService<IDatabaseProfileService>();
        var alpha = await profiles.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
            environment.CreatePostgreSqlProfile("alpha"), "Alpha"));
        Assert.True(alpha.IsSuccess);
        Assert.True((await profiles.ActivateAsync(alpha.Value)).IsSuccess);
        var accessor = services.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var bootstrapper = services.GetRequiredService<IAppDatabaseBootstrapper>();
        await bootstrapper.EnsureCurrentProfileReadyAsync();
        await using var scope = services.CreateAsyncScope();
        var tracker = scope.ServiceProvider.GetRequiredService<IBackgroundJobTracker>();
        var first = await tracker.CreateTrackedAsync("alpha", "before activation");
        var beta = await profiles.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
            environment.CreatePostgreSqlProfile("beta"), "Beta"));
        Assert.True(beta.IsSuccess);
        var target = accessor.ResolveProfile(beta.Value);
        await bootstrapper.EnsureProfileReadyAsync(target);
        Assert.True((await services.GetRequiredService<IDatabaseSwitchCoordinator>().SwitchAsync(beta.Value)).IsSuccess);
        var second = await tracker.CreateTrackedAsync("alpha", "after pending activation");
        Assert.Equal(alpha.Value, accessor.ResolveCurrentProfile().Profile.Id);
        Assert.Equal(new[] { first, second }.Order(), (await tracker.ListAsync()).Select(job => job.Id).Order());
        var options = new DbContextOptionsBuilder<BackgroundJobsDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, target);
        await using (var targetContext = new BackgroundJobsDbContext(options.Options)) {
            Assert.Empty(await targetContext.Set<BackgroundJobRecord>().ToListAsync());
        }
        await using var restarted = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(environment);
        await restarted.GetRequiredService<IAppDatabaseBootstrapper>().EnsureCurrentProfileReadyAsync();
        await using var restartedScope = restarted.CreateAsyncScope();
        var restartedTracker = restartedScope.ServiceProvider.GetRequiredService<IBackgroundJobTracker>();
        Assert.Empty(await restartedTracker.ListAsync());
        var third = await restartedTracker.CreateTrackedAsync("beta", "after restart");
        Assert.Equal(third, Assert.Single(await restartedTracker.ListAsync()).Id);
        Assert.DoesNotContain(await tracker.ListAsync(), job => job.Id == third);
    }

    [Fact]
    public async Task Tracker_preserves_queue_correlation_state_transitions_and_latest_fifty_without_deleting_history() {
        await using var application = await TestApplication.CreateAsync();
        var factory = application.Services.GetRequiredService<IDbContextFactory<BackgroundJobsDbContext>>();
        var epoch = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using (var owner = await factory.CreateDbContextAsync()) {
            owner.AddRange(Enumerable.Range(0, 55).Select(index => new BackgroundJobRecord {
                JobType = "historical", Description = $"old {index}", CorrelationId = Guid.NewGuid(),
                CreatedAtUtc = epoch.AddMinutes(index), UpdatedAtUtc = epoch.AddMinutes(index)
            }));
            await owner.SaveChangesAsync();
        }
        await using var scope = application.Services.CreateAsyncScope();
        var tracker = Assert.IsType<BackgroundJobTracker>(scope.ServiceProvider.GetRequiredService<IBackgroundJobTracker>());
        var id = await tracker.EnqueueTrackedAsync("  accepted  ", "  queued description  ", new Dictionary<string, string> { ["Key"] = "value" });
        var queued = await application.Services.GetRequiredService<IBackgroundJobQueue>().DequeueAsync();
        Assert.Equal("accepted", queued.JobType);
        Assert.Equal("queued description", queued.Description);
        Assert.Equal("value", queued.Metadata!["key"]);
        await using (var owner = await factory.CreateDbContextAsync()) {
            Assert.Equal(queued.CorrelationId, (await owner.Set<BackgroundJobRecord>().SingleAsync(job => job.Id == id)).CorrelationId);
        }
        await tracker.MarkFailedAsync(id, "synthetic fault");
        Assert.Equal("synthetic fault", (await tracker.ListAsync())[0].ErrorSummary);
        await tracker.MarkQueuedAsync(id);
        await tracker.MarkRunningAsync(id);
        await tracker.MarkSucceededAsync(id);
        await tracker.MarkFailedAsync(Guid.NewGuid(), "missing record");
        var records = await tracker.ListAsync();
        Assert.Equal(50, records.Count);
        Assert.Equal(id, records[0].Id);
        Assert.Equal(BackgroundJobState.Succeeded, records[0].State);
        Assert.Null(records[0].ErrorSummary);
        Assert.True(records.Zip(records.Skip(1)).All(pair => pair.First.UpdatedAtUtc >= pair.Second.UpdatedAtUtc));
        await using var readback = await factory.CreateDbContextAsync();
        Assert.Equal(56, await readback.Set<BackgroundJobRecord>().CountAsync());
    }

    [Theory]
    [InlineData(EnqueueFault.BeforeAccept)]
    [InlineData(EnqueueFault.AfterAccept)]
    [InlineData(EnqueueFault.Cancellation)]
    public async Task Enqueue_failure_exposes_committed_identity_without_retry_or_compensation(EnqueueFault mode) {
        await using var application = await TestApplication.CreateAsync();
        var factory = application.Services.GetRequiredService<IDbContextFactory<BackgroundJobsDbContext>>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Exception failure = mode == EnqueueFault.Cancellation
            ? new OperationCanceledException(cancellation.Token)
            : new InvalidOperationException("synthetic enqueue failure");
        var queue = new FaultingQueue(mode, failure, async request => {
            await using var persisted = await factory.CreateDbContextAsync();
            var record = await persisted.Set<BackgroundJobRecord>().SingleAsync();
            Assert.Equal(request.CorrelationId, record.CorrelationId);
            Assert.Equal(BackgroundJobState.Queued.ToString(), record.State);
        });
        var tracker = new BackgroundJobTracker(factory, queue, new SystemClock());
        var caught = await Record.ExceptionAsync(() => tracker.EnqueueTrackedAsync("fault", "committed row"));
        Guid jobId;
        Guid correlationId;
        if (mode == EnqueueFault.Cancellation) {
            var canceled = Assert.IsType<BackgroundJobEnqueueCanceledException>(caught);
            Assert.Equal(cancellation.Token, canceled.CancellationToken);
            jobId = canceled.JobId;
            correlationId = canceled.CorrelationId;
        } else {
            var unconfirmed = Assert.IsType<BackgroundJobEnqueueException>(caught);
            jobId = unconfirmed.JobId;
            correlationId = unconfirmed.CorrelationId;
        }
        Assert.Same(failure, caught!.InnerException);
        Assert.Equal(1, queue.Calls);
        Assert.Equal(mode == EnqueueFault.AfterAccept, queue.Accepted);
        await using var readback = await factory.CreateDbContextAsync();
        var retained = Assert.Single(await readback.Set<BackgroundJobRecord>().ToListAsync());
        Assert.Equal(jobId, retained.Id);
        Assert.Equal(correlationId, retained.CorrelationId);
        Assert.Equal(BackgroundJobState.Queued.ToString(), retained.State);
    }

    private static string[] Properties(IEntityType type) {
        var table = StoreObjectIdentifier.Table(type.GetTableName()!, type.GetSchema());
        return type.GetProperties().OrderBy(property => property.Name).Select(property =>
            $"{property.Name}|{property.GetColumnName(table)}|{property.GetColumnType()}|{property.GetMaxLength()}|{property.IsNullable}|{property.ValueGenerated}").ToArray();
    }

    public enum EnqueueFault { BeforeAccept, AfterAccept, Cancellation }

    private sealed class FaultingQueue(EnqueueFault mode, Exception failure,
        Func<BackgroundJobRequest, Task> assertPersisted) : IBackgroundJobQueue {
        public int Calls { get; private set; }
        public bool Accepted { get; private set; }
        public async ValueTask EnqueueAsync(BackgroundJobRequest job, CancellationToken cancellationToken = default) {
            Calls++;
            await assertPersisted(job);
            Accepted = mode == EnqueueFault.AfterAccept;
            throw failure;
        }
        public ValueTask<BackgroundJobRequest> DequeueAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
