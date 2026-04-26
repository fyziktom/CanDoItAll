using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private async Task<IDbContextTransaction> BeginCoordinatedTransactionAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await dbContext.Database.BeginTransactionAsync(cancellationToken);
            }
            catch (Exception ex) when (
                dbContext.Database.IsSqlite() &&
                SqliteWriteCoordination.IsBusy(ex) &&
                attempt < SqliteWriteCoordination.RetryAttemptCount)
            {
                await Task.Delay(SqliteWriteCoordination.GetRetryDelay(attempt), cancellationToken);
            }
        }
    }
}
