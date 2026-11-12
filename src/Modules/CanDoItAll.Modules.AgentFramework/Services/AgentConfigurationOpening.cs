using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentConfigurationOpening : IAsyncDisposable {
    private readonly AgentConfigurationChanges changes;
    private readonly EditingObservation observation;
    private readonly EditingScope scope;
    private readonly Func<bool> owns;
    private bool stopped;

    public AgentConfigurationOpening(AgentConfigurationChanges changes, EditingOwner owner, Guid? id,
        Guid editorId, Func<bool> owns) {
        this.changes = changes;
        scope = changes.Scope;
        observation = changes.Observe(owner, id);
        this.owns = owns;
        EditorId = editorId;
    }

    public Guid EditorId { get; }
    public bool IsReady { get; private set; }
    public CancellationToken Lifetime => observation.Lifetime;

    public async Task StartAsync(Func<EditingChangeBatch, Task> changed, Func<Task> retired, bool catalog = false) {
        await observation.StartAsync(changed, async () => {
            Stop();
            await retired();
        }, catalog);
        await AuthorizeAsync(false, Lifetime);
        IsReady = true;
    }

    public async ValueTask AuthorizeAsync(bool write, CancellationToken cancellationToken) {
        try {
            RequireCurrent();
            await observation.AuthorizeAsync(write, cancellationToken);
            RequireCurrent();
        } catch (UnauthorizedAccessException) {
            Stop();
            throw;
        }
    }

    public async Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken) {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Lifetime);
        await AuthorizeAsync(false, request.Token);
        var result = await read(request.Token);
        await AuthorizeAsync(false, request.Token);
        return result;
    }

    public async Task<T> WriteAsync<T>(Func<CancellationToken, Task<T>> write, CancellationToken cancellationToken, Guid? operationId = null) {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Lifetime);
        await AuthorizeAsync(true, request.Token);
        using var admission = changes.EnterUiWrite(observation, scope, EditorId, () => !stopped && owns(), operationId);
        return await write(request.Token);
    }

    public async Task WriteAsync(Func<CancellationToken, Task> write, CancellationToken cancellationToken) {
        await WriteAsync(async token => {
            await write(token);
            return true;
        }, cancellationToken);
    }

    private void RequireCurrent() {
        if (stopped || !owns() || scope != changes.Scope) {
            Stop();
            throw new UnauthorizedAccessException("The original agent configuration opening or dataset is no longer current.");
        }
    }

    public void Stop() {
        stopped = true;
        IsReady = false;
        observation.Stop();
    }

    public async ValueTask DisposeAsync() {
        Stop();
        await observation.DisposeAsync();
    }
}
