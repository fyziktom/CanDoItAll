using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class PromptGallerySearchListTests
{
    [Fact]
    public void Actual_chat_model_filter_uses_the_pinned_provider_model_and_can_show_all_prompts()
    {
        const string provider = "OpenAi";
        const string model = "gpt-5.4-mini";
        var gallery = new TestPromptGallery();
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IPromptGalleryService>(gallery);

        var cut = context.Render<PromptGallerySearchList>(parameters => parameters
            .Add(component => component.Consumer, PromptGalleryConsumer.Chat)
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, model)
            .Add(component => component.ShowActualChatModelFilter, true)
            .Add(component => component.Compact, true));

        cut.WaitForAssertion(() =>
        {
            var query = Assert.Single(gallery.Queries);
            Assert.Equal(provider, query.Provider);
            Assert.Equal(model, query.Model);
            Assert.Contains("Show actual chat model only", cut.Markup, StringComparison.Ordinal);
        });

        cut.Find("[data-testid='prompt-gallery-clear-filters']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, gallery.Queries.Count);
            Assert.Null(gallery.Queries[^1].Provider);
            Assert.Null(gallery.Queries[^1].Model);
        });

        cut.Find("[data-testid='prompt-gallery-actual-model-filter']").Change(true);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, gallery.Queries.Count);
            Assert.Equal(provider, gallery.Queries[^1].Provider);
            Assert.Equal(model, gallery.Queries[^1].Model);
        });

        cut.Find("[data-testid='prompt-gallery-actual-model-filter']").Change(false);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(4, gallery.Queries.Count);
            Assert.Null(gallery.Queries[^1].Provider);
            Assert.Null(gallery.Queries[^1].Model);
        });
    }

    [Fact]
    public void Desktop_filters_share_one_rail_and_item_title_and_favorite_are_explicit()
    {
        var gallery = new TestPromptGallery();
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IPromptGalleryService>(gallery);

        var cut = context.Render<PromptGallerySearchList>(parameters => parameters
            .Add(component => component.Compact, false)
            .Add(component => component.ShowSelectAction, false));

        cut.WaitForAssertion(() =>
        {
            var rail = cut.Find("[data-testid='prompt-gallery-filter-rail']");
            Assert.Contains("--cda-grid-columns:", rail.GetAttribute("style"), StringComparison.Ordinal);
            Assert.NotNull(rail.QuerySelector("[data-testid='prompt-gallery-search']"));
            Assert.NotNull(rail.QuerySelector("[data-testid='prompt-gallery-kind-filter']"));
            Assert.NotNull(rail.QuerySelector("[data-testid='prompt-gallery-status-filter']"));
            Assert.NotNull(rail.QuerySelector("[data-testid='prompt-gallery-tag-filter']"));
            Assert.NotNull(rail.QuerySelector("[data-testid='prompt-gallery-favorites-filter']"));
            Assert.Contains("cda-text-block--subtitle2", cut.Find("[data-testid='prompt-gallery-grid']").InnerHtml);
            Assert.Equal("false", cut.Find("[data-testid='prompt-gallery-favorite']").GetAttribute("aria-pressed"));
        });

        cut.Find("[data-testid='prompt-gallery-favorite']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(gallery.ItemId, gallery.FavoriteItemId);
            Assert.True(gallery.FavoriteValue);
            Assert.Equal("true", cut.Find("[data-testid='prompt-gallery-favorite']").GetAttribute("aria-pressed"));
        });
    }

    private sealed class TestPromptGallery : IPromptGalleryService
    {
        public Guid ItemId { get; } = Guid.NewGuid();

        public Guid? FavoriteItemId { get; private set; }

        public bool FavoriteValue { get; private set; }

        public List<PromptGalleryQuery> Queries { get; } = [];

        public Task<PromptGalleryPage<PromptGallerySearchItem>> SearchAsync(
            PromptGalleryQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            PromptGallerySearchItem item = new(
                ItemId,
                "Reusable architecture review",
                "Review boundaries.",
                "Inspect dependency direction and report violations.",
                PromptGalleryItemKind.Part,
                "design",
                PromptArtifactStatus.Final,
                IsArchived: false,
                CollectionName: null,
                Tags: ["architecture"],
                SupportedModels: [new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true)],
                Recommendations: new PromptModelRecommendations(),
                CurrentVersionNumber: 1,
                UpdatedAtUtc: DateTimeOffset.UnixEpoch,
                IsFavorite: FavoriteValue);
            return Task.FromResult(new PromptGalleryPage<PromptGallerySearchItem>([item], 0, query.PageSize, 1));
        }

        public Task<Result> SetFavoriteAsync(
            Guid promptArtifactId,
            bool favorite,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FavoriteItemId = promptArtifactId;
            FavoriteValue = favorite;
            return Task.FromResult(Result.Success());
        }

        public Task<Result<PromptGalleryItemDetails>> GetItemAsync(
            Guid promptArtifactId,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result<PromptDraftSaveReceipt>> SaveDraftAsync(
            PromptGalleryDraft draft,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result<PromptVersionSnapshot>> CreateVersionAsync(
            Guid promptArtifactId,
            PromptVersionCreateRequest request,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(
            Guid promptVersionId,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(
            Guid promptArtifactId,
            int versionNumber,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result<IReadOnlyList<PromptVersionSnapshot>>> GetVersionSnapshotsAsync(
            IReadOnlyCollection<Guid> promptVersionIds,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result<IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot>>> GetCompatibilitySnapshotsAsync(
            IReadOnlyCollection<Guid> promptArtifactIds,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result> ArchiveAsync(
            Guid promptArtifactId,
            bool archived,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result<PromptCompatibilityResult>> EvaluateCompatibilityAsync(
            Guid promptArtifactId,
            PromptGalleryConsumerContext context,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result> SetWarningSuppressionAsync(
            Guid promptArtifactId,
            PromptGalleryConsumer consumer,
            PromptCompatibilityIssueCode issueCode,
            bool suppressed,
            CancellationToken cancellationToken = default)
            => throw Unused();

        private static NotSupportedException Unused()
            => new("This dependency member is not used by the search-list rendering test.");
    }
}
