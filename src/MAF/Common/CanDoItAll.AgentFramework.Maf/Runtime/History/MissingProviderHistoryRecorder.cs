using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MissingProviderHistoryRecorder : IProviderHistoryRecorder {
    public Task<HistoryAttemptStart> BeginAsync(
        HistoryInvocation invocation,
        CancellationToken cancellationToken) =>
        Task.FromException<HistoryAttemptStart>(Missing());

    public Task CompleteAsync(
        HistoryAttemptStart start,
        HistoryAttemptCompletion completion,
        string? response,
        CancellationToken cancellationToken) =>
        Task.FromException(Missing());

    private static ProviderHistoryException Missing() => new(
        HistoryFailure.Unavailable,
        "Provider history persistence is required before provider invocation. Register AddProviderHistoryPersistence().");
}