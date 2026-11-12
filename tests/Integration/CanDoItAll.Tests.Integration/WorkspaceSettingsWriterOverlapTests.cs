using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkspaceSettingsWriterOverlapTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Bootstrap_and_ordinary_save_observe_the_head_after_their_actual_exclusion_wait(bool bootstrapFirst) {
        var saveGate = new SavedHeadGate();
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = saveGate.Configure });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = services.GetRequiredService<WorkspaceService>();
        var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var chosenProvider = Guid.NewGuid();
        await using (var providers = await services.GetRequiredService<IDbContextFactory<ProvidersDbContext>>().CreateDbContextAsync()) {
            providers.Add(new ProviderProfile { Id = chosenProvider, Name = "Explicit retained provider", IsEnabled = true });
            await providers.SaveChangesAsync();
        }
        var initial = await owner.ReadSettingsSnapshotAsync();
        var original = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await owner.SaveSettingsAsync(new(initial.Expectation,
            new("Original name", Guid.NewGuid(), "JSON", "EUR", "de-DE", "Original notes")))).Snapshot;
        var policy = " \n" + DecisionDataPolicyCodec.Serialize(DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(4))) + "\n ";
        await SetPolicyAsync(application, policy);
        var desired = new WorkspaceSettingsValues("Human name", chosenProvider, "Markdown", "USD", "en-US", "Human notes");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var observer = new NpgsqlConnection(profile.ConnectionString);
        await observer.OpenAsync(deadline.Token);
        var bootstrapGate = new HeadGate();
        Task<WorkspaceSettingsSaveOutcome>? saving = null;
        Task<(bool Changed, Guid Provider)>? bootstrapping = null;
        try {
            if (bootstrapFirst) {
                bootstrapping = BootstrapAsync(services, profile, bootstrapGate, deadline.Token);
                await bootstrapGate.Entered.Task.WaitAsync(deadline.Token);
                saving = owner.SaveSettingsAsync(new(original.Expectation, desired), deadline.Token);
                await WaitForBlockedAsync(observer, bootstrapGate.BackendPid, "LOCK TABLE \"Workspace_Settings\" IN SHARE ROW EXCLUSIVE MODE", deadline.Token);
                Assert.False(saving.IsCompleted);
                bootstrapGate.Release.TrySetResult();
            } else {
                saveGate.Armed = true;
                saving = owner.SaveSettingsAsync(new(original.Expectation, desired), deadline.Token);
                await saveGate.Gate.Entered.Task.WaitAsync(deadline.Token);
                bootstrapping = BootstrapAsync(services, profile, null, deadline.Token);
                await WaitForBlockedAsync(observer, saveGate.Gate.BackendPid, "LOCK TABLE \"Workspace_Settings\" IN SHARE ROW EXCLUSIVE MODE", deadline.Token);
                Assert.False(bootstrapping.IsCompleted);
                saveGate.Gate.Release.TrySetResult();
            }
            var bootstrap = await bootstrapping;
            var outcome = await saving;
            var current = await owner.ReadSettingsSnapshotAsync(deadline.Token);
            if (bootstrapFirst) {
                Assert.True(bootstrap.Changed);
                Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged, Assert.IsType<WorkspaceSettingsSaveOutcome.Rejected>(outcome).Reason);
                Assert.Equal(new WorkspaceSettingsValues(original.Values.WorkspaceName, bootstrap.Provider,
                    original.Values.DefaultPromptOutputFormat, original.Values.CurrencyCode,
                    original.Values.CurrencyCultureName, original.Values.Notes), current.Values);
                Assert.NotEqual(original.Expectation.Head!.Revision, current.Expectation.Head!.Revision);
            } else {
                Assert.False(bootstrap.Changed);
                Assert.Equal(desired, current.Values);
                Assert.Equal(Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(outcome).Snapshot, current);
            }
            Assert.Equal(policy, await ReadPolicyAsync(application));
        } finally {
            bootstrapGate.Release.TrySetResult();
            saveGate.Gate.Release.TrySetResult();
            await DrainAsync(saving, bootstrapping);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Transfer_and_ordinary_save_preserve_the_actual_predecessor_and_only_change_owned_fields(bool transferFirst) {
        var saveGate = new SavedHeadGate();
        await using var environment = CanDoItAllTestEnvironment.Create("workspace-writer-overlap");
        await using var source = await TestApplication.CreateAsync(new() {
            TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("source")
        });
        await using var target = await TestApplication.CreateAsync(new() {
            TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("target"), ConfigureServices = saveGate.Configure
        });
        await using var sourceScope = source.Services.CreateAsyncScope();
        await using var targetScope = target.Services.CreateAsyncScope();
        var sourceOwner = sourceScope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var targetOwner = targetScope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var sourceOriginal = await sourceOwner.ReadSettingsSnapshotAsync();
        var copiedProvider = Guid.NewGuid();
        var sourceAccepted = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await sourceOwner.SaveSettingsAsync(new(sourceOriginal.Expectation,
            new("Source name", copiedProvider, "JSON", "EUR", "de-DE", "Source notes")))).Snapshot;
        var original = await targetOwner.ReadSettingsSnapshotAsync();
        var sourcePolicy = DecisionDataPolicyCodec.Serialize(DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(6)));
        var targetPolicy = " \n" + DecisionDataPolicyCodec.Serialize(DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(7), TimeSpan.FromMinutes(8))) + "\n ";
        await SetPolicyAsync(source, sourcePolicy);
        await SetPolicyAsync(target, targetPolicy);
        var desired = new WorkspaceSettingsValues("Human target", Guid.NewGuid(), "JSON", "EUR", "de-DE", "Retain target notes");
        var sourceProfile = source.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var targetProfile = target.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var transferGate = new SavedHeadGate { Armed = transferFirst };
        await using var sourceContext = new AppDbContext(AppDbContextOptionsConfigurator.CreateOptions(sourceProfile));
        await using var targetContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(AppDbContextOptionsConfigurator.CreateOptions(targetProfile))
            .AddInterceptors(transferGate).Options);
        var transfer = new DatabaseTransferOperation(sourceProfile, targetProfile, false);
        var sessions = new DatabaseTransferOwnerSessionRunner();
        var handler = new WorkspaceDefaultProviderDatabaseTransferHandler(DatabaseTransferTestSupport.For(transfer, sourceContext,
            targetContext, sessions, activation: target.Services.GetRequiredService<DatasetActivationService>()), sessions);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var observer = new NpgsqlConnection(targetProfile.ConnectionString);
        await observer.OpenAsync(deadline.Token);
        Task<WorkspaceSettingsSaveOutcome>? saving = null;
        Task<DatabaseTransferItemResult>? transferring = null;
        try {
            if (transferFirst) {
                transferring = handler.TransferAsync(transfer, deadline.Token);
                await transferGate.Gate.Entered.Task.WaitAsync(deadline.Token);
                saving = targetOwner.SaveSettingsAsync(new(original.Expectation, desired), deadline.Token);
                await WaitForBlockedAsync(observer, transferGate.Gate.BackendPid, "FROM \"Infrastructure_DatasetLifecycle\" WHERE \"Id\" = 1 FOR SHARE", deadline.Token);
                Assert.False(saving.IsCompleted);
                transferGate.Gate.Release.TrySetResult();
            } else {
                saveGate.Armed = true;
                saving = targetOwner.SaveSettingsAsync(new(original.Expectation, desired), deadline.Token);
                await saveGate.Gate.Entered.Task.WaitAsync(deadline.Token);
                transferring = handler.TransferAsync(transfer, deadline.Token);
                await WaitForBlockedAsync(observer, saveGate.Gate.BackendPid, "LOCK TABLE \"Infrastructure_DatasetLifecycle\" IN EXCLUSIVE MODE", deadline.Token);
                Assert.False(transferring.IsCompleted);
                saveGate.Gate.Release.TrySetResult();
            }
            var transferred = await transferring;
            Assert.True(transferred.Success);
            Assert.Equal(1, transferred.RecordsCopied);
            var saved = await saving;
            var current = await targetOwner.ReadSettingsSnapshotAsync(deadline.Token);
            var retained = transferFirst ? original.Values : Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(saved).Snapshot.Values;
            Assert.Equal(new WorkspaceSettingsValues(retained.WorkspaceName, copiedProvider, retained.DefaultPromptOutputFormat,
                retained.CurrencyCode, retained.CurrencyCultureName, retained.Notes), current.Values);
            Assert.Equal(original.Expectation.Head!.Id, current.Expectation.Head!.Id);
            Assert.NotEqual(original.Expectation.Head.Revision, current.Expectation.Head.Revision);
            Assert.Equal(sourceAccepted, await sourceOwner.ReadSettingsSnapshotAsync(deadline.Token));
            Assert.Equal(sourcePolicy, await ReadPolicyAsync(source));
            Assert.Equal(targetPolicy, await ReadPolicyAsync(target));
            if (transferFirst) {
                Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged, Assert.IsType<WorkspaceSettingsSaveOutcome.Rejected>(saved).Reason);
            } else {
                Assert.NotEqual(Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(saved).Snapshot.Expectation.Head!.Revision,
                    current.Expectation.Head.Revision);
            }
            var noOp = await handler.TransferAsync(transfer, deadline.Token);
            Assert.True(noOp.Success);
            Assert.Equal(0, noOp.RecordsCopied);
            Assert.Equal(current, await targetOwner.ReadSettingsSnapshotAsync(deadline.Token));
            Assert.Equal(targetPolicy, await ReadPolicyAsync(target));
            await using (var seed = await target.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync(deadline.Token)) {
                await seed.Set<WorkspaceSettings>().Where(row => row.IsCurrent)
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.IsCurrent, false), deadline.Token);
            }
            var created = await handler.TransferAsync(transfer, deadline.Token);
            Assert.True(created.Success);
            Assert.Equal(1, created.RecordsCopied);
            var newTarget = await targetOwner.ReadSettingsSnapshotV2Async(deadline.Token);
            Assert.Equal(copiedProvider, newTarget.Values.DefaultProviderProfileId);
            Assert.NotEqual(current.Expectation.Head!.Id, newTarget.Expectation.Head!.Id);
            Assert.Null(newTarget.DecisionDataPolicy);
            Assert.Null(await ReadPolicyAsync(target));
        } finally {
            saveGate.Gate.Release.TrySetResult();
            transferGate.Gate.Release.TrySetResult();
            await DrainAsync(saving, transferring);
        }
    }

    private static async Task SetPolicyAsync(TestApplication application, string policy) {
        await using var context = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await context.Set<WorkspaceSettings>().Where(row => row.IsCurrent)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.DecisionDataPolicyJson, policy));
    }

    private static async Task<string?> ReadPolicyAsync(TestApplication application) {
        await using var context = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        return await context.Set<WorkspaceSettings>().Where(row => row.IsCurrent).Select(row => row.DecisionDataPolicyJson).SingleAsync();
    }

    private static async Task<(bool Changed, Guid Provider)> BootstrapAsync(IServiceProvider services,
        ResolvedDatabaseProfile profile, HeadGate? gate, CancellationToken cancellationToken) {
        await using var stage = await services.GetRequiredService<ProviderDefaultsBootstrapService>().PrepareAsync(profile, null, cancellationToken);
        using var participation = stage.EnterTransaction();
        var options = new DbContextOptionsBuilder<WorkspaceSettingsDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        await using var enlisted = await stage.Transactions.CreateEnlistedAsync(options.Options,
            static configured => new WorkspaceSettingsDbContext(configured), cancellationToken);
        var changed = await services.GetRequiredService<WorkspaceDefaultsBootstrapService>().EnsureAsync(profile, stage.Transactions,
            stage.NewWorkspaceDefaultProviderId, stage.MatchedProviderId, async (id, token) => {
                var exists = await stage.ProviderExistsAsync(id, token);
                if (gate is not null) {
                    await gate.HoldAsync(enlisted, token);
                }
                return exists;
            }, DateTimeOffset.UtcNow, cancellationToken);
        await stage.CommitAsync(changed, cancellationToken);
        Assert.True(stage.IsCommitted);
        return (changed, stage.MatchedProviderId);
    }

    private static async Task WaitForBlockedAsync(NpgsqlConnection observer, int blocker, string commandFragment,
        CancellationToken cancellationToken) {
        Assert.True(blocker > 0);
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM pg_stat_activity
            WHERE datname = current_database() AND wait_event_type = 'Lock'
                AND position(@fragment in query) > 0 AND @blocker = ANY(pg_blocking_pids(pid))
            """, observer);
        command.Parameters.AddWithValue("fragment", commandFragment);
        command.Parameters.AddWithValue("blocker", blocker);
        while ((long)(await command.ExecuteScalarAsync(cancellationToken))! != 1) {
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }
    }

    private static async Task DrainAsync(params Task?[] tasks) {
        foreach (var task in tasks) {
            if (task is not null) {
                try {
                    await task;
                } catch (Exception) {
                }
            }
        }
    }

    private sealed class HeadGate {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int BackendPid { get; private set; }

        public async Task HoldAsync(DbContext context, CancellationToken cancellationToken) {
            BackendPid = ((NpgsqlConnection)context.Database.GetDbConnection()).ProcessID;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class SavedHeadGate : SaveChangesInterceptor {
        private int entered;
        public bool Armed { get; set; }
        public HeadGate Gate { get; } = new();

        public void Configure(IServiceCollection services) {
            services.AddSingleton<IDbContextFactory<WorkspaceSettingsDbContext>>(provider => new ContextFactory(
                new DbContextOptionsBuilder<WorkspaceSettingsDbContext>(provider.GetRequiredService<DbContextOptions<WorkspaceSettingsDbContext>>())
                    .AddInterceptors(this).Options));
        }

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && eventData.Context is WorkspaceSettingsDbContext context && Interlocked.Exchange(ref entered, 1) == 0) {
                await Gate.HoldAsync(context, cancellationToken);
            }
            return result;
        }
    }

    private sealed class ContextFactory(DbContextOptions<WorkspaceSettingsDbContext> options) : IDbContextFactory<WorkspaceSettingsDbContext> {
        public WorkspaceSettingsDbContext CreateDbContext() => new(options);
    }
}
