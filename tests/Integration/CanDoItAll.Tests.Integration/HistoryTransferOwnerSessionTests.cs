using CanDoItAll.Tests.Support;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Integration;

public sealed class HistoryTransferOwnerSessionTests {
    [Fact]
    public async Task Handler_copies_multiple_pages_without_changing_history_or_locator_payloads_and_uses_all_three_owner_models() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var projectId = Guid.NewGuid();
        await using (var seed = target.Factory.CreateDbContext()) {
            seed.Add(new Project { Id = projectId, Name = "Existing target owner" });
            await seed.SaveChangesAsync();
        }
        await using (var seed = source.Factory.CreateDbContext()) {
            var sources = new[] {
                new HistorySourceRow { PartitionId = source.Partition.StorageLineageId, Kind = HistorySourceKind.AgentConversation, OwnerId = "owner-a", EvidenceId = "evidence-a", Version = 12 },
                new HistorySourceRow { PartitionId = source.Partition.StorageLineageId, Kind = HistorySourceKind.AgentConversation, OwnerId = "owner-b", EvidenceId = "evidence-b", Version = 18 }
            };
            seed.AddRange(sources);
            for (var index = 0; index < 503; index++) {
                var entry = new HistoryEntryRow {
                    Id = Guid.NewGuid(), PartitionId = source.Partition.StorageLineageId, Granularity = HistoryGranularity.LegacyAggregate,
                    SortAtUtc = source.Clock.Now.AddSeconds(index), ProviderName = $"Historical provider {index}", ProviderKind = "legacy",
                    ExternalReferenceType = "fixture", ExternalReferenceValue = $"preserve:{index}", Version = index + 1,
                    ExpiresAtUtc = source.Clock.Now.AddDays(31), IsVisible = index % 3 != 0
                };
                seed.Add(entry);
                seed.Add(new HistoryOwnerRow {
                    PartitionId = source.Partition.StorageLineageId, SourceId = sources[index < 501 ? 0 : 1].Id,
                    EntryId = entry.Id, Role = HistoryOwnerRole.ContentOwner, State = HistoryOwnerState.Linked
                });
                seed.Add(Locator(source.Partition.StorageLineageId, projectId, index));
            }
            await seed.SaveChangesAsync();
        }
        var probe = new CommandProbe();
        await using var sourceDb = Maintenance(source.Profile, probe);
        await using var targetDb = Maintenance(target.Profile, probe);
        var sourceConnection = sourceDb.Database.GetDbConnection();
        var targetConnection = targetDb.Database.GetDbConnection();
        var sessions = new DatabaseTransferOwnerSessionRunner();
        var handler = new HistoryDatabaseTransferHandler([Participant(sessions)], sessions, DatabaseTransferTestSupport.For(new(source.Profile, target.Profile, true), sourceDb, targetDb, sessions));
        var context = new DatabaseTransferOperation(source.Profile, target.Profile, true);
        var preview = await handler.PreviewAsync(context);
        Assert.Equal("Transfer is a snapshot. Canonical source files and protection keys must remain accessible; they are not copied by this group.", preview.Warning);
        probe.Commands.Clear();

        var result = await handler.TransferAsync(context);

