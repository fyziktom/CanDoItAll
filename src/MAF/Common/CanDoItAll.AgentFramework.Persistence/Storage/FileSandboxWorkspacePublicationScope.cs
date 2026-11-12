using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspacePublicationScope : IAsyncDisposable {
    private static readonly TimeSpan KeyAcquisitionTimeout = TimeSpan.FromMinutes(1);
    private readonly FileSandboxWorkspaceCoordinationGate gate;
    private readonly FileSandboxWorkspaceCrossProcessLock workspaceLock;
    private readonly FileSandboxWorkspaceStorageLayout layout;
    private readonly DurableFileWriter writer;
    private readonly TimeProvider timeProvider;
    private readonly FileSandboxWorkspaceAccessPriority priority;
    private IAsyncDisposable? heldWorkspace;
    private IDisposable? heldGate;
    private bool disposed;
    private Exception? operationFailure;
    private FileSandboxWorkspaceWriteSet? heldWriteSet;
    private IAsyncDisposable? heldAuthority;
    private readonly List<IAsyncDisposable> heldKeys = [];

    private FileSandboxWorkspacePublicationScope(FileSandboxWorkspaceCoordinationGate gate, FileSandboxWorkspaceCrossProcessLock workspaceLock,
        FileSandboxWorkspaceStorageLayout layout, DurableFileWriter writer, TimeProvider timeProvider, FileSandboxWorkspaceAccessPriority priority) {
        this.gate = gate;
        this.workspaceLock = workspaceLock;
        this.layout = layout;
        this.writer = writer;
        this.timeProvider = timeProvider;
        this.priority = priority;
    }

    internal static async Task<FileSandboxWorkspacePublicationScope> EnterAsync(FileSandboxWorkspaceCoordinationGate gate,
        FileSandboxWorkspaceCrossProcessLock workspaceLock, FileSandboxWorkspaceStorageLayout layout,
        DurableFileWriter writer, CancellationToken cancellationToken, TimeProvider? timeProvider = null,
        FileSandboxWorkspaceAccessPriority priority = FileSandboxWorkspaceAccessPriority.Ordinary) {
        var scope = new FileSandboxWorkspacePublicationScope(gate, workspaceLock, layout, writer, timeProvider ?? TimeProvider.System, priority);
        await scope.EnterWorkspaceAsync(cancellationToken).ConfigureAwait(false);
        return scope;
    }

    internal async Task<IAsyncDisposable> AcquireAsync(FileSandboxWorkspaceWriteSet writeSet,
        FileSandboxWorkspaceMutationWitness witness, CancellationToken cancellationToken,
        Func<CancellationToken, Task>? requireCurrentAuthority = null,
        Func<CancellationToken, Task<IAsyncDisposable>>? acquireAuthority = null) {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (heldGate is null || heldWorkspace is null) {
            throw new InvalidOperationException("A prepared workspace publication requires the short workspace guard.");
        }
        if (heldWriteSet is not null) {
            if (!heldWriteSet.Contains(writeSet)) {
                throw new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.WriteSetChanged);
            }
            if (requireCurrentAuthority is not null || acquireAuthority is not null) {
                throw new InvalidOperationException("External authority cannot be introduced inside an existing publication.");
            }
            return EmptyLease.Instance;
        }
        await witness.RequireUnchangedAsync(cancellationToken).ConfigureAwait(false);
        await ExitWorkspaceAsync().ConfigureAwait(false);
        try {
            var acquisitionStarted = timeProvider.GetTimestamp();
            using (AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.publication.keys.wait")) {
                foreach (var path in writeSet.LockPaths(layout)) {
                    var remaining = RequireRemainingBudget();
                    heldKeys.Add(await writer.AcquireCoordinationAsync(layout.RootPath, path, remaining,
                        requirePrivateUnixMode: false, cancellationToken).ConfigureAwait(false));
                    _ = RequireRemainingBudget();
                }
            }
            if (requireCurrentAuthority is not null) {
                await requireCurrentAuthority(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (acquireAuthority is not null) {
                heldAuthority = await acquireAuthority(cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The publication owner returned no authority lease.");
                cancellationToken.ThrowIfCancellationRequested();
            }
            using (AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.publication.workspace.reacquire")) {
                await EnterWorkspaceAsync(cancellationToken).ConfigureAwait(false);
            }
            await witness.RequireUnchangedAsync(cancellationToken).ConfigureAwait(false);
            heldWriteSet = writeSet;
            return new PublicationLease(this);

            TimeSpan RequireRemainingBudget() {
                cancellationToken.ThrowIfCancellationRequested();
                var elapsed = timeProvider.GetElapsedTime(acquisitionStarted);
                if (elapsed < TimeSpan.Zero) {
                    throw new InvalidOperationException("The workspace publication clock moved backwards.");
                }
                var remaining = KeyAcquisitionTimeout - elapsed;
                if (remaining <= TimeSpan.Zero) {
                    throw new TimeoutException("Timed out acquiring the complete workspace publication key set.");
                }
                return remaining;
            }
        } catch (Exception failure) {
            RetainFailure(failure);
            try {
                await ReleaseKeysAsync().ConfigureAwait(false);
            } catch (Exception cleanup) {
                var combined = new AggregateException("Workspace publication admission and key release failed.", failure, cleanup);
                RetainFailure(combined);
                throw combined;
            }
            ExceptionDispatchInfo.Capture(failure).Throw();
            throw;
        }
    }

    internal void RetainFailure(Exception failure) => operationFailure = failure;

    internal ValueTask ReleaseWorkspaceForAuthoritySnapshotAsync() {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (heldWriteSet is null || heldGate is null || heldWorkspace is null) {
            throw new InvalidOperationException("An authorization snapshot requires its complete selected key set before releasing workspace coordination.");
        }
        return ExitWorkspaceAsync();
    }

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        Exception? failure = null;
        try {
            await ExitWorkspaceAsync().ConfigureAwait(false);
        } catch (Exception exception) {
            failure = exception;
        }
        try {
            await ReleaseKeysAsync().ConfigureAwait(false);
        } catch (Exception cleanup) {
            if (failure is not null) {
                failure = new AggregateException("Workspace coordination release failed.", failure, cleanup);
            } else {
                failure = cleanup;
            }
        }
        if (failure is not null) {
            if (operationFailure is not null) {
                throw new AggregateException("Workspace publication and coordination release failed.", operationFailure, failure);
            }
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private async Task EnterWorkspaceAsync(CancellationToken cancellationToken) {
        heldGate = await gate.AcquireAsync(priority, cancellationToken).ConfigureAwait(false);
        try {
            heldWorkspace = await workspaceLock.AcquireAsync(cancellationToken).ConfigureAwait(false);
        } catch {
            heldGate.Dispose();
            heldGate = null;
            throw;
        }
    }

    private async ValueTask ExitWorkspaceAsync() {
        var acquired = heldWorkspace;
        heldWorkspace = null;
        try {
            if (acquired is not null) {
                await acquired.DisposeAsync().ConfigureAwait(false);
            }
        } finally {
            heldGate?.Dispose();
            heldGate = null;
        }
    }

    private async ValueTask ReleaseKeysAsync() {
        heldWriteSet = null;
        List<Exception>? failures = null;
        var authority = heldAuthority;
        heldAuthority = null;
        if (authority is not null) {
            try {
                await authority.DisposeAsync().ConfigureAwait(false);
            } catch (Exception failure) {
                (failures ??= []).Add(failure);
            }
        }
        for (var index = heldKeys.Count - 1; index >= 0; index--) {
            var acquired = heldKeys[index];
            heldKeys.RemoveAt(index);
            try {
                await acquired.DisposeAsync().ConfigureAwait(false);
            } catch (Exception failure) {
                (failures ??= []).Add(failure);
            }
        }
        if (failures is not null) {
            throw new AggregateException("Workspace publication key release failed.", failures);
        }
    }

    private sealed class PublicationLease(FileSandboxWorkspacePublicationScope scope) : IAsyncDisposable {
        private FileSandboxWorkspacePublicationScope? owner = scope;

        public async ValueTask DisposeAsync() {
            var acquired = Interlocked.Exchange(ref owner, null);
            if (acquired is null) {
                return;
            }
            try {
                await acquired.ReleaseKeysAsync().ConfigureAwait(false);
            } catch (Exception cleanup) {
                if (acquired.operationFailure is not null) {
                    var combined = new AggregateException("Workspace publication and key release failed.", acquired.operationFailure, cleanup);
                    acquired.RetainFailure(combined);
                    throw combined;
                }
                acquired.RetainFailure(cleanup);
                throw;
            }
        }
    }

    private sealed class EmptyLease : IAsyncDisposable {
        internal static EmptyLease Instance { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
