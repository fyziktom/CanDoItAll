using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed partial class FileSystemStorageBrowseDriverTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revisioned_filesystem_replace_checks_authority_after_real_temporary_bytes_are_flushed(bool denyAfterFlush) {
        var root = Path.Combine(Path.GetTempPath(), $"candoitall-revision-authority-{Guid.NewGuid():N}");
        output.WriteLine($"Owned before creation: {root}; cleanup: DeleteRoot after this physical filesystem case.");
        Directory.CreateDirectory(root);
        try {
            var fullPath = Path.Combine(root, "original.txt");
            await File.WriteAllTextAsync(fullPath, "Original stored bytes");
            var flushed = false;
            var allowed = true;
            var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory(), stage => {
                if (stage == DurableFileWriteStage.TemporaryFileFlushed) {
                    flushed = true;
                    Assert.Equal("Original stored bytes", File.ReadAllText(fullPath));
                    allowed = !denyAfterFlush;
                }
            });
            var driver = new FileSystemStorageDriver(new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(root)), writer);
            var storage = CreateStorage(root).ToDriverInput();
            var reference = new StorageObjectReference(storage.Id, StorageProviderKind.FileSystem, StorageLocatorKind.RelativePath,
                "original.txt", "original.txt", "text/plain", new FileInfo(fullPath).Length);
            var before = await driver.GetRevisionAsync(storage, reference);
            var request = new StorageRevisionedWriteRequest(reference, "Accepted replacement bytes"u8.ToArray(), before,
                allowOverwrite: false, authorizeCommit: token => {
                    token.ThrowIfCancellationRequested();
                    Assert.True(flushed);
                    if (!allowed) {
                        throw new UnauthorizedAccessException("Original native write authority retired after temporary-file flush.");
                    }
                    return ValueTask.CompletedTask;
                });
            if (denyAfterFlush) {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => driver.ReplaceAsync(storage, request));
                Assert.Equal("Original stored bytes", await File.ReadAllTextAsync(fullPath));
                Assert.Equal(before, await driver.GetRevisionAsync(storage, reference));
            } else {
                var accepted = await driver.ReplaceAsync(storage, request);
                Assert.Equal("Accepted replacement bytes", await File.ReadAllTextAsync(fullPath));
                Assert.NotEqual(before, accepted.PersistedRevision);
            }
            Assert.True(flushed);
            Assert.Empty(Directory.EnumerateFiles(root, ".*.tmp"));
        } finally {
            DeleteRoot(root);
        }
        Assert.False(Directory.Exists(root));
    }
}
