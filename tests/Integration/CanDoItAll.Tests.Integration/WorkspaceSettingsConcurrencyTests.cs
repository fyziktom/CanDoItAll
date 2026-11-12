using System.Collections.Concurrent;
using System.Data.Common;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed partial class WorkspaceSettingsConcurrencyTests {
    [Fact]
    public async Task Two_original_heads_have_exactly_one_accepted_replacement_across_independent_contexts() {
        await using var application = await TestApplication.CreateAsync();
        await using var firstScope = application.Services.CreateAsyncScope();
        await using var secondScope = application.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var second = secondScope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var a = await first.ReadSettingsSnapshotAsync();
        var b = await second.ReadSettingsSnapshotAsync();
        Assert.Equal(a.Expectation, b.Expectation);
        var outcomes = await Task.WhenAll(first.SaveSettingsAsync(Command(a, "First")), second.SaveSettingsAsync(Command(b, "Second")));
        var accepted = Assert.Single(outcomes.OfType<WorkspaceSettingsSaveOutcome.Accepted>());
        Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged, Assert.Single(outcomes.OfType<WorkspaceSettingsSaveOutcome.Rejected>()).Reason);
        Assert.Equal(accepted.Snapshot, await first.ReadSettingsSnapshotAsync());
    }

    [Fact]
    public async Task Two_empty_observations_create_exactly_one_current_head() {
        await using var application = await TestApplication.CreateAsync();
        await using (var seed = await CanonicalAsync(application)) {
            await seed.Set<WorkspaceSettings>().Where(item => item.IsCurrent).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsCurrent, false));
        }
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var a = await owner.ReadSettingsSnapshotAsync();
        var b = await owner.ReadSettingsSnapshotAsync();
        Assert.Equal(WorkspaceSettingsHeadState.Empty, a.Expectation.State);
        var outcomes = await Task.WhenAll(owner.SaveSettingsAsync(Command(a, "First creation")), owner.SaveSettingsAsync(Command(b, "Second creation")));
        var accepted = Assert.Single(outcomes.OfType<WorkspaceSettingsSaveOutcome.Accepted>());
        Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged, Assert.Single(outcomes.OfType<WorkspaceSettingsSaveOutcome.Rejected>()).Reason);
        await using var read = await CanonicalAsync(application);
        Assert.Equal(accepted.Snapshot.Expectation.Head!.Id, (await read.Set<WorkspaceSettings>().SingleAsync(item => item.IsCurrent)).Id);
    }

    [Fact]
    public async Task Accepted_normalized_noop_advances_revision_and_SQL_requires_the_original_revision() {
        var probe = new MutationProbe();
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = probe.Configure });
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var original = await owner.ReadSettingsSnapshotAsync();
        probe.Armed = true;
        var accepted = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await owner.SaveSettingsAsync(new(original.Expectation, original.Values)));
        Assert.Equal(original.Values, accepted.Snapshot.Values);
        Assert.Equal(original.Expectation.Head!.Id, accepted.Snapshot.Expectation.Head!.Id);
        Assert.NotEqual(original.Expectation.Head.Revision, accepted.Snapshot.Expectation.Head.Revision);
        var update = Assert.Single(probe.Updates);
        Assert.Contains("WHERE", update.Sql, StringComparison.Ordinal);
        Assert.Contains("\"ConcurrencyToken\"", update.Sql[update.Sql.IndexOf("WHERE", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.Contains(original.Expectation.Head.Revision, update.Identifiers);
        Assert.Equal(1, probe.Commits);
    }

    [Fact]
    public async Task Reactivation_rejects_the_old_namespace_even_when_row_and_revision_are_unchanged() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var original = await owner.ReadSettingsSnapshotAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        await application.Services.GetRequiredService<DatasetActivationCoordinator>().ReactivateAsync(profile, original.Expectation.Namespace, Guid.NewGuid());
        var outcome = Assert.IsType<WorkspaceSettingsSaveOutcome.Rejected>(await owner.SaveSettingsAsync(Command(original, "Old dataset draft")));
        Assert.Equal(WorkspaceSettingsRejectionReason.DatasetChanged, outcome.Reason);
        var current = await owner.ReadSettingsSnapshotAsync();
        Assert.NotEqual(original.Expectation.Namespace, current.Expectation.Namespace);
        Assert.Equal(original.Expectation.Head, current.Expectation.Head);
        Assert.Equal(original.Values, current.Values);
    }

    [Fact]
    public async Task A_newer_inactive_timestamp_cannot_replace_the_explicit_current_head() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var original = await owner.ReadSettingsSnapshotAsync();
        var retained = new WorkspaceSettings { WorkspaceName = "Inactive future row", UpdatedAtUtc = new(2100, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        await using (var seed = await CanonicalAsync(application)) {
            seed.Add(retained);
            await seed.SaveChangesAsync();
        }
        Assert.Equal(original, await owner.ReadSettingsSnapshotAsync());
        var accepted = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await owner.SaveSettingsAsync(Command(original, "Current replacement")));
        Assert.Equal(original.Expectation.Head!.Id, accepted.Snapshot.Expectation.Head!.Id);
        await using var read = await CanonicalAsync(application);
        var inactive = await read.Set<WorkspaceSettings>().SingleAsync(item => item.Id == retained.Id);
        Assert.False(inactive.IsCurrent);
        Assert.Equal(retained.WorkspaceName, inactive.WorkspaceName);
        Assert.Equal(retained.ConcurrencyToken, inactive.ConcurrencyToken);
        Assert.Equal(retained.UpdatedAtUtc, inactive.UpdatedAtUtc);
    }

    [Fact]
    public async Task Real_commit_followed_by_lost_acknowledgement_remains_unconfirmed_without_a_receipt() {
        var probe = new MutationProbe(Fault.LostCommitAcknowledgement);
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = probe.Configure });
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var original = await owner.ReadSettingsSnapshotAsync();
        probe.Armed = true;
        Assert.IsType<WorkspaceSettingsSaveOutcome.Unconfirmed>(await owner.SaveSettingsAsync(Command(original, "Actually committed")));
        Assert.Equal(1, probe.Commits);
        Assert.Equal(1, probe.Faults);
        var current = await owner.ReadSettingsSnapshotAsync();
        Assert.Equal("Actually committed", current.Values.WorkspaceName);
        Assert.NotEqual(original.Expectation.Head!.Revision, current.Expectation.Head!.Revision);
    }

    [Fact]
    public async Task Real_acknowledged_commit_followed_by_owned_connection_disposal_failure_stays_accepted() {
        var probe = new MutationProbe(Fault.CleanupAfterAcknowledgement);
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = probe.Configure });
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var original = await owner.ReadSettingsSnapshotAsync();
        probe.Armed = true;
        var accepted = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await owner.SaveSettingsAsync(Command(original, "Acknowledged")));
        Assert.Equal(WorkspaceSettingsSecondaryDiagnostic.CleanupPending, accepted.Diagnostic);
        Assert.Equal(1, probe.Commits);
        Assert.Equal(1, probe.Disposals);
        Assert.Equal(1, probe.Faults);
        Assert.Equal(accepted.Snapshot, await owner.ReadSettingsSnapshotAsync());
    }

    [Fact]
    public async Task Precommit_failure_preserves_its_primary_error_and_releases_exclusion_despite_cleanup_failure() {
        var probe = new MutationProbe(Fault.BeforeSaveAndCleanup);
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = probe.Configure });
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var original = await owner.ReadSettingsSnapshotAsync();
        probe.Armed = true;
        var failure = await Assert.ThrowsAsync<IOException>(() => owner.SaveSettingsAsync(Command(original, "Must roll back")));
        Assert.Equal("Owned Workspace precommit failure.", failure.Message);
        Assert.Equal(0, probe.Commits);
        Assert.Equal(1, probe.Disposals);
        Assert.Equal(1, probe.Faults);
        Assert.Equal(original, await owner.ReadSettingsSnapshotAsync());
        await using var independent = await CanonicalAsync(application);
        await using var transaction = await independent.Database.BeginTransactionAsync();
        await independent.Database.ExecuteSqlRawAsync("LOCK TABLE \"Workspace_Settings\" IN SHARE ROW EXCLUSIVE MODE NOWAIT");
    }

    [Fact]
    public async Task Current_head_reads_do_not_wait_for_the_ordinary_writer_table_exclusion() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var original = await owner.ReadSettingsSnapshotAsync();
        await using var guard = await CanonicalAsync(application);
        await using var transaction = await guard.Database.BeginTransactionAsync();
        await guard.Database.ExecuteSqlRawAsync("LOCK TABLE \"Workspace_Settings\" IN SHARE ROW EXCLUSIVE MODE");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(original, await owner.ReadSettingsSnapshotAsync(deadline.Token));
        Assert.NotNull(guard.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Provider_bootstrap_waits_for_lifecycle_before_acquiring_any_provider_table_lock() {
        await using var application = await TestApplication.CreateAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var binding = await application.Services.GetRequiredService<DatasetActivationService>().ReadActiveBindingAsync(profile.Profile.Id);
        await using var guard = await CanonicalAsync(application);
        await using var transaction = await guard.Database.BeginTransactionAsync();
        await DatasetLifecycleStore.RequireActiveForMaintenanceAsync(guard, binding, default);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var preparing = application.Services.GetRequiredService<ProviderDefaultsBootstrapService>().PrepareAsync(profile, null, deadline.Token);
        try {
            await using var observer = new NpgsqlConnection(profile.ConnectionString);
            await observer.OpenAsync(deadline.Token);
            await using var query = new NpgsqlCommand("""
                SELECT pid FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                    AND query LIKE '%FROM "Infrastructure_DatasetLifecycle" WHERE "Id" = 1 FOR SHARE%'
                """, observer);
            int? blocked = null;
            while (blocked is null) {
                blocked = await query.ExecuteScalarAsync(deadline.Token) as int?;
                if (blocked is null) {
                    await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token);
                }
            }
            await using var held = new NpgsqlCommand("""
                SELECT count(*) FROM pg_locks locks JOIN pg_class tables ON locks.relation = tables.oid
                WHERE locks.pid = @pid AND tables.relname = 'Workspace_ProviderProfiles'
                """, observer);
            held.Parameters.AddWithValue("pid", blocked.Value);
            Assert.Equal(0L, (long)(await held.ExecuteScalarAsync(deadline.Token))!);
            await transaction.RollbackAsync(deadline.Token);
            await using var stage = await preparing;
            Assert.False(stage.IsCommitted);
        } finally {
            await transaction.DisposeAsync();
            await using var completed = await preparing;
        }
    }

    private static ReplaceWorkspaceSettingsCommand Command(WorkspaceSettingsSnapshot original, string name) => new(original.Expectation,
        new(name, original.Values.DefaultProviderProfileId, original.Values.DefaultPromptOutputFormat,
            original.Values.CurrencyCode, original.Values.CurrencyCultureName, original.Values.Notes));

    private static Task<AppDbContext> CanonicalAsync(TestApplication application)
        => application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

    private enum Fault { None, LostCommitAcknowledgement, CleanupAfterAcknowledgement, BeforeSaveAndCleanup }

    private sealed class MutationProbe(Fault fault = Fault.None) {
        public bool Armed { get; set; }
        public ConcurrentDictionary<Guid, byte> Mutations { get; } = new();
        public ConcurrentQueue<(string Sql, Guid[] Identifiers)> Updates { get; } = new();
        public int Commits;
        public int Disposals;
        public int Faults;
        public Fault Failure => fault;

        public void Configure(IServiceCollection services) {
            services.AddSingleton<IDbContextFactory<WorkspaceSettingsDbContext>>(provider => new ContextFactory(
                new DbContextOptionsBuilder<WorkspaceSettingsDbContext>(provider.GetRequiredService<DbContextOptions<WorkspaceSettingsDbContext>>())
                    .AddInterceptors(new SaveProbe(this), new CommitProbe(this), new DisposalProbe(this), new CommandProbe(this)).Options));
        }
    }

    private sealed class ContextFactory(DbContextOptions<WorkspaceSettingsDbContext> options) : IDbContextFactory<WorkspaceSettingsDbContext> {
        public WorkspaceSettingsDbContext CreateDbContext() => new(options);
    }

    private sealed class SaveProbe(MutationProbe probe) : SaveChangesInterceptor {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (probe.Armed && eventData.Context is WorkspaceSettingsDbContext context) {
                probe.Mutations.TryAdd(context.ContextId.InstanceId, 0);
                if (probe.Failure == Fault.BeforeSaveAndCleanup) {
                    throw new IOException("Owned Workspace precommit failure.");
                }
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CommitProbe(MutationProbe probe) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (eventData.Context is { } context && probe.Mutations.ContainsKey(context.ContextId.InstanceId)) {
                Interlocked.Increment(ref probe.Commits);
                if (probe.Failure == Fault.LostCommitAcknowledgement) {
                    Interlocked.Increment(ref probe.Faults);
                    throw new IOException("Owned interruption after the actual Workspace commit.");
                }
            }
            return Task.CompletedTask;
        }
    }

    private sealed class DisposalProbe(MutationProbe probe) : DbConnectionInterceptor {
        public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            if (eventData.Context is { } context && probe.Mutations.ContainsKey(context.ContextId.InstanceId)) {
                Interlocked.Increment(ref probe.Disposals);
                if (probe.Failure is Fault.CleanupAfterAcknowledgement or Fault.BeforeSaveAndCleanup) {
                    Interlocked.Increment(ref probe.Faults);
                    throw new IOException("Owned failure after the Workspace connection was disposed.");
                }
            }
            return Task.CompletedTask;
        }
    }

    private sealed class CommandProbe(MutationProbe probe) : DbCommandInterceptor {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (probe.Armed && command.CommandText.Contains("UPDATE \"Workspace_Settings\"", StringComparison.Ordinal)) {
                probe.Updates.Enqueue((command.CommandText, command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value).OfType<Guid>().ToArray()));
            }
            return ValueTask.FromResult(result);
        }
    }
}
