using CanDoItAll.Infrastructure;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceCrossProcessLock(
    string managedRoot,
    string lockPath,
    DurableFileWriter durableFileWriter)
{
    private static readonly TimeSpan AcquisitionTimeout = TimeSpan.FromMinutes(1);

    public ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
        => durableFileWriter.AcquireCoordinationAsync(
            managedRoot,
            lockPath,
            AcquisitionTimeout,
            requirePrivateUnixMode: false,
            cancellationToken);
}
