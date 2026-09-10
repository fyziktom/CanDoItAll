using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var assembly in AppDbContextModelRegistry.Assemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        return SaveChanges(true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplicationManagedConcurrencyTokens.Stamp(ChangeTracker);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(true, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplicationManagedConcurrencyTokens.Stamp(ChangeTracker);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

}

internal sealed class AppDbContextModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context)
    {
        return Create(context, designTime: false);
    }

    public object Create(DbContext context, bool designTime)
    {
        return context is AppDbContext
            ? new AppDbContextModelCacheKey(context.GetType(), designTime, AppDbContextModelRegistry.ModelCacheKey)
            : new DefaultModelCacheKey(context.GetType(), designTime);
    }

    private sealed record AppDbContextModelCacheKey(
        Type ContextType,
        bool DesignTime,
        string RegistryKey);

    private sealed record DefaultModelCacheKey(
        Type ContextType,
        bool DesignTime);
}
