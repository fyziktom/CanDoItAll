using System.Security.Cryptography;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

internal sealed class WorkflowSourceFileContentIdentityResolver
{
    private const int HashBufferSize = 64 * 1024;

    public async ValueTask<WorkflowSourceFileContentIdentity> ResolveAsync(
        WorkflowSourceIngestionFile file,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = CaptureSnapshot(file);
        await using var stream = new FileStream(
            file.FullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            HashBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var sha256 = await ComputeSha256Async(stream, cancellationToken).ConfigureAwait(false);
        EnsureSnapshotUnchanged(file, snapshot);

        return new WorkflowSourceFileContentIdentity(
            snapshot.Length,
            snapshot.LastWriteTimeUtc,
            sha256);
    }

    public async ValueTask EnsureUnchangedAsync(
        WorkflowSourceIngestionFile file,
        WorkflowSourceFileContentIdentity identity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();

        var expectedSnapshot = new WorkflowSourceFileSnapshot(identity.Length, identity.LastWriteTimeUtc);
        EnsureSnapshotUnchanged(file, expectedSnapshot);
        var currentIdentity = await ResolveAsync(file, cancellationToken).ConfigureAwait(false);
        if (currentIdentity != identity)
        {
            throw CreateChangedException(file);
        }
    }

    internal static async ValueTask<string> ComputeSha256Async(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        var buffer = GC.AllocateUninitializedArray<byte>(HashBufferSize);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        while (true)
        {
            var read = await stream
                .ReadAsync(buffer.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            hasher.AppendData(buffer, 0, read);
        }

        return Convert.ToHexString(hasher.GetHashAndReset());
    }

    private static WorkflowSourceFileSnapshot CaptureSnapshot(WorkflowSourceIngestionFile file)
    {
        var fileInfo = new FileInfo(file.FullPath);
        fileInfo.Refresh();
        if (!fileInfo.Exists)
        {
            throw new IOException($"Source file '{file.DisplayPath}' was not found.");
        }

        return new WorkflowSourceFileSnapshot(fileInfo.Length, fileInfo.LastWriteTimeUtc);
    }

    private static void EnsureSnapshotUnchanged(
        WorkflowSourceIngestionFile file,
        WorkflowSourceFileSnapshot expected)
    {
        var current = CaptureSnapshot(file);
        if (current != expected)
        {
            throw CreateChangedException(file);
        }
    }

    private static InvalidOperationException CreateChangedException(WorkflowSourceIngestionFile file)
        => new($"Source file '{file.DisplayPath}' changed while it was being ingested.");

    private readonly record struct WorkflowSourceFileSnapshot(
        long Length,
        DateTime LastWriteTimeUtc);
}

internal readonly record struct WorkflowSourceFileContentIdentity(
    long Length,
    DateTime LastWriteTimeUtc,
    string Sha256)
{
    public WorkflowSourceFileContentKey Key => new(Length, Sha256);
}

internal readonly record struct WorkflowSourceFileContentKey(
    long Length,
    string Sha256);
