using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectCrossModuleMutationTimeSource
{
    public static Task<DateTimeOffset> GetUtcNowAsync(
        AppDbContext dbContext,
        IClock fallbackClock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(fallbackClock);
        if (!dbContext.Database.IsRelational())
        {
            return Task.FromResult(fallbackClock.GetUtcNow());
        }

        if (!string.Equals(
                dbContext.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Database-authoritative mutation time is not supported by relational provider '{dbContext.Database.ProviderName ?? "unknown"}'.");
        }

        return dbContext.Database
            .SqlQueryRaw<DateTimeOffset>(
                "SELECT CURRENT_TIMESTAMP AS \"Value\"")
            .SingleAsync(cancellationToken);
    }
}
