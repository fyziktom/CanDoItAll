namespace CanDoItAll.Web.Api.Streaming;

internal static class ApiCommandTaskLifetime
{
    private static readonly TimeSpan DefaultDrainTimeout = TimeSpan.FromSeconds(5);

    public static async Task CancelAndObserveAsync(
        CancellationTokenSource commandLifetime,
        Task completion,
        ILogger logger,
        object operationId,
        TimeSpan? drainTimeout = null)
    {
        Task cancellation;
        try
        {
            cancellation = commandLifetime.CancelAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Cancelling API command after its response stream ended failed. ApiOperationId={ApiOperationId} FailureType={FailureType}.",
                operationId,
                exception.GetType().Name);
            cancellation = Task.CompletedTask;
        }

        var cleanup = Task.WhenAll(cancellation, completion);
        try
        {
            await cleanup
                .WaitAsync(drainTimeout ?? DefaultDrainTimeout)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            logger.LogWarning(
                "API command {ApiOperationId} did not finish cancellation and stop within the response-stream drain timeout. Its eventual cleanup will be observed in the background.",
                operationId);
            _ = ObserveLateCleanupAsync(
                cancellation,
                completion,
                logger,
                operationId);
            return;
        }
        catch (Exception)
        {
        }

        await ObserveCancellationAsync(
            cancellation,
            logger,
            operationId);
        await ObserveCompletionAsync(
            completion,
            commandLifetime,
            logger,
            operationId);
    }

    private static async Task ObserveLateCleanupAsync(
        Task cancellation,
        Task completion,
        ILogger logger,
        object operationId)
    {
        await Task.WhenAll(
            ObserveCancellationAsync(
                cancellation,
                logger,
                operationId),
            ObserveCompletionAsync(
                completion,
                commandLifetime: null,
                logger,
                operationId));
    }

    private static async Task ObserveCancellationAsync(
        Task cancellation,
        ILogger logger,
        object operationId)
    {
        try
        {
            await cancellation.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Cancelling API command after its response stream ended failed. ApiOperationId={ApiOperationId} FailureType={FailureType}.",
                operationId,
                exception.GetType().Name);
        }
    }

    private static async Task ObserveCompletionAsync(
        Task completion,
        CancellationTokenSource? commandLifetime,
        ILogger logger,
        object operationId)
    {
        try
        {
            await completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            commandLifetime is null ||
            commandLifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                "API command failed while its terminated response stream was being drained. ApiOperationId={ApiOperationId} FailureType={FailureType}.",
                operationId,
                exception.GetType().Name);
        }
    }
}
