using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PromptGalleryServiceValidationTests
{
    [Fact]
    public async Task Save_rejects_invalid_model_recommendations_before_persistence()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(nameof(Save_rejects_invalid_model_recommendations_before_persistence));
        var service = new PromptsService(
            factory,
            new PromptGalleryTestSupport.FixedClock(),
            new NullActivityStream(),
            new EfPromptGallerySearchDriver(factory),
            PromptGalleryTestSupport.CreateDisabledProjectionCoordinator(factory),
            new PromptGalleryCompatibilityEvaluator(),
            NullLogger<PromptsService>.Instance);

        var result = await service.SaveDraftAsync(new PromptGalleryDraft(
            Id: null,
            ProjectId: null,
            CollectionId: null,
            "Invalid recommendations",
            "",
            PromptGalleryItemKind.FullPrompt,
            "",
            "Content",
            Recommendations: new PromptModelRecommendations(
                Temperature: 2.1,
                MaxOutputTokens: 0,
                TopP: 1.1)));

        Assert.True(result.IsFailure);
        Assert.Equal(
            [
                "prompts.gallery.temperature-invalid",
                "prompts.gallery.max-output-tokens-invalid",
                "prompts.gallery.top-p-invalid"
            ],
            result.Errors.Select(error => error.Code));
        await using var dbContext = factory.CreateDbContext();
        Assert.Empty(dbContext.Set<PromptArtifact>());
    }
}
