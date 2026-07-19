using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Prompts;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PromptGalleryImportTests
{
    [Fact]
    public async Task Workflow_import_retry_reuses_canonical_item_and_version()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(
            nameof(Workflow_import_retry_reuses_canonical_item_and_version));
        var service = PromptGalleryTestSupport.CreateService(factory);
        var request = new PromptGalleryImportRequest(
            PromptArtifactProvenance.WorkflowMigration,
            "workflow-component:00000000-0000-0000-0000-000000000123",
            "agent-framework-workflow-components",
            new PromptGalleryDraft(
                Id: null,
                ProjectId: null,
                CollectionId: null,
                "Migrated workflow prompt",
                "Legacy workflow instructions.",
                PromptGalleryItemKind.FullPrompt,
                "workflow",
                "Use the canonical prompt snapshot.",
                Tags: ["workflow"],
                SupportedConsumers: [PromptGalleryConsumer.Workflow]),
            new PromptVersionCreateRequest("Imported from workflow component"));

        var first = await service.ImportVersionAsync(request);
        var retry = await service.ImportVersionAsync(request);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        var firstSnapshot = Assert.IsType<PromptVersionSnapshot>(first.Value);
        var retrySnapshot = Assert.IsType<PromptVersionSnapshot>(retry.Value);
        Assert.Equal(firstSnapshot.PromptArtifactId, retrySnapshot.PromptArtifactId);
        Assert.Equal(firstSnapshot.PromptVersionId, retrySnapshot.PromptVersionId);

        await using var dbContext = factory.CreateDbContext();
        var artifact = Assert.Single(await dbContext.Set<PromptArtifact>().ToListAsync());
        Assert.Equal(firstSnapshot.PromptArtifactId, artifact.Id);
        Assert.Equal(PromptArtifactProvenance.WorkflowMigration, artifact.Provenance);
        Assert.Equal(request.SourceKey, artifact.SourceKey);
        Assert.Equal(request.SourceCatalog, artifact.SourceCatalog);
        Assert.Equal(1, artifact.CurrentVersionNumber);
        var version = Assert.Single(await dbContext.Set<PromptVersion>().ToListAsync());
        Assert.Equal(firstSnapshot.PromptVersionId, version.Id);
        Assert.Equal(1, version.VersionNumber);
    }
}
