using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.Tests.Unit;

internal sealed class RecordingProviderHistory : IProviderHistoryRecorder {
    private readonly HistoryPartition partition = new(Guid.NewGuid(), Guid.NewGuid(), "test");
    public ConcurrentQueue<HistoryAttemptStart> Starts { get; } = new();
    public ConcurrentQueue<(HistoryAttemptStart Start, HistoryAttemptCompletion Completion, string? Response)> Completions { get; } = new();
    public bool FailBegin { get; init; }
    public bool FailCompletion { get; init; }
    public HistoryCaptureMode Mode { get; init; } = HistoryCaptureMode.Detailed;

    public Task<HistoryAttemptStart> BeginAsync(HistoryInvocation invocation, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (FailBegin) {
            throw new ProviderHistoryException(HistoryFailure.Unavailable, "Fixture start failure.");
        }
        var context = invocation.Context;
        var owner = context.Owner is { } source
            ? new CanonicalEvidenceReference(partition, source.Kind, source.OwnerId, source.EvidenceId) : null;
        var start = new HistoryAttemptStart(HistoryEntryId.New(), partition, new(0, 0), context.RequestId,
            ProviderAttemptId.New(), DateTimeOffset.UtcNow, invocation.Provider, invocation.Operation, context.Workload,
            context.Caller, new(new() { CaptureMode = Mode }, 0), owner, context.CorrelationId);
        Starts.Enqueue(start);
        context.Attempts.Add(start);
        return Task.FromResult(start);
    }

    public Task CompleteAsync(HistoryAttemptStart start, HistoryAttemptCompletion completion, string? response,
        CancellationToken cancellationToken) {
        Completions.Enqueue((start, completion, response));
        return FailCompletion ? Task.FromException(new IOException("Fixture terminal write failure.")) : Task.CompletedTask;
    }
}
