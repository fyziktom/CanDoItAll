using System.Data;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Integration;

public sealed class DatabaseTransferContractPersistenceTests {
    [Fact]
    public async Task Project_Workbench_and_Storage_share_one_exact_target_transaction_and_rollback_together() {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var maintenance = database.Factory.CreateDbContext();
        var operations = DatabaseTransferTestSupport.ForProfile(database.Profile, maintenance);
        var projects = new ProjectsProfileTransferStore(operations);
        var projectId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var storageId = Guid.NewGuid();
        DatabaseTransferProfileSession? captured = null;
        WorkbenchDbContext? retained = null;
        await Assert.ThrowsAsync<RollbackRequested>(() => operations.RunSerializableAsync<bool>(database.Profile, [ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey], async (session, token) => {
            captured = session;
            await Assert.ThrowsAsync<InvalidOperationException>(() => projects.CountAsync(session with { ProfileId = Guid.NewGuid() }, token));
            await projects.SaveAsync(session, new([new ProjectTransferProject { Id = projectId, Name = "Staged project" }], [], [], [], [], []), token);
            retained = await operations.CreateOwnerAsync<WorkbenchDbContext>(session, static options => new WorkbenchDbContext(options), token);
            await using var storage = await operations.CreateOwnerAsync<StorageDbContext>(session, static options => new StorageDbContext(options), token);
            Assert.Same(maintenance.Database.GetDbConnection(), retained.Database.GetDbConnection());
            Assert.Same(retained.Database.GetDbConnection(), storage.Database.GetDbConnection());
            Assert.Same(retained.Database.CurrentTransaction!.GetDbTransaction(), storage.Database.CurrentTransaction!.GetDbTransaction());
            Assert.Equal(IsolationLevel.Serializable, storage.Database.CurrentTransaction!.GetDbTransaction().IsolationLevel);
            retained.Add(new ProjectObjectRecord { Id = nodeId, ProjectId = projectId, NodeKey = "native:staged", Title = "Staged object" });
            storage.Add(new StorageCatalogRecord { Id = storageId, Name = "Staged catalog row" });
            await retained.SaveChangesAsync(token);
            await storage.SaveChangesAsync(token);
            await using var observer = database.Factory.CreateDbContext();
            Assert.False(await observer.Set<Project>().AnyAsync(row => row.Id == projectId, token));
            Assert.False(await observer.Set<ProjectObjectRecord>().AnyAsync(row => row.Id == nodeId, token));
            Assert.False(await observer.Set<StorageCatalogRecord>().AnyAsync(row => row.Id == storageId, token));
            throw new RollbackRequested();
        }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => projects.CountAsync(Assert.IsType<DatabaseTransferProfileSession>(captured)));
        var expired = Assert.IsType<WorkbenchDbContext>(retained);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => expired.Set<ProjectObjectRecord>().AnyAsync());
        await expired.DisposeAsync();
        await using var restarted = database.Factory.CreateDbContext();
        Assert.False(await restarted.Set<Project>().AnyAsync(row => row.Id == projectId));
        Assert.False(await restarted.Set<ProjectObjectRecord>().AnyAsync(row => row.Id == nodeId));
        Assert.False(await restarted.Set<StorageCatalogRecord>().AnyAsync(row => row.Id == storageId));
    }

    [Fact]
    public async Task An_independent_transfer_read_does_not_borrow_an_ambient_business_transaction() {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var business = database.Factory.CreateDbContext();
        await using var transaction = await business.Database.BeginTransactionAsync();
        var project = new Project { Name = "Uncommitted ordinary write" };
        business.Add(project);
        await business.SaveChangesAsync();
        using var ambient = database.Transactions.Enter(business);
        var operations = DatabaseTransferTestSupport.Create();
        var projects = new ProjectsProfileTransferStore(operations);
        await operations.RunIndependentAsync(database.Profile, async (session, token) => {
            Assert.DoesNotContain((await projects.LoadAsync(session, token)).Projects, row => row.Id == project.Id);
            await using var owner = await operations.CreateOwnerAsync<ProjectsDbContext>(session, static options => new ProjectsDbContext(options), token);
            Assert.NotSame(business.Database.GetDbConnection(), owner.Database.GetDbConnection());
            Assert.Null(owner.Database.CurrentTransaction);
            await Assert.ThrowsAsync<InvalidOperationException>(() => projects.ClearAsync(session, token));
            return true;
        });
        await transaction.RollbackAsync();
    }

    private sealed class RollbackRequested : Exception;
}
