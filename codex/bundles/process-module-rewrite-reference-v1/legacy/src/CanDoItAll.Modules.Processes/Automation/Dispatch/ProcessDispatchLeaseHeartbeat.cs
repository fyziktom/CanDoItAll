namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDispatchLeaseHeartbeat : IAsyncDisposable
{
    private readonly Guid stepRunId;
    private readonly TimeSpan heartbeatInterval;
    private readonly Func<CancellationToken, Task> renewLeaseAsync;
    private readonly CancellationTokenSource stopCancellation;
    private readonly CancellationTokenSource dispatchCancellation;
    private readonly Task heartbeatTask;
    private Exception? claimLostFailure;
    private int claimLost;

    private ProcessDispatchLeaseHeartbeat(
        Guid stepRunId,
        TimeSpan heartbeatInterval,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        this.stepRunId = stepRunId;
        this.heartbeatInterval = heartbeatInterval;
        this.renewLeaseAsync = renewLeaseAsync;
        stopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        dispatchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        heartbeatTask = RunAsync();
    }

    public CancellationToken DispatchCancellationToken => dispatchCancellation.Token;

    public bool ClaimLost => Volatile.Read(ref claimLost) == 1;

    public static ProcessDispatchLeaseHeartbeat Start(
        Guid stepRunId,
        TimeSpan heartbeatInterval,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(renewLeaseAsync);

        if (stepRunId == Guid.Empty)
        {
            throw new ArgumentException("Step run id is required.", nameof(stepRunId));
        }

        if (heartbeatInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(heartbeatInterval), "Heartbeat interval must be positive.");
        }

        return new ProcessDispatchLeaseHeartbeat(stepRunId, heartbeatInterval, renewLeaseAsync, cancellationToken);
    }

    public void ThrowIfClaimLost()
    {
        if (!ClaimLost)
        {
            return;
        }

        throw CreateClaimLostException();
    }

    public ProcessDispatchClaimLostException CreateClaimLostException()
        => claimLostFailure is null
            ? new ProcessDispatchClaimLostException(stepRunId)
            : new ProcessDispatchClaimLostException(stepRunId, claimLostFailure);

    public async ValueTask DisposeAsync()
    {
        await stopCancellation.CancelAsync();
        try
        {
            await heartbeatTask;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            stopCancellation.Dispose();
            dispatchCancellation.Dispose();
        }
    }

    private async Task RunAsync()
    {
        while (!stopCancellation.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(heartbeatInterval, stopCancellation.Token);
                await renewLeaseAsync(stopCancellation.Token);
            }
            catch (OperationCanceledException) when (stopCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                MarkClaimLost(exception);
                return;
            }
        }
    }

    private void MarkClaimLost(Exception exception)
    {
        claimLostFailure = exception;
        if (Interlocked.Exchange(ref claimLost, 1) == 1)
        {
            return;
        }

        dispatchCancellation.Cancel();
    }
}

internal sealed class ProcessDispatchClaimLostException : InvalidOperationException
{
    public ProcessDispatchClaimLostException(Guid stepRunId)
        : base($"The durable dispatch claim for process step run {stepRunId:D} is no longer held.")
    {
        StepRunId = stepRunId;
    }

    public ProcessDispatchClaimLostException(Guid stepRunId, Exception innerException)
        : base($"The durable dispatch claim for process step run {stepRunId:D} is no longer held.", innerException)
    {
        StepRunId = stepRunId;
    }

    public Guid StepRunId { get; }
}
