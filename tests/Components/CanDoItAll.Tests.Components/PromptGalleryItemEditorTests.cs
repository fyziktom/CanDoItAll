using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class PromptGalleryItemEditorTests
{
    [Fact]
    public void Existing_item_renders_supported_model_and_version_metadata()
    {
        var itemId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var item = new PromptGalleryItemDetails(
            itemId,
            ProjectId: null,
            CollectionId: null,
            "Reusable prompt",
            "Reusable summary",
            PromptGalleryItemKind.FullPrompt,
            "workflow",
            PromptArtifactStatus.Final,
            IsArchived: false,
            "Prompt content",
            CurrentVersionNumber: 1,
            Tags: ["workflow"],
            TemplateTokens: [],
            SupportedModels: [new PromptProviderModel("OpenAI", "gpt-5.4-mini")],
            SupportedConsumers: [PromptGalleryConsumer.Workflow],
            WarningSuppressions: [],
            new PromptModelRecommendations(0.1, 1_400, 0.9),
            new PromptGallerySourceInfo(
                PromptArtifactProvenance.User,
                Catalog: null,
                Key: null,
                GroupKey: null,
                GroupName: null,
                ItemKind: null,
                OrderIndex: null),
            Versions: [new PromptGalleryVersionInfo(versionId, 1, "Ready for reuse", "Markdown", now)],
            now,
            now);

        using var context = new TestContext();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IPromptGalleryService>(new TestPromptGallery(item));

        var cut = context.RenderComponent<PromptGalleryItemEditor>(parameters => parameters
            .Add(component => component.ItemId, itemId));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='prompt-gallery-item-editor']"));
            Assert.Contains("gpt-5.4-mini", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Ready for reuse", cut.Markup, StringComparison.Ordinal);
        });
    }

    private sealed class TestPromptGallery(PromptGalleryItemDetails item) : IPromptGalleryService
    {
        public Task<Result<PromptGalleryItemDetails>> GetItemAsync(
            Guid promptArtifactId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(item.Id, promptArtifactId);
            return Task.FromResult(Result<PromptGalleryItemDetails>.Success(item));
        }

        public Task<PromptGalleryPage<PromptGallerySearchItem>> SearchAsync(
            PromptGalleryQuery query,
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

        public Task<Result> SetFavoriteAsync(
            Guid promptArtifactId,
            bool favorite,
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
            => new("This member is not used by the editor rendering test.");
    }
}
