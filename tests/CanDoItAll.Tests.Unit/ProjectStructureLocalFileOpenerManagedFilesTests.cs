using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureLocalFileOpenerManagedFilesTests
{
    [Fact]
    public void CanOpen_returns_true_for_an_existing_file_inside_the_active_managed_root()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-opener");

        try
        {
            var managedFilePath = Path.Combine(workspaceRoot, "managed-files", "proof", "alpha.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(managedFilePath)!);
            File.WriteAllText(managedFilePath, "alpha");

            var sut = CreateSut(workspaceRoot);
            var node = CreateNode(mediaRelativePath: Path.Combine("proof", "alpha.txt"));

            Assert.True(sut.CanOpen(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void CanOpen_returns_false_when_the_media_path_escapes_the_active_managed_root()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-opener");

        try
        {
            var sut = CreateSut(workspaceRoot);
            var node = CreateNode(mediaRelativePath: Path.Combine("..", "exports", "escape.txt"));

            Assert.False(sut.CanOpen(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void CanOpen_uses_the_storage_reference_when_available()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-opener");

        try
        {
            var managedFilePath = Path.Combine(workspaceRoot, "managed-files", "proof", "alpha.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(managedFilePath)!);
            File.WriteAllText(managedFilePath, "alpha");

            var sut = CreateSut(workspaceRoot);
            var node = CreateNode(
                mediaRelativePath: string.Empty,
                storageObjectReferenceJson: StorageJson.SerializeReference(
                    StorageJson.CreateLegacyManagedFileReference("proof/alpha.txt", "text/plain", "alpha.txt")));

            Assert.True(sut.CanOpen(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void CanOpen_returns_true_for_an_existing_directory_inside_the_active_managed_root()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-opener");

        try
        {
            var managedDirectoryPath = Path.Combine(workspaceRoot, "managed-files", "proof", "folder-output");
            Directory.CreateDirectory(managedDirectoryPath);

            var sut = CreateSut(workspaceRoot);
            var node = CreateNode(
                mediaRelativePath: string.Empty,
                storageObjectReferenceJson: StorageJson.SerializeReference(
                    new StorageObjectReference(
                        null,
                        StorageProviderKind.FileSystem,
                        StorageLocatorKind.RelativePath,
                        "managed-files/proof/folder-output",
                        string.Empty,
                        "application/x-directory",
                        null,
                        "/managed-files/proof/folder-output")));

            Assert.True(sut.CanOpen(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    private static ProjectStructureLocalFileOpener CreateSut(string workspaceRoot)
        => new(
            new WorkspacePathAccessGuard(new TestWorkspacePathResolver(workspaceRoot)),
            NullLogger<ProjectStructureLocalFileOpener>.Instance);

    private static ProjectStructureNode CreateNode(string mediaRelativePath, string storageObjectReferenceJson = "")
        => new(
            "node-1",
            "project:1",
            ProjectObjectType.Note,
            "attachment",
            "Attachment",
            "Context",
            "Planned",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            mediaRelativePath,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("rect", "accent", "file", string.Empty),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0,
            null,
            null,
            string.Empty,
            storageObjectReferenceJson);

    private sealed class TestWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, ".artifacts");
    }
}
