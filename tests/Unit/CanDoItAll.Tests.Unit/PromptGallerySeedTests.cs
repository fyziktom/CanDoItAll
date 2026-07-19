using CanDoItAll.Modules.Prompts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PromptGallerySeedTests
{
    [Fact]
    public void Embedded_catalog_is_typed_complete_and_stable()
    {
        var resources = typeof(PromptGallerySeedLoader)
            .Assembly
            .GetManifestResourceNames()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var loader = new PromptGallerySeedLoader();
        var pack = loader.Load();

        Assert.Contains("CanDoItAll.Modules.Prompts.SeedAssets.PromptLibrary.manifest.json", resources);
        Assert.Contains("CanDoItAll.Modules.Prompts.SeedAssets.PromptLibrary.group-catalog.json", resources);
        Assert.Contains("CanDoItAll.Modules.Prompts.SeedAssets.PromptLibrary.prompt-component-library.json", resources);
        Assert.Contains("CanDoItAll.Modules.Prompts.SeedAssets.PromptLibrary.factory-prompt-flow-templates.seed.json", resources);
        Assert.Contains("CanDoItAll.Modules.Prompts.SeedAssets.PromptLibrary.factory-prompt-blueprints.seed.json", resources);
        Assert.Equal(12, pack.Groups.Count);
        Assert.Equal(111, pack.Components.Count);
        Assert.Equal(10, pack.Flows.Count);
        Assert.Equal(13, pack.Blueprints.Count);

        var component = Assert.Single(pack.Components, item => item.Key == "role-architecture-lead");
        Assert.Equal(Guid.Parse("4ab32001-a86b-526a-84a2-850affeea6c6"), component.Id);
        Assert.Equal("session-framing", component.GroupMetadata?.Key);
        Assert.Contains("architecture", component.Tags);
        Assert.Contains("target_feature_or_problem", component.TemplateTokens);
        Assert.Equal(
            PromptGallerySeedFingerprint.Compute(component),
            PromptGallerySeedFingerprint.Compute(component));
    }

    [Fact]
    public async Task Import_is_idempotent_and_never_overwrites_modified_items()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(nameof(Import_is_idempotent_and_never_overwrites_modified_items));
        var importer = new PromptGallerySeedImporter(
            factory,
            new PromptGallerySeedLoader(),
            new PromptGalleryTestSupport.FixedClock(),
            PromptGalleryTestSupport.CreateDisabledProjectionCoordinator(factory),
            NullLogger<PromptGallerySeedImporter>.Instance);

        var first = await importer.ImportAsync();
        var second = await importer.ImportAsync();

        Assert.True(first.Succeeded);
        Assert.Equal(111, first.CatalogComponentCount);
        Assert.Equal(111, first.CreatedCount);
        Assert.Equal(0, first.ExistingCount);
        Assert.True(second.Succeeded);
        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(111, second.ExistingCount);

        var componentId = Guid.Parse("4ab32001-a86b-526a-84a2-850affeea6c6");
        await using (var dbContext = factory.CreateDbContext())
        {
            Assert.Equal(111, await dbContext.Set<PromptArtifact>().CountAsync());
            Assert.Equal(111, await dbContext.Set<PromptVersion>().CountAsync());
            var artifact = await dbContext.Set<PromptArtifact>().SingleAsync(item => item.Id == componentId);
            Assert.Equal(PromptGalleryItemKind.Part, artifact.Kind);
            Assert.Equal("role-architecture-lead", artifact.SourceKey);
            Assert.Equal("session-framing", artifact.SourceGroupKey);
            Assert.Equal(PromptArtifactProvenance.PackagedComponentCatalog, artifact.Provenance);
            Assert.Contains(
                await dbContext.Set<PromptTemplateToken>()
                    .Where(token => token.PromptArtifactId == componentId)
                    .Select(token => token.Name)
                    .ToListAsync(),
                token => token == "target_feature_or_problem");

            artifact.CurrentDraftText = "User-modified content";
            await dbContext.SaveChangesAsync();
        }

        var afterUserEdit = await importer.ImportAsync();
        Assert.Equal(111, afterUserEdit.ExistingCount);
        await using (var dbContext = factory.CreateDbContext())
        {
            var artifact = await dbContext.Set<PromptArtifact>().SingleAsync(item => item.Id == componentId);
            Assert.Equal("User-modified content", artifact.CurrentDraftText);
            artifact.SourceFingerprint = "SOURCE-CHANGED";
            await dbContext.SaveChangesAsync();
        }

        var conflict = await importer.ImportAsync();
        var reported = Assert.Single(conflict.Conflicts);
        Assert.Equal("role-architecture-lead", reported.SourceKey);
        Assert.Equal(PromptGallerySeedConflictCode.SourceChangedOrItemModified, reported.Code);
        await using (var dbContext = factory.CreateDbContext())
        {
            Assert.Equal(
                "User-modified content",
                (await dbContext.Set<PromptArtifact>().SingleAsync(item => item.Id == componentId)).CurrentDraftText);
        }
    }

    [Fact]
    public async Task Import_rebuilds_an_enabled_projection_once_after_the_canonical_batch_commits()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(
            nameof(Import_rebuilds_an_enabled_projection_once_after_the_canonical_batch_commits));
        var driver = new RecordingProjectionDriver();
        var importer = new PromptGallerySeedImporter(
            factory,
            new PromptGallerySeedLoader(),
            new PromptGalleryTestSupport.FixedClock(),
            new PromptGalleryProjectionCoordinator(factory, driver),
            NullLogger<PromptGallerySeedImporter>.Instance);

        var result = await importer.ImportAsync();

        Assert.Equal(111, result.CreatedCount);
        Assert.Equal(1, driver.RebuildCount);
        Assert.Equal(111, driver.ProjectedDocumentCount);
    }

    [Fact]
    public async Task Import_remains_successful_when_the_derivative_projection_fails_after_commit()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(
            nameof(Import_remains_successful_when_the_derivative_projection_fails_after_commit));
        var importer = new PromptGallerySeedImporter(
            factory,
            new PromptGallerySeedLoader(),
            new PromptGalleryTestSupport.FixedClock(),
            new PromptGalleryProjectionCoordinator(factory, new RecordingProjectionDriver(throwOnRebuild: true)),
            NullLogger<PromptGallerySeedImporter>.Instance);

        var result = await importer.ImportAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(111, result.CreatedCount);
        await using var dbContext = factory.CreateDbContext();
        Assert.Equal(111, await dbContext.Set<PromptArtifact>().CountAsync());
        Assert.Equal(111, await dbContext.Set<PromptVersion>().CountAsync());
    }

    private sealed class RecordingProjectionDriver(bool throwOnRebuild = false) : IPromptGalleryProjectionDriver
    {
        private static readonly PromptGalleryProjectionStatus Ready = new(
            "Recording",
            Enabled: true,
            PromptGalleryProjectionHealth.Ready,
            "Ready");

        public int RebuildCount { get; private set; }

        public int ProjectedDocumentCount { get; private set; }

        public string Name => Ready.DriverName;

        public bool Enabled => true;

        public Task<PromptGalleryProjectionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Ready);

        public Task UpsertAsync(
            PromptGalleryProjectionDocument document,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveAsync(Guid promptArtifactId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public async Task<int> RebuildAsync(
            IAsyncEnumerable<PromptGalleryProjectionDocument> documents,
            CancellationToken cancellationToken = default)
        {
            RebuildCount++;
            if (throwOnRebuild)
            {
                throw new InvalidOperationException("Projection unavailable.");
            }

            await foreach (var _ in documents.WithCancellation(cancellationToken))
            {
                ProjectedDocumentCount++;
            }

            return ProjectedDocumentCount;
        }
    }
}
