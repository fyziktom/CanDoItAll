using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Projects.Pages;

public sealed class ProjectCatalogObservation(IEditingChangeObserver changes, IEditingUiAccess access,
    EditingScope scope, ILogger<EditingObservation> logger) : IAsyncDisposable {
    private readonly EditingObservation observation = new(changes, access, new(scope, EditingOwner.Project), logger);
    private Func<Task> read = null!;
    private Action retired = null!;
    private Task refresh = Task.CompletedTask;
    private bool pending;
    private bool running;
    private bool disposed;
    private int retirement;
    public bool IsRetired => Volatile.Read(ref retirement) != 0;

    public async Task StartAsync(Func<Task> refreshPortfolio, Action retire, Func<Func<Task>, Task> dispatch,
        Action<EditingChangeBatch>? observed = null) {
        read = refreshPortfolio;
        retired = retire;
        try {
            await observation.StartAsync(batch => dispatch(() => {
                observed?.Invoke(batch);
                QueueRefresh(coalesce: true);
                return Task.CompletedTask;
            }), () => dispatch(() => {
                Retire();
                return Task.CompletedTask;
            }), catalog: true);
        } catch (UnauthorizedAccessException) {
            Retire();
        }
    }

    public async ValueTask AuthorizeReadAsync(CancellationToken cancellationToken) {
        try {
            await observation.AuthorizeAsync(write: false, cancellationToken);
        } catch (UnauthorizedAccessException) {
            Retire();
            throw;
        }
    }
    public ValueTask AuthorizeWriteAsync(CancellationToken cancellationToken) => observation.AuthorizeAsync(write: true, cancellationToken);

    public async Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> acquire, CancellationToken cancellationToken) {
        try {
            await AuthorizeReadAsync(cancellationToken);
            var result = await acquire(cancellationToken);
            await AuthorizeReadAsync(cancellationToken);
            return result;
        } catch (UnauthorizedAccessException) {
            Retire();
            throw;
        }
    }

    public Task RefreshAsync() => QueueRefresh(coalesce: false);

    private Task QueueRefresh(bool coalesce) {
        if (disposed || IsRetired) {
            return Task.CompletedTask;
        }
        pending = true;
        if (!running) {
            running = true;
            refresh = RefreshLoopAsync(coalesce);
        }
        return refresh;
    }

    private async Task RefreshLoopAsync(bool coalesce) {
        try {
            await Task.Yield();
            while (pending && !disposed && !IsRetired) {
                if (coalesce) {
                    await Task.Delay(TimeSpan.FromMilliseconds(150), observation.Lifetime);
                }
                pending = false;
                await AuthorizeReadAsync(observation.Lifetime);
                if (!disposed && !IsRetired) {
                    await read();
                }
                coalesce = true;
            }
        } catch (OperationCanceledException) when (disposed || observation.Lifetime.IsCancellationRequested) {
            if (!disposed) {
                Retire();
            }
        } catch (Exception exception) {
            logger.LogWarning("Project catalog observation retired after {FailureType}, activation {ActivationGeneration}.",
                exception.GetType().Name, scope.ActivationGeneration);
            Retire();
        } finally {
            running = false;
        }
    }

    private void Retire() {
        if (disposed || Interlocked.Exchange(ref retirement, 1) != 0) {
            return;
        }
        pending = false;
        observation.Stop();
        retired();
    }

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        pending = false;
        await observation.DisposeAsync();
        await refresh;
    }
}
