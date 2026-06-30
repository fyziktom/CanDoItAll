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

    [Fact]
    public void CanOpen_returns_true_for_an_existing_directory_inside_the_managed_artifact_root()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-opener");

        try
        {
            var artifactDirectoryPath = Path.Combine(workspaceRoot, "artifacts", "scopes", "organization", "demo", "deliveries", "workflow-suite", "process", "qa-validation");
            Directory.CreateDirectory(artifactDirectoryPath);

            var sut = CreateSut(workspaceRoot);
            var node = CreateNode(
                mediaRelativePath: string.Empty,
                storageObjectReferenceJson: StorageJson.SerializeReference(
                    new StorageObjectReference(
                        null,
                        StorageProviderKind.FileSystem,
                        StorageLocatorKind.RelativePath,
                        "artifacts/scopes/organization/demo/deliveries/workflow-suite/process/qa-validation",
                        string.Empty,
                        "application/x-directory",
                        null,
                        "/managed-files/artifacts/scopes/organization/demo/deliveries/workflow-suite/process/qa-validation")));

            Assert.True(sut.CanOpen(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void CanOpen_returns_true_for_a_repository_folder_path_on_the_local_drive()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-opener");
        var externalFolder = TestFileSystem.CreateTemporaryRoot("local-file-opener-folder");

        try
        {
            var sut = CreateSut(workspaceRoot);
            var node = CreateNode(
                mediaRelativePath: string.Empty,
                objectType: ProjectObjectType.Repository,
                objectSubtype: "folder",
                metadata: new ProjectObjectMetadataEnvelope
                {
                    Repository = new ProjectRepositoryMetadata
                    {
                        RepositoryMode = ProjectRepositoryMode.LocalFolder,
                        LocalPath = externalFolder
                    }
                });

            Assert.True(sut.CanOpen(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(externalFolder);
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void CanOpen_returns_true_for_a_file_node_external_path_on_the_local_drive()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-opener");
        var externalFolder = TestFileSystem.CreateTemporaryRoot("local-file-opener-file");

        try
        {
            var externalFile = Path.Combine(externalFolder, "docs", "readme.md");
            Directory.CreateDirectory(Path.GetDirectoryName(externalFile)!);
            File.WriteAllText(externalFile, "# Readme");

            var sut = CreateSut(workspaceRoot);
            var node = CreateNode(
                mediaRelativePath: string.Empty,
                objectType: ProjectObjectType.File,
                objectSubtype: "markdown",
                metadata: new ProjectObjectMetadataEnvelope
                {
                    File = new ProjectFileMetadata
                    {
                        FileSubtype = ProjectFileSubtype.Markdown,
                        ExternalPath = externalFile
                    }
                });

            Assert.True(sut.CanOpen(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(externalFolder);
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void CanOpen_returns_false_for_a_blocked_external_script_file()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-opener");
        var externalFolder = TestFileSystem.CreateTemporaryRoot("local-file-opener-blocked");

        try
        {
            var externalFile = Path.Combine(externalFolder, "scripts", "run.ps1");
            Directory.CreateDirectory(Path.GetDirectoryName(externalFile)!);
            File.WriteAllText(externalFile, "Write-Host blocked");

            var sut = CreateSut(workspaceRoot);
            var node = CreateNode(
                mediaRelativePath: string.Empty,
                objectType: ProjectObjectType.File,
                objectSubtype: "text",
                metadata: new ProjectObjectMetadataEnvelope
                {
                    File = new ProjectFileMetadata
                    {
                        FileSubtype = ProjectFileSubtype.Text,
                        ExternalPath = externalFile
                    }
                });

            Assert.False(sut.CanOpen(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(externalFolder);
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void CanOpen_returns_true_for_an_infrastructure_deployment_folder_path()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-opener");
        var externalFolder = TestFileSystem.CreateTemporaryRoot("local-file-opener-deploy");

        try
        {
            var sut = CreateSut(workspaceRoot);
            var node = CreateNode(
                mediaRelativePath: string.Empty,
                objectType: ProjectObjectType.Infrastructure,
                objectSubtype: "deployment-folder",
                metadata: new ProjectObjectMetadataEnvelope
                {
                    Infrastructure = new ProjectInfrastructureMetadata
                    {
                        InfrastructureKind = ProjectInfrastructureKind.DeploymentFolder,
                        FolderPath = externalFolder
                    }
                });

            Assert.True(sut.CanOpen(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(externalFolder);
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    private static ProjectStructureLocalFileOpener CreateSut(string workspaceRoot)
        => new(
            new WorkspacePathAccessGuard(new TestWorkspacePathResolver(workspaceRoot)),
            NullLogger<ProjectStructureLocalFileOpener>.Instance);

    private static ProjectStructureNode CreateNode(
        string mediaRelativePath,
        string storageObjectReferenceJson = "",
        ProjectObjectType objectType = ProjectObjectType.Note,
        string objectSubtype = "attachment",
        ProjectObjectMetadataEnvelope? metadata = null)
        => new(
            "node-1",
            "project:1",
            objectType,
            objectSubtype,
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
            metadata is null ? string.Empty : ProjectObjectMetadataSerializer.Serialize(metadata),
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
