using System.Collections.Immutable;
using System.Security.Cryptography;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceRunReadSnapshot {
    private readonly string fullPath;
    private readonly string digest;

    internal FileSandboxWorkspaceRunReadSnapshot(string fullPath, ExecutionRunRecord run, string digest) {
        this.fullPath = fullPath;
        this.digest = digest;
        Run = run with { PendingApprovals = run.PendingApprovals.ToImmutableArray() };
    }

    internal ExecutionRunRecord Run { get; }

    internal bool MatchesPrevious(string path, ExecutionRunRecord previous) =>
        string.Equals(fullPath, path, StringComparison.Ordinal) && ReferenceEquals(Run, previous);

    internal bool MatchesDigest(ReadOnlySpan<byte> observedDigest) =>
        string.Equals(digest, Convert.ToHexString(observedDigest), StringComparison.Ordinal);
}

internal sealed class FileSandboxWorkspaceRunHashingStream(Stream input) : Stream {
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private long bytesRead;
    private bool reachedEnd;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer) {
        var count = input.Read(buffer);
        Observe(buffer[..count], buffer.Length);
        return count;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        var count = await input.ReadAsync(buffer, cancellationToken);
        Observe(buffer.Span[..count], buffer.Length);
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    internal string Complete(long expectedLength) {
        if (!reachedEnd || bytesRead != expectedLength || input.Length != expectedLength) {
            throw new IOException("The execution run changed size while its original snapshot was read.");
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private void Observe(ReadOnlySpan<byte> bytes, int requested) {
        if (requested > 0 && bytes.IsEmpty) {
            reachedEnd = true;
        }
        hash.AppendData(bytes);
        bytesRead = checked(bytesRead + bytes.Length);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            hash.Dispose();
        }
        base.Dispose(disposing);
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
