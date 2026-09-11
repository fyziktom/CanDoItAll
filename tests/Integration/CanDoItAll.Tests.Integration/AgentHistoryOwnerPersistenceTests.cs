using System.Data;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentHistoryOwnerPersistenceTests {
    [Fact]
    public async Task Locator_model_matches_the_complete_mapping_and_excludes_foreign_writers() {
        await using var application = await TestApplication.CreateAsync();
        await using var canonical = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<AgentHistoryDbContext>>().CreateDbContextAsync();
        var entity = Assert.Single(owner.Model.GetEntityTypes());
        Assert.Equal(typeof(AgentHistoryLocator), entity.ClrType);
        Assert.Equal(canonical.Model.FindEntityType(typeof(AgentHistoryLocator))!.ToDebugString(MetadataDebugStringOptions.LongDefault),
            entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        Assert.Throws<InvalidOperationException>(() => owner.Set<Project>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => owner.Set<HistorySourceRow>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => owner.Set<WorkflowRunRecordEntity>().ToQueryString());
        Assert.False(await owner.Set<AgentHistoryLocator>().AnyAsync());
    }

    [Fact]
    public async Task Canonical_locator_payload_survives_restart_and_concurrent_profile_factories() {
        await using var environment = CanDoItAllTestEnvironment.Create("agent-history-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var record = new AgentHistoryLocator {
            PartitionId = Guid.NewGuid(), EvidenceId = Guid.NewGuid(), OwnerId = Guid.NewGuid(),
            ScopeKind = WorkspaceScopeKind.Project, ScopeKey = Guid.NewGuid().ToString("D"),
            ProjectId = Guid.NewGuid(), SourceVersion = 197, IsDeleted = true, ConcurrencyToken = Guid.NewGuid()
        };
        var expected = JsonSerializer.Serialize(record);
        await using (var application = await TestApplication.CreateAsync(harness)) {
            await using var canonical = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            canonical.Add(record);
            await canonical.SaveChangesAsync();
        }

        await using var restarted = await TestApplication.CreateAsync(harness);
        var originalFactory = restarted.Services.GetRequiredService<IDbContextFactory<AgentHistoryDbContext>>();
        await using var owner = await originalFactory.CreateDbContextAsync();
        var restored = await owner.Set<AgentHistoryLocator>().SingleAsync(row => row.PartitionId == record.PartitionId && row.EvidenceId == record.EvidenceId);
        Assert.Equal(expected, JsonSerializer.Serialize(restored));
        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = otherProfile });
        await using var otherOwner = await other.Services.GetRequiredService<IDbContextFactory<AgentHistoryDbContext>>().CreateDbContextAsync();
        Assert.False(await otherOwner.Set<AgentHistoryLocator>().AnyAsync(row => row.PartitionId == record.PartitionId));
        await using var originalAgain = await originalFactory.CreateDbContextAsync();
        Assert.Equal(expected, JsonSerializer.Serialize(await originalAgain.Set<AgentHistoryLocator>()
            .SingleAsync(row => row.PartitionId == record.PartitionId && row.EvidenceId == record.EvidenceId)));
    }

    [Fact]
    public async Task Independent_owner_writers_reject_a_stale_locator_update() {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        var factory = AgentHistoryOwnerPersistenceTestFactory.Locators(database.Factory);
        var record = Locator(database.Partition.StorageLineageId, Id(1), Guid.NewGuid());
        await using (var canonical = database.Factory.CreateDbContext()) {
            canonical.Add(record);
            await canonical.SaveChangesAsync();
        }

        await using var first = await factory.CreateDbContextAsync();
        await using var stale = await factory.CreateDbContextAsync();
        var current = await first.Set<AgentHistoryLocator>().SingleAsync();
        var previous = await stale.Set<AgentHistoryLocator>().SingleAsync();
        current.SourceVersion = 2;
        previous.SourceVersion = 3;
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        await using var verify = await factory.CreateDbContextAsync();
        Assert.Equal(2, (await verify.Set<AgentHistoryLocator>().SingleAsync()).SourceVersion);
    }

    [Fact]
    public async Task Orphan_predicate_precedes_the_limit_and_retains_partition_tombstone_and_GUID_ordering() {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        var partition = database.Partition.StorageLineageId;
        var liveProject = Guid.NewGuid();
        var missingProject = Guid.NewGuid();
        var firstOrphan = Locator(partition, Id(2000), missingProject);
        var secondOrphan = Locator(partition, Id(2001), missingProject);
        var lastOrphan = Locator(partition, Id(4000), missingProject);
        var alreadyDeleted = Locator(partition, Id(1750), missingProject);
        alreadyDeleted.IsDeleted = true;
        var unscoped = Locator(partition, Id(1751), null);
        var foreignPartition = Locator(Guid.NewGuid(), Id(1752), missingProject);
        await using (var canonical = database.Factory.CreateDbContext()) {
            canonical.Add(new Project { Id = liveProject, Name = "Existing history scope", Slug = liveProject.ToString("N") });
            canonical.AddRange(Enumerable.Range(1, 1100).Select(index => Locator(partition, Id(index), liveProject)));
            canonical.AddRange(firstOrphan, secondOrphan, lastOrphan, alreadyDeleted, unscoped, foreignPartition);
            await canonical.SaveChangesAsync();
        }

        var probe = new OrphanQueryProbe();
        var store = AgentHistoryOwnerPersistenceTestFactory.Publications(database.Factory.WithInterceptor(probe),
            database.Partitions, database.Projection, database.Transactions);
        Assert.Equal(2, await store.ReconcileDeletedProjectsAsync(database.Partition, 2, default));
        var query = Assert.Single(probe.Queries);
        Assert.Contains("NOT EXISTS", query.Sql, StringComparison.Ordinal);
        Assert.True(query.Sql.IndexOf("NOT EXISTS", StringComparison.Ordinal) < query.Sql.IndexOf("LIMIT", StringComparison.Ordinal));
        Assert.Equal(IsolationLevel.ReadCommitted, query.Isolation);
        await using (var verify = database.Factory.CreateDbContext()) {
            Assert.Equal(new[] { firstOrphan.EvidenceId, secondOrphan.EvidenceId }, await verify.Set<AgentHistoryLocator>()
                .Where(row => row.PartitionId == partition && row.ProjectId == missingProject && row.IsDeleted && row.EvidenceId != alreadyDeleted.EvidenceId)
                .OrderBy(row => row.EvidenceId).Select(row => row.EvidenceId).ToArrayAsync());
            Assert.Equal(2, await verify.Set<HistorySourceRow>().CountAsync());
            Assert.False((await verify.Set<AgentHistoryLocator>().SingleAsync(row => row.PartitionId == foreignPartition.PartitionId)).IsDeleted);
            Assert.Equal(1100, await verify.Set<AgentHistoryLocator>().CountAsync(row => row.ProjectId == liveProject && !row.IsDeleted));
        }

        var restarted = AgentHistoryOwnerPersistenceTestFactory.Publications(database.Factory,
            database.Partitions, database.Projection, database.Transactions);
        Assert.Equal(1, await restarted.ReconcileDeletedProjectsAsync(database.Partition, 2, default));
        Assert.Equal(0, await restarted.ReconcileDeletedProjectsAsync(database.Partition, 2, default));
        await using var final = database.Factory.CreateDbContext();
        Assert.Equal(3, await final.Set<HistorySourceRow>().CountAsync());
        Assert.Equal(1, (await final.Set<AgentHistoryLocator>().SingleAsync(row => row.PartitionId == partition && row.EvidenceId == alreadyDeleted.EvidenceId)).SourceVersion);
        Assert.False((await final.Set<AgentHistoryLocator>().SingleAsync(row => row.PartitionId == partition && row.EvidenceId == unscoped.EvidenceId)).IsDeleted);
    }

    [Fact]
    public async Task Project_identity_facts_preserve_GUID_cursor_and_explicit_same_transaction_visibility() {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        var first = new Project { Id = Id(101), Name = "Zulu", Slug = "first" };
        var second = new Project { Id = Id(103), Name = "Alpha", Slug = "second" };
        await using (var canonical = database.Factory.CreateDbContext()) {
            canonical.AddRange(second, first);
            await canonical.SaveChangesAsync();
        }

        var probe = new ProjectQueryProbe();
        var projects = AgentHistoryOwnerPersistenceTestFactory.Projects(database.Factory.WithInterceptor(probe), database.Transactions);
        Assert.Equal(first.Id, await projects.FindNextIdAfterAsync(Guid.Empty));
        Assert.Equal(second.Id, await projects.FindNextIdAfterAsync(first.Id));
        Assert.Null(await projects.FindNextIdAfterAsync(second.Id));
        Assert.False(await projects.ExistsAsync(Guid.Empty));
        await Assert.ThrowsAsync<InvalidOperationException>(() => projects.ExistsForMutationAsync(first.Id));
        var pending = new Project { Id = Id(102), Name = "Pending scope", Slug = "pending" };
        await using var owner = await AgentHistoryOwnerPersistenceTestFactory.Locators(database.Factory).CreateDbContextAsync();
        await using (var transaction = await owner.Database.BeginTransactionAsync()) {
            using var coordination = database.Transactions.Enter(owner);
            var options = AgentHistoryOwnerPersistenceTestFactory.Options<ProjectsDbContext>(database.Factory);
            await using (var participant = await database.Transactions.CreateEnlistedAsync(options, static value => new ProjectsDbContext(value))) {
                participant.Add(pending);
                await participant.SaveChangesAsync();
            }

            Assert.False(await projects.ExistsAsync(pending.Id));
            Assert.True(await projects.ExistsForMutationAsync(pending.Id));
            Assert.Same(owner.Database.GetDbConnection(), probe.Connection);
            Assert.Same(transaction.GetDbTransaction(), probe.Transaction);
            await transaction.RollbackAsync();
        }

        Assert.False(await projects.ExistsAsync(pending.Id));
        Assert.Equal(second.Id, await projects.FindNextIdAfterAsync(first.Id));
    }

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:x12}");

    private static AgentHistoryLocator Locator(Guid partition, Guid evidence, Guid? project) => new() {
        PartitionId = partition, EvidenceId = evidence, OwnerId = Guid.NewGuid(), ProjectId = project,
        ScopeKind = project.HasValue ? WorkspaceScopeKind.Project : WorkspaceScopeKind.Organization,
        ScopeKey = (project ?? partition).ToString("D"), SourceVersion = 1
    };

    private sealed class OrphanQueryProbe : DbCommandInterceptor {
        public List<(string Sql, IsolationLevel? Isolation)> Queries { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (eventData.Context is AgentHistoryDbContext && command.CommandText.Contains("NOT EXISTS", StringComparison.Ordinal)) {
                Queries.Add((command.CommandText, command.Transaction?.IsolationLevel));
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ProjectQueryProbe : DbCommandInterceptor {
        public DbConnection? Connection { get; private set; }
        public DbTransaction? Transaction { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (eventData.Context is ProjectsDbContext) {
                Connection = command.Connection;
                Transaction = command.Transaction;
            }
            return ValueTask.FromResult(result);
        }
    }
}
