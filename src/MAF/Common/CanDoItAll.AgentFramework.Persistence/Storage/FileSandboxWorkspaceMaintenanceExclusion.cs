using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed class FileSandboxWorkspaceMaintenanceExclusion(DurableFileWriter files) {
    public async ValueTask<IAsyncDisposable> AcquireAsync(string rootPath, WorkspaceScopeDescriptor scope,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(scope);
        if (!Path.IsPathFullyQualified(rootPath) || scope.Kind is not (WorkspaceScopeKind.Organization or WorkspaceScopeKind.Project or WorkspaceScopeKind.Sandbox) ||
                scope != new WorkspaceScopeDescriptor(scope.Kind, scope.Key)) {
            throw new ArgumentException("Maintenance exclusion requires an exact supported original workspace scope.");
        }
        var layout = new FileSandboxWorkspaceStorageLayout(Path.GetFullPath(rootPath), new(scope.Kind, scope.Key));
        var paths = new PhysicalFileSystemPathPolicyFactory().Create(layout.RootPath, PhysicalFileSystemCaseSensitivity.Sensitive);
        paths.EnsureSafePath(layout.DataRoot);
        var held = await files.AcquireExistingCoordinationAsync(layout.RootPath, layout.WorkspaceLockPath,
            TimeSpan.FromSeconds(5), requirePrivateUnixMode: false, cancellationToken);
        try {
            paths.EnsureSafePath(layout.DataRoot);
            cancellationToken.ThrowIfCancellationRequested();
            return held;
        } catch (Exception primary) {
            try {
                await held.DisposeAsync();
            } catch (Exception cleanup) {
                throw new AggregateException("Workspace maintenance admission and release both failed.", primary, cleanup);
            }
            throw;
        }
    }
}
