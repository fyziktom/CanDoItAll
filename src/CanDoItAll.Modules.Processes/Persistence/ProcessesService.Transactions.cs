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
        return await dbContext.Database.BeginTransactionAsync(cancellationToken);
    }
}
