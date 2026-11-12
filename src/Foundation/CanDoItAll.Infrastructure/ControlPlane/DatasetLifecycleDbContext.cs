using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class DatasetLifecycleDbContext(DbContextOptions<DatasetLifecycleDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new DatasetLifecycleRecordConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
