using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureFileInteractionIntegrationTests
{
    [Fact]
    public async Task Direct_markdown_interaction_saves_with_current_revision_and_publishes_after_persistence()
    {
        await using var fixture = await InteractionFixture.CreateAsync();
        await using ProjectStructureKnownFileInteraction interaction = await fixture.Coordinator.OpenAsync(
            fixture.ProjectId,
            fixture.NodeKey);
        FileCatalogRevision before = fixture.Revisions.Get(fixture.Scope, fixture.Storage.Id);
        const string replacement = "# SB16 persisted\n\nAwaited authorized save.";
        var args = CreateSaveArgs(interaction, replacement, interaction.Request.ContentRevision);

        await interaction.SaveAsync(args);

        Assert.True(interaction.CanEdit);
        Assert.Equal("text/markdown", interaction.Request.MediaType);
        Assert.True(args.HasPersistedRevision);
        Assert.NotEqual(interaction.Request.ContentRevision, args.PersistedRevision);
        Assert.Equal(replacement, await File.ReadAllTextAsync(fixture.FullPath));
        Assert.Equal(before.Scope + 1, fixture.Revisions.Get(fixture.Scope, fixture.Storage.Id).Scope);
    }

    [Fact]
    public async Task Stale_and_overwrite_retries_fail_without_replacing_current_bytes_or_revision()
    {
        await using var fixture = await InteractionFixture.CreateAsync();
        await using ProjectStructureKnownFileInteraction interaction = await fixture.Coordinator.OpenAsync(
            fixture.ProjectId,
            fixture.NodeKey);
        FileCatalogRevision before = fixture.Revisions.Get(fixture.Scope, fixture.Storage.Id);
        await File.WriteAllTextAsync(fixture.FullPath, "# External replacement");

        await Assert.ThrowsAsync<FileSaveConflictException>(
            () => interaction.SaveAsync(CreateSaveArgs(
                interaction,
                "# Stale local edit",
                interaction.Request.ContentRevision)));
        FileAccessDeniedException overwrite = await Assert.ThrowsAsync<FileAccessDeniedException>(
            () => interaction.SaveAsync(CreateSaveArgs(interaction, "# Forced overwrite", expectedRevision: null)));

        Assert.Equal(FileAccessFailureCode.OperationDenied, overwrite.Code);
        Assert.Equal("# External replacement", await File.ReadAllTextAsync(fixture.FullPath));
        Assert.Equal(before, fixture.Revisions.Get(fixture.Scope, fixture.Storage.Id));
    }

    private static FileInteractionSaveRequestedEventArgs CreateSaveArgs(
        ProjectStructureKnownFileInteraction interaction,
        string content,
        FileContentRevision? expectedRevision)
        => new(new FileSaveRequest(
            interaction.Session.File,
            editRevision: 1,
            new BufferedFileSaveContent(System.Text.Encoding.UTF8.GetBytes(content)),
            expectedRevision,
            "text/markdown",
            "utf-8"));

    private sealed class InteractionFixture : IAsyncDisposable
    {
        private readonly TestApplication application;
        private readonly AsyncServiceScope scope;

        private InteractionFixture(
            TestApplication application,
            AsyncServiceScope scope,
            ProjectStructureKnownFileInteractionCoordinator coordinator,
            IFileCatalogRevisionReader revisions,
            FileToolsSemanticScope semanticScope,
            Guid projectId,
            string nodeKey,
            StorageCatalogRecord storage,
            string fullPath)
        {
            this.application = application;
            this.scope = scope;
            Coordinator = coordinator;
            Revisions = revisions;
            Scope = semanticScope;
            ProjectId = projectId;
            NodeKey = nodeKey;
            Storage = storage;
            FullPath = fullPath;
        }

        public ProjectStructureKnownFileInteractionCoordinator Coordinator { get; }

        public IFileCatalogRevisionReader Revisions { get; }

        public FileToolsSemanticScope Scope { get; }

        public Guid ProjectId { get; }

        public string NodeKey { get; }

        public StorageCatalogRecord Storage { get; }

        public string FullPath { get; }

        public static async Task<InteractionFixture> CreateAsync()
        {
            TestApplication application = await TestApplication.CreateAsync();
            AsyncServiceScope scope = application.Services.CreateAsyncScope();
            IServiceProvider services = scope.ServiceProvider;
            var projects = services.GetRequiredService<ProjectsService>();
            var storageCatalog = services.GetRequiredService<IStorageCatalogService>();
            var dbContextFactory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            var scopeProvider = services.GetRequiredService<IProjectStructureNodeFileScopeProvider>();
            var coordinator = services.GetRequiredService<ProjectStructureKnownFileInteractionCoordinator>();
            var revisions = services.GetRequiredService<IFileCatalogRevisionReader>();
            var projectResult = await projects.SaveAsync(new ProjectEditorModel
            {
                Name = "SB16 interaction integration",
                Description = "Governed direct FileInteraction proof",
                Objective = "Prove revision-aware save",
                CurrentPhase = "Verification"
            });
            Assert.True(projectResult.IsSuccess, string.Join(" ", projectResult.Errors.Select(error => error.Message)));
            Guid projectId = projectResult.Value;
            StorageCatalogRecord storage = await storageCatalog.EnsureBootstrapFileSystemStorageAsync();
            string fileName = $"sb16-interaction-{Guid.NewGuid():N}.md";
            string relativePath = fileName;
            string fullPath = Path.Combine(storage.EndpointOrRoot, fileName);
            await File.WriteAllTextAsync(fullPath, "# SB16 initial");
            string nodeKey = $"file:{Guid.NewGuid():N}";
            await using (AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync())
            {
                var node = new ProjectObjectRecord
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    NodeKey = nodeKey,
                    ObjectType = ProjectObjectType.File,
                    ObjectSubtype = "markdown",
                    Title = fileName,
                    MetadataJson = "{}"
                };
                dbContext.Set<ProjectObjectRecord>().Add(node);
                dbContext.Set<ProjectNodeBindingRecord>().Add(new ProjectNodeBindingRecord
                {
                    ProjectObjectId = node.Id,
                    MediaContentType = "text/plain",
                    MediaOriginalFileName = fileName,
                    StorageObjectReferenceJson = StorageJson.SerializeReference(new StorageObjectReference(
                        storage.Id,
                        StorageProviderKind.FileSystem,
                        StorageLocatorKind.RelativePath,
                        relativePath,
                        fileName,
                        "text/plain",
                        new FileInfo(fullPath).Length))
                });
                await dbContext.SaveChangesAsync();
            }

            FileToolsKnownFileScope resolved = await scopeProvider.ResolveKnownFileAsync(projectId, nodeKey);
            return new InteractionFixture(
                application,
                scope,
                coordinator,
                revisions,
                resolved.Scope,
                projectId,
                nodeKey,
                storage,
                fullPath);
        }

        public async ValueTask DisposeAsync()
        {
            File.Delete(FullPath);
            await scope.DisposeAsync();
            await application.DisposeAsync();
        }
    }
}
