using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Integration;

public sealed class ProviderTransferGuardOwnerTests {
    [Fact]
    public async Task Guard_uses_selected_target_and_only_secrets_selected_for_replacement() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var sourceDb = source.Factory.CreateDbContext();
        await using var targetDb = target.Factory.CreateDbContext();
        var secretId = Guid.NewGuid();
        await SeedProviderAsync(sourceDb, secretId);
        var unrelatedSecretId = Guid.NewGuid();
        targetDb.AddRange(Secret(secretId), Secret(unrelatedSecretId));
        var remote = RemoteSource(secretId);
        targetDb.Add(remote);
        await targetDb.SaveChangesAsync();
        var context = new DatabaseTransferOperation(source.Profile, target.Profile, true);
        var harness = new TransferHarness(source.Profile, target.Profile);

        var blocked = await harness.Handler.PreviewAsync(context);

        Assert.False(blocked.IsAvailable);
        var warning = Assert.IsType<string>(blocked.Warning);
        Assert.Contains("a target shared-provider source uses a secret that the transfer would replace", warning);
        Assert.DoesNotContain("referenced by shared-provider publications or imports", warning, StringComparison.Ordinal);
        AssertIndependentOwnerReads(harness, source.Profile, target.Profile);
        remote.ApiTokenSecretId = unrelatedSecretId;
        await targetDb.SaveChangesAsync();
        var allowed = await harness.Handler.PreviewAsync(context);
        Assert.True(allowed.IsAvailable);
        Assert.Null(allowed.Warning);

