using CanDoItAll.Editing;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Prompts.Components;

public sealed class PromptGalleryObservation(PromptGalleryChanges changes, IPromptGalleryUiAccess access,
    ILogger<PromptGalleryObservation> logger) : IAsyncDisposable {
    private readonly CancellationTokenSource lifetime = new();
    private PromptGalleryUiGrant? grant;
    private IEditingChangeSubscription? subscription;
    private Task? pump;

    public async Task StartAsync(Guid? target, Func<EditingChangeBatch, Task> deliver, Func<Task> retired, bool catalog = false) {
        grant = await access.OpenAsync(changes.Scope, lifetime.Token);
        await access.ValidateAsync(grant, write: false, lifetime.Token);
        if (target.HasValue || catalog) {
            subscription = changes.Subscribe(target);
        }
        pump = PumpAsync(deliver, retired);
    }

    public async ValueTask AuthorizeAsync(bool write, CancellationToken cancellationToken) {
        if (grant is null) {
            throw new UnauthorizedAccessException("Open a current Gallery access grant before continuing.");
        }
        lifetime.Token.ThrowIfCancellationRequested();
        await access.ValidateAsync(grant, write, cancellationToken);
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
                    await deliver(batch);
                }
            }
        } catch (OperationCanceledException) when (reading.IsCancellationRequested) {
            if (!lifetime.IsCancellationRequested) {
                subscription?.Dispose();
                await retired();
            }
        } catch (Exception exception) {
            logger.LogWarning("Prompt observation retired after {FailureType}; dataset activation {ActivationGeneration}.",
                exception.GetType().Name, changes.Scope.ActivationGeneration);
            subscription?.Dispose();
            if (!lifetime.IsCancellationRequested) {
                await retired();
            }
        }
    }

    public async ValueTask DisposeAsync() {
        if (lifetime.IsCancellationRequested) {
            return;
        }
        lifetime.Cancel();
        subscription?.Dispose();
        if (pump is not null) {
            await pump;
        }
        grant?.Dispose();
        lifetime.Dispose();
    }
}
