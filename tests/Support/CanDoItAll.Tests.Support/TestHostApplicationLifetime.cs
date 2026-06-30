using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Support;

public sealed class TestHostApplicationLifetime : IHostApplicationLifetime, IDisposable
{
    private readonly CancellationTokenSource _started = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly CancellationTokenSource _stopped = new();

    public CancellationToken ApplicationStarted => _started.Token;

    public CancellationToken ApplicationStopping => _stopping.Token;

    public CancellationToken ApplicationStopped => _stopped.Token;

    public void NotifyStarted()
    {
        if (!_started.IsCancellationRequested)
        {
            _started.Cancel();
        }
    }

    public void NotifyStopping()
    {
        if (!_stopping.IsCancellationRequested)
        {
            _stopping.Cancel();
        }
    }

    public void NotifyStopped()
    {
        if (!_stopped.IsCancellationRequested)
        {
            _stopped.Cancel();
        }
    }

    public void StopApplication()
    {
        NotifyStopping();
    }

    public void Dispose()
    {
        _started.Dispose();
        _stopping.Dispose();
        _stopped.Dispose();
    }
}
