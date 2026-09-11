using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentHistoryDbContextTests {
    [Fact]
    public void Runtime_model_contains_only_the_owned_locator_and_its_GUID_token() {
        using var db = new AgentHistoryDbContext(new DbContextOptionsBuilder<AgentHistoryDbContext>()
            .UseNpgsql("Host=localhost;Database=agent_history_owner_model").Options);
        var entity = Assert.Single(db.Model.GetEntityTypes());
        Assert.Equal(typeof(AgentHistoryLocator), entity.ClrType);
        Assert.Equal("AgentFramework_HistoryLocators", entity.GetTableName());
        Assert.Equal(new[] { nameof(AgentHistoryLocator.PartitionId), nameof(AgentHistoryLocator.EvidenceId) },
            entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(nameof(AgentHistoryLocator.ConcurrencyToken), Assert.Single(entity.GetProperties(), property => property.IsConcurrencyToken).Name);
        Assert.Empty(entity.GetForeignKeys());
        Assert.Throws<InvalidOperationException>(() => db.Set<Project>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => db.Set<WorkflowRunRecordEntity>().ToQueryString());
    }

    [Theory]
    [InlineData(SaveOverload.SynchronousDefault)]
    [InlineData(SaveOverload.SynchronousAccept)]
    [InlineData(SaveOverload.SynchronousRetain)]
    [InlineData(SaveOverload.AsynchronousDefault)]
    [InlineData(SaveOverload.AsynchronousAccept)]
    [InlineData(SaveOverload.AsynchronousRetain)]
    public async Task Save_overloads_preserve_imported_tokens_and_stamp_changes_without_advancing_source_versions(SaveOverload overload) {
        await using var db = new AgentHistoryDbContext(new DbContextOptionsBuilder<AgentHistoryDbContext>()
            .UseInMemoryDatabase($"agent-history-owner-tokens-{Guid.NewGuid():N}").Options);
        var imported = new AgentHistoryLocator {
            PartitionId = Guid.NewGuid(), EvidenceId = Guid.NewGuid(), OwnerId = Guid.NewGuid(),
            ScopeKind = WorkspaceScopeKind.Organization, SourceVersion = 17, ConcurrencyToken = Guid.NewGuid()
        };
        var created = new AgentHistoryLocator {
            PartitionId = imported.PartitionId, EvidenceId = Guid.NewGuid(), OwnerId = imported.OwnerId, SourceVersion = 23
        };
        var original = imported.ConcurrencyToken;
        db.AddRange(imported, created);
        await SaveAsync();
        Assert.Equal(original, imported.ConcurrencyToken);
        Assert.NotEqual(Guid.Empty, created.ConcurrencyToken);
        var generated = created.ConcurrencyToken;
        await SaveAsync();
        Assert.Equal(original, imported.ConcurrencyToken);
        Assert.Equal(generated, created.ConcurrencyToken);
        imported.IsDeleted = true;
        created.ScopeKey = "changed";
        await SaveAsync();
        Assert.NotEqual(original, imported.ConcurrencyToken);
        Assert.NotEqual(generated, created.ConcurrencyToken);
        Assert.Equal(17, imported.SourceVersion);
        Assert.Equal(23, created.SourceVersion);
        var beforeDelete = created.ConcurrencyToken;
        db.Remove(created);
        await SaveAsync();
        Assert.Equal(beforeDelete, created.ConcurrencyToken);

        async Task SaveAsync() {
            switch (overload) {
                case SaveOverload.SynchronousDefault:
                    db.SaveChanges();
                    break;
                case SaveOverload.SynchronousAccept:
                    db.SaveChanges(true);
                    break;
                case SaveOverload.SynchronousRetain:
                    db.SaveChanges(false);
                    db.ChangeTracker.AcceptAllChanges();
                    break;
                case SaveOverload.AsynchronousDefault:
                    await db.SaveChangesAsync();
                    break;
                case SaveOverload.AsynchronousAccept:
                    await db.SaveChangesAsync(true);
                    break;
                case SaveOverload.AsynchronousRetain:
                    await db.SaveChangesAsync(false);
                    db.ChangeTracker.AcceptAllChanges();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(overload));
            }
        }
    }

    public enum SaveOverload {
        SynchronousDefault,
        SynchronousAccept,
        SynchronousRetain,
        AsynchronousDefault,
        AsynchronousAccept,
        AsynchronousRetain
    }
}
