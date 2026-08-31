using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.ProviderHistory;

public sealed class HistoryAuthorizedOperation(
    IProviderHistoryAccess access, HistoryReadConcurrency concurrency,
    TimeProvider clock, ILogger<HistoryAuthorizedOperation> logger) {
    public async Task<T> RunAsync<T>(HistoryPermission permission,
        Func<HistoryAccessContext, CancellationToken, Task<T>> action, CancellationToken cancellationToken) {
        using var lease = concurrency.Enter();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var operationId = Guid.NewGuid();
        try {
            var context = await access.AuthorizeAsync(permission, linked.Token);
            if (permission == HistoryPermission.ReadContent) {
                await access.EnsureCurrentAsync(context, HistoryPermission.ReadMetadata, linked.Token);
            }
            var result = await action(context, linked.Token);
            if (permission == HistoryPermission.ReadContent) {
                await access.EnsureCurrentAsync(context, HistoryPermission.ReadMetadata, linked.Token);
            }
            await access.EnsureCurrentAsync(context, permission, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            return result;
        } catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            throw new ProviderHistoryException(HistoryFailure.TimedOut, "History exceeded its ten-second deadline. Narrow the range or retry.");
        } catch (Exception exception) when (exception is not (OperationCanceledException or ProviderHistoryException)) {
            logger.LogError("History operation {OperationId} failed with {FailureType}; inspect database and source availability.",
                operationId, exception.GetType().Name);
            throw new ProviderHistoryException(HistoryFailure.Unavailable, $"History could not be accessed. Diagnostic reference: {operationId:N}.");
        }
    }
}
