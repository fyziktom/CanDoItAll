using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class LocalFileStorageTests
{
    [Fact]
    public async Task SaveTextAsync_writes_and_reads_inside_the_active_workspace_root()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-store");

        try
        {
            var sut = CreateSut(workspaceRoot);
            var relativePath = Path.Combine("managed-files", "proof", "alpha.txt");

            var fullPath = await sut.SaveTextAsync(relativePath, "alpha");
            var restoredContent = await sut.ReadTextAsync(relativePath);

            Assert.Equal(Path.Combine(workspaceRoot, "managed-files", "proof", "alpha.txt"), fullPath);
            Assert.Equal("alpha", restoredContent);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task SaveTextAsync_rejects_paths_outside_the_active_workspace_root()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-store");

        try
        {
            var sut = CreateSut(workspaceRoot);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.SaveTextAsync(Path.Combine("..", "outside.txt"), "alpha"));

            Assert.Contains("outside the active workspace root", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    private static LocalFileStore CreateSut(string workspaceRoot)
        => new(new WorkspacePathAccessGuard(new TestWorkspacePathResolver(workspaceRoot)));

    private sealed class TestWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, ".artifacts");
    }
}
