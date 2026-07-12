namespace CanDoItAll.Memory.Application;

public sealed class MemoryRetentionWorker(
    IMemoryRetentionProjectionStore retentionProjectionStore,
    TimeProvider timeProvider,
    MemoryAsyncWorkerOptions options) : IMemoryRetentionWorker
{
    public async Task<MemoryAsyncWorkerRunResult> ApplyDueRetentionAsync(CancellationToken cancellationToken = default)
    {
        options.Validate();
        var now = timeProvider.GetUtcNow();
        var candidates = await retentionProjectionStore.ListDueAsync(
            now,
            options.MaxBatchSize,
            cancellationToken);
        var completed = 0;
        var retried = 0;
        var unpinRequests = 0;
        var diagnostics = new List<string>();

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var applied = await retentionProjectionStore.ApplyAsync(
                    candidate,
                    now,
                    $"Memory ledger retention applied '{candidate.Decision}'.",
                    cancellationToken);
                completed++;
                unpinRequests += applied.IpfsUnpinRequested ? 1 : 0;
                diagnostics.Add(
                    $"Applied '{candidate.Decision}' to '{candidate.LedgerName}' record '{candidate.RecordId}'.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                retried++;
                diagnostics.Add(MemoryWorkerExceptionDiagnostic.Create(
                    $"Memory retention for '{candidate.LedgerName}' record '{candidate.RecordId}'",
                    exception));
            }
        }

        return new MemoryAsyncWorkerRunResult(
            candidates.Count,
            completed,
            retried,
            0,
            0,
            0,
            0,
            0,
            0,
            unpinRequests,
            diagnostics);
    }
}
