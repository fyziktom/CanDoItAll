using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class ProvidersDbContext(DbContextOptions<ProvidersDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new ProviderProfileConfiguration());
        modelBuilder.ApplyConfiguration(new ProviderSharePublicationConfiguration());
        modelBuilder.ApplyConfiguration(new SharedProviderSourceConfiguration(includeSecurityRelationship: false));
        modelBuilder.ApplyConfiguration(new SharedProviderImportConfiguration());
        modelBuilder.ApplyConfiguration(new SharedProviderInvocationRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SharedProviderServiceIdentityConfiguration());
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) {
        ApplicationManagedConcurrencyTokens.Stamp(ChangeTracker);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) {
        ApplicationManagedConcurrencyTokens.Stamp(ChangeTracker);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
