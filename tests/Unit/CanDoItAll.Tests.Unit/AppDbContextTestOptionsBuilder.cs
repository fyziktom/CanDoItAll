using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

internal static class AppDbContextTestOptionsBuilder
{
    public static DbContextOptionsBuilder<AppDbContext> Create()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        ConfigureModelCacheKey(optionsBuilder);
        return optionsBuilder;
    }

    public static void ConfigureModelCacheKey(DbContextOptionsBuilder optionsBuilder)
    {
        AppDbContextOptionsConfigurator.ConfigureModelCacheKey(optionsBuilder);
    }
}
