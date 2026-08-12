using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Infrastructure.FileSystem;
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

    [Fact]
    public void ResolveWorkspacePath_uses_detected_root_case_semantics()
    {
        var parentRoot = TestFileSystem.CreateTemporaryRoot("workspace-path-case");

        try
        {
            string workspaceRoot = Path.Combine(parentRoot, "workspace");
            string caseDifferentSibling = Path.Combine(parentRoot, "Workspace", "escape.txt");
            Directory.CreateDirectory(workspaceRoot);
            var sut = CreateSut(workspaceRoot);
            PhysicalFileSystemCaseSensitivity caseSensitivity = TestWorkspaceServices
                .PhysicalPathPolicyFactory
                .Create(workspaceRoot)
                .CaseSensitivity;

            WorkspacePathAccessResult result = sut.ResolveWorkspacePath(caseDifferentSibling);

            Assert.Equal(
                caseSensitivity == PhysicalFileSystemCaseSensitivity.Insensitive,
                result.IsSuccess);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(parentRoot);
        }
    }

    [Fact]
    public void Unix_absolute_paths_preserve_significant_trailing_spaces()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("workspace-path-space");

        try
        {
            string workspaceDirectory = Directory.CreateDirectory(
                Path.Combine(workspaceRoot, "workspace folder ")).FullName;
            string managedDirectory = Directory.CreateDirectory(
                Path.Combine(workspaceRoot, "managed-files", "managed folder ")).FullName;
            var sut = CreateSut(workspaceRoot);

            WorkspacePathAccessResult workspaceResult = sut.ResolveWorkspacePath(workspaceDirectory);
            WorkspacePathAccessResult managedResult = sut.ResolveManagedFilePath(managedDirectory);

            Assert.True(workspaceResult.IsSuccess);
            Assert.Equal(workspaceDirectory, workspaceResult.FullPath);
            Assert.True(managedResult.IsSuccess);
            Assert.Equal(managedDirectory, managedResult.FullPath);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    private static IWorkspacePathAccessGuard CreateSut(string workspaceRoot)
        => new WorkspacePathAccessGuard(
            new TestWorkspacePathResolver(workspaceRoot),
            TestWorkspaceServices.PhysicalPathPolicyFactory);

    private sealed class TestWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, ".artifacts");
    }
}
