using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Security;

public sealed class SecurityDbContext(DbContextOptions<SecurityDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new SecretRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SecretReferenceConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
