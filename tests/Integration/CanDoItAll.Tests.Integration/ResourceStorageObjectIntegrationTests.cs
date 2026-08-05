using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ResourceStorageObjectIntegrationTests
{
    [Fact]
    public async Task Browse_promotion_persists_stable_identity_advances_revision_and_reopens_current_content()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var storageCatalog = scope.ServiceProvider.GetRequiredService<IStorageCatalogService>();
        var sourceCatalog = scope.ServiceProvider.GetRequiredService<IResourceFileSourceCatalog>();
        var browseCoordinator = scope.ServiceProvider.GetRequiredService<ResourceFileBrowseCoordinator>();
        var promotion = scope.ServiceProvider.GetRequiredService<ResourceStorageObjectPromotionService>();
        var interactionService = scope.ServiceProvider.GetRequiredService<ResourceStorageObjectInteractionService>();
        var revisions = scope.ServiceProvider.GetRequiredService<IFileCatalogRevisionReader>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        Guid projectId = await CreateProjectAsync(projects);
        StorageCatalogRecord storage = await storageCatalog.EnsureBootstrapFileSystemStorageAsync();
        string fileName = $"governed-resource-{Guid.NewGuid():N}.txt";
        string fullPath = Path.Combine(storage.EndpointOrRoot, fileName);
        const string expectedContent = "Governed storage-object integration proof";
        await File.WriteAllTextAsync(fullPath, expectedContent);

        try
        {
            ResourceFileSourceCatalogSnapshot sources = await sourceCatalog.LoadAsync();
            ResourceFileSourceDescriptor source = Assert.Single(
                sources.Sources,
                candidate => candidate.Key == ResourceFileSourceKey.ForStorage(storage.Id));
            FileCatalogRevision before = revisions.Get(source.Scope, storage.Id);
            await using ResourceFileBrowseWorkspace workspace = await browseCoordinator.OpenAsync(source.Key);
            await workspace.Browser.InitializeAsync();
            FileBrowserItem item = Assert.Single(
                workspace.Browser.Snapshot.Items,
                candidate => candidate.Name == fileName);

            ResourceStorageObjectPromotionResult result = await promotion.PromoteAsync(
                new ResourceStorageObjectPromotionCommand(
                    source.Key,
                    item.Key,
                    projectId,
                    "Governed integration resource"));

            Assert.True(result.Created);
            Assert.Equal(before.Scope + 1, result.Revision.Scope);
            await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            ProjectResource resource = await dbContext.Set<ProjectResource>()
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == result.ResourceId);
            StorageObjectResourceConfig config = StorageObjectResourceConnectorPlugin.Deserialize(resource.ConfigJson);
            Assert.Equal(storage.Id, config.StorageId);
            Assert.Equal(StorageLocatorKind.RelativePath, config.LocatorKind);
            Assert.Equal(fileName, config.Locator);
            Assert.DoesNotContain(fileName, resource.LocationOrIdentifier, StringComparison.Ordinal);

            await using ResourceStorageObjectInteraction interaction = await interactionService.OpenAsync(result.ResourceId);
            await using FileContentLease lease = await interaction.Session.ContentSource.OpenReadAsync(
                new FileContentReadRequest(interaction.Session.File));
            using var reader = new StreamReader(lease.Stream);
            string reopenedContent = await reader.ReadToEndAsync();
            Assert.Equal(expectedContent, reopenedContent);
        }
        finally
        {
            File.Delete(fullPath);
        }
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Storage-object integration project",
            Description = "Governed integration proof",
            Objective = "Prove governed promotion and reopen",
            CurrentPhase = "Execution"
        });
        Assert.True(result.IsSuccess, string.Join(" ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }
}
