using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;

namespace CanDoItAll.Modules.AgentFramework;

public interface IAgentSettingsAccess {
    Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken token);
    Task<T> WriteAsync<T>(Func<CancellationToken, Task<T>> write, CancellationToken token);
    ValueTask AuthorizePreviewAsync(CancellationToken token);
}

public sealed class AgentSettingsOpening(AgentConfigurationChanges configuration, WorkflowChanges writes,
    EditingOwner owner, Func<bool> owns) : IAgentSettingsAccess, IAsyncDisposable {
    private readonly EditingScope scope = configuration.Scope;
    private readonly EditingObservation observation = configuration.Observe(owner, configuration.Scope.Id);
    private readonly EditingObservation preview = configuration.Observe(EditingOwner.AgentVoicePreview);
    private readonly Guid editorId = Guid.NewGuid();
    private bool previewStarted;
    public CancellationToken Lifetime => observation.Lifetime;

    public Task StartAsync(Func<Task> changed, Func<Task> retired) => observation.StartAsync(
        batch => batch.RequiresResync || batch.Changes.Any(change => change.OriginEditorId != editorId) ? changed() : Task.CompletedTask, () => {
            observation.Stop();
            return retired();
        });

    private async ValueTask AuthorizeAsync(bool write, CancellationToken token) {
        if (!owns() || scope != configuration.Scope) {
            throw new UnauthorizedAccessException("The original settings editor or dataset is no longer current.");
        }
        await observation.AuthorizeAsync(write, token);
        if (!owns() || scope != configuration.Scope) {
            throw new UnauthorizedAccessException("The original settings editor changed during authorization.");
        }
    }

    public async Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken token) {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(token, Lifetime);
        await AuthorizeAsync(false, request.Token);
        var result = await read(request.Token);
        await AuthorizeAsync(false, request.Token);
        return result;
    }

    public async Task<T> WriteAsync<T>(Func<CancellationToken, Task<T>> write, CancellationToken token) {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(token, Lifetime);
        await AuthorizeAsync(true, request.Token);
        using var origin = writes.Enter(new(editorId, Guid.NewGuid()));
        using var admission = writes.EnterUiWrite(current => AuthorizeAsync(true, current));
        return await write(request.Token);
    }

    public async ValueTask AuthorizePreviewAsync(CancellationToken token) {
        await AuthorizeAsync(false, token);
        if (!previewStarted) {
            await preview.StartAsync(_ => Task.CompletedTask, () => Task.CompletedTask);
            previewStarted = true;
        }
        await preview.AuthorizeAsync(true, token);
        await AuthorizeAsync(false, token);
    }

    public async ValueTask DisposeAsync() {
        observation.Stop();
        preview.Stop();
        await observation.DisposeAsync();
        await preview.DisposeAsync();
    }
}
