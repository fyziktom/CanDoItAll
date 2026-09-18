using CanDoItAll.Memory.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Tests.Persistence;

public sealed class MemoryDbContextTests {
    [Fact]
    public void Runtime_model_maps_only_owned_records_and_rejects_foreign_queries() {
        using var context = new MemoryDbContext(new DbContextOptionsBuilder<MemoryDbContext>()
            .UseNpgsql("Host=localhost;Database=memory_model")
            .Options);
        var entities = context.Model.GetEntityTypes().ToArray();
        var lease = Assert.Single(entities, entity => entity.GetTableName() == "Memory_WorkerLeases");
        Type[] publicTypes = [typeof(MemoryProviderProfileEntity), typeof(MemoryOperationLedgerEntity),
            typeof(MemoryFeedbackLedgerEntity), typeof(MemoryEventInboxLedgerEntity),
            typeof(MemoryEventOutboxLedgerEntity), typeof(MemorySourceRequestLedgerEntity)];

        Assert.Equal(publicTypes.OrderBy(type => type.Name), entities.Where(entity => entity != lease)
            .Select(entity => entity.ClrType).OrderBy(type => type.Name));
        Assert.False(lease.ClrType.IsPublic);
        Assert.Equal(typeof(MemoryDbContext).Namespace, lease.ClrType.Namespace);
        Assert.False(context.Model.FindEntityType(typeof(MemoryProviderProfileEntity))!
            .FindProperty(nameof(MemoryProviderProfileEntity.ConcurrencyToken))!.IsConcurrencyToken);
        Assert.Throws<InvalidOperationException>(() => context.Set<Project>().ToQueryString());
    }

    [Theory]
    [InlineData(SaveEntryPoint.SynchronousDefault)]
    [InlineData(SaveEntryPoint.SynchronousAccept)]
    [InlineData(SaveEntryPoint.SynchronousRetain)]
    [InlineData(SaveEntryPoint.AsynchronousDefault)]
    [InlineData(SaveEntryPoint.AsynchronousAccept)]
    [InlineData(SaveEntryPoint.AsynchronousRetain)]
    public async Task Every_save_entry_point_preserves_inserted_tokens_and_restamps_only_modified_profiles(SaveEntryPoint entryPoint) {
        await using var context = new MemoryDbContext(new DbContextOptionsBuilder<MemoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var suppliedToken = Guid.NewGuid();
        var original = new MemoryProviderProfileEntity {
            InstanceId = "provider.original", DisplayName = "Original", ConcurrencyToken = suppliedToken
        };
        var generated = new MemoryProviderProfileEntity { InstanceId = "provider.generated", DisplayName = "Generated" };
        context.AddRange(original, generated);
        await SaveAsync();

        Assert.Equal(suppliedToken, original.ConcurrencyToken);
        Assert.NotEqual(Guid.Empty, generated.ConcurrencyToken);
        var generatedToken = generated.ConcurrencyToken;
        await SaveAsync();
        Assert.Equal(suppliedToken, original.ConcurrencyToken);
        Assert.Equal(generatedToken, generated.ConcurrencyToken);

        original.DisplayName = "Edited";
        await SaveAsync();
        Assert.NotEqual(suppliedToken, original.ConcurrencyToken);
        Assert.Equal(generatedToken, generated.ConcurrencyToken);

        var editedToken = original.ConcurrencyToken;
        context.Remove(original);
        await SaveAsync();
        Assert.Equal(editedToken, original.ConcurrencyToken);
        Assert.Equal(generatedToken, generated.ConcurrencyToken);

        async Task SaveAsync() {
            switch (entryPoint) {
                case SaveEntryPoint.SynchronousDefault:
                    context.SaveChanges();
                    break;
                case SaveEntryPoint.SynchronousAccept:
                    context.SaveChanges(true);
                    break;
                case SaveEntryPoint.SynchronousRetain:
                    context.SaveChanges(false);
                    break;
                case SaveEntryPoint.AsynchronousDefault:
                    await context.SaveChangesAsync();
                    break;
                case SaveEntryPoint.AsynchronousAccept:
                    await context.SaveChangesAsync(true);
                    break;
                case SaveEntryPoint.AsynchronousRetain:
                    await context.SaveChangesAsync(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entryPoint));
            }

            context.ChangeTracker.AcceptAllChanges();
        }
    }

    public enum SaveEntryPoint {
        SynchronousDefault,
        SynchronousAccept,
        SynchronousRetain,
        AsynchronousDefault,
        AsynchronousAccept,
        AsynchronousRetain
    }
}
