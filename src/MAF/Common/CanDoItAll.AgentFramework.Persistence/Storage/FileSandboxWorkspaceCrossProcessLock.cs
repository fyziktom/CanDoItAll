using CanDoItAll.Infrastructure;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceCrossProcessLock(
    string managedRoot,
    string lockPath,
    DurableFileWriter durableFileWriter)
{
    private static readonly TimeSpan AcquisitionTimeout = TimeSpan.FromMinutes(1);

    public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken) {
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.coordination.wait");
        return await durableFileWriter.AcquireCoordinationAsync(
            managedRoot,
            lockPath,
            AcquisitionTimeout,
            requirePrivateUnixMode: false,
            cancellationToken);
    }
}
