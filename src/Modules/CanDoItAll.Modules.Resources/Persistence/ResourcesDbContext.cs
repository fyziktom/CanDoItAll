using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Resources;

public sealed class ResourcesDbContext(DbContextOptions<ResourcesDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new ProjectResourceConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
