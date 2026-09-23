using System.Data;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkspacePreferenceTransferOwnerTests {
    [Fact]
    public async Task Transfer_changes_only_the_latest_target_preference_and_preserves_source_and_other_profiles() {
        await using var environment = CanDoItAllTestEnvironment.Create("workspace-preference-transfer");
        var sourceProfile = environment.CreatePostgreSqlProfile("source");
        var targetProfile = environment.CreatePostgreSqlProfile("target");
        await using var sourceApplication = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = sourceProfile });
        await using var targetApplication = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = targetProfile });
        await using var source = await CreateCanonicalAsync(sourceApplication);
        await using var target = await CreateCanonicalAsync(targetApplication);
        var original = Settings("Source", 2003, Guid.NewGuid());
        var older = Settings("Older target", 2001, Guid.NewGuid());
        var current = Settings("Current target", 2002, Guid.NewGuid());
        await SeedLatestAsync(source, original);
        target.Add(older);
        await SeedLatestAsync(target, current);
        var sourceSnapshot = await SettingsSnapshotAsync(source);
        var otherTargetRows = await SettingsSnapshotAsync(target, current.Id);
        var sourceJson = JsonSerializer.Serialize(original);
        var olderJson = JsonSerializer.Serialize(older);
        var expected = JsonSerializer.Deserialize<WorkspaceSettings>(JsonSerializer.Serialize(current))!;
        var handler = CreateHandler();
        var transfer = Context(sourceApplication, targetApplication, source, target);

        var preview = await handler.PreviewAsync(transfer);
        var result = await handler.TransferAsync(transfer);

        Assert.True(preview.IsAvailable);
        Assert.Null(preview.Warning);
        Assert.Equal(1, preview.SourceRecordCount);
        Assert.Equal(1, preview.TargetRecordCount);
        Assert.True(result.Success);
        Assert.Equal(1, result.RecordsCopied);
        source.ChangeTracker.Clear();
        target.ChangeTracker.Clear();
        var updated = await target.Set<WorkspaceSettings>().SingleAsync(item => item.Id == current.Id);
        expected.DefaultProviderProfileId = original.DefaultProviderProfileId;
        expected.UpdatedAtUtc = updated.UpdatedAtUtc;
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(updated));
        Assert.True(updated.UpdatedAtUtc > current.UpdatedAtUtc);
        Assert.Equal(olderJson, JsonSerializer.Serialize(await target.Set<WorkspaceSettings>().SingleAsync(item => item.Id == older.Id)));
        Assert.Equal(sourceJson, JsonSerializer.Serialize(await source.Set<WorkspaceSettings>().SingleAsync(item => item.Id == original.Id)));
        Assert.False(await target.Set<WorkspaceSettings>().AnyAsync(item => item.Id == original.Id));
        Assert.Equal(sourceSnapshot, await SettingsSnapshotAsync(source));
        Assert.Equal(otherTargetRows, await SettingsSnapshotAsync(target, current.Id));
    }

    [Fact]
    public async Task Transfer_preserves_source_snapshot_and_rolls_back_target_after_a_completed_owner_save() {
        await using var environment = CanDoItAllTestEnvironment.Create("workspace-preference-transaction");
        await using var sourceApplication = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("source")
        });
        await using var targetApplication = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("target")
        });
        var sourceProfile = sourceApplication.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var targetProfile = targetApplication.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var original = Settings("Source", 2001, Guid.NewGuid());
        var retained = Settings("Target", 2001, Guid.NewGuid());
        string otherSourceRows;
        string targetSnapshot;
        await using (var seed = await CreateCanonicalAsync(sourceApplication)) {
            await SeedLatestAsync(seed, original);
            otherSourceRows = await SettingsSnapshotAsync(seed, original.Id);
        }
        await using (var seed = await CreateCanonicalAsync(targetApplication)) {
            await SeedLatestAsync(seed, retained);
            targetSnapshot = await SettingsSnapshotAsync(seed);
        }
        var sessions = new DatabaseTransferOwnerSessionRunner();
        var changedProviderId = Guid.NewGuid();
        var observed = false;
        var probeSource = new SourceSnapshotProbe();
        var probe = new RejectCompletedOwnerSave(async saved => {
            Assert.Equal(original.DefaultProviderProfileId, (await saved.Set<WorkspaceSettings>().AsNoTracking()
                .SingleAsync(item => item.Id == retained.Id)).DefaultProviderProfileId);
            Assert.Equal(IsolationLevel.Serializable, saved.Database.CurrentTransaction!.GetDbTransaction().IsolationLevel);
            await using (var changed = await CreateCanonicalAsync(sourceApplication)) {
                await changed.Set<WorkspaceSettings>().Where(item => item.Id == original.Id)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.DefaultProviderProfileId, changedProviderId));
            }
            Assert.Equal(IsolationLevel.RepeatableRead, probeSource.Transaction!.IsolationLevel);
            await using var snapshotCommand = probeSource.Connection!.CreateCommand();
            snapshotCommand.Transaction = probeSource.Transaction;
            snapshotCommand.CommandText = Assert.IsType<string>(probeSource.CommandText);
            Assert.Equal(original.DefaultProviderProfileId, (Guid?)await snapshotCommand.ExecuteScalarAsync());
            await using var independent = await CreateCanonicalAsync(targetApplication);
            Assert.Equal(retained.DefaultProviderProfileId, (await independent.Set<WorkspaceSettings>().AsNoTracking()
                .SingleAsync(item => item.Id == retained.Id)).DefaultProviderProfileId);
            Assert.Equal(targetSnapshot, await SettingsSnapshotAsync(independent));
            observed = true;
        });
        await using var source = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(AppDbContextOptionsConfigurator.CreateOptions(sourceProfile)).AddInterceptors(probeSource).Options);
        await using var target = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(AppDbContextOptionsConfigurator.CreateOptions(targetProfile))
            .AddInterceptors(probe).Options);
        var transfer = new DatabaseTransferOperation(sourceProfile, targetProfile, false);
        var operations = DatabaseTransferTestSupport.For(transfer, source, target, sessions);
        var handler = new WorkspaceDefaultProviderDatabaseTransferHandler(operations, sessions);
        Assert.Same(probe.Failure, await Assert.ThrowsAsync<InjectedPreferenceFailure>(() => handler.TransferAsync(transfer)));
        Assert.True(observed);
        await using var verification = await CreateCanonicalAsync(targetApplication);
        Assert.Equal(retained.DefaultProviderProfileId, (await verification.Set<WorkspaceSettings>()
            .SingleAsync(item => item.Id == retained.Id)).DefaultProviderProfileId);
        Assert.Equal(targetSnapshot, await SettingsSnapshotAsync(verification));
        await using var sourceVerification = await CreateCanonicalAsync(sourceApplication);
        var expectedSource = JsonSerializer.Deserialize<WorkspaceSettings>(JsonSerializer.Serialize(original))!;
        expectedSource.DefaultProviderProfileId = changedProviderId;
        Assert.Equal(JsonSerializer.Serialize(expectedSource), JsonSerializer.Serialize(await sourceVerification.Set<WorkspaceSettings>()
            .SingleAsync(item => item.Id == original.Id)));
        Assert.Equal(otherSourceRows, await SettingsSnapshotAsync(sourceVerification, original.Id));
    }

    [Fact]
    public async Task Latest_empty_source_preference_does_not_clear_or_replace_target_settings() {
        await using var environment = CanDoItAllTestEnvironment.Create("workspace-preference-empty");
        await using var sourceApplication = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("source")
        });
        await using var targetApplication = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("target")
        });
        await using var source = await CreateCanonicalAsync(sourceApplication);
        await using var target = await CreateCanonicalAsync(targetApplication);
        source.Add(Settings("Older source", 2001, Guid.NewGuid()));
        await SeedLatestAsync(source, Settings("Latest source", 2002, null));
        var retained = Settings("Retained target", 2001, Guid.NewGuid());
        await SeedLatestAsync(target, retained);
        var sourceSnapshot = await SettingsSnapshotAsync(source);
        var targetSnapshot = await SettingsSnapshotAsync(target);
        var serialized = JsonSerializer.Serialize(retained);
        var handler = CreateHandler();
        var transfer = Context(sourceApplication, targetApplication, source, target);

        var preview = await handler.PreviewAsync(transfer);
        Assert.False(preview.IsAvailable);
        Assert.Equal(0, preview.SourceRecordCount);
        var result = await handler.TransferAsync(transfer);

        Assert.False(result.Success);
        Assert.Equal(0, result.RecordsCopied);
        target.ChangeTracker.Clear();
        Assert.Equal(serialized, JsonSerializer.Serialize(await target.Set<WorkspaceSettings>().SingleAsync(item => item.Id == retained.Id)));
        Assert.Equal(sourceSnapshot, await SettingsSnapshotAsync(source));
        Assert.Equal(targetSnapshot, await SettingsSnapshotAsync(target));
    }

    [Fact]
    public async Task Owner_session_rejects_wrong_profiles_global_models_and_retained_contexts_after_scope_exit() {
        await using var application = await TestApplication.CreateAsync();
        await using var canonical = await CreateCanonicalAsync(application);
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var settings = new Npgsql.NpgsqlConnectionStringBuilder(profile.ConnectionString) { Database = "different-transfer-database" };
        var wrong = profile with { ConnectionString = settings.ConnectionString };
        Assert.Throws<InvalidOperationException>(() => new DatabaseTransferOwnerSession(wrong, canonical));
        await using var globalModel = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(AppDbContextOptionsConfigurator.CreateOptions(profile))
            .UseModel(canonical.Model).Options);
        Assert.Throws<InvalidOperationException>(() => new DatabaseTransferOwnerSession(profile, globalModel));

        using var session = new DatabaseTransferOwnerSession(profile, canonical);
        await using var owner = await session.CreateAsync<WorkspaceSettingsDbContext>(static options => new(options));
        Assert.Single(owner.Model.GetEntityTypes());
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CreateAsync<AppDbContext>(static options => new(options)));
        session.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.Set<WorkspaceSettings>().AnyAsync());
        owner.Add(Settings("Must not persist", 2001, Guid.NewGuid()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.SaveChangesAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.CreateAsync<WorkspaceSettingsDbContext>(static options => new(options)));
        Assert.True(await canonical.Database.CanConnectAsync());
    }

    private static WorkspaceSettings Settings(string name, int year, Guid? providerId) => new() {
        WorkspaceName = name, DefaultProviderProfileId = providerId, DefaultPromptOutputFormat = "JSON",
        CurrencyCode = "EUR", CurrencyCultureName = "de-DE", Notes = $"Keep {name} notes exactly. ",
        UpdatedAtUtc = new DateTimeOffset(year, 1, 2, 3, 4, 5, TimeSpan.Zero)
    };

    private static async Task SeedLatestAsync(AppDbContext database, WorkspaceSettings latest) {
        var previousLatest = await database.Set<WorkspaceSettings>().MaxAsync(item => (DateTimeOffset?)item.UpdatedAtUtc);
        latest.UpdatedAtUtc = DateTimeOffset.UtcNow;
        database.Add(latest);
        await database.SaveChangesAsync();
        await database.Entry(latest).ReloadAsync();
        Assert.True(!previousLatest.HasValue || latest.UpdatedAtUtc > previousLatest.Value);
        Assert.Equal(latest.Id, await database.Set<WorkspaceSettings>().OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => item.Id).FirstAsync());
    }

    private static async Task<string> SettingsSnapshotAsync(AppDbContext database, Guid? excludedId = null) {
        var query = database.Set<WorkspaceSettings>().AsNoTracking();
        if (excludedId is { } id) {
            query = query.Where(item => item.Id != id);
        }
        return JsonSerializer.Serialize(await query.OrderBy(item => item.Id).ToArrayAsync());
    }

    private static Task<AppDbContext> CreateCanonicalAsync(TestApplication application)
        => application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

    private static DatabaseTransferOperation Context(TestApplication sourceApplication, TestApplication targetApplication,
        AppDbContext source, AppDbContext target) => new(
        sourceApplication.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile,
        targetApplication.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile, true);

    private sealed class OwnerCommandProbe : DbCommandInterceptor {
        public List<(Type? ContextType, DbConnection? Connection, DbTransaction? Transaction)> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add((eventData.Context?.GetType(), command.Connection, command.Transaction));
            return ValueTask.FromResult(result);
        }
    }
    private static WorkspaceDefaultProviderDatabaseTransferHandler CreateHandler() {
        var sessions = new DatabaseTransferOwnerSessionRunner();
        return new(DatabaseTransferTestSupport.Create(sessions), sessions);
    }

    private sealed class InjectedPreferenceFailure : Exception;

    private sealed class RejectCompletedOwnerSave(Func<WorkspaceSettingsDbContext, Task> afterSave) : SaveChangesInterceptor {
        public InjectedPreferenceFailure Failure { get; } = new();

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (eventData.Context is WorkspaceSettingsDbContext owner) {
                await afterSave(owner);
                throw Failure;
            }
            return result;
        }
    }

    private sealed class SourceSnapshotProbe : DbCommandInterceptor {
        public DbConnection? Connection { get; private set; }
        public DbTransaction? Transaction { get; private set; }
        public string? CommandText { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (eventData.Context is WorkspaceSettingsDbContext) {
                Connection = command.Connection;
                Transaction = command.Transaction;
                CommandText = command.CommandText;
            }
            return ValueTask.FromResult(result);
        }
    }

}
