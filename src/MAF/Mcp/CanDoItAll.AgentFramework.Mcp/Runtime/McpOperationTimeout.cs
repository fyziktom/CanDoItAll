namespace CanDoItAll.AgentFramework.Mcp;

internal sealed class McpOperationTimeout(TimeSpan timeout)
{
    public async Task RunAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CreateTimeoutSource(cancellationToken);
        try
        {
            await operation(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"{operationName} exceeded {timeout}.");
        }
    }

    public async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CreateTimeoutSource(cancellationToken);
        try
        {
            return await operation(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"{operationName} exceeded {timeout}.");
        }
    }

    private CancellationTokenSource CreateTimeoutSource(
        CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);
        return source;
    }
}