        var wrong = context with { TargetProfile = source.Profile };
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Handler.TransferAsync(wrong));
        await harness.Operations.RunIndependentAsync(target.Profile, (session, token) =>
            harness.Operations.InspectTargetAsync(session, async (inspection, inspectionToken) => {
                Assert.Equal(target.Profile.Profile.Id, inspection.TargetProfileId);
                var selected = await harness.Inspections.ReadOwnerAsync<ProvidersDbContext, Guid>(inspection,
                    static options => new ProvidersDbContext(options),
                    (owner, readToken) => owner.Set<SharedProviderSource>().Select(item => item.ApiTokenSecretId).SingleAsync(readToken), inspectionToken);
                Assert.Equal(unrelatedSecretId, selected);
                var mismatched = inspection with { TargetProfileId = source.Profile.Profile.Id };
                await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Inspections.ReadOwnerAsync<ProvidersDbContext, int>(mismatched,
                    static options => new ProvidersDbContext(options), (owner, readToken) => owner.Set<SharedProviderSource>().CountAsync(readToken), inspectionToken));
                await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Operations.CreateOwnerAsync<ProvidersDbContext>(
                    session with { ProfileId = source.Profile.Profile.Id }, static options => new ProvidersDbContext(options), token));
                return true;
            }, token));
        Assert.Equal(unrelatedSecretId, (await targetDb.Set<SharedProviderSource>().AsNoTracking().SingleAsync()).ApiTokenSecretId);
        Assert.Equal(0, harness.Secrets.CopyCalls);
    }

    [Fact]
    public async Task Final_transfer_rechecks_references_added_after_an_available_preview_without_deleting_target_profiles() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var sourceDb = source.Factory.CreateDbContext();
        await using var targetDb = target.Factory.CreateDbContext();
        var sourceProvider = await SeedProviderAsync(sourceDb);
        var targetProvider = await SeedProviderAsync(targetDb);
        var context = new DatabaseTransferOperation(source.Profile, target.Profile, true);
        var harness = new TransferHarness(source.Profile, target.Profile);
        Assert.True((await harness.Handler.PreviewAsync(context)).IsAvailable);
        AssertIndependentOwnerReads(harness, source.Profile, target.Profile);
        var publication = SharedProviderPublicationTransitions.Create(targetProvider.Id, SharedProviderPublicationId.New(), DateTimeOffset.UtcNow);
        targetDb.Add(publication);
        await targetDb.SaveChangesAsync();
        var finalReadStart = harness.Probe.Commands.Count;

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Handler.TransferAsync(context));

        Assert.Contains("the target contains provider profiles referenced by shared-provider publications or imports", failure.Message);
        var finalReads = harness.Probe.Commands.Skip(finalReadStart).Where(command => command.ContextType == typeof(ProvidersDbContext)).ToArray();
        Assert.Contains(finalReads, command => command.Isolation == IsolationLevel.RepeatableRead
            && harness.Contexts.IsConnection(source.Profile.Profile.Id, command.Connection));
        Assert.Contains(finalReads, command => command.Isolation == IsolationLevel.Serializable
            && harness.Contexts.IsConnection(target.Profile.Profile.Id, command.Connection));
        await using var verify = target.Factory.CreateDbContext();
        Assert.True(await verify.Set<ProviderProfile>().AnyAsync(item => item.Id == targetProvider.Id));
        Assert.False(await verify.Set<ProviderProfile>().AnyAsync(item => item.Id == sourceProvider.Id));
        Assert.True(await verify.Set<ProviderSharePublication>().AnyAsync(item => item.Id == publication.Id));
        Assert.Equal(0, harness.Secrets.CopyCalls);
    }

    [Fact]
    public async Task Guard_reads_uncommitted_reference_on_the_exact_target_transaction_and_preserves_rollback() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var secretId = Guid.NewGuid();
        ProviderProfile sourceProvider;
        ProviderProfile targetProvider;
        await using (var seed = source.Factory.CreateDbContext()) {
            sourceProvider = await SeedProviderAsync(seed, secretId);
        }
        await using (var seed = target.Factory.CreateDbContext()) {
            seed.Add(Secret(secretId));
            targetProvider = await SeedProviderAsync(seed);
        }
        var remote = RemoteSource(secretId);
        var publication = SharedProviderPublicationTransitions.Create(targetProvider.Id, SharedProviderPublicationId.New(), DateTimeOffset.UtcNow);
        var context = new DatabaseTransferOperation(source.Profile, target.Profile, true);
        var harness = new TransferHarness(source.Profile, target.Profile);
        var independent = new TransferHarness(source.Profile, target.Profile);
        Assert.True((await harness.Handler.PreviewAsync(context)).IsAvailable);
        DbConnection? sourceConnection = null;
        DbConnection? targetConnection = null;
        DbTransaction? sourceTransaction = null;
        DbTransaction? targetTransaction = null;
        var guardReadStart = -1;
        harness.Secrets.BeforeTargetLocks = async (request, token) => {
            await using var ownedSource = await harness.Owners.CreateSourceAsync<ProvidersDbContext>(request,
                static options => new ProvidersDbContext(options), token);
            await using var ownedTarget = await harness.Owners.CreateTargetAsync<ProvidersDbContext>(request,
                static options => new ProvidersDbContext(options), token);
            sourceConnection = ownedSource.Database.GetDbConnection();
            targetConnection = ownedTarget.Database.GetDbConnection();
            sourceTransaction = ownedSource.Database.CurrentTransaction!.GetDbTransaction();
            targetTransaction = ownedTarget.Database.CurrentTransaction!.GetDbTransaction();
            Assert.Equal(IsolationLevel.RepeatableRead, sourceTransaction.IsolationLevel);
            Assert.Equal(IsolationLevel.Serializable, targetTransaction.IsolationLevel);
            Assert.True(harness.Contexts.IsConnection(source.Profile.Profile.Id, sourceConnection));
            Assert.True(harness.Contexts.IsConnection(target.Profile.Profile.Id, targetConnection));
            ownedTarget.AddRange(remote, publication);
            await ownedTarget.SaveChangesAsync(token);
            Assert.True(await ownedTarget.Set<SharedProviderSource>().AnyAsync(item => item.Id == remote.Id, token));
            Assert.True(await ownedTarget.Set<ProviderSharePublication>().AnyAsync(item => item.Id == publication.Id, token));

            Assert.True((await independent.Handler.PreviewAsync(context, token)).IsAvailable);
            AssertIndependentOwnerReads(independent, source.Profile, target.Profile);
            await using var outside = target.Factory.CreateDbContext();
            Assert.False(await outside.Set<SharedProviderSource>().AnyAsync(item => item.Id == remote.Id, token));
            Assert.False(await outside.Set<ProviderSharePublication>().AnyAsync(item => item.Id == publication.Id, token));
            guardReadStart = harness.Probe.Commands.Count;
        };

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Handler.TransferAsync(context));

        Assert.Contains("the target contains provider profiles referenced by shared-provider publications or imports", failure.Message);
        Assert.Contains("a target shared-provider source uses a secret that the transfer would replace", failure.Message);
        Assert.Equal(1, harness.Secrets.TableCalls);
        Assert.Equal(0, harness.Secrets.CopyCalls);
        Assert.True(guardReadStart >= 0);
        Assert.Contains(harness.Probe.Commands, command => command.ContextType == typeof(ProvidersDbContext)
            && ReferenceEquals(command.Connection, sourceConnection) && ReferenceEquals(command.Transaction, sourceTransaction));
        Assert.Contains(harness.Probe.Commands.Skip(guardReadStart), command => command.ContextType == typeof(ProvidersDbContext)
            && ReferenceEquals(command.Connection, targetConnection) && ReferenceEquals(command.Transaction, targetTransaction));
        await using var verify = target.Factory.CreateDbContext();
        Assert.False(await verify.Set<SharedProviderSource>().AnyAsync(item => item.Id == remote.Id));
        Assert.False(await verify.Set<ProviderSharePublication>().AnyAsync(item => item.Id == publication.Id));
        Assert.True(await verify.Set<ProviderProfile>().AnyAsync(item => item.Id == targetProvider.Id));
        Assert.False(await verify.Set<ProviderProfile>().AnyAsync(item => item.Id == sourceProvider.Id));
        Assert.True(await verify.Set<SecretRecord>().AnyAsync(item => item.Id == secretId));
        await using var verifySource = source.Factory.CreateDbContext();
        Assert.True(await verifySource.Set<ProviderProfile>().AnyAsync(item => item.Id == sourceProvider.Id));
        Assert.True(await verifySource.Set<SecretRecord>().AnyAsync(item => item.Id == secretId));
        Assert.True((await independent.Handler.PreviewAsync(context)).IsAvailable);
    }

    private static void AssertIndependentOwnerReads(TransferHarness harness,
        ResolvedDatabaseProfile source, ResolvedDatabaseProfile target) {
        var reads = harness.Probe.Commands.Where(command => command.ContextType == typeof(ProvidersDbContext)).ToArray();
        Assert.NotEmpty(reads);
        Assert.All(reads, command => Assert.Null(command.Transaction));
        Assert.Contains(reads, command => harness.Contexts.IsConnection(source.Profile.Id, command.Connection));
        Assert.Contains(reads, command => harness.Contexts.IsConnection(target.Profile.Id, command.Connection));
    }

    private static async Task<ProviderProfile> SeedProviderAsync(AppDbContext context, Guid? secretId = null) {
        if (secretId is { } id) {
            context.Add(Secret(id));
        }
        var profile = new ProviderProfile {
            Name = "Transfer fixture", ConnectorPluginKey = ProviderConnectorKeys.OpenAi, ConfigSchemaVersion = "1.0",
            BaseUrl = "https://provider.example.test", DefaultModel = "fixture-model", ApiKeySecretId = secretId,
            ConcurrencyToken = Guid.NewGuid()
        };
        context.Add(profile);
        await context.SaveChangesAsync();
        return profile;
    }

    private static SecretRecord Secret(Guid id) => new() {
        Id = id, Name = "Fixture encrypted value", Kind = SecretKind.ApiKey, EncryptedPayload = "fixture-protected-value",
        CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private static SharedProviderSource RemoteSource(Guid secretId) => new() {
        Name = "Shared source", BaseUri = "https://remote.example.test", ApiTokenSecretId = secretId,
        RemoteInstanceId = SharedProviderSourceInstanceId.New(), CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class TransferHarness {
        public TransferHarness(ResolvedDatabaseProfile source, ResolvedDatabaseProfile target) {
            Contexts = new ProfileContexts([source, target], Probe);
            Operations = new DatabaseTransferOperationRunner(Contexts, Owners, Inspections);
            Secrets = new ObservedSecrets(new SecretDatabaseTransferParticipant(Owners));
            Handler = new AiProvidersDatabaseTransferHandler(Secrets, Owners, Operations, [new SharedProviderDatabaseTransferGuard()]);
        }
        public DatabaseTransferOwnerSessionRunner Owners { get; } = new();
        public ProjectTransferTargetInspectionRunner Inspections { get; } = new();
        public CommandProbe Probe { get; } = new();
        public ProfileContexts Contexts { get; }
        public DatabaseTransferOperationRunner Operations { get; }
        public ObservedSecrets Secrets { get; }
        public AiProvidersDatabaseTransferHandler Handler { get; }
    }

    private sealed class ProfileContexts(IReadOnlyList<ResolvedDatabaseProfile> profiles, CommandProbe probe) : IProfileAppDbContextFactory {
        private readonly List<(Guid ProfileId, DbConnection Connection)> connections = [];
        public bool IsConnection(Guid profileId, DbConnection? connection)
            => connections.Any(item => item.ProfileId == profileId && ReferenceEquals(item.Connection, connection));

        public Task<AppDbContext> CreateDbContextForProfileAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = profiles.Single(item => item.Profile.Id == profile.Profile.Id);
            if (selected.Profile.ProviderKind != profile.Profile.ProviderKind || selected.ConnectionString != profile.ConnectionString) {
                throw new InvalidOperationException("The transfer fixture profile does not match its selected database.");
            }
            var options = new DbContextOptionsBuilder<AppDbContext>(AppDbContextOptionsConfigurator.CreateOptions(profile)).AddInterceptors(probe);
            var context = new AppDbContext(options.Options);
            connections.Add((profile.Profile.Id, context.Database.GetDbConnection()));
            return Task.FromResult(context);
        }
    }

    private sealed class ObservedSecrets(ISecretDatabaseTransferParticipant inner) : ISecretDatabaseTransferParticipant {
        public Func<DatabaseTransferOwnerRequest, CancellationToken, Task>? BeforeTargetLocks { get; set; }
        public int TableCalls { get; private set; }
        public int CopyCalls { get; private set; }
        public async Task<DatabaseTransferTable> GetTargetTableAsync(DatabaseTransferOwnerRequest request, CancellationToken cancellationToken = default) {
            TableCalls++;
            var table = await inner.GetTargetTableAsync(request, cancellationToken);
            if (BeforeTargetLocks is not null) {
                await BeforeTargetLocks(request, cancellationToken);
            }
            return table;
        }
        public Task<int> CopySelectedAsync(DatabaseTransferOwnerRequest request, IReadOnlyCollection<Guid> secretIds,
            CancellationToken cancellationToken = default) {
            CopyCalls++;
            return inner.CopySelectedAsync(request, secretIds, cancellationToken);
        }
    }

    private sealed record CommandObservation(Type? ContextType, DbConnection? Connection,
        DbTransaction? Transaction, IsolationLevel? Isolation);

    private sealed class CommandProbe : DbCommandInterceptor {
        public List<CommandObservation> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add(new(eventData.Context?.GetType(), command.Connection, command.Transaction, command.Transaction?.IsolationLevel));
            return ValueTask.FromResult(result);
        }
    }
}
