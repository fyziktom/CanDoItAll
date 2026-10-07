using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace.StorageSelection.Contracts;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceStorageCatalogSelectionSource : IStorageCatalogSelectionSource, IDisposable {
    private readonly WorkspaceService workspace;
    private readonly IDatabaseRuntimeState runtime;
    private readonly IDatabaseSwitchNotificationService notifications;
    private readonly DatabaseRuntimeSnapshot snapshot;
    private bool disposed;

    public WorkspaceStorageCatalogSelectionSource(WorkspaceService workspace, ICanonicalRuntimeDatabase canonical,
        IDatabaseRuntimeState runtime, IDatabaseSwitchNotificationService notifications) {
        this.workspace = workspace;
        this.runtime = runtime;
        this.notifications = notifications;
        snapshot = runtime.GetSnapshot();
        Context = new(canonical.Profile.Profile.Id, snapshot.Generation);
        notifications.Changed += ProfileChanged;
    }

    public StorageCatalogSelectionContext Context { get; }
    public bool IsCurrent => !disposed && snapshot.ActiveProfileId == Context.ProfileId && runtime.GetSnapshot() == snapshot;
    public event Action? ContextChanged;

    public async Task<IReadOnlyList<StorageSelectionItem>> ListAsync(CancellationToken cancellationToken = default) {
        RequireCurrent();
        var rows = await workspace.ListStorageCatalogAsync(cancellationToken);
        RequireCurrent();
        return [.. rows.Select(Project)];
    }

    public static StorageSelectionItem Project(StorageCatalogSummary row) => new(row.Id, row.Name,
        StoragePresentation.DescribeProvider(row.ProviderKind), StoragePresentation.DescribeConnectionMode(row.ConnectionMode),
        SafeEndpoint(row.EndpointOrRoot, row.ConnectionMode), row.DisplayOrder, row.IsEnabled, row.IsSystemDefault, row.IsReadOnly,
        StoragePresentation.DescribeHealth(row.HealthStatus));

    private static string SafeEndpoint(string endpoint, StorageConnectionMode connection) {
        if (!endpoint.Contains("://", StringComparison.Ordinal)) {
            if (connection == StorageConnectionMode.Local) {
                return endpoint;
            }
            var path = endpoint.Split('?', '#')[0];
            return path.Contains('@', StringComparison.Ordinal) ? "Endpoint unavailable" : path;
        }
        var address = endpoint.Split('?', '#')[0];
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) {
            return "Endpoint unavailable";
        }
        return new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty, Query = string.Empty, Fragment = string.Empty }.Uri.AbsoluteUri;
    }

    private void RequireCurrent() {
        if (!IsCurrent) {
            throw new InvalidOperationException("The storage selection source belongs to a retired profile. Reopen the editor.");
        }
    }

    private void ProfileChanged(object? sender, DatabaseProfileChangedNotification notification) => ContextChanged?.Invoke();

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        notifications.Changed -= ProfileChanged;
        ContextChanged?.Invoke();
        ContextChanged = null;
    }
}
