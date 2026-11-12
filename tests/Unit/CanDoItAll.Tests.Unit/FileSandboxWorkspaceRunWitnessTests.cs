using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class FileSandboxWorkspaceRunWitnessTests {
    [Fact]
    public async Task Streaming_digest_covers_all_read_shapes_and_requires_complete_unchanged_length() {
        byte[] bytes = [1, 2, 3, 4, 5, 6, 7];
        using var input = new MemoryStream(bytes);
        using (var stream = new FileSandboxWorkspaceRunHashingStream(input)) {
            Assert.Equal(1, stream.ReadByte());
            var buffer = new byte[2];
            Assert.Equal(1, stream.Read(buffer, 0, 1));
            Assert.Equal(1, stream.Read(buffer.AsSpan(0, 1)));
            Assert.Equal(2, await stream.ReadAsync(buffer, 0, 2, CancellationToken.None));
            Assert.Equal(2, await stream.ReadAsync(buffer.AsMemory(), CancellationToken.None));
            Assert.Throws<IOException>(() => stream.Complete(bytes.Length));
            Assert.Equal(0, await stream.ReadAsync(buffer.AsMemory(), CancellationToken.None));
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), stream.Complete(bytes.Length));
            Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
            Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        }
        Assert.True(input.CanRead);
        using var changedInput = new MemoryStream(bytes);
        using var changed = new FileSandboxWorkspaceRunHashingStream(changedInput);
        await changed.CopyToAsync(Stream.Null);
        Assert.Throws<IOException>(() => changed.Complete(bytes.Length + 1));
    }

    [Fact]
    public async Task Real_large_decode_binds_BOM_and_whitespace_rejects_trailing_data_and_preserves_cancel_and_IO_failure() {
        var root = TestFileSystem.CreateTemporaryRoot("run-witness-json");
        try {
            var path = Path.Combine(root, "run.json");
            var store = new FileSandboxWorkspaceJsonStore();
            var run = Run([]) with { InputSummary = new string('x', 9 * 1024 * 1024) };
            var json = JsonSerializer.SerializeToUtf8Bytes(run, store.SerializerOptions);
            byte[] bytes = [.. Encoding.UTF8.Preamble, (byte)' ', .. json, (byte)'\r', (byte)'\n'];
            await File.WriteAllBytesAsync(path, bytes);

            var captured = Assert.IsType<FileSandboxWorkspaceRunReadSnapshot>(await store.ReadRunSnapshotAsync(path, default));
            Assert.Equal(run.InputSummary, captured.Run.InputSummary);
            Assert.True(captured.MatchesDigest(SHA256.HashData(bytes)));
            Assert.True(await store.MatchesRunSnapshotAsync(path, captured, default));

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadRunSnapshotAsync(path, cancelled.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.MatchesRunSnapshotAsync(path, captured, cancelled.Token));
            using (var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                await Assert.ThrowsAsync<InvalidDataException>(() => store.MatchesRunSnapshotAsync(path, captured, default));
            }
            Assert.True(await store.MatchesRunSnapshotAsync(path, captured, default));

            await File.AppendAllTextAsync(path, " {}");
            Assert.False(await store.MatchesRunSnapshotAsync(path, captured, default));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadRunSnapshotAsync(path, default));
            await File.WriteAllBytesAsync(path, bytes);
            Assert.NotNull(await store.ReadRunSnapshotAsync(path, default));
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public void Original_run_binding_freezes_caller_collection_and_rejects_equal_copies_or_another_path() {
        List<PendingToolApprovalRecord> approvals = [new("approval", "call", "tool", "native", "Approve", "{}")];
        var run = Run(approvals);
        var snapshot = new FileSandboxWorkspaceRunReadSnapshot("owned/run.json", run, Convert.ToHexString(SHA256.HashData("original"u8)));
        approvals.Clear();

        Assert.Equal("approval", Assert.Single(snapshot.Run.PendingApprovals).ApprovalId);
        Assert.Throws<NotSupportedException>(() => ((IList<PendingToolApprovalRecord>)snapshot.Run.PendingApprovals).Clear());
        Assert.True(snapshot.MatchesPrevious("owned/run.json", snapshot.Run));
        Assert.False(snapshot.MatchesPrevious("other/run.json", snapshot.Run));
        Assert.False(snapshot.MatchesPrevious("owned/run.json", snapshot.Run with { }));
        Assert.False(snapshot.MatchesPrevious("owned/run.json", run));
    }

    private static ExecutionRunRecord Run(IReadOnlyList<PendingToolApprovalRecord> approvals) {
        var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), Guid.NewGuid(), null, "Original", "test", "source", "correlation", "causation",
            "test", "test", "{}", "input", string.Empty, "Synthetic", "Synthetic", ExecutionState.Preparing, null,
            now, now, now, null, string.Empty, null, approvals);
    }
}
