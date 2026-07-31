using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureFileActionCoordinatorTests
{
    [Fact]
    public async Task OpenAsync_uses_the_neutral_project_scope_provider_and_host_include_policy()
    {
        Guid projectId = Guid.NewGuid();
        var projectScope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.Project,
            new FileToolsSemanticScopeId(projectId.ToString("N")),
            "Delivery");
        var projectScopes = new RecordingProjectFileScopeProvider(new ProjectFileScopeSet(
            projectId,
            [projectScope],
            new string('a', 64)));
        var browseSessions = new RecordingBrowseSessionFactory();
        ProjectStructureFileActionCoordinator sut = CreateSut(projectScopes, browseSessions);
        ProjectStructureFileCollectionRequest request = sut.CreateRequest(projectId, "Delivery", node: null);

        await using ProjectStructureFileBrowserWorkspace workspace = await sut.OpenAsync(
            request,
            includeSubprojects: false);

        Assert.False(projectScopes.LastIncludeSubprojects);
        Assert.Equal(1, projectScopes.CallCount);
        Assert.Equal(1, browseSessions.CallCount);
        Assert.Equal(1, workspace.SourceCount);
        Assert.False(workspace.IncludeSubprojects);
        Assert.IsType<ProjectStructureProjectFileCollectionRequest>(workspace.Request);
    }

    [Fact]
    public void CreateRequest_keeps_project_node_and_browse_intents_typed()
    {
        Guid currentProjectId = Guid.NewGuid();
        Guid relatedProjectId = Guid.NewGuid();
        ProjectStructureFileActionCoordinator sut = CreateSut(
            new RecordingProjectFileScopeProvider(),
            new RecordingBrowseSessionFactory());
        var projectNode = ProjectStructureTestNodes.Create(
            "project-child",
            ProjectStructureProjectRole.Subproject,
            relatedProjectId);
        var storageNode = ProjectStructureTestNodes.CreateStorageNode("storage-node");

        ProjectStructureFileCollectionRequest projectRequest = sut.CreateRequest(
            currentProjectId,
            "Current project",
            projectNode);
        ProjectStructureFileCollectionRequest nodeRequest = sut.CreateRequest(
            currentProjectId,
            "Current project",
            storageNode);

        Assert.Equal(relatedProjectId, Assert.IsType<ProjectStructureProjectFileCollectionRequest>(projectRequest).ProjectId);
        ProjectStructureNodeFileCollectionRequest typedNodeRequest =
            Assert.IsType<ProjectStructureNodeFileCollectionRequest>(nodeRequest);
        Assert.Equal(currentProjectId, typedNodeRequest.ProjectId);
        Assert.Equal(storageNode.Id, typedNodeRequest.NodeId);
    }

    private static ProjectStructureFileActionCoordinator CreateSut(
        IProjectFileScopeProvider projectScopes,
        IFileToolsBrowseSessionFactory browseSessions)
        => new(
            projectScopes,
            new ProjectStructureFileScopeResolver(
                new ThrowingDbContextFactory(),
                new ProjectStructureAssemblyService([], new SystemClock()),
                new ThrowingStorageCatalog()),
            browseSessions,
            new ThrowingBrowseItemActivator(),
            new ThrowingBrowseItemActionService(),
            new ThrowingKnownFileSessionFactory(),
            new NoopKnownFileSessionReleaser(),
            NullLogger<ProjectStructureFileActionCoordinator>.Instance);

    private sealed class ThrowingBrowseItemActionService : IFileToolsBrowseItemActionService
    {
        public bool IsLocalLaunchAvailable => false;

        public ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(
            FileToolsSemanticScope scope,
            FileBrowserItemKey itemKey,
            FileToolsLocalFileAction action,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<FileToolsBrowseItemActionResult>(new InvalidOperationException("Unexpected file launch."));

        public ValueTask<IFileToolsDownloadLease> AuthorizeDownloadAsync(
            FileToolsSemanticScope scope,
            FileBrowserItemKey itemKey,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<IFileToolsDownloadLease>(new InvalidOperationException("Unexpected file download."));
    }

    private sealed class RecordingProjectFileScopeProvider(ProjectFileScopeSet? result = null)
        : IProjectFileScopeProvider
    {
        public int CallCount { get; private set; }

        public bool LastIncludeSubprojects { get; private set; }

        public ValueTask<ProjectFileScopeSet> ResolveAsync(
            Guid projectId,
            bool includeSubprojects,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastIncludeSubprojects = includeSubprojects;
            return result is null
                ? ValueTask.FromException<ProjectFileScopeSet>(new InvalidOperationException("Unexpected scope resolution."))
                : ValueTask.FromResult(result);
        }
    }

    private sealed class RecordingBrowseSessionFactory : IFileToolsBrowseSessionFactory
    {
        public int CallCount { get; private set; }

        public ValueTask<FileToolsBrowseSession> CreateAsync(
            FileToolsSemanticScope scope,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            IFileBrowserProvider provider = new NoopFileBrowserProvider(scope.Id.Value);
            return ValueTask.FromResult(new FileToolsBrowseSession(
                scope,
                [provider],
                new FileBrowserSortDescriptor(
                    FileBrowserSortField.ProviderNative,
                    FileBrowserSortDirection.Ascending,
                    FoldersFirst: false),
                new FileToolsBrowseSessionRevision($"revision-{CallCount}")));
        }
    }

    private sealed class NoopFileBrowserProvider(string sourceId) : IFileBrowserProvider
    {
        public FileBrowserSourceDescriptor Descriptor { get; } = new(
            new FileBrowserSourceId($"test-{sourceId}"),
            "Test files");

        public ValueTask<FileBrowserItem> GetRootAsync(
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<FileBrowserItem>(new InvalidOperationException("Unexpected browser initialization."));

        public ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(
            FileBrowserItemKey itemKey,
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<IReadOnlyList<FileBrowserItem>>(new InvalidOperationException("Unexpected path resolution."));

        public ValueTask<FileBrowserPage> BrowseAsync(
            FileBrowserBrowseRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<FileBrowserPage>(new InvalidOperationException("Unexpected browse call."));
    }

    private sealed class ThrowingBrowseItemActivator : IFileToolsBrowseItemActivator
    {
        public ValueTask<FileToolsKnownFileActivation> ActivateAsync(
            FileToolsSemanticScope scope,
            FileBrowserItemKey itemKey,
            FileToolsKnownFileIntent intent,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<FileToolsKnownFileActivation>(new InvalidOperationException("Unexpected activation."));
    }

    private sealed class ThrowingKnownFileSessionFactory : IFileToolsKnownFileSessionFactory
    {
        public ValueTask<FileToolsKnownFileSession> CreateAsync(
            FileToolsKnownFileRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<FileToolsKnownFileSession>(new InvalidOperationException("Unexpected session creation."));
    }

    private sealed class NoopKnownFileSessionReleaser : IFileToolsKnownFileSessionReleaser
    {
        public ValueTask ReleaseAsync(
            FileReference file,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class ThrowingStorageCatalog : IStorageCatalogService
    {
        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
            => UnexpectedAsync<IReadOnlyList<StorageCatalogRecord>>();

        public Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => UnexpectedAsync<StorageCatalogRecord?>();

        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(
            CancellationToken cancellationToken = default)
            => UnexpectedAsync<StorageCatalogRecord>();

        public Task<StorageCatalogRecord> SaveAsync(
            StorageCatalogRecord record,
            CancellationToken cancellationToken = default)
            => UnexpectedAsync<StorageCatalogRecord>();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("Unexpected storage scope resolution."));

        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(
            CancellationToken cancellationToken = default)
            => UnexpectedAsync<IReadOnlyList<StorageRoutingRule>>();

        public Task<StorageRoutingRule> SaveRuleAsync(
            StorageRoutingRule rule,
            CancellationToken cancellationToken = default)
            => UnexpectedAsync<StorageRoutingRule>();

        private static Task<T> UnexpectedAsync<T>()
            => Task.FromException<T>(new InvalidOperationException("Unexpected storage scope resolution."));
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => throw new InvalidOperationException("Unexpected node scope resolution.");

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromException<AppDbContext>(new InvalidOperationException("Unexpected node scope resolution."));
    }

    private static class ProjectStructureTestNodes
    {
        public static ProjectStructureNode Create(
            string id,
            ProjectStructureProjectRole role,
            Guid relatedProjectId)
            => CreateCore(
                id,
                ProjectObjectType.ProjectRoot,
                role,
                relatedProjectId,
                ProjectNodeReferenceCollection.Empty);

        public static ProjectStructureNode CreateStorageNode(string id)
            => CreateCore(
                id,
                ProjectObjectType.Infrastructure,
                ProjectStructureProjectRole.None,
                relatedProjectId: null,
                new ProjectNodeReferenceCollection
                {
                    InfrastructureStorageCatalogId = Guid.NewGuid()
                });

        private static ProjectStructureNode CreateCore(
            string id,
            ProjectObjectType objectType,
            ProjectStructureProjectRole role,
            Guid? relatedProjectId,
            ProjectNodeReferenceCollection nodeReferences)
            => new(
                Id: id,
                ParentId: null,
                ObjectType: objectType,
                ObjectSubtype: string.Empty,
                Title: id,
                Subtitle: string.Empty,
                Status: "Ready",
                Notes: string.Empty,
                Route: string.Empty,
                ArtifactKind: string.Empty,
                ArtifactId: null,
                MediaRelativePath: string.Empty,
                MediaContentType: string.Empty,
                MediaOriginalFileName: string.Empty,
                X: 0,
                Y: 0,
                VisualProfile: new ProjectObjectVisualProfile("rectangle", "#000000", "node", string.Empty),
                Badges: [],
                ProgressMode: string.Empty,
                ProgressPercent: 0,
                MarkerIcon: string.Empty,
                MarkerTone: string.Empty,
                MarkerLabel: string.Empty,
                Markers: [],
                Priority: 0,
                ProjectRole: role,
                RelatedProjectId: relatedProjectId,
                NodeReferences: nodeReferences);
    }
}
