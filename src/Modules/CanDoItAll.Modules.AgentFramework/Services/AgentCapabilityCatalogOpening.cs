using CanDoItAll.Editing;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentCapabilityCatalogOpening : IAsyncDisposable {
    private readonly AgentConfigurationOpening agents;
    private readonly AgentConfigurationOpening capabilities;
    private bool stopped;
    private long sequence;
    private long covered;

    public AgentCapabilityCatalogOpening(AgentConfigurationChanges changes, Func<bool> owns) {
        EditorId = Guid.NewGuid();
        agents = new(changes, EditingOwner.AgentDefinition, null, EditorId, () => !stopped && owns());
        capabilities = new(changes, EditingOwner.AgentCapability, null, EditorId, () => !stopped && owns());
    }

    public Guid EditorId { get; }
    public bool IsReady => !stopped && agents.IsReady && capabilities.IsReady;
    public long Sequence => Interlocked.Read(ref sequence);
    public bool HasChanges => Sequence > Interlocked.Read(ref covered);

    public async Task StartAsync(Func<Task> changed, Func<Task> retired) {
        async Task Changed(EditingChangeBatch batch) {
            if (batch.RequiresResync || batch.Changes.Any(change => change.OriginEditorId != EditorId && change.Kind != EditingChangeKind.MetadataChanged)) {
                Interlocked.Increment(ref sequence);
                await changed();
            }
        }
        async Task Retired() {
            Stop();
            await retired();
        }
        await agents.StartAsync(Changed, Retired, catalog: true);
        await capabilities.StartAsync(Changed, Retired, catalog: true);
    }

    public Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken token)
        => agents.ReadAsync(current => capabilities.ReadAsync(read, current), token);

    public async Task<T> WriteAsync<T>(Func<CancellationToken, Task<T>> write, CancellationToken token) {
        await capabilities.AuthorizeAsync(true, token);
        return await agents.WriteAsync(write, token);
    }

    public void Acknowledge(long observedSequence) => Interlocked.Exchange(ref covered, Math.Max(Interlocked.Read(ref covered), observedSequence));

    public void Stop() {
        stopped = true;
        agents.Stop();
        capabilities.Stop();
    }

    public async ValueTask DisposeAsync() {
        Stop();
        await agents.DisposeAsync();
        await capabilities.DisposeAsync();
    }
}
