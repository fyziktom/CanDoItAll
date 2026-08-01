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

    [Fact]
    public async Task Save_receipt_uses_database_timestamp_precision()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(
            nameof(Save_receipt_uses_database_timestamp_precision));
        var service = new PromptsService(
            factory,
            new PromptGalleryTestSupport.FixedClock(DateTimeOffset.UnixEpoch.AddTicks(17)),
            new NullActivityStream(),
            new EfPromptGallerySearchDriver(factory),
            PromptGalleryTestSupport.CreateDisabledProjectionCoordinator(factory),
            new PromptGalleryCompatibilityEvaluator(),
            NullLogger<PromptsService>.Instance);

        var receipt = (await service.SaveDraftAsync(CreateDraft())).Value;
        var persisted = Assert.IsType<PromptGalleryItemDetails>(
            (await service.GetItemAsync(receipt.PromptArtifactId)).Value);

        Assert.Equal(0, receipt.UpdatedAtUtc.Ticks % TimeSpan.TicksPerMicrosecond);
        Assert.Equal(receipt.UpdatedAtUtc, persisted.UpdatedAtUtc);
    }

    [Fact]
    public async Task Favorite_is_atomic_and_stale_draft_update_fails_without_losing_tags()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(
            nameof(Favorite_is_atomic_and_stale_draft_update_fails_without_losing_tags));
        var service = PromptGalleryTestSupport.CreateService(factory);
        var create = await service.SaveDraftAsync(CreateDraft());
        var promptId = create.Value.PromptArtifactId;
        var beforeFavorite = Assert.IsType<PromptGalleryItemDetails>((await service.GetItemAsync(promptId)).Value);
        Assert.Equal(create.Value.UpdatedAtUtc, beforeFavorite.UpdatedAtUtc);

        Assert.True((await service.SetFavoriteAsync(promptId, favorite: true)).IsSuccess);
        var favorited = Assert.IsType<PromptGalleryItemDetails>((await service.GetItemAsync(promptId)).Value);
        Assert.True(favorited.IsFavorite);
        Assert.Equal(["architecture"], favorited.Tags);

        var staleVersion = await service.CreateVersionAsync(
            promptId,
            new PromptVersionCreateRequest(
                "Reviewed before the favorite change",
                ExpectedUpdatedAtUtc: beforeFavorite.UpdatedAtUtc));
        Assert.True(staleVersion.IsFailure);
        Assert.Contains(staleVersion.Errors, error => error.Code == "prompts.gallery.concurrency-conflict");

        var stale = await service.SaveDraftAsync(CreateDraft(promptId, beforeFavorite.UpdatedAtUtc));
        Assert.True(stale.IsFailure);
        Assert.Contains(stale.Errors, error => error.Code == "prompts.gallery.concurrency-conflict");

        var update = await service.SaveDraftAsync(CreateDraft(promptId, favorited.UpdatedAtUtc));
        Assert.True(update.IsSuccess);
        var updated = Assert.IsType<PromptGalleryItemDetails>((await service.GetItemAsync(promptId)).Value);
        Assert.True(updated.IsFavorite);
        Assert.Equal(["architecture"], updated.Tags);
    }

    [Fact]
    public async Task Create_version_requires_the_last_read_concurrency_token()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(
            nameof(Create_version_requires_the_last_read_concurrency_token));
        var service = PromptGalleryTestSupport.CreateService(factory);
        var promptId = (await service.SaveDraftAsync(CreateDraft())).Value.PromptArtifactId;

        var result = await service.CreateVersionAsync(
            promptId,
            new PromptVersionCreateRequest(
                "Reviewed draft",
                ExpectedUpdatedAtUtc: default));

        Assert.True(result.IsFailure);
        Assert.Contains(
            result.Errors,
            error => error.Code == "prompts.version.expected-updated-at-required");
    }

    [Fact]
    public async Task Save_rejects_multiple_preferred_provider_models()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(
            nameof(Save_rejects_multiple_preferred_provider_models));
        var service = PromptGalleryTestSupport.CreateService(factory);
        var draft = CreateDraft() with
        {
            SupportedModels =
            [
                new PromptProviderModel("OpenAI", "gpt-5", IsPreferred: true),
                new PromptProviderModel("Anthropic", "claude-sonnet", IsPreferred: true)
            ]
        };

        var result = await service.SaveDraftAsync(draft);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "prompts.gallery.models-preferred-duplicate");
    }

    private static PromptGalleryDraft CreateDraft(
        Guid? id = null,
        DateTimeOffset? expectedUpdatedAtUtc = null)
        => new(
            id,
            ProjectId: null,
            CollectionId: null,
            "Architecture prompt",
            "Reusable architecture guidance.",
            PromptGalleryItemKind.Part,
            "design",
            "Review the dependency direction.",
            Tags: ["architecture"],
            ExpectedUpdatedAtUtc: expectedUpdatedAtUtc);
}
