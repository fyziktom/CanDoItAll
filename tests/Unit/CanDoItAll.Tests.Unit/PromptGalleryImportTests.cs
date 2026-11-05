using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Prompts;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.Infrastructure;

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
            new PromptImportVersionRequest("Imported from workflow component"));

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

    [Fact]
    public async Task Existing_import_replacement_requires_its_original_stamp_and_never_overwrites_a_foreign_change() {
        var factory = PromptGalleryTestSupport.CreateFactory(nameof(Existing_import_replacement_requires_its_original_stamp_and_never_overwrites_a_foreign_change));
        var service = PromptGalleryTestSupport.CreateService(factory);
        var request = new PromptGalleryImportRequest(PromptArtifactProvenance.WorkflowMigration, "original-source", "test-catalog",
            new(null, null, null, "Original", "", PromptGalleryItemKind.FullPrompt, "", "Original content"), new("Initial import"));
        var imported = (await service.ImportVersionAsync(request)).Value!;
        var original = (await service.GetItemAsync(imported.PromptArtifactId)).Value!;
        var changed = request with { Draft = request.Draft with { Content = "New source content" } };
        var missing = await service.ImportVersionAsync(changed);
        Assert.Contains(missing.Errors, error => error.Code == PromptGalleryErrorCodes.ImportExpectationRequired);
        Assert.True((await service.SetFavoriteAsync(original.Id, true)).IsSuccess);
        var stale = await service.ImportVersionAsync(changed with { ExpectedUpdatedAtUtc = original.UpdatedAtUtc });
        Assert.Contains(stale.Errors, error => error.Code == PromptGalleryErrorCodes.ConcurrencyConflict);
        var current = (await service.GetItemAsync(original.Id)).Value!;
        Assert.Equal("Original content", current.DraftContent);
        Assert.True(current.IsFavorite);
        var accepted = await service.ImportVersionAsync(changed with { ExpectedUpdatedAtUtc = current.UpdatedAtUtc });
        Assert.True(accepted.IsSuccess);
        Assert.Equal("New source content", (await service.GetItemAsync(original.Id)).Value!.DraftContent);
    }

    [Fact]
    public async Task Exact_import_replay_is_read_only_and_keeps_the_original_witness() {
        var factory = PromptGalleryTestSupport.CreateFactory(nameof(Exact_import_replay_is_read_only_and_keeps_the_original_witness));
        var service = PromptGalleryTestSupport.CreateService(factory);
        var request = new PromptGalleryImportRequest(PromptArtifactProvenance.WorkflowMigration, "unchanged-source", "test-catalog",
            new(null, null, null, "Original", "", PromptGalleryItemKind.FullPrompt, "", "Content"), new("Initial import"));
        var imported = (await service.ImportVersionAsync(request)).Value!;
        var before = (await service.GetItemAsync(imported.PromptArtifactId)).Value!;
        Assert.True((await service.ImportVersionAsync(request)).IsSuccess);
        var after = (await service.GetItemAsync(imported.PromptArtifactId)).Value!;
        Assert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc);
        Assert.Equal(before.CurrentVersionNumber, after.CurrentVersionNumber);
    }
}
