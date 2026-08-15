using CanDoItAll.FileTools.Integration;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureLocalFileActionCoordinatorTests
{
    [Fact]
    public async Task LaunchAsync_routes_a_current_managed_attachment_through_governed_authorization()
    {
        Guid projectId = Guid.NewGuid();
        ProjectStructureNode currentNode = CreateNode(
            storageObjectReferenceJson: "{\"current\":true}");
        var currentNodeResolver = new StaticCurrentNodeResolver(currentNode);
        var scopeProvider = new StaticScopeProvider();
        var knownFileActions = new RecordingKnownFileActionService();
        var localFileOpener = new RecordingLocalFileOpener();
        var sut = new ProjectStructureLocalFileActionCoordinator(
            currentNodeResolver,
            scopeProvider,
            knownFileActions,
            localFileOpener);

        ProjectStructureLocalFileOpenResult result = await sut.LaunchAsync(
            projectId,
            currentNode.Id,
            FileToolsLocalFileAction.OpenInPreferredApplication);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, currentNodeResolver.CallCount);
        Assert.Equal(1, scopeProvider.CallCount);
        Assert.Equal(1, knownFileActions.CallCount);
        Assert.Equal(FileToolsLocalFileAction.OpenInPreferredApplication, knownFileActions.LastAction);
        Assert.Equal(0, localFileOpener.CallCount);
    }

    [Fact]
    public async Task LaunchAsync_fails_closed_when_the_current_node_was_deleted()
    {
        var knownFileActions = new RecordingKnownFileActionService();
        var localFileOpener = new RecordingLocalFileOpener();
        var sut = new ProjectStructureLocalFileActionCoordinator(
            new StaticCurrentNodeResolver(null),
            new StaticScopeProvider(),
            knownFileActions,
            localFileOpener);

        ProjectStructureLocalFileOpenResult result = await sut.LaunchAsync(
            Guid.NewGuid(),
            "deleted-node",
            FileToolsLocalFileAction.OpenContainingFolder);

        Assert.False(result.IsSuccess);
        Assert.Contains("no longer exists", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, knownFileActions.CallCount);
        Assert.Equal(0, localFileOpener.CallCount);
    }

    private static ProjectStructureNode CreateNode(string storageObjectReferenceJson = "")
        => new(
            Id: "node-1",
            ParentId: "project:1",
            ObjectType: ProjectObjectType.File,
            ObjectSubtype: "spreadsheet",
            Title: "Forecast",
            Subtitle: string.Empty,
            Status: "Ready",
            Notes: string.Empty,
            Route: string.Empty,
            ArtifactKind: string.Empty,
            ArtifactId: null,
            MediaRelativePath: string.Empty,
            MediaContentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            MediaOriginalFileName: "forecast.xlsx",
            X: 0,
            Y: 0,
            VisualProfile: new ProjectObjectVisualProfile("rectangle", "accent", "file", string.Empty),
            Badges: [],
            ProgressMode: string.Empty,
            ProgressPercent: 0,
            MarkerIcon: string.Empty,
            MarkerTone: string.Empty,
            MarkerLabel: string.Empty,
            Markers: [],
            Priority: 0,
            StorageObjectReferenceJson: storageObjectReferenceJson);

    private sealed class StaticCurrentNodeResolver(ProjectStructureNode? node)
        : IProjectStructureCurrentNodeResolver
    {
        public int CallCount { get; private set; }

        public ValueTask<ProjectStructureNode?> ResolveAsync(
            Guid projectId,
            string nodeId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(node);
        }
    }

    private sealed class StaticScopeProvider : IProjectStructureNodeFileScopeProvider
    {
        private static readonly FileToolsSemanticScope Scope = new(
            FileToolsSemanticScopeKind.ProjectNode,
            new FileToolsSemanticScopeId("known:1"),
            "Known file");

        public int CallCount { get; private set; }

        public ValueTask<FileToolsKnownFileScope> ResolveKnownFileAsync(
            Guid projectId,
            string nodeId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(new FileToolsKnownFileScope(
                Scope,
                new FileToolsKnownFileOccurrence(
                    Guid.NewGuid(),
                    FileToolsKnownFileOccurrenceKind.RelativePath,
                    "managed-files/forecast.xlsx",
                    "forecast.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    0)));
        }

        public ValueTask<FileToolsSemanticScope> ResolveNodeCollectionAsync(
            Guid projectId,
            string nodeId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingKnownFileActionService : IFileToolsKnownFileActionService
    {
        public bool IsLocalLaunchAvailable => true;

        public int CallCount { get; private set; }

        public FileToolsLocalFileAction? LastAction { get; private set; }

        public ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(
            FileToolsSemanticScope scope,
            FileToolsKnownFileOccurrence occurrence,
            FileToolsLocalFileAction action,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastAction = action;
            return ValueTask.FromResult(FileToolsBrowseItemActionResult.Success("Opened."));
        }
    }

    private sealed class RecordingLocalFileOpener : IProjectStructureLocalFileOpener
    {
        public bool IsAvailable => true;

        public int CallCount { get; private set; }

        public bool CanOpen(ProjectStructureNode? node) => true;

        public bool CanOpenInPreferredApplication(ProjectStructureNode? node) => true;

        public Task<ProjectStructureLocalFileOpenResult> OpenAsync(
            ProjectStructureNode node,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ProjectStructureLocalFileOpenResult(true, "Opened."));
        }

        public Task<ProjectStructureLocalFileOpenResult> OpenInPreferredApplicationAsync(
            ProjectStructureNode node,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ProjectStructureLocalFileOpenResult(true, "Opened."));
        }
    }
}
