using CanDoItAll.FileTools.Desktop;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed partial class ProjectStructureLocalFileOpenerManagedFilesTests {
    private readonly ITestOutputHelper authorityOutput;
    public ProjectStructureLocalFileOpenerManagedFilesTests(ITestOutputHelper output) => authorityOutput = output;
    public enum OriginalReadBoundary { Allowed, BeforeRead, AfterNativeRootRead }

    [Theory]
    [InlineData(OriginalReadBoundary.Allowed)]
    [InlineData(OriginalReadBoundary.BeforeRead)]
    [InlineData(OriginalReadBoundary.AfterNativeRootRead)]
    public async Task Native_Process_folder_open_rechecks_original_read_after_native_root_resolution_before_OS_dispatch(OriginalReadBoundary boundary) {
        var ownedRoot = Path.Combine(Path.GetTempPath(), nameof(ProjectStructureLocalFileOpenerManagedFilesTests), Guid.NewGuid().ToString("N"));
        authorityOutput.WriteLine("Owned native Process folder root before creation: " + ownedRoot);
        Directory.CreateDirectory(ownedRoot);
        try {
            var project = Guid.NewGuid();
            var run = Guid.NewGuid();
            var logical = $"artifacts/process-runs/{run:D}";
            var scoped = "owned-process-output";
            var directory = Path.Combine(ownedRoot, scoped);
            Directory.CreateDirectory(directory);
            var allowed = boundary != OriginalReadBoundary.BeforeRead;
            var readNative = false;
            var roots = new ProcessFileRoots(project, run, scoped) { AfterResolve = () => {
                readNative = true;
                allowed = boundary != OriginalReadBoundary.AfterNativeRootRead;
            } };
            var launcher = new RecordingDesktopFileLauncher();
            var opener = CreateSut(ownedRoot, launcher, roots);
            var node = CreateNode("", objectType: ProjectObjectType.File, objectSubtype: "folder", metadata: new() {
                File = new() { FileSubtype = ProjectFileSubtype.Folder, ExternalPath = logical }
            }) with {
                Id = ProjectStructureProcessNodeKeys.BuildProcessRunOutputNodeKey(run, logical),
                ParentId = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(run),
                ArtifactKind = ProjectStructureProcessNodeKeys.ProcessRunOutputFolderArtifactKind,
                ArtifactId = run, IsSystemManaged = true, ProjectId = project
            };
            var pending = opener.OpenAsync(node, authorizeRead: token => {
                token.ThrowIfCancellationRequested();
                if (!allowed) {
                    throw new UnauthorizedAccessException("Original Process folder read retired.");
                }
                return ValueTask.CompletedTask;
            });
            if (boundary == OriginalReadBoundary.Allowed) {
                Assert.True((await pending).IsSuccess);
                Assert.Equal(Path.GetFullPath(directory), launcher.LastRequest!.TargetPath);
                Assert.Equal(DesktopFileLaunchOperation.OpenContainingFolder, launcher.LastRequest.Operation);
            } else {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pending);
                Assert.Null(launcher.LastRequest);
            }
            Assert.Equal(boundary != OriginalReadBoundary.BeforeRead, readNative);
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(ownedRoot);
            Assert.False(Directory.Exists(ownedRoot));
        }
    }
}
