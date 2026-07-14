using CanDoItAll.FileTools.Desktop;
using CanDoItAll.Infrastructure.ControlPlane;
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
    public async Task Preferred_application_open_uses_the_exact_trusted_document_path()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-opener");

        try
        {
            var filePath = Path.Combine(workspaceRoot, "managed-files", "reports", "forecast.xlsx");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "forecast");
            var launcher = new RecordingDesktopFileLauncher();
            var sut = CreateSut(workspaceRoot, launcher);
            var node = CreateNode(mediaRelativePath: Path.Combine("reports", "forecast.xlsx"));

            Assert.True(sut.CanOpenInPreferredApplication(node));
            ProjectStructureLocalFileOpenResult result = await sut.OpenInPreferredApplicationAsync(node);

            Assert.True(result.IsSuccess);
            Assert.NotNull(launcher.LastRequest);
            Assert.Equal(Path.GetFullPath(filePath), launcher.LastRequest.TargetPath);
            Assert.Equal(DesktopFileLaunchOperation.Open, launcher.LastRequest.Operation);
            Assert.Null(launcher.LastRequest.ExecutablePath);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task Reveal_uses_the_driver_containing_folder_operation()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-opener");

        try
        {
            var filePath = Path.Combine(workspaceRoot, "managed-files", "reports", "forecast.xlsx");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "forecast");
            var launcher = new RecordingDesktopFileLauncher();
            var sut = CreateSut(workspaceRoot, launcher);
            var node = CreateNode(mediaRelativePath: Path.Combine("reports", "forecast.xlsx"));

            ProjectStructureLocalFileOpenResult result = await sut.OpenAsync(node);

            Assert.True(result.IsSuccess);
            Assert.Equal(DesktopFileLaunchOperation.OpenContainingFolder, launcher.LastRequest?.Operation);
            Assert.Equal(Path.GetFullPath(filePath), launcher.LastRequest?.TargetPath);
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
    public void CanOpen_returns_false_for_a_repository_folder_outside_the_active_workspace()
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

            Assert.False(sut.CanOpen(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(externalFolder);
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void CanOpen_returns_false_for_a_file_node_path_outside_the_active_workspace()
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

            Assert.False(sut.CanOpen(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(externalFolder);
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void Script_inside_the_workspace_can_be_revealed_but_not_shell_opened()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-opener");

        try
        {
            var scriptFile = Path.Combine(workspaceRoot, "scripts", "run.ps1");
            Directory.CreateDirectory(Path.GetDirectoryName(scriptFile)!);
            File.WriteAllText(scriptFile, "Write-Host blocked");

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
                        ExternalPath = scriptFile
                    }
                });

            Assert.True(sut.CanOpen(node));
            Assert.False(sut.CanOpenInPreferredApplication(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void CanOpen_returns_false_for_an_infrastructure_folder_outside_the_active_workspace()
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

            Assert.False(sut.CanOpen(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(externalFolder);
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    private static ProjectStructureLocalFileOpener CreateSut(
        string workspaceRoot,
        IDesktopFileLauncher? desktopFileLauncher = null)
    {
        var pathResolver = new TestWorkspacePathResolver(workspaceRoot);
        return new ProjectStructureLocalFileOpener(
            new WorkspacePathAccessGuard(pathResolver),
            new FileSystemStoragePathPolicy(pathResolver),
            new EmptyFileApplicationPreferenceService(),
            desktopFileLauncher ?? new AvailableDesktopFileLauncher(),
            NullLogger<ProjectStructureLocalFileOpener>.Instance);
    }

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

    private sealed class EmptyFileApplicationPreferenceService : IFileApplicationPreferenceService
    {
        public Task<IReadOnlyList<FileApplicationPreference>> ListAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FileApplicationPreference>>([]);

        public Task SaveAsync(
            FileApplicationPreference preference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            FileApplicationExtension extension,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public FileApplicationPreference? ResolveForFile(string fileName) => null;
    }

    private sealed class AvailableDesktopFileLauncher : IDesktopFileLauncher
    {
        public bool IsAvailable => true;

        public ValueTask<DesktopFileLaunchResult> LaunchAsync(
            DesktopFileLaunchRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(DesktopFileLaunchResult.Success(request.TargetPath));
    }

    private sealed class RecordingDesktopFileLauncher : IDesktopFileLauncher
    {
        public bool IsAvailable => true;

        public DesktopFileLaunchRequest? LastRequest { get; private set; }

        public ValueTask<DesktopFileLaunchResult> LaunchAsync(
            DesktopFileLaunchRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return ValueTask.FromResult(DesktopFileLaunchResult.Success(request.TargetPath));
        }
    }

    [Fact]
    public void CanOpen_returns_false_when_a_workspace_path_traverses_a_symbolic_link()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-opener");
        var externalFolder = TestFileSystem.CreateTemporaryRoot("local-file-opener-link-target");

        try
        {
            string externalFile = Path.Combine(externalFolder, "forecast.xlsx");
            File.WriteAllText(externalFile, "forecast");
            string linkedFolder = Path.Combine(workspaceRoot, "linked");
            Directory.CreateSymbolicLink(linkedFolder, externalFolder);
            var sut = CreateSut(workspaceRoot);
            var node = CreateNode(
                mediaRelativePath: string.Empty,
                objectType: ProjectObjectType.File,
                objectSubtype: "spreadsheet",
                metadata: new ProjectObjectMetadataEnvelope
                {
                    File = new ProjectFileMetadata
                    {
                        FileSubtype = ProjectFileSubtype.Excel,
                        ExternalPath = Path.Combine(linkedFolder, "forecast.xlsx")
                    }
                });

            Assert.False(sut.CanOpen(node));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
            TestFileSystem.DeleteDirectoryWithRetry(externalFolder);
        }
    }
}
