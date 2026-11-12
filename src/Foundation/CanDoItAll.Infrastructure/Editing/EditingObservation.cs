using CanDoItAll.Editing;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.Editing;

public sealed class EditingObservation(IEditingChangeObserver observer, IEditingUiAccess access,
    EditingTopic topic, ILogger<EditingObservation> logger) : IAsyncDisposable {
    private readonly CancellationTokenSource lifetime = new();
    private EditingUiGrant? grant;
    private CancellationTokenSource? authorizedLifetime;
    private IEditingChangeSubscription? subscription;
    private Task? pump;
    private bool disposed;
    private int stopped;
    public string Actor => grant?.Actor ?? throw new UnauthorizedAccessException("Open an editing grant before obtaining its actor.");
    public string PrincipalStamp => RequireCurrentGrant().PrincipalStamp;
    public CancellationToken Lifetime => authorizedLifetime?.Token ?? lifetime.Token;

    public async Task StartAsync(Func<EditingChangeBatch, Task> deliver, Func<Task> retired, bool catalog = false) {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Volatile.Read(ref stopped) != 0) {
            throw new UnauthorizedAccessException("This editing observation is no longer current.");
        }
        if (grant is not null) {
            throw new InvalidOperationException("This editing observation was already opened.");
        }
        try {
            grant = await access.OpenAsync(topic, lifetime.Token);
            await access.ValidateAsync(grant, write: false, lifetime.Token);
            ObjectDisposedException.ThrowIf(disposed, this);
            RequireCurrentGrant();
            authorizedLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, grant.Lifetime);
            if (topic.ResourceId.HasValue || catalog) {
                subscription = observer.Subscribe(topic);
            }
            RequireCurrentGrant();
            pump = PumpAsync(deliver, retired);
        } catch {
            subscription?.Dispose();
            grant?.Dispose();
            grant = null;
            throw;
        }
    }

    public async ValueTask AuthorizeAsync(bool write, CancellationToken cancellationToken) {
        var current = RequireCurrentGrant();
        await access.ValidateAsync(current, write, cancellationToken);
        if (!ReferenceEquals(current, RequireCurrentGrant())) {
            throw new UnauthorizedAccessException("The original editing access grant is no longer current.");
        }
    }

    private EditingUiGrant RequireCurrentGrant() {
        var current = grant;
        if (current is null || disposed || Volatile.Read(ref stopped) != 0 || current.IsDisposed || current.Lifetime.IsCancellationRequested) {
            throw new UnauthorizedAccessException("Open a current editing access grant before continuing.");
        }
        return current;
    }

    public void Stop() {
        if (Interlocked.Exchange(ref stopped, 1) != 0) {
            return;
        }
        lifetime.Cancel();
        subscription?.Dispose();
    }

    private async Task PumpAsync(Func<EditingChangeBatch, Task> deliver, Func<Task> retired) {
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, grant!.Lifetime);
        try {
            if (subscription is null) {
                await Task.Delay(Timeout.InfiniteTimeSpan, reading.Token);
                return;
            }
            while (!reading.IsCancellationRequested) {
                var batch = await subscription.ReadAsync(reading.Token);
                await AuthorizeAsync(write: false, reading.Token);
                if (!reading.IsCancellationRequested) {
                    await deliver(batch).WaitAsync(reading.Token);
                }
            }
        } catch (OperationCanceledException) when (reading.IsCancellationRequested) {
            if (!lifetime.IsCancellationRequested && !grant.IsDisposed) {
                subscription?.Dispose();
                await retired().WaitAsync(lifetime.Token);
            }
        } catch (UnauthorizedAccessException) when (lifetime.IsCancellationRequested) {
        } catch (Exception exception) {
            logger.LogWarning("Editing observation retired after {FailureType}; owner {Owner}, dataset activation {ActivationGeneration}.",
                exception.GetType().Name, topic.Owner, topic.Scope.ActivationGeneration);
            subscription?.Dispose();
            if (!lifetime.IsCancellationRequested && !grant.IsDisposed) {
                await retired().WaitAsync(lifetime.Token);
            }
        }
    }

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        Stop();
        if (pump is not null) {
            try {
                await pump;
            } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
            }
        }
        grant?.Dispose();
        authorizedLifetime?.Dispose();
        lifetime.Dispose();
    }
}

public sealed class UnavailableEditingUiAccess : IEditingUiAccess {
    public Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken) =>
        Task.FromException<EditingUiGrant>(new UnauthorizedAccessException("Editing UI access is unavailable in this host."));
    public ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken) =>
        ValueTask.FromException(new UnauthorizedAccessException("Editing UI access is unavailable in this host."));
}
