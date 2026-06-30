using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspacePathResolverGuardTests
{
    [Fact]
    public void ResolveManagedFilePath_returns_a_path_under_the_active_managed_root()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("workspace-path-guard");

        try
        {
            var sut = CreateSut(workspaceRoot);

            var result = sut.ResolveManagedFilePath(Path.Combine("proof", "alpha.txt"));

            Assert.True(result.IsSuccess);
            Assert.Equal(
                Path.Combine(workspaceRoot, "managed-files", "proof", "alpha.txt"),
                result.FullPath);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void ResolveManagedFilePath_rejects_traversal_outside_the_active_managed_root()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("workspace-path-guard");

        try
        {
            var sut = CreateSut(workspaceRoot);

            var result = sut.ResolveManagedFilePath(Path.Combine("..", "exports", "escape.txt"));

            Assert.False(result.IsSuccess);
            Assert.Contains("outside the active managed files root", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    private static IWorkspacePathAccessGuard CreateSut(string workspaceRoot)
        => new WorkspacePathAccessGuard(new TestWorkspacePathResolver(workspaceRoot));

    private sealed class TestWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, ".artifacts");
    }
}
