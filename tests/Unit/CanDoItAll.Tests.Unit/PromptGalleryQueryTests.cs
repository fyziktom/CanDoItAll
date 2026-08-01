using CanDoItAll.Modules.Prompts;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PromptGalleryQueryTests
{
    [Fact]
    public void Query_rejects_invalid_paging()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PromptGalleryQuery(PageIndex: -1).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new PromptGalleryQuery(PageSize: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromptGalleryQuery(PageSize: PromptGalleryQuery.MaximumPageSize + 1).Validate());
        Assert.Throws<OverflowException>(() =>
            new PromptGalleryQuery(PageIndex: int.MaxValue, PageSize: 2).Validate());
    }

    [Fact]
    public async Task Ef_driver_applies_text_tag_kind_status_and_provider_model_filters()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(nameof(Ef_driver_applies_text_tag_kind_status_and_provider_model_filters));
        var matchingId = Guid.Parse("00000000-0000-0000-0000-000000000010");
        await using (var dbContext = factory.CreateDbContext())
        {
            var architecture = new PromptTag { Name = "architecture", NameKey = "ARCHITECTURE" };
            var quality = new PromptTag { Name = "quality", NameKey = "QUALITY" };
            dbContext.AddRange(architecture, quality);
            dbContext.AddRange(
                CreateArtifact(matchingId, "Matching", "needle in summary", PromptGalleryItemKind.Part),
                CreateArtifact(Guid.Parse("00000000-0000-0000-0000-000000000020"), "Wrong model", "needle", PromptGalleryItemKind.Part),
                CreateArtifact(Guid.Parse("00000000-0000-0000-0000-000000000030"), "Archived", "needle", PromptGalleryItemKind.Part, archived: true));
            dbContext.AddRange(
                new PromptArtifactTag { PromptArtifactId = matchingId, PromptTagId = architecture.Id },
                new PromptArtifactTag { PromptArtifactId = matchingId, PromptTagId = quality.Id });
            dbContext.AddRange(
                SupportedModel(matchingId, "OpenAI", "gpt-5"),
                SupportedModel(Guid.Parse("00000000-0000-0000-0000-000000000020"), "OpenAI", "gpt-4"),
                SupportedModel(Guid.Parse("00000000-0000-0000-0000-000000000030"), "OpenAI", "gpt-5"));
            await dbContext.SaveChangesAsync();
        }

        var result = await new EfPromptGallerySearchDriver(factory).SearchAsync(new PromptGalleryQuery(
            Text: "NEEDLE",
            Tags: ["architecture", "QUALITY"],
            Kind: PromptGalleryItemKind.Part,
            Status: PromptArtifactStatus.Final,
            Provider: "openai",
            Model: "GPT-5",
            PageSize: 10));

        var item = Assert.Single(result.Items);
        Assert.Equal(matchingId, item.Id);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(["architecture", "quality"], item.Tags);
        Assert.Equal(new PromptProviderModel("OpenAI", "gpt-5"), Assert.Single(item.SupportedModels));
    }

    [Fact]
    public async Task Ef_driver_returns_stable_pages_and_total_count()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(nameof(Ef_driver_returns_stable_pages_and_total_count));
        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.AddRange(
                CreateArtifact(Guid.Parse("00000000-0000-0000-0000-000000000003"), "Beta", "", PromptGalleryItemKind.FullPrompt),
                CreateArtifact(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Alpha", "", PromptGalleryItemKind.FullPrompt),
                CreateArtifact(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Alpha", "", PromptGalleryItemKind.FullPrompt));
            await dbContext.SaveChangesAsync();
        }

        var driver = new EfPromptGallerySearchDriver(factory);
        var first = await driver.SearchAsync(new PromptGalleryQuery(PageSize: 2));
        var second = await driver.SearchAsync(new PromptGalleryQuery(PageIndex: 1, PageSize: 2));

        Assert.Equal(3, first.TotalCount);
        Assert.Equal(3, second.TotalCount);
        Assert.Equal(2, first.Items.Count);
        Assert.Single(second.Items);
        Assert.Equal(
            [
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Guid.Parse("00000000-0000-0000-0000-000000000003")
            ],
            first.Items.Concat(second.Items).Select(item => item.Id));
    }

    [Fact]
    public async Task Ef_driver_keeps_unrestricted_items_when_provider_and_model_are_filtered()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(
            nameof(Ef_driver_keeps_unrestricted_items_when_provider_and_model_are_filtered));
        var unrestrictedId = Guid.Parse("00000000-0000-0000-0000-000000000040");
        var incompatibleId = Guid.Parse("00000000-0000-0000-0000-000000000050");
        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.AddRange(
                CreateArtifact(unrestrictedId, "Unrestricted", "", PromptGalleryItemKind.Part),
                CreateArtifact(incompatibleId, "Restricted", "", PromptGalleryItemKind.Part));
            dbContext.Add(SupportedModel(incompatibleId, "OpenAI", "gpt-4"));
            await dbContext.SaveChangesAsync();
        }

        var result = await new EfPromptGallerySearchDriver(factory).SearchAsync(new PromptGalleryQuery(
            Provider: "OpenAI",
            Model: "gpt-5",
            PageSize: 10));

        var item = Assert.Single(result.Items);
        Assert.Equal(unrestrictedId, item.Id);
        Assert.Equal("body", item.ContentPreview);
    }

    [Fact]
    public async Task Ef_driver_filters_and_orders_favorites_before_stable_secondary_order()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(
            nameof(Ef_driver_filters_and_orders_favorites_before_stable_secondary_order));
        var favoriteId = Guid.Parse("00000000-0000-0000-0000-000000000060");
        var ordinaryId = Guid.Parse("00000000-0000-0000-0000-000000000070");
        await using (var dbContext = factory.CreateDbContext())
        {
            var favorite = CreateArtifact(favoriteId, "Favorite", "", PromptGalleryItemKind.Part);
            favorite.IsFavorite = true;
            favorite.UpdatedAtUtc = DateTimeOffset.UnixEpoch;
            var ordinary = CreateArtifact(ordinaryId, "Ordinary", "", PromptGalleryItemKind.Part);
            ordinary.UpdatedAtUtc = DateTimeOffset.UnixEpoch.AddDays(1);
            dbContext.AddRange(favorite, ordinary);
            await dbContext.SaveChangesAsync();
        }

        var driver = new EfPromptGallerySearchDriver(factory);
        var all = await driver.SearchAsync(new PromptGalleryQuery(PageSize: 10));
        var favorites = await driver.SearchAsync(new PromptGalleryQuery(PageSize: 10, FavoritesOnly: true));

        Assert.Equal([favoriteId, ordinaryId], all.Items.Select(item => item.Id));
        var favoriteItem = Assert.Single(favorites.Items);
        Assert.Equal(favoriteId, favoriteItem.Id);
        Assert.True(favoriteItem.IsFavorite);
    }

    private static PromptArtifact CreateArtifact(
        Guid id,
        string title,
        string summary,
        PromptGalleryItemKind kind,
        bool archived = false)
        => new()
        {
            Id = id,
            Title = title,
            Summary = summary,
            Kind = kind,
            Status = PromptArtifactStatus.Final,
            CurrentDraftText = "body",
            SearchText = string.Join('\n', title, summary, "body").ToUpperInvariant(),
            CurrentVersionNumber = 1,
            IsArchived = archived,
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch
        };

    private static PromptSupportedProviderModel SupportedModel(Guid artifactId, string provider, string model)
        => new()
        {
            PromptArtifactId = artifactId,
            Provider = provider,
            Model = model,
            ProviderKey = provider.ToUpperInvariant(),
            ModelKey = model.ToUpperInvariant()
        };
}
