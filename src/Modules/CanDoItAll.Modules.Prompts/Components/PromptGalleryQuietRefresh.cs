namespace CanDoItAll.Modules.Prompts.Components;

public sealed class PromptGalleryQuietRefresh(Func<Task> refresh, Func<CancellationToken, Task>? delay = null) : IAsyncDisposable {
    private readonly CancellationTokenSource lifetime = new();
    private Task? running;
    private bool pending;

    public Task InvalidateAsync() {
        if (lifetime.IsCancellationRequested) {
            return Task.CompletedTask;
        }
        pending = true;
        return running is { IsCompleted: false } ? running : running = RunAsync();
    }

    private async Task RunAsync() {
        try {
            while (pending && !lifetime.IsCancellationRequested) {
                pending = false;
                await (delay?.Invoke(lifetime.Token) ?? Task.Delay(150, lifetime.Token));
                pending = false;
                await refresh();
            }
        } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
        }
    }

    public async ValueTask DisposeAsync() {
        lifetime.Cancel();
        if (running is not null) {
            await running;
        }
        lifetime.Dispose();
    }
}
