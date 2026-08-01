using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Persistence.Hosting;

public enum MemoryBackgroundWorkerPhase
{
    OperationPolling = 0,
    FeedbackDelivery = 1,
    ProviderEventPolling = 2,
    ProviderEventInbox = 3,
    ProviderEventOutbox = 4,
    Retention = 5
}

public sealed record MemoryWorkerLeaseOwnerId
{
    public const int MaxLength = 180;

    private MemoryWorkerLeaseOwnerId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static MemoryWorkerLeaseOwnerId CreateUnique() =>
        Parse($"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}");

    public static MemoryWorkerLeaseOwnerId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Memory worker lease owner identifiers cannot exceed {MaxLength} characters.");
        }

        return new MemoryWorkerLeaseOwnerId(normalized);
    }

    public override string ToString() => Value;
}

public readonly record struct MemoryWorkerLeaseToken
{
    private MemoryWorkerLeaseToken(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static MemoryWorkerLeaseToken New() => Parse(Guid.NewGuid());

    public static MemoryWorkerLeaseToken Parse(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Memory worker lease tokens cannot be empty.", nameof(value));
        }

        return new MemoryWorkerLeaseToken(value);
    }
}

public sealed record MemoryWorkerLease(
    MemoryBackgroundWorkerPhase Phase,
    MemoryWorkerLeaseOwnerId OwnerId,
    MemoryWorkerLeaseToken Token,
    DateTimeOffset ExpiresAtUtc);

public sealed record MemoryWorkerLeaseExecution(
    bool Acquired,
    MemoryAsyncWorkerRunResult? WorkerResult)
{
    public static readonly MemoryWorkerLeaseExecution NotAcquired = new(false, null);

    public static MemoryWorkerLeaseExecution Completed(MemoryAsyncWorkerRunResult result) =>
        new(true, result ?? throw new ArgumentNullException(nameof(result)));
}

public interface IMemoryWorkerLeaseStore
{
    Task<MemoryWorkerLease?> TryAcquireAsync(
        MemoryBackgroundWorkerPhase phase,
        MemoryWorkerLeaseOwnerId ownerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> RenewAsync(
        MemoryWorkerLease lease,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> CompleteAsync(
        MemoryWorkerLease lease,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> ReleaseAsync(
        MemoryWorkerLease lease,
        DateTimeOffset releasedAtUtc,
        CancellationToken cancellationToken = default);
}

public interface IMemoryWorkerLeaseRunner
{
    Task<MemoryWorkerLeaseExecution> RunAsync(
        MemoryBackgroundWorkerPhase phase,
        Func<CancellationToken, Task<MemoryAsyncWorkerRunResult>> execute,
        CancellationToken cancellationToken = default);
}

public sealed class MemoryWorkerLeaseLostException(MemoryBackgroundWorkerPhase phase) :
    InvalidOperationException($"Memory background worker lease for phase '{phase}' was lost before completion.");
