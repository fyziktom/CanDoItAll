using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class ProvidersDbContextTests {
    [Fact]
    public void Runtime_model_maps_only_six_provider_entities_and_rejects_secret_queries() {
        using var context = new ProvidersDbContext(new DbContextOptionsBuilder<ProvidersDbContext>()
            .UseNpgsql("Host=localhost;Database=providers_model").Options);
        Type[] owned = [typeof(ProviderProfile), typeof(ProviderSharePublication), typeof(SharedProviderSource),
            typeof(SharedProviderImport), typeof(SharedProviderInvocationRecord), typeof(SharedProviderServiceIdentity)];

        Assert.Equal(owned.OrderBy(type => type.Name), context.Model.GetEntityTypes()
            .Select(entity => entity.ClrType).OrderBy(type => type.Name));
        Assert.Throws<InvalidOperationException>(() => context.Set<SecretRecord>().ToQueryString());
        var source = context.Model.FindEntityType(typeof(SharedProviderSource))!;
        Assert.Contains(source.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(SharedProviderSource.ApiTokenSecretId)]));
    }

    [Fact]
    public void Assembly_scanning_retains_the_complete_schema_secret_foreign_key() {
        using var context = new AssemblyScanContext(new DbContextOptionsBuilder<AssemblyScanContext>()
            .UseNpgsql("Host=localhost;Database=providers_complete_model").Options);
        var source = context.Model.FindEntityType(typeof(SharedProviderSource))!;
        var relationship = Assert.Single(source.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(SecretRecord));
        Assert.Equal([nameof(SharedProviderSource.ApiTokenSecretId)], relationship.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Save_preserves_insert_tokens_and_restamps_only_modified_records(bool asynchronous, bool acceptAllChanges) {
        await using var context = new ProvidersDbContext(new DbContextOptionsBuilder<ProvidersDbContext>()
            .UseInMemoryDatabase($"providers-owner-{Guid.NewGuid():N}").Options);
        var supplied = Guid.NewGuid();
        var profile = new ProviderProfile { Name = "Original", ConcurrencyToken = supplied };
        var source = new SharedProviderSource { Name = "Source", ApiTokenSecretId = Guid.NewGuid() };
        context.AddRange(profile, source);
        await SaveAsync();
        Assert.Equal(supplied, profile.ConcurrencyToken);
        Assert.NotEqual(Guid.Empty, source.ConcurrencyToken);
        var sourceToken = source.ConcurrencyToken;

        await SaveAsync();
        Assert.Equal(supplied, profile.ConcurrencyToken);
        Assert.Equal(sourceToken, source.ConcurrencyToken);
        profile.Name = "Edited";
        await SaveAsync();
        Assert.NotEqual(supplied, profile.ConcurrencyToken);
        Assert.Equal(sourceToken, source.ConcurrencyToken);

        var edited = profile.ConcurrencyToken;
        context.Remove(profile);
        await SaveAsync();
        Assert.Equal(edited, profile.ConcurrencyToken);

        async Task SaveAsync() {
            if (asynchronous) {
                await context.SaveChangesAsync(acceptAllChanges);
            } else {
                context.SaveChanges(acceptAllChanges);
            }
            if (!acceptAllChanges) {
                context.ChangeTracker.AcceptAllChanges();
            }
        }
    }

    private sealed class AssemblyScanContext(DbContextOptions<AssemblyScanContext> options) : DbContext(options) {
        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProvidersDbContext).Assembly);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(SecretRecord).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