        Assert.True(result.Success);
        await AssertSameRowsAsync<HistoryEntryRow>(sourceDb, targetDb, rows => rows.OrderBy(row => row.Id));
        await AssertSameRowsAsync<HistorySourceRow>(sourceDb, targetDb, rows => rows.OrderBy(row => row.Id));
        await AssertSameRowsAsync<HistoryOwnerRow>(sourceDb, targetDb, rows => rows.OrderBy(row => row.SourceId).ThenBy(row => row.EntryId));
        await AssertSameRowsAsync<AgentHistoryLocator>(sourceDb, targetDb, rows => rows.OrderBy(row => row.PartitionId).ThenBy(row => row.EvidenceId));
        Assert.Equal(source.Partition, await target.Partitions.GetAsync(default));
        Assert.All(probe.Commands.Where(command => command.ContextType == typeof(ProviderHistoryDbContext) || command.ContextType == typeof(AgentHistoryDbContext)), command => {
            Assert.True(ReferenceEquals(command.Connection, sourceConnection) || ReferenceEquals(command.Connection, targetConnection));
            Assert.NotNull(command.Transaction);
            Assert.Equal(ReferenceEquals(command.Connection, sourceConnection) ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable, command.Isolation);
        });
        Assert.Contains(probe.Commands, command => command.ContextType == typeof(AgentHistoryDbContext) && ReferenceEquals(command.Connection, targetConnection));
        Assert.Contains(probe.Commands, command => command.ContextType == typeof(ProjectsDbContext) && ReferenceEquals(command.Connection, targetConnection)
            && command.Isolation == IsolationLevel.Serializable);
        Assert.Contains(probe.Commands, command => command.ContextType == typeof(ProviderHistoryDbContext) && ReferenceEquals(command.Connection, sourceConnection));
    }

    [Fact]
    public async Task Failure_after_Agent_owner_save_restores_target_bootstrap_and_rolls_back_every_copied_owner() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        await source.Capture.BeginAsync(source.Start(), null, default);
        await using var sourceDb = source.Factory.CreateDbContext();
        await using var targetDb = target.Factory.CreateDbContext();
        sourceDb.Add(Locator(source.Partition.StorageLineageId, null, 1));
        await sourceDb.SaveChangesAsync();
        var sessions = new DatabaseTransferOwnerSessionRunner();
        var participant = new FailingAfterCopy(Participant(sessions));
        var handler = new HistoryDatabaseTransferHandler([participant], sessions, DatabaseTransferTestSupport.For(new(source.Profile, target.Profile, true), sourceDb, targetDb, sessions));

        await Assert.ThrowsAsync<InjectedCopyFailure>(() => handler.TransferAsync(new(source.Profile, target.Profile, true)));

        Assert.Equal(1, participant.Copied);
        await using var verify = target.Factory.CreateDbContext();
        Assert.Equal(target.Partition, await target.Partitions.GetAsync(default));
        Assert.Empty(await verify.Set<AgentHistoryLocator>().ToArrayAsync());
        Assert.Empty(await verify.Set<HistoryEntryRow>().ToArrayAsync());
        Assert.Single(await verify.Set<HistoryPartitionRow>().ToArrayAsync());
        Assert.Equal(target.Partition.StorageLineageId, (await verify.Set<HistoryStorageIdentity>().SingleAsync()).PartitionId);
        Assert.Single(await sourceDb.Set<AgentHistoryLocator>().AsNoTracking().ToArrayAsync());
        Assert.Single(await sourceDb.Set<HistoryEntryRow>().AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task Agent_copy_sees_the_original_source_snapshot_and_staged_target_Project_on_the_exact_transactions() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var projectId = Guid.NewGuid();
        var original = Locator(source.Partition.StorageLineageId, projectId, 1);
        await using var sourceDb = source.Factory.CreateDbContext();
        await using var targetDb = target.Factory.CreateDbContext();
        sourceDb.Add(original);
        await sourceDb.SaveChangesAsync();
        await using var sourceTransaction = await sourceDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using var targetTransaction = await targetDb.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var sessions = new DatabaseTransferOwnerSessionRunner();
        using var scope = sessions.Begin(new(source.Profile, target.Profile, true), sourceDb, targetDb, out var request);
        await using var sourceOwner = await sessions.CreateSourceAsync<AgentHistoryDbContext>(request, static options => new AgentHistoryDbContext(options));
        await using var targetOwner = await sessions.CreateTargetAsync<AgentHistoryDbContext>(request, static options => new AgentHistoryDbContext(options));
        Assert.Same(sourceDb.Database.GetDbConnection(), sourceOwner.Database.GetDbConnection());
        Assert.Same(sourceTransaction.GetDbTransaction(), sourceOwner.Database.CurrentTransaction!.GetDbTransaction());
        Assert.Same(targetDb.Database.GetDbConnection(), targetOwner.Database.GetDbConnection());
        Assert.Same(targetTransaction.GetDbTransaction(), targetOwner.Database.CurrentTransaction!.GetDbTransaction());
        Assert.Single(await sourceOwner.Set<AgentHistoryLocator>().ToArrayAsync());
        await using (var late = source.Factory.CreateDbContext()) {
            late.Add(Locator(source.Partition.StorageLineageId, null, 2));
            await late.SaveChangesAsync();
        }
        targetDb.Add(new Project { Id = projectId, Name = "Staged target project" });
        await targetDb.SaveChangesAsync();

        Assert.Equal(1, await Participant(sessions).CopyAsync(request, default));

        Assert.Equal(original.EvidenceId, (await targetOwner.Set<AgentHistoryLocator>().AsNoTracking().SingleAsync()).EvidenceId);
        await using (var independent = target.Factory.CreateDbContext()) {
            Assert.Empty(await independent.Set<AgentHistoryLocator>().ToArrayAsync());
            Assert.False(await independent.Set<Project>().AnyAsync(item => item.Id == projectId));
        }
        scope.Dispose();
        await targetTransaction.RollbackAsync();
        await sourceTransaction.RollbackAsync();
        await using var verify = target.Factory.CreateDbContext();
        Assert.Empty(await verify.Set<AgentHistoryLocator>().ToArrayAsync());
        Assert.False(await verify.Set<Project>().AnyAsync(item => item.Id == projectId));
    }

    [Fact]
    public async Task Participants_reject_absent_mismatched_retired_and_committed_transfer_sessions() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var sourceDb = source.Factory.CreateDbContext();
        await using var targetDb = target.Factory.CreateDbContext();
        var sessions = new DatabaseTransferOwnerSessionRunner();
        var participant = Participant(sessions);
        var absent = new DatabaseTransferOwnerRequest(Guid.NewGuid(), source.Profile.Profile.Id, target.Profile.Profile.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.ValidateTargetAsync(absent, default));
        await using var sourceTransaction = await sourceDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using var targetTransaction = await targetDb.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        using var scope = sessions.Begin(new(source.Profile, target.Profile, true), sourceDb, targetDb, out var request);
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.CopyAsync(request with { SessionId = Guid.NewGuid() }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.CopyAsync(request with { TargetProfileId = source.Profile.Profile.Id }, default));
        await using var retained = await sessions.CreateTargetAsync<AgentHistoryDbContext>(request, static options => new AgentHistoryDbContext(options));
        await targetTransaction.CommitAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.ValidateTargetAsync(request, default));
        scope.Dispose();
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.ValidateTargetAsync(request, default));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => retained.Set<AgentHistoryLocator>().AnyAsync());
        await sourceTransaction.RollbackAsync();
    }

    [Fact]
    public async Task Distinct_logical_profiles_for_the_same_physical_database_are_rejected_before_transactions_or_bootstrap_deletion() {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var source = database.Factory.CreateDbContext();
        await using var target = database.Factory.CreateDbContext();
        var alias = database.Profile with { Profile = new DatabaseProfileRecord { Id = Guid.NewGuid(), DisplayName = "Alias", ProviderKind = DatabaseProviderKind.PostgreSql } };
        var sessions = new DatabaseTransferOwnerSessionRunner();
        var handler = new HistoryDatabaseTransferHandler([Participant(sessions)], sessions, DatabaseTransferTestSupport.For(new(database.Profile, alias, true), source, target, sessions));
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.TransferAsync(new(database.Profile, alias, true)));
        Assert.Contains("same physical database", failure.Message);
        Assert.Null(source.Database.CurrentTransaction);
        Assert.Null(target.Database.CurrentTransaction);
        Assert.Equal(database.Partition.StorageLineageId, (await source.Set<HistoryStorageIdentity>().SingleAsync()).PartitionId);
        Assert.Single(await source.Set<HistoryPartitionRow>().ToArrayAsync());
    }

    [Fact]
    public async Task Retained_deleted_Agent_locator_blocks_transfer_even_when_History_is_only_bootstrap() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var sourceDb = source.Factory.CreateDbContext();
        await using var targetDb = target.Factory.CreateDbContext();
        var locator = Locator(target.Partition.StorageLineageId, Guid.NewGuid(), 3);
        locator.IsDeleted = true;
        targetDb.Add(locator);
        await targetDb.SaveChangesAsync();
        var before = JsonSerializer.Serialize(locator);
        var sessions = new DatabaseTransferOwnerSessionRunner();
        var handler = new HistoryDatabaseTransferHandler([Participant(sessions)], sessions, DatabaseTransferTestSupport.For(new(source.Profile, target.Profile, true), sourceDb, targetDb, sessions));
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.TransferAsync(new(source.Profile, target.Profile, true)));
        Assert.Contains("retained canonical file locators", failure.Message);
        targetDb.ChangeTracker.Clear();
        Assert.Equal(before, JsonSerializer.Serialize(await targetDb.Set<AgentHistoryLocator>().SingleAsync()));
        Assert.Equal(target.Partition.StorageLineageId, (await targetDb.Set<HistoryStorageIdentity>().SingleAsync()).PartitionId);
    }

    private static AgentHistoryTransferParticipant Participant(DatabaseTransferOwnerSessionRunner sessions)
        => new(sessions, new ProjectTransferReferenceQuery(sessions));

    private static AgentHistoryLocator Locator(Guid partitionId, Guid? projectId, int index) => new() {
        PartitionId = partitionId, EvidenceId = Guid.NewGuid(), OwnerId = Guid.NewGuid(), ProjectId = projectId,
        ScopeKind = projectId.HasValue ? WorkspaceScopeKind.Project : WorkspaceScopeKind.Organization,
        ScopeKey = projectId?.ToString("D") ?? $"historical-scope:{index}", SourceVersion = index + 1,
        IsDeleted = index % 3 == 0, ConcurrencyToken = Guid.NewGuid()
    };

    private static AppDbContext Maintenance(ResolvedDatabaseProfile profile, IInterceptor interceptor)
        => new(new DbContextOptionsBuilder<AppDbContext>(AppDbContextOptionsConfigurator.CreateOptions(profile)).AddInterceptors(interceptor).Options);

    private static async Task AssertSameRowsAsync<T>(AppDbContext source, AppDbContext target,
        Func<IQueryable<T>, IOrderedQueryable<T>> order) where T : class {
        var expected = await order(source.Set<T>().AsNoTracking()).ToArrayAsync();
        var actual = await order(target.Set<T>().AsNoTracking()).ToArrayAsync();
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    private sealed class InjectedCopyFailure : Exception;

    private sealed class FailingAfterCopy(AgentHistoryTransferParticipant inner) : IHistoryTransferParticipant {
        public HistorySourceKind Kind => inner.Kind;
        public int Copied { get; private set; }
        public Task ValidateTargetAsync(DatabaseTransferOwnerRequest transfer, CancellationToken cancellationToken)
            => inner.ValidateTargetAsync(transfer, cancellationToken);
        public async Task<int> CopyAsync(DatabaseTransferOwnerRequest transfer, CancellationToken cancellationToken) {
            Copied = await inner.CopyAsync(transfer, cancellationToken);
            throw new InjectedCopyFailure();
        }
    }

    private sealed class CommandProbe : DbCommandInterceptor {
        public List<(Type? ContextType, DbConnection? Connection, DbTransaction? Transaction, IsolationLevel? Isolation)> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add((eventData.Context?.GetType(), command.Connection, command.Transaction, command.Transaction?.IsolationLevel));
            return ValueTask.FromResult(result);
        }
    }
}
