using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Modules.AgentFramework;

internal static class WorkflowPersistenceProvider
{
    private const string InMemoryProviderName = "Microsoft.EntityFrameworkCore.InMemory";
    private static readonly SemaphoreSlim InMemoryMutationGate = new(1, 1);

    public static bool IsInMemory(AppDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return string.Equals(
            dbContext.Database.ProviderName,
            InMemoryProviderName,
            StringComparison.Ordinal);
    }

    public static async ValueTask<IDisposable?> EnterInMemoryMutationAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (!IsInMemory(dbContext))
        {
            EnsureRelational(dbContext);
            return null;
        }

        await InMemoryMutationGate.WaitAsync(cancellationToken);
        return new SemaphoreLease(InMemoryMutationGate);
    }

    public static void EnsureRelational(AppDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (dbContext.Database.IsRelational())
        {
            return;
        }

        throw new InvalidOperationException(
            $"Workflow persistence does not support database provider "
            + $"'{dbContext.Database.ProviderName ?? "unknown"}'.");
    }

    public static Task CommitAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
        => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    public static Task RollbackAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
        => transaction?.RollbackAsync(cancellationToken) ?? Task.CompletedTask;

    private sealed class SemaphoreLease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? gate = gate;

        public void Dispose()
        {
            Interlocked.Exchange(ref gate, null)?.Release();
        }
    }
}
