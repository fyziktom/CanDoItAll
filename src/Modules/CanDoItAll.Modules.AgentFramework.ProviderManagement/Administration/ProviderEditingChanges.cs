using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Editing;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class ProviderEditingChanges(EditingChangeFeed feed, ICanonicalRuntimeDatabase database,
    IEditingUiAccess access, ProviderEditingWrites writes, ILogger<EditingObservation> logger)
    : IProviderProfileCommitObserver, ISharedProviderPublicationCommitObserver {
    public EditingScope Scope => new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation);
    public ProviderEditingOpening Open(Guid editorId, Func<bool> owns, EditingOwner owner = EditingOwner.ProviderProfile,
        ProviderEditingOpening? parent = null) {
        if (owner is not (EditingOwner.ProviderProfile or EditingOwner.SharedProviderSource)) {
            throw new ArgumentOutOfRangeException(nameof(owner));
        }
        return new(feed, access, new(parent?.Scope ?? Scope, owner), editorId, owns, writes, logger, parent);
    }
    public Task ProviderSavedAsync(Guid providerId, CancellationToken cancellationToken = default) => Publish(providerId, EditingChangeKind.EditableContentChanged);
    public Task ProviderDeletedAsync(Guid providerId, CancellationToken cancellationToken = default) => Publish(providerId, EditingChangeKind.Deleted);
    public Task ProviderHealthChangedAsync(Guid providerId, CancellationToken cancellationToken = default) => Publish(providerId, EditingChangeKind.MetadataChanged);
    public Task PublicationChangedAsync(Guid providerId, CancellationToken cancellationToken = default) => Publish(providerId, EditingChangeKind.LifecycleChanged);
    public void SourcesChanged(Guid sourceId) {
        var origin = writes.Origin;
        feed.Publish(new(Scope, EditingOwner.SharedProviderSource, sourceId), EditingChangeKind.MetadataChanged, origin?.EditorId, origin?.OperationId);
    }
    private Task Publish(Guid id, EditingChangeKind kind) {
        var origin = writes.Origin;
        feed.Publish(new(Scope, EditingOwner.ProviderProfile, id), kind, origin?.EditorId, origin?.OperationId);
        return Task.CompletedTask;
    }
}

public sealed class ProviderEditingOpening : IAsyncDisposable {
    private readonly EditingObservation observation;
    private readonly EditingTopic topic;
    private readonly ProviderEditingWrites writes;
    private readonly Func<bool> owns;
    private readonly ProviderEditingOpening? parent;
    private bool stopped;
    private Func<Task>? retired;
    public Guid EditorId { get; }
    public EditingScope Scope => topic.Scope;
    public string PrincipalStamp => observation.PrincipalStamp;

    internal ProviderEditingOpening(EditingChangeFeed feed, IEditingUiAccess access, EditingTopic topic, Guid editorId,
        Func<bool> owns, ProviderEditingWrites writes, ILogger<EditingObservation> logger, ProviderEditingOpening? parent = null) {
        observation = new(feed, access, topic, logger);
        this.topic = topic;
        this.owns = owns;
        this.writes = writes;
        this.parent = parent;
        EditorId = editorId;
    }

    public async Task StartAsync(Func<EditingChangeBatch, Task> changed, Func<Task> retired) {
        this.retired = retired;
        if (parent is not null) {
            await parent.AuthorizeAsync(false, CancellationToken.None);
        }
        await observation.StartAsync(changed, async () => {
            Stop();
            await retired();
        }, catalog: true);
        await AuthorizeAsync(false, CancellationToken.None);
    }

    private async ValueTask AuthorizeAsync(bool write, CancellationToken cancellationToken) {
        try {
            if (stopped || !owns()) {
                throw new UnauthorizedAccessException("The original provider workspace is no longer current.");
            }
            if (parent is not null) {
                await parent.AuthorizeAsync(write, cancellationToken);
                if (Scope != parent.Scope || PrincipalStamp != parent.PrincipalStamp) {
                    throw new UnauthorizedAccessException("The child provider workspace cannot change its original dataset or actor.");
                }
            }
            await observation.AuthorizeAsync(write, cancellationToken);
            if (stopped || !owns()) {
                throw new UnauthorizedAccessException("The original provider workspace changed during authorization.");
            }
        } catch (UnauthorizedAccessException) {
            Stop();
            if (retired is not null) {
                await retired();
            }
            throw;
        }
    }

    public async Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken) {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, observation.Lifetime);
        await AuthorizeAsync(false, request.Token);
        var result = await read(request.Token);
        await AuthorizeAsync(false, request.Token);
        return result;
    }

    public async Task<T> WriteAsync<T>(Func<Task<T>> write, Func<bool> current, CancellationToken cancellationToken) {
        await AuthorizeAsync(true, cancellationToken);
        using var admission = writes.Enter(topic, new(EditorId, Guid.NewGuid()), async token => {
            if (!current()) {
                throw new UnauthorizedAccessException("The original provider editor target is no longer current.");
            }
            await AuthorizeAsync(true, token);
            if (!current()) {
                throw new UnauthorizedAccessException("The provider target changed during authorization.");
            }
        });
        return await write();
    }

    public void Stop() {
        stopped = true;
        observation.Stop();
    }
    public async ValueTask DisposeAsync() {
        Stop();
        await observation.DisposeAsync();
    }
}
