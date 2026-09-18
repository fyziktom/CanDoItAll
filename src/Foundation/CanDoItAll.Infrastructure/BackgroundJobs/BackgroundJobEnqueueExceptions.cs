namespace CanDoItAll.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobEnqueueException(Guid jobId, Guid correlationId, Exception innerException)
    : Exception($"Background job '{jobId:D}' was committed, but its enqueue outcome is unconfirmed.", innerException) {
    public Guid JobId { get; } = jobId;
    public Guid CorrelationId { get; } = correlationId;
}

public sealed class BackgroundJobEnqueueCanceledException(Guid jobId, Guid correlationId, OperationCanceledException innerException)
    : OperationCanceledException($"Background job '{jobId:D}' was committed before enqueue cancellation; its enqueue outcome is unconfirmed.",
        innerException, innerException.CancellationToken) {
    public Guid JobId { get; } = jobId;
    public Guid CorrelationId { get; } = correlationId;
}
