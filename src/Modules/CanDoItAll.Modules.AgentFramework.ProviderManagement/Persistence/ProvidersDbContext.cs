using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class ProvidersDbContext(DbContextOptions<ProvidersDbContext> options) : DbContext(options) {
    internal Task<int> SaveDiagnosticChangesAsync(CancellationToken cancellationToken) {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries().Any(entry => entry.State is EntityState.Added or EntityState.Deleted ||
            entry.State == EntityState.Modified && (entry.Entity is not ProviderProfile || entry.Properties.Any(property =>
                property.IsModified && property.Metadata.Name is not (nameof(ProviderProfile.LastHealthCheckAtUtc) or nameof(ProviderProfile.LastHealthStatus)))))) {
            throw new InvalidOperationException("A diagnostic write can change only provider health metadata.");
        }
        return base.SaveChangesAsync(true, cancellationToken);
    }

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
