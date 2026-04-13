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

    public override int SaveChanges()
    {
        return SaveChanges(true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampApplicationManagedConcurrencyTokens();
        return ExecuteSaveChangesWithCoordination(() => base.SaveChanges(acceptAllChangesOnSuccess));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(true, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampApplicationManagedConcurrencyTokens();
        return ExecuteSaveChangesWithCoordinationAsync(
            () => base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken),
            cancellationToken);
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

    private void StampApplicationManagedConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<IHasConcurrencyToken>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.ConcurrencyToken == Guid.Empty)
                {
                    entry.Entity.ConcurrencyToken = Guid.NewGuid();
                }

                continue;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.ConcurrencyToken = Guid.NewGuid();
            }
        }
    }

    private int ExecuteSaveChangesWithCoordination(Func<int> saveChanges)
    {
        ArgumentNullException.ThrowIfNull(saveChanges);

        var writeGate = ResolveSqliteWriteGate();
        if (writeGate is null)
        {
            return ExecuteSaveChangesWithRetry(saveChanges);
        }

        writeGate.Wait();
        try
        {
            return ExecuteSaveChangesWithRetry(saveChanges);
        }
        finally
        {
            writeGate.Release();
        }
    }

    private async Task<int> ExecuteSaveChangesWithCoordinationAsync(
        Func<Task<int>> saveChanges,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(saveChanges);

        var writeGate = ResolveSqliteWriteGate();
        if (writeGate is null)
        {
            return await ExecuteSaveChangesWithRetryAsync(saveChanges, cancellationToken);
        }

        await writeGate.WaitAsync(cancellationToken);
        try
        {
            return await ExecuteSaveChangesWithRetryAsync(saveChanges, cancellationToken);
        }
        finally
        {
            writeGate.Release();
        }
    }

    private SemaphoreSlim? ResolveSqliteWriteGate()
    {
        if (!Database.IsSqlite())
        {
            return null;
        }

        return SqliteWriteCoordination.GetWriteGate(Database.GetConnectionString());
    }

    private static int ExecuteSaveChangesWithRetry(Func<int> saveChanges)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return saveChanges();
            }
            catch (Exception ex) when (SqliteWriteCoordination.IsBusy(ex) && attempt < SqliteWriteCoordination.RetryAttemptCount)
            {
                Thread.Sleep(SqliteWriteCoordination.GetRetryDelay(attempt));
            }
        }
    }

    private static async Task<int> ExecuteSaveChangesWithRetryAsync(
        Func<Task<int>> saveChanges,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await saveChanges();
            }
            catch (Exception ex) when (SqliteWriteCoordination.IsBusy(ex) && attempt < SqliteWriteCoordination.RetryAttemptCount)
            {
                await Task.Delay(SqliteWriteCoordination.GetRetryDelay(attempt), cancellationToken);
            }
        }
    }
}
