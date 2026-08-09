using CanDoItAll.Infrastructure;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceCrossProcessLock(
    string managedRoot,
    string lockPath,
    DurableFileWriter durableFileWriter)
{
    public ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
        => durableFileWriter.AcquireCoordinationAsync(
            managedRoot,
            lockPath,
            TimeSpan.FromSeconds(15),
            requirePrivateUnixMode: false,
            cancellationToken);
}
