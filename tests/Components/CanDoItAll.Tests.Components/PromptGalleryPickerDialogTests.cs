using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class PromptGalleryPickerDialogTests
{
    [Fact]
    public async Task Edit_requested_override_closes_picker_before_invoking_callback()
    {
        var gallery = new TestPromptGallery();
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IPromptGalleryService>(gallery);

        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        Guid? receivedItemId = null;
        var dialogCountDuringCallback = -1;
        var pickerWasCompletedDuringCallback = false;
        Task<object?>? pickerTask = null;
        var editRequested = EventCallback.Factory.Create<Guid>(
            this,
            (Guid itemId) =>
            {
                receivedItemId = itemId;
                dialogCountDuringCallback = dialogService.Dialogs.Count;
                pickerWasCompletedDuringCallback = pickerTask?.IsCompleted is true;
            });

        pickerTask = dialogService.OpenAsync<PromptGalleryPickerDialog>(
            "Choose a prompt",
            new Dictionary<string, object?>
            {
                [nameof(PromptGalleryPickerDialog.Consumer)] = PromptGalleryConsumer.Workflow,
                [nameof(PromptGalleryPickerDialog.EditRequested)] = editRequested
            },
            new DialogOptions { TestId = "prompt-gallery-picker-dialog" });

        host.WaitForAssertion(() => Assert.NotNull(host.Find("[data-testid='prompt-gallery-edit']")));

        host.Find("[data-testid='prompt-gallery-edit']").Click();
        await pickerTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(gallery.ItemId, receivedItemId);
        Assert.Equal(0, dialogCountDuringCallback);
        Assert.True(pickerWasCompletedDuringCallback);
        Assert.Empty(dialogService.Dialogs);
    }

    private sealed class TestPromptGallery : IPromptGalleryService
    {
        public Guid ItemId { get; } = Guid.NewGuid();

        public Task<PromptGalleryPage<PromptGallerySearchItem>> SearchAsync(
            PromptGalleryQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PromptGallerySearchItem item = new(
                ItemId,
                "Reusable workflow prompt",
                "Prompt details",
                "Use this prompt in a workflow.",
                PromptGalleryItemKind.FullPrompt,
                "workflow",
                PromptArtifactStatus.Final,
                IsArchived: false,
                CollectionName: null,
                Tags: ["workflow"],
                SupportedModels: [],
                Recommendations: new PromptModelRecommendations(),
                CurrentVersionNumber: 1,
                UpdatedAtUtc: DateTimeOffset.UnixEpoch,
                IsFavorite: false);
            return Task.FromResult(new PromptGalleryPage<PromptGallerySearchItem>([item], 0, query.PageSize, 1));
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
            => new("This dependency member is not used by the picker edit-override test.");
    }
}
