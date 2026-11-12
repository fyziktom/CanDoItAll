using System.Text;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "HostPlatform")]
public sealed class ApiAuthorityFileReadLifecycleTests {
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Pending_read_cancellation_is_awaited_until_the_stream_retires_without_late_success() {
        await using var stream = new PendingReadStream();
        using var cancellation = new CancellationTokenSource();
        var reading = ApiAuthorityFileRead.ReadBoundedAsync(stream, 32, cancellation.Token).AsTask();
        try {
            await stream.Entered.Task.WaitAsync(TestTimeout);
            Assert.False(reading.IsCompleted);
            cancellation.Cancel();
            await stream.CancellationObserved.Task.WaitAsync(TestTimeout);
            Assert.False(reading.IsCompleted);
            Assert.Equal(1, stream.ActiveReads);
            stream.Release.TrySetResult();
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading.WaitAsync(TestTimeout));
            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.True(reading.IsCanceled);
            Assert.Equal(0, stream.ActiveReads);
            Assert.Equal(1, stream.ReadCalls);
        } finally {
            cancellation.Cancel();
            stream.Release.TrySetResult();
            try {
                await reading.WaitAsync(TestTimeout);
            } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
            }
        }
    }

    [Fact]
    public async Task Held_production_read_handle_allows_atomic_publication_and_preserves_complete_original_bytes() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-read-replacement");
        var root = environment.ControlPlaneRootPath;
        var path = Path.Combine(root, "authority.json");
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        var original = Encoding.UTF8.GetBytes("{\"value\":\"" + new string('a', 16_384) + "\"}");
        var replacement = Encoding.UTF8.GetBytes("{\"value\":\"" + new string('b', 24_576) + "\"}");
        await writer.WriteBytesAsync(root, path, original, DurableFileWriteOptions.Private);

        await using (var held = ApiAuthorityFileRead.OpenRead(path)) {
            using var publicationBudget = new CancellationTokenSource(TestTimeout);
            await writer.WriteBytesAsync(root, path, replacement, DurableFileWriteOptions.Private, publicationBudget.Token);
            var retained = await ApiAuthorityFileRead.ReadBoundedAsync(held, 32_768, CancellationToken.None);
            Assert.Equal(original, retained.ToArray());
            await using var fresh = ApiAuthorityFileRead.OpenRead(path);
            var current = await ApiAuthorityFileRead.ReadBoundedAsync(fresh, 32_768, CancellationToken.None);
            Assert.Equal(replacement, current.ToArray());
        }

        await using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(replacement.LongLength, exclusive.Length);
        Assert.Empty(Directory.EnumerateFiles(root, ".*.tmp"));
    }

    private sealed class PendingReadStream : MemoryStream {
        private int activeReads;
        private int readCalls;

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ActiveReads => Volatile.Read(ref activeReads);
        public int ReadCalls => Volatile.Read(ref readCalls);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref activeReads);
            Interlocked.Increment(ref readCalls);
            try {
                using var registration = cancellationToken.Register(() => CancellationObserved.TrySetResult());
                Entered.TrySetResult();
                await Release.Task;
                cancellationToken.ThrowIfCancellationRequested();
                return await base.ReadAsync(buffer, cancellationToken);
            } finally {
                Interlocked.Decrement(ref activeReads);
            }
        }
    }
}
