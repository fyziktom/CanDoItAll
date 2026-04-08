using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IDisposable? runtimeLease = null) : DbContext(options)
{
    private IDisposable? _runtimeLease = runtimeLease;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var assembly in AppDbContextModelRegistry.Assemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }

        base.OnModelCreating(modelBuilder);
    }

    public override void Dispose()
    {
        base.Dispose();
        ReleaseRuntimeLease();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        ReleaseRuntimeLease();
    }

    private void ReleaseRuntimeLease()
    {
        Interlocked.Exchange(ref _runtimeLease, null)?.Dispose();
    }
}
