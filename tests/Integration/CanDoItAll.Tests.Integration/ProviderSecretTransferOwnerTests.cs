using CanDoItAll.Tests.Support;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Integration;

public sealed class ProviderSecretTransferOwnerTests {
    [Fact]
    public async Task Transfer_preserves_the_original_source_snapshot_full_protected_values_and_unselected_target_state() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var selected = Secret(Guid.NewGuid(), "selected source");
        var missingSecretId = Guid.NewGuid();
        var profiles = new[] { Provider(selected.Id), Provider(selected.Id), Provider(missingSecretId) };
        var unrelated = Secret(Guid.NewGuid(), "unrelated target");
        var preference = new WorkspaceSettings { WorkspaceName = "Target preference", DefaultProviderProfileId = Guid.NewGuid(), Notes = "Keep all settings." };
        await using var sourceDb = source.Factory.CreateDbContext();
        await using var targetDb = target.Factory.CreateDbContext();
        sourceDb.Add(selected);
        sourceDb.AddRange(profiles);
        targetDb.AddRange(Secret(selected.Id, "replaced target"), Secret(missingSecretId, "missing source reference"), unrelated, preference, Provider(null));
        var reference = new SecretReference { SecretRecordId = selected.Id, ContextType = "retained", ContextId = "target", Purpose = "Keep reference identity." };
        targetDb.Add(reference);
        await sourceDb.SaveChangesAsync();
        await targetDb.SaveChangesAsync();
        var expectedSecret = JsonSerializer.Serialize(selected);
        var expectedProfiles = profiles.OrderBy(profile => profile.Id).Select(profile => JsonSerializer.Serialize(profile)).ToArray();
        var sessions = new DatabaseTransferOwnerSessionRunner();
        var actualSecrets = new SecretDatabaseTransferParticipant(sessions);
        var secrets = new ObservedSecrets(actualSecrets, afterTable: async _ => {
            await using var changed = source.Factory.CreateDbContext();
            await changed.Set<SecretRecord>().Where(secret => secret.Id == selected.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(secret => secret.EncryptedPayload, "changed after source snapshot"));
            await changed.Set<ProviderProfile>().Where(profile => profile.Id == profiles[0].Id)
                .ExecuteUpdateAsync(update => update.SetProperty(profile => profile.Name, "Changed after source snapshot"));
        });
        var handler = new AiProvidersDatabaseTransferHandler(secrets, sessions, DatabaseTransferTestSupport.For(new(source.Profile, target.Profile, true), sourceDb, targetDb, sessions), [new SharedProviderDatabaseTransferGuard()]);

        var result = await handler.TransferAsync(new(source.Profile, target.Profile, true));

