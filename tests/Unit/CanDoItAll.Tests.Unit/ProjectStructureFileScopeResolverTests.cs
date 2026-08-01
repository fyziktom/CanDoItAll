using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureFileScopeResolverTests
{
    [Fact]
    public void Known_file_interaction_coordinator_has_no_FileBrowser_runtime_dependency()
    {
        Type[] dependencyTypes = typeof(ProjectStructureKnownFileInteractionCoordinator)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.DoesNotContain(
            dependencyTypes,
            type => type.FullName?.Contains("FileBrowser", StringComparison.Ordinal) == true ||
                    type.Name.Contains("BrowseSession", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Known_file_interaction_coordinator_opens_and_releases_direct_interaction_once()
    {
        await using var fixture = await ResolverFixture.CreateAsync(
            ProjectObjectType.ImageAsset,
            new StorageObjectReference(
                ResolverFixture.StorageId,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                "managed-files/project-media/images/asset.png",
                "asset.png",
                "image/png",
                512));
        var activator = new RecordingKnownFileActivator();
        var sessionFactory = new RecordingKnownFileSessionFactory();
        var releaser = new RecordingKnownFileSessionReleaser();
        StorageCatalogRecord storage = CreateStorage(isReadOnly: true);
        var sut = new ProjectStructureKnownFileInteractionCoordinator(
            fixture.Sut,
            activator,
            sessionFactory,
            releaser,
            new StaticStorageCatalog(storage),
            new StaticStorageDriverRegistry(new PolicyStorageDriver(revisioned: false)));

        ProjectStructureKnownFileInteraction interaction = await sut.OpenAsync(
            fixture.ProjectId,
            fixture.NodeKey);
        await interaction.DisposeAsync();

        Assert.Equal(1, activator.CallCount);
        Assert.Equal(1, sessionFactory.CallCount);
        Assert.Equal(1, releaser.CallCount);
        Assert.Equal("asset.png", interaction.Request.FileName);
        Assert.Equal(FileInteractionMode.View, interaction.Request.Mode);
        Assert.False(interaction.CanEdit);
        Assert.Equal(FileToolsKnownFileIntent.ReadOnly, activator.LastIntent);
    }

    [Fact]
    public async Task Known_text_file_on_revisioned_writable_storage_opens_with_edit_and_base_revision()
    {
        await using var fixture = await ResolverFixture.CreateAsync(
            ProjectObjectType.File,
            new StorageObjectReference(
                ResolverFixture.StorageId,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                "managed-files/project-media/notes.md",
                "notes.md",
                "text/plain",
                64));
        var activator = new RecordingKnownFileActivator("revision-1");
        var sessionFactory = new RecordingKnownFileSessionFactory(new RecordingSaveTarget());
        var releaser = new RecordingKnownFileSessionReleaser();
        StorageCatalogRecord storage = CreateStorage(isReadOnly: false);
        var sut = new ProjectStructureKnownFileInteractionCoordinator(
            fixture.Sut,
            activator,
            sessionFactory,
            releaser,
            new StaticStorageCatalog(storage),
            new StaticStorageDriverRegistry(new PolicyStorageDriver(revisioned: true)));

        await using ProjectStructureKnownFileInteraction interaction = await sut.OpenAsync(
            fixture.ProjectId,
            fixture.NodeKey);

        Assert.True(interaction.CanEdit);
        Assert.Equal(FileToolsKnownFileIntent.Edit, activator.LastIntent);
        Assert.Equal("text/markdown", interaction.Request.MediaType);
        Assert.Equal("revision-1", interaction.Request.ContentRevision?.Value);
        Assert.Equal(FileInteractionMode.Edit, interaction.WithMode(FileInteractionMode.Edit).Mode);
    }

    [Fact]
    public async Task Known_file_interaction_slot_replaces_and_releases_the_previous_interaction()
    {
        var releaser = new RecordingKnownFileSessionReleaser();
        await using var slot = new ProjectStructureKnownFileInteractionSlot(
            (_, nodeId, _) => ValueTask.FromResult(CreateInteraction(nodeId, releaser)));

        ProjectStructureKnownFileInteraction? first = await slot.OpenAsync(Guid.NewGuid(), "first");
        ProjectStructureKnownFileInteraction? second = await slot.OpenAsync(Guid.NewGuid(), "second");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Same(second, slot.Current);
        Assert.Equal(1, releaser.CallCount);

        await slot.CloseAsync();

        Assert.Null(slot.Current);
        Assert.Equal(2, releaser.CallCount);
    }

    [Fact]
    public async Task Known_file_interaction_slot_cancels_a_superseded_open_without_leaking_it()
    {
        var releaser = new RecordingKnownFileSessionReleaser();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var slot = new ProjectStructureKnownFileInteractionSlot(OpenAsync);

        Task<ProjectStructureKnownFileInteraction?> first = slot.OpenAsync(
            Guid.NewGuid(),
            "first").AsTask();
        await firstStarted.Task;
        ProjectStructureKnownFileInteraction? second = await slot.OpenAsync(
            Guid.NewGuid(),
            "second");

        Assert.Null(await first);
        Assert.NotNull(second);
        Assert.Same(second, slot.Current);

        async ValueTask<ProjectStructureKnownFileInteraction> OpenAsync(
            Guid _,
            string nodeId,
            CancellationToken cancellationToken)
        {
            if (nodeId == "first")
            {
                firstStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return CreateInteraction(nodeId, releaser);
        }
    }

    [Fact]
    public async Task ResolveKnownFileAsync_returns_a_typed_exact_file_scope()
    {
        await using var fixture = await ResolverFixture.CreateAsync(
            ProjectObjectType.ImageAsset,
            new StorageObjectReference(
                ResolverFixture.StorageId,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                "managed-files/project-media/images/asset.png",
                "asset.png",
                "image/png",
                512));

        FileToolsKnownFileScope resolved = await fixture.Sut.ResolveKnownFileAsync(
            fixture.ProjectId,
            fixture.NodeKey);
        IReadOnlyList<FileToolsStorageBinding> bindings = await fixture.Sut.ResolveAsync(resolved.Scope);

        Assert.Equal(FileToolsSemanticScopeKind.ProjectNode, resolved.Scope.Kind);
        Assert.Equal(FileToolsKnownFileOccurrenceKind.RelativePath, resolved.Occurrence.Kind);
        Assert.Equal("managed-files/project-media/images/asset.png", resolved.Occurrence.OccurrenceId);
        FileToolsStorageBinding binding = Assert.Single(bindings);
        Assert.Equal(ResolverFixture.StorageId, binding.StorageId);
        Assert.Equal(resolved.Occurrence.OccurrenceId, binding.Root.Value);
        Assert.Equal(FileToolsHostBrowseCacheMode.Disabled, binding.HostCacheMode);
    }

    [Theory]
    [InlineData(@"C:\workspace\secret.pdf")]
    [InlineData("../secret.pdf")]
    [InlineData("managed-files/../../secret.pdf")]
    [InlineData("https://example.invalid/secret.pdf")]
    public async Task ResolveKnownFileAsync_rejects_absolute_or_escaped_metadata_before_a_provider(
        string locator)
    {
        await using var fixture = await ResolverFixture.CreateAsync(
            ProjectObjectType.File,
            new StorageObjectReference(
                ResolverFixture.StorageId,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                locator,
                "secret.pdf",
                "application/pdf"));

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => fixture.Sut.ResolveKnownFileAsync(fixture.ProjectId, fixture.NodeKey).AsTask());

        Assert.Equal(FileBrowserErrorCode.Forbidden, exception.Error.Code);
    }

    [Fact]
    public async Task ResolveNodeCollectionAsync_returns_only_the_declared_storage_prefix()
    {
        await using var fixture = await ResolverFixture.CreateCollectionAsync("deliveries/reports");

        FileToolsSemanticScope scope = await fixture.Sut.ResolveNodeCollectionAsync(
            fixture.ProjectId,
            fixture.NodeKey);
        FileToolsStorageBinding binding = Assert.Single(await fixture.Sut.ResolveAsync(scope));

        Assert.Equal(ResolverFixture.StorageId, binding.StorageId);
        Assert.Equal("deliveries/reports", binding.Root.Value);
        Assert.Equal(FileToolsHostBrowseCacheMode.Disabled, binding.HostCacheMode);
    }

    [Fact]
    public void Projected_collection_scope_key_round_trips()
    {
        Guid projectId = Guid.NewGuid();
        string nodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunOutputNodeKey(
            Guid.NewGuid(),
            "artifacts/process-runs/run");
        var expected = ProjectStructureNodeFileScopeKey.CreateProjected(
            ProjectStructureNodeFileScopeMode.Collection,
            projectId,
            nodeKey);

        bool parsed = ProjectStructureNodeFileScopeKey.TryParse(expected.ToScopeId(), out var actual);

        Assert.True(parsed);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanBrowseFiles_accepts_only_governed_projected_process_run_folders()
    {
        Guid runId = Guid.NewGuid();
        string root = $"artifacts/process-runs/{runId:D}";
        ProjectStructureNode governedFolder = CreateProjectedFolderNode(runId, root);

        Assert.True(ProjectStructureFileActions.CanBrowseFiles(governedFolder));
        Assert.False(ProjectStructureFileActions.CanBrowseFiles(governedFolder with
        {
            ArtifactKind = "external-folder"
        }));
        Assert.False(ProjectStructureFileActions.CanBrowseFiles(governedFolder with
        {
            IsSystemManaged = false
        }));
        Assert.False(ProjectStructureFileActions.CanBrowseFiles(governedFolder with
        {
            MetadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
            {
                File = new ProjectFileMetadata
                {
                    FileSubtype = ProjectFileSubtype.Folder,
                    ExternalPath = $"artifacts/process-runs/{Guid.NewGuid():D}"
                }
            })
        }));
    }

    [Fact]
    public async Task ResolveNodeCollectionAsync_authorizes_current_projected_process_run_folder()
    {
        Guid runId = Guid.NewGuid();
        string root = $"artifacts/process-runs/{runId:D}";
        await using var fixture = await ResolverFixture.CreateProjectedCollectionAsync(runId, root);

        FileToolsSemanticScope scope = await fixture.Sut.ResolveNodeCollectionAsync(
            fixture.ProjectId,
            fixture.NodeKey);
        FileToolsStorageBinding binding = Assert.Single(await fixture.Sut.ResolveAsync(scope));

        Assert.Equal(ResolverFixture.StorageId, binding.StorageId);
        Assert.Equal(root, binding.Root.Value);
        Assert.Equal(FileToolsHostBrowseCacheMode.Disabled, binding.HostCacheMode);
        Assert.Contains(":v2:collection:", scope.Id.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"C:\workspace\process-runs")]
    [InlineData("artifacts/process-runs/../secret")]
    public async Task ResolveNodeCollectionAsync_rejects_hostile_projected_folder_path(string root)
    {
        await using var fixture = await ResolverFixture.CreateProjectedCollectionAsync(Guid.NewGuid(), root);

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => fixture.Sut.ResolveNodeCollectionAsync(fixture.ProjectId, fixture.NodeKey).AsTask());

        Assert.Equal(FileBrowserErrorCode.Forbidden, exception.Error.Code);
    }

    [Fact]
    public async Task ResolveNodeCollectionAsync_rejects_projected_folder_bound_to_another_run()
    {
        Guid runId = Guid.NewGuid();
        string root = $"artifacts/process-runs/{runId:D}";
        await using var fixture = await ResolverFixture.CreateProjectedCollectionAsync(
            runId,
            root,
            artifactId: Guid.NewGuid());

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => fixture.Sut.ResolveNodeCollectionAsync(fixture.ProjectId, fixture.NodeKey).AsTask());

        Assert.Equal(FileBrowserErrorCode.Forbidden, exception.Error.Code);
    }

    [Fact]
    public async Task ResolveAsync_rejects_stale_projected_folder_scope()
    {
        Guid runId = Guid.NewGuid();
        string root = $"artifacts/process-runs/{runId:D}";
        await using var fixture = await ResolverFixture.CreateProjectedCollectionAsync(runId, root);
        FileToolsSemanticScope scope = await fixture.Sut.ResolveNodeCollectionAsync(
            fixture.ProjectId,
            fixture.NodeKey);

        fixture.RemoveProjection();
        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => fixture.Sut.ResolveAsync(scope).AsTask());

        Assert.Equal(FileBrowserErrorCode.Conflict, exception.Error.Code);
    }

    [Theory]
    [InlineData(@"C:\workspace\reports")]
    [InlineData("../../reports")]
    [InlineData("file:///workspace/reports")]
    public async Task ResolveNodeCollectionAsync_rejects_hostile_prefix_metadata(string prefix)
    {
        await using var fixture = await ResolverFixture.CreateCollectionAsync(prefix);

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => fixture.Sut.ResolveNodeCollectionAsync(fixture.ProjectId, fixture.NodeKey).AsTask());

        Assert.Equal(FileBrowserErrorCode.Forbidden, exception.Error.Code);
    }

    [Fact]
    public async Task ResolveNodeCollectionAsync_rejects_unsupported_node_types()
    {
        await using var fixture = await ResolverFixture.CreateAsync(ProjectObjectType.Note, reference: null);

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => fixture.Sut.ResolveNodeCollectionAsync(fixture.ProjectId, fixture.NodeKey).AsTask());

        Assert.Equal(FileBrowserErrorCode.Unsupported, exception.Error.Code);
    }

    [Fact]
    public async Task ResolveAsync_rejects_a_stale_scope_before_a_provider()
    {
        await using var fixture = await ResolverFixture.CreateAsync(ProjectObjectType.File, reference: null);
        var key = ProjectStructureNodeFileScopeKey.CreatePersisted(
            ProjectStructureNodeFileScopeMode.KnownFile,
            Guid.NewGuid());
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ProjectNode,
            key.ToScopeId(),
            "Stale file");

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => fixture.Sut.ResolveAsync(scope).AsTask());

        Assert.Equal(FileBrowserErrorCode.NotFound, exception.Error.Code);
    }

    private static ProjectStructureNode CreateProjectedFolderNode(Guid runId, string root)
    {
        var record = new ProjectObjectRecord
        {
            NodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunOutputNodeKey(runId, root),
            ParentNodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId),
            ObjectType = ProjectObjectType.File,
            ObjectSubtype = "folder",
            Title = "Run artifacts",
            MetadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
            {
                File = new ProjectFileMetadata
                {
                    FileSubtype = ProjectFileSubtype.Folder,
                    ExternalPath = root
                }
            }),
            Binding = ProjectStructureProjectionBindingFactory.Create(
                "/projects/project/structure",
                ProjectStructureProcessNodeKeys.ProcessRunOutputFolderArtifactKind,
                runId),
            IsSystemManaged = true
        };
        return ProjectWorkbenchNodeMapper.MapStructureNode(record);
    }

    private sealed class ResolverFixture : IAsyncDisposable
    {
        public static readonly Guid StorageId = Guid.Parse("4a94a2c2-c6df-41ac-91ce-d5c851995303");
        private readonly DbContextOptions<AppDbContext> options;
        private readonly MutableProjectionContributor? projectionContributor;

        private ResolverFixture(
            DbContextOptions<AppDbContext> options,
            Guid projectId,
            string nodeKey,
            MutableProjectionContributor? projectionContributor = null)
        {
            this.options = options;
            this.projectionContributor = projectionContributor;
            ProjectId = projectId;
            NodeKey = nodeKey;
            IReadOnlyList<IProjectStructureProjectionContributor> projectionContributors = projectionContributor is null
                ? []
                : [projectionContributor];
            Sut = new ProjectStructureFileScopeResolver(
                new TestDbContextFactory(options),
                new ProjectStructureAssemblyService(projectionContributors, new SystemClock()),
                new StaticStorageCatalog(CreateStorage(isReadOnly: false)));
        }

        public Guid ProjectId { get; }

        public string NodeKey { get; }

        public ProjectStructureFileScopeResolver Sut { get; }

        public void RemoveProjection()
        {
            if (projectionContributor is not null)
            {
                projectionContributor.IsEnabled = false;
            }
        }

        public static async Task<ResolverFixture> CreateAsync(
            ProjectObjectType objectType,
            StorageObjectReference? reference)
        {
            AppDbContextModelRegistry.ConfigureAssemblies([typeof(WorkbenchModuleAssemblyMarker).Assembly]);
            var options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase($"project-structure-file-scope-{Guid.NewGuid():N}")
                .Options;
            Guid projectId = Guid.NewGuid();
            string nodeKey = $"node:{Guid.NewGuid():N}";
            await using var dbContext = new AppDbContext(options);
            var node = new ProjectObjectRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NodeKey = nodeKey,
                ObjectType = objectType,
                Title = "Stored asset",
                MetadataJson = "{}"
            };
            dbContext.Set<ProjectObjectRecord>().Add(node);
            if (reference is not null)
            {
                dbContext.Set<ProjectNodeBindingRecord>().Add(new ProjectNodeBindingRecord
                {
                    ProjectObjectId = node.Id,
                    MediaContentType = reference.ContentType,
                    MediaOriginalFileName = reference.DisplayName,
                    StorageObjectReferenceJson = StorageJson.SerializeReference(reference)
                });
            }

            await dbContext.SaveChangesAsync();
            return new ResolverFixture(options, projectId, nodeKey);
        }

        public static async Task<ResolverFixture> CreateCollectionAsync(string prefix)
        {
            AppDbContextModelRegistry.ConfigureAssemblies([typeof(WorkbenchModuleAssemblyMarker).Assembly]);
            var options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase($"project-structure-file-collection-{Guid.NewGuid():N}")
                .Options;
            Guid projectId = Guid.NewGuid();
            string nodeKey = $"node:{Guid.NewGuid():N}";
            await using var dbContext = new AppDbContext(options);
            var node = new ProjectObjectRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NodeKey = nodeKey,
                ObjectType = ProjectObjectType.Infrastructure,
                Title = "Report storage",
                MetadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Infrastructure = new ProjectInfrastructureMetadata
                    {
                        StoragePathPrefix = prefix
                    }
                })
            };
            dbContext.Set<ProjectObjectRecord>().Add(node);
            dbContext.Set<ProjectNodeReferenceRecord>().Add(new ProjectNodeReferenceRecord
            {
                ProjectObjectId = node.Id,
                ReferenceKind = ProjectNodeReferenceKinds.InfrastructureStorageCatalog,
                ReferenceId = StorageId.ToString("D")
            });
            await dbContext.SaveChangesAsync();
            return new ResolverFixture(options, projectId, nodeKey);
        }

        public static Task<ResolverFixture> CreateProjectedCollectionAsync(
            Guid runId,
            string root,
            Guid? artifactId = null)
        {
            AppDbContextModelRegistry.ConfigureAssemblies([typeof(WorkbenchModuleAssemblyMarker).Assembly]);
            var options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase($"project-structure-projected-file-collection-{Guid.NewGuid():N}")
                .Options;
            Guid projectId = Guid.NewGuid();
            string nodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunOutputNodeKey(runId, root);
            var contributor = new MutableProjectionContributor(new ProjectObjectRecord
            {
                ProjectId = projectId,
                NodeKey = nodeKey,
                ObjectType = ProjectObjectType.File,
                ObjectSubtype = "folder",
                Title = "Run artifacts",
                MetadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    File = new ProjectFileMetadata
                    {
                        FileSubtype = ProjectFileSubtype.Folder,
                        ExternalPath = root
                    }
                }),
                Binding = ProjectStructureProjectionBindingFactory.Create(
                    $"/projects/{projectId:D}/structure",
                    ProjectStructureProcessNodeKeys.ProcessRunOutputFolderArtifactKind,
                    artifactId ?? runId),
                ParentNodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId)
            });
            return Task.FromResult(new ResolverFixture(options, projectId, nodeKey, contributor));
        }

        public async ValueTask DisposeAsync()
        {
            await using var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureDeletedAsync();
        }
    }

    private sealed class MutableProjectionContributor(ProjectObjectRecord node)
        : IProjectStructureProjectionContributor
    {
        public bool IsEnabled { get; set; } = true;

        public Task ContributeAsync(
            ProjectStructureProjectionContext context,
            CancellationToken cancellationToken)
        {
            if (IsEnabled)
            {
                context.AddNode(node);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private static StorageCatalogRecord CreateStorage(bool isReadOnly)
        => new()
        {
            Id = ResolverFixture.StorageId,
            Name = "Project Structure files",
            ProviderKind = StorageProviderKind.FileSystem,
            EndpointOrRoot = "test",
            IsEnabled = true,
            IsReadOnly = isReadOnly,
            CapabilityMask = StorageCapability.Read | StorageCapability.Write | StorageCapability.MutableUpdate
        };

    private static ProjectStructureKnownFileInteraction CreateInteraction(
        string nodeId,
        IFileToolsKnownFileSessionReleaser releaser)
    {
        var file = new FileReference("authorized", nodeId);
        var session = new FileToolsKnownFileSession(
            file,
            new EmptyContentSource(),
            FileToolsKnownFileIntent.ReadOnly);
        var request = new FileInteractionRequest(
            file,
            $"{nodeId}.png",
            FileInteractionMode.View,
            "image/png",
            3);
        return new ProjectStructureKnownFileInteraction(request, session, releaser);
    }

    private sealed class RecordingKnownFileActivator(string? revision = null) : IFileToolsKnownFileActivator
    {
        public int CallCount { get; private set; }

        public FileToolsKnownFileIntent? LastIntent { get; private set; }

        public ValueTask<FileToolsKnownFileActivation> ActivateAsync(
            FileToolsSemanticScope scope,
            FileToolsKnownFileOccurrence occurrence,
            FileToolsKnownFileIntent intent,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastIntent = intent;
            var file = new FileReference("authorized", "known-file-handle", revision);
            return ValueTask.FromResult(new FileToolsKnownFileActivation(
                new FileToolsKnownFileRequest(scope, file, intent),
                occurrence.FileName,
                occurrence.MediaType,
                occurrence.Size));
        }
    }

    private sealed class RecordingKnownFileSessionFactory(IFileSaveTarget? saveTarget = null)
        : IFileToolsKnownFileSessionFactory
    {
        private static readonly IFileContentSource ContentSource = new EmptyContentSource();

        public int CallCount { get; private set; }

        public ValueTask<FileToolsKnownFileSession> CreateAsync(
            FileToolsKnownFileRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(new FileToolsKnownFileSession(
                request.File,
                ContentSource,
                request.Intent,
                request.Intent == FileToolsKnownFileIntent.Edit ? saveTarget : null));
        }
    }

    private sealed class RecordingSaveTarget : IFileSaveTarget
    {
        public ValueTask<FileSaveTargetResult> SaveAsync(
            FileSaveRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileSaveTargetResult(new FileContentRevision("revision-2")));
    }

    private sealed class StaticStorageCatalog(StorageCatalogRecord storage) : IStorageCatalogService
    {
        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>([storage]);

        public Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == storage.Id ? storage : null);

        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(storage);

        public Task<StorageCatalogRecord> SaveAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
            => Task.FromResult(record);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

        public Task<StorageRoutingRule> SaveRuleAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default)
            => Task.FromResult(rule);
    }

    private sealed class StaticStorageDriverRegistry(IStorageDriver driver) : IStorageDriverRegistry
    {
        public IReadOnlyCollection<StorageProviderKind> RegisteredKinds => [driver.ProviderKind];

        public bool TryResolve(StorageProviderKind providerKind, out IStorageDriver resolved)
        {
            resolved = driver;
            return providerKind == driver.ProviderKind;
        }

        public IStorageDriver Resolve(StorageProviderKind providerKind)
            => providerKind == driver.ProviderKind
                ? driver
                : throw new NotSupportedException();
    }

    private sealed class PolicyStorageDriver(bool revisioned)
        : IStorageDriver, IStorageRevisionedContentDriver
    {
        public StorageProviderKind ProviderKind => StorageProviderKind.FileSystem;

        public StorageCapability SupportedCapabilities =>
            StorageCapability.Read |
            (revisioned ? StorageCapability.Write | StorageCapability.MutableUpdate : StorageCapability.None);

        public Task<StorageConnectionTestResult> TestConnectionAsync(
            StorageCatalogRecord storage,
            string? secretValue,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageWriteResult> SaveAsync(
            StorageCatalogRecord storage,
            StorageWriteRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageContentRevision?> GetRevisionAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageRevisionedWriteResult> ReplaceAsync(
            StorageCatalogRecord storage,
            StorageRevisionedWriteRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingKnownFileSessionReleaser : IFileToolsKnownFileSessionReleaser
    {
        public int CallCount { get; private set; }

        public ValueTask ReleaseAsync(
            FileReference file,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class EmptyContentSource : IFileContentSource
    {
        public ValueTask<FileContentLease> OpenReadAsync(
            FileContentReadRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileContentLease(
                new MemoryStream([1, 2, 3]),
                "image/png",
                3));
    }
}
