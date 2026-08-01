namespace CanDoItAll.Memory.Application;

internal sealed record MemoryOperationWorkerOutcome(
    int Completed,
    int Retried,
    int DeadLettered,
    int TimedOut,
    int Cancelled,
    string Diagnostic)
{
    public static MemoryOperationWorkerOutcome ForCompleted(string diagnostic) => new(1, 0, 0, 0, 0, diagnostic);
    public static MemoryOperationWorkerOutcome ForRetried(string diagnostic) => new(0, 1, 0, 0, 0, diagnostic);
    public static MemoryOperationWorkerOutcome ForDeadLettered(string diagnostic) => new(0, 0, 1, 0, 0, diagnostic);
    public static MemoryOperationWorkerOutcome ForTimedOut(string diagnostic) => new(0, 0, 0, 1, 0, diagnostic);
    public static MemoryOperationWorkerOutcome ForCancelled(string diagnostic) => new(0, 0, 0, 0, 1, diagnostic);
}