        Assert.True(result.Success);
        Assert.Equal(4, result.RecordsCopied);
        targetDb.ChangeTracker.Clear();
        Assert.Equal(expectedSecret, JsonSerializer.Serialize(await targetDb.Set<SecretRecord>().SingleAsync(secret => secret.Id == selected.Id)));
        Assert.Equal(expectedProfiles, (await targetDb.Set<ProviderProfile>().AsNoTracking().ToArrayAsync()).OrderBy(profile => profile.Id).Select(profile => JsonSerializer.Serialize(profile)));
        Assert.Equal(JsonSerializer.Serialize(unrelated), JsonSerializer.Serialize(await targetDb.Set<SecretRecord>().SingleAsync(secret => secret.Id == unrelated.Id)));
        Assert.Equal(JsonSerializer.Serialize(preference), JsonSerializer.Serialize(await targetDb.Set<WorkspaceSettings>().SingleAsync()));
        Assert.Equal(JsonSerializer.Serialize(reference), JsonSerializer.Serialize(await targetDb.Set<SecretReference>().SingleAsync()));
        Assert.False(await targetDb.Set<SecretRecord>().AnyAsync(secret => secret.Id == missingSecretId));
        Assert.Equal("changed after source snapshot", await sourceDb.Set<SecretRecord>().AsNoTracking().Where(secret => secret.Id == selected.Id)
            .Select(secret => secret.EncryptedPayload).SingleAsync());
    }

    [Fact]
    public async Task Secret_stage_failure_rolls_back_intermediate_deletes_and_protected_value_replacement_on_the_exact_transaction() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var selectedId = Guid.NewGuid();
        var sourceSecret = Secret(selectedId, "source");
        var targetSecret = Secret(selectedId, "target");
        var originalProvider = Provider(selectedId);
        await using var sourceDb = source.Factory.CreateDbContext();
        await using var targetDb = target.Factory.CreateDbContext();
        sourceDb.AddRange(sourceSecret, Provider(selectedId));
        targetDb.AddRange(targetSecret, originalProvider);
        await sourceDb.SaveChangesAsync();
        await targetDb.SaveChangesAsync();
        var sessions = new DatabaseTransferOwnerSessionRunner();
        var observed = false;
        var secrets = new ObservedSecrets(new SecretDatabaseTransferParticipant(sessions), afterCopy: async request => {
            await using var secretOwner = await sessions.CreateTargetAsync<SecurityDbContext>(request, static options => new SecurityDbContext(options));
            await using var providerOwner = await sessions.CreateTargetAsync<ProvidersDbContext>(request, static options => new ProvidersDbContext(options));
            Assert.Same(targetDb.Database.GetDbConnection(), secretOwner.Database.GetDbConnection());
            Assert.Same(providerOwner.Database.CurrentTransaction!.GetDbTransaction(), secretOwner.Database.CurrentTransaction!.GetDbTransaction());
            Assert.Equal(IsolationLevel.Serializable, secretOwner.Database.CurrentTransaction!.GetDbTransaction().IsolationLevel);
            Assert.Empty(await providerOwner.Set<ProviderProfile>().ToArrayAsync());
            Assert.Equal(sourceSecret.EncryptedPayload, (await secretOwner.Set<SecretRecord>().SingleAsync()).EncryptedPayload);
            await using var independent = target.Factory.CreateDbContext();
            Assert.True(await independent.Set<ProviderProfile>().AnyAsync(profile => profile.Id == originalProvider.Id));
            Assert.Equal(targetSecret.EncryptedPayload, (await independent.Set<SecretRecord>().SingleAsync()).EncryptedPayload);
            observed = true;
            throw new InjectedTransferFailure();
        });
        var handler = new AiProvidersDatabaseTransferHandler(secrets, sessions, DatabaseTransferTestSupport.For(new(source.Profile, target.Profile, true), sourceDb, targetDb, sessions), [new SharedProviderDatabaseTransferGuard()]);

        await Assert.ThrowsAsync<InjectedTransferFailure>(() => handler.TransferAsync(new(source.Profile, target.Profile, true)));

        Assert.True(observed);
        targetDb.ChangeTracker.Clear();
        Assert.Equal(JsonSerializer.Serialize(originalProvider), JsonSerializer.Serialize(await targetDb.Set<ProviderProfile>().SingleAsync()));
        Assert.Equal(JsonSerializer.Serialize(targetSecret), JsonSerializer.Serialize(await targetDb.Set<SecretRecord>().SingleAsync()));
    }

    [Fact]
    public async Task Final_guard_observes_a_shared_reference_committed_while_waiting_for_target_table_locks() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var secretId = Guid.NewGuid();
        await using var sourceDb = source.Factory.CreateDbContext();
        sourceDb.AddRange(Secret(secretId, "source"), Provider(secretId));
        await sourceDb.SaveChangesAsync();
        var retained = Provider(secretId);
        await using (var seed = target.Factory.CreateDbContext()) {
            seed.AddRange(Secret(secretId, "target"), retained);
            await seed.SaveChangesAsync();
        }
        await using var writer = target.Factory.CreateDbContext();
        await using var writerTransaction = await writer.Database.BeginTransactionAsync();
        writer.Add(new SharedProviderSource {
            Name = "Concurrent shared source", BaseUri = "https://remote.example.test", ApiTokenSecretId = secretId,
            RemoteInstanceId = SharedProviderSourceInstanceId.New(), CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await writer.SaveChangesAsync();
        var probe = new LockProbe();
        await using var targetDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(AppDbContextOptionsConfigurator.CreateOptions(target.Profile))
            .AddInterceptors(probe).Options);
        var sessions = new DatabaseTransferOwnerSessionRunner();
        var handler = new AiProvidersDatabaseTransferHandler(new SecretDatabaseTransferParticipant(sessions), sessions, DatabaseTransferTestSupport.For(new(source.Profile, target.Profile, true), sourceDb, targetDb, sessions), [new SharedProviderDatabaseTransferGuard()]);
        var transfer = handler.TransferAsync(new(source.Profile, target.Profile, true));
        await probe.Requested.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await writerTransaction.CommitAsync();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => transfer);

        Assert.Contains("a target shared-provider source uses a secret", failure.Message);
        Assert.True(await targetDb.Set<ProviderProfile>().AnyAsync(profile => profile.Id == retained.Id));
        Assert.Equal("protected target value", (await targetDb.Set<SecretRecord>().SingleAsync()).EncryptedPayload);
        var command = Assert.IsType<string>(probe.Command);
        Assert.Contains("Security_SecretRecords", command, StringComparison.Ordinal);
        Assert.Contains("Workspace_SharedProviderSources", command, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Distinct_logical_profiles_for_the_same_physical_database_are_rejected_before_transaction_or_mutation() {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var sourceDb = database.Factory.CreateDbContext();
        await using var targetDb = database.Factory.CreateDbContext();
        var provider = Provider(null);
        sourceDb.Add(provider);
        await sourceDb.SaveChangesAsync();
        var alias = database.Profile with { Profile = new DatabaseProfileRecord {
            DisplayName = "Same physical database", ProviderKind = DatabaseProviderKind.PostgreSql
        } };
        var sessions = new DatabaseTransferOwnerSessionRunner();
        var handler = new AiProvidersDatabaseTransferHandler(new SecretDatabaseTransferParticipant(sessions), sessions, DatabaseTransferTestSupport.For(new(database.Profile, alias, true), sourceDb, targetDb, sessions));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.TransferAsync(new(database.Profile, alias, true)));

        Assert.Null(sourceDb.Database.CurrentTransaction);
        Assert.Null(targetDb.Database.CurrentTransaction);
        Assert.Equal(JsonSerializer.Serialize(provider), JsonSerializer.Serialize(await targetDb.Set<ProviderProfile>().SingleAsync()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mixed_provider_transfer_rejects_before_changing_either_store(bool postgresIsSource) {
        await using var postgres = await HistoryPersistenceTestDatabase.CreateAsync();
        var inMemory = new ResolvedDatabaseProfile(new DatabaseProfileRecord { ProviderKind = DatabaseProviderKind.InMemory },
            DatabaseProfileResolutionSource.ExplicitOverride, $"mixed-provider-transfer-{Guid.NewGuid():N}");
        await using var memoryDb = new AppDbContext(AppDbContextOptionsConfigurator.CreateOptions(inMemory));
        await using var postgresDb = postgres.Factory.CreateDbContext();
        memoryDb.Add(Provider(null));
        postgresDb.Add(Provider(null));
        await memoryDb.SaveChangesAsync();
        await postgresDb.SaveChangesAsync();
        var memoryBefore = JsonSerializer.Serialize(await memoryDb.Set<ProviderProfile>().AsNoTracking().ToArrayAsync());
        var postgresBefore = JsonSerializer.Serialize(await postgresDb.Set<ProviderProfile>().AsNoTracking().ToArrayAsync());
        var transfer = postgresIsSource
            ? new DatabaseTransferOperation(postgres.Profile, inMemory, true)
            : new DatabaseTransferOperation(inMemory, postgres.Profile, true);
        var sessions = new DatabaseTransferOwnerSessionRunner();
        var handler = new AiProvidersDatabaseTransferHandler(new SecretDatabaseTransferParticipant(sessions), sessions, DatabaseTransferTestSupport.For(transfer, postgresIsSource ? postgresDb : memoryDb, postgresIsSource ? memoryDb : postgresDb, sessions));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.TransferAsync(transfer));

        Assert.Contains("mixed-provider transfer is unsupported", failure.Message);
        Assert.Null(postgresDb.Database.CurrentTransaction);
        Assert.Equal(memoryBefore, JsonSerializer.Serialize(await memoryDb.Set<ProviderProfile>().AsNoTracking().ToArrayAsync()));
        Assert.Equal(postgresBefore, JsonSerializer.Serialize(await postgresDb.Set<ProviderProfile>().AsNoTracking().ToArrayAsync()));
    }

    [Fact]
    public async Task Failure_of_final_Provider_save_rolls_back_the_completed_Secret_owner_stage() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var secretId = Guid.NewGuid();
        var retained = Provider(secretId);
        var retainedSecret = Secret(secretId, "target");
        await using var sourceDb = source.Factory.CreateDbContext();
        sourceDb.AddRange(Secret(secretId, "source"), Provider(secretId));
        await sourceDb.SaveChangesAsync();
        await using (var seed = target.Factory.CreateDbContext()) {
            seed.AddRange(retained, retainedSecret);
            await seed.SaveChangesAsync();
        }
        var failure = new RejectFinalProviderSave();
        await using var targetDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(AppDbContextOptionsConfigurator.CreateOptions(target.Profile))
            .AddInterceptors(failure).Options);
        var sessions = new DatabaseTransferOwnerSessionRunner();
        var handler = new AiProvidersDatabaseTransferHandler(new SecretDatabaseTransferParticipant(sessions), sessions, DatabaseTransferTestSupport.For(new(source.Profile, target.Profile, true), sourceDb, targetDb, sessions));

        await Assert.ThrowsAsync<InjectedTransferFailure>(() => handler.TransferAsync(new(source.Profile, target.Profile, true)));

        Assert.True(failure.Reached);
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(await targetDb.Set<ProviderProfile>().AsNoTracking().SingleAsync()));
        Assert.Equal(JsonSerializer.Serialize(retainedSecret), JsonSerializer.Serialize(await targetDb.Set<SecretRecord>().AsNoTracking().SingleAsync()));
    }

    private static SecretRecord Secret(Guid id, string name) => new() {
        Id = id, Name = name, Kind = SecretKind.ApiKey, EncryptedPayload = $"protected {name} value", Scope = "custom-scope",
        MetadataJson = "{\"unknown\":{\"retain\":true}}", RotationNote = "Keep rotation note. ",
        CreatedAtUtc = new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero), UpdatedAtUtc = new DateTimeOffset(2025, 2, 3, 4, 5, 6, TimeSpan.Zero)
    };

    private static ProviderProfile Provider(Guid? secretId) => new() {
        Name = "Historical provider", ConnectorPluginKey = ProviderConnectorKeys.OpenAi, ConfigSchemaVersion = "1.0",
        ApiKeySecretId = secretId, BaseUrl = "https://provider.example.test", DefaultModel = "retained-model", ConcurrencyToken = Guid.NewGuid()
    };

    private sealed class ObservedSecrets(ISecretDatabaseTransferParticipant inner,
        Func<DatabaseTransferOwnerRequest, Task>? afterTable = null, Func<DatabaseTransferOwnerRequest, Task>? afterCopy = null) : ISecretDatabaseTransferParticipant {
        public async Task<DatabaseTransferTable> GetTargetTableAsync(DatabaseTransferOwnerRequest transfer, CancellationToken cancellationToken = default) {
            var table = await inner.GetTargetTableAsync(transfer, cancellationToken);
            if (afterTable is not null) {
                await afterTable(transfer);
            }
            return table;
        }
        public async Task<int> CopySelectedAsync(DatabaseTransferOwnerRequest transfer, IReadOnlyCollection<Guid> secretIds, CancellationToken cancellationToken = default) {
            var count = await inner.CopySelectedAsync(transfer, secretIds, cancellationToken);
            if (afterCopy is not null) {
                await afterCopy(transfer);
            }
            return count;
        }
    }

    private sealed class LockProbe : DbCommandInterceptor {
        public TaskCompletionSource Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? Command { get; private set; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.StartsWith("LOCK TABLE ", StringComparison.Ordinal)) {
                Command = command.CommandText;
                Requested.TrySetResult();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class InjectedTransferFailure : Exception;

    private sealed class RejectFinalProviderSave : SaveChangesInterceptor {
        public bool Reached { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (eventData.Context is ProvidersDbContext context && context.ChangeTracker.Entries<ProviderProfile>().Any(entry => entry.State == EntityState.Added)) {
                Reached = true;
                throw new InjectedTransferFailure();
            }
            return ValueTask.FromResult(result);
        }
    }
}
