using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.Plugins;

public sealed class PluginsDbContextTests {
    [Fact]
    public void Runtime_model_maps_only_owned_records_and_rejects_foreign_queries() {
        using var context = new PluginsDbContext(new DbContextOptionsBuilder<PluginsDbContext>()
            .UseNpgsql("Host=localhost;Database=plugins_model")
            .Options);
        Type[] ownedTypes = [typeof(PluginInstallationRecord), typeof(PluginCapabilityGrantRecord),
            typeof(PluginConnectionRecord), typeof(PluginOAuthConnectionRecord),
            typeof(PluginOAuthSessionRecord), typeof(PluginLogRecord)];

        Assert.Equal(ownedTypes.OrderBy(type => type.Name), context.Model.GetEntityTypes()
            .Select(entity => entity.ClrType).OrderBy(type => type.Name));
        Assert.All(context.Model.GetEntityTypes(), entity => {
            Assert.Empty(entity.GetForeignKeys());
            Assert.False(entity.FindProperty(nameof(IHasConcurrencyToken.ConcurrencyToken))!.IsConcurrencyToken);
        });
        Assert.Throws<InvalidOperationException>(() => context.Set<Project>().ToQueryString());
    }

    [Theory]
    [InlineData(SaveEntryPoint.SynchronousDefault)]
    [InlineData(SaveEntryPoint.SynchronousAccept)]
    [InlineData(SaveEntryPoint.SynchronousRetain)]
    [InlineData(SaveEntryPoint.AsynchronousDefault)]
    [InlineData(SaveEntryPoint.AsynchronousAccept)]
    [InlineData(SaveEntryPoint.AsynchronousRetain)]
    public async Task Every_save_entry_point_preserves_supplied_tokens_and_stamps_owned_records(SaveEntryPoint entryPoint) {
        await using var context = new PluginsDbContext(new DbContextOptionsBuilder<PluginsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var suppliedToken = Guid.NewGuid();
        var installation = new PluginInstallationRecord { PluginId = "owner.original", ConcurrencyToken = suppliedToken };
        IHasConcurrencyToken[] generated = [new PluginCapabilityGrantRecord(), new PluginConnectionRecord(),
            new PluginOAuthConnectionRecord(), new PluginOAuthSessionRecord(), new PluginLogRecord()];
        context.Add(installation);
        context.AddRange(generated);
        await SaveAsync();

        Assert.Equal(suppliedToken, installation.ConcurrencyToken);
        Assert.All(generated, item => Assert.NotEqual(Guid.Empty, item.ConcurrencyToken));
        var generatedTokens = generated.Select(item => item.ConcurrencyToken).ToArray();
        await SaveAsync();
        Assert.Equal(suppliedToken, installation.ConcurrencyToken);
        Assert.Equal(generatedTokens, generated.Select(item => item.ConcurrencyToken));

        installation.DisplayNameSnapshot = "Edited";
        await SaveAsync();
        Assert.NotEqual(suppliedToken, installation.ConcurrencyToken);
        Assert.Equal(generatedTokens, generated.Select(item => item.ConcurrencyToken));

        var editedToken = installation.ConcurrencyToken;
        context.Remove(installation);
        await SaveAsync();
        Assert.Equal(editedToken, installation.ConcurrencyToken);
        Assert.Equal(generatedTokens, generated.Select(item => item.ConcurrencyToken));

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
