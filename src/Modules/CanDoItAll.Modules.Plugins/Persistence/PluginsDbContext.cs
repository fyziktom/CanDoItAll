using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Plugins;

public sealed class PluginsDbContext(DbContextOptions<PluginsDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new PluginInstallationRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PluginCapabilityGrantRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PluginConnectionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PluginOAuthConnectionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PluginOAuthSessionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PluginLogRecordConfiguration());
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
