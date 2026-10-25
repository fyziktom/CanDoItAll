using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed class FiniteOperationDbContext(DbContextOptions<FiniteOperationDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new FiniteOperationStateRecordConfiguration());
        modelBuilder.ApplyConfiguration(new FiniteOperationEpochRecordConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
