using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.TestLab;

public sealed class TestLabDbContext(DbContextOptions<TestLabDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new TestPlanConfiguration());
        modelBuilder.ApplyConfiguration(new TestCaseRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TestEvidenceRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TestRunRecordConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
