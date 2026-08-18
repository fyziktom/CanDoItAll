using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class PromptGalleryChatComposerButtonTests
{
    [Fact]
    public async Task Selection_evaluates_chat_compatibility_and_emits_trimmed_content()
    {
        const string provider = "OpenAI";
        const string model = "gpt-5.4-mini";
        var gallery = new RecordingPromptGalleryService();
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IPromptGalleryService>(gallery);
        string? selectedContent = null;
        var cut = context.Render<PromptGalleryChatComposerButton>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, model)
            .Add(component => component.ContentSelected, content => selectedContent = content));
        var picker = cut.FindComponent<PromptGalleryPickerButton>();
        var selection = CreateSelection("  Keep the answer concise.  ");

        Assert.Equal(provider, picker.Instance.Provider);
        Assert.Equal(model, picker.Instance.Model);
        Assert.True(picker.Instance.ShowActualChatModelFilter);

        await cut.InvokeAsync(() => picker.Instance.Selected.InvokeAsync(selection));

        Assert.Equal(selection.ArtifactId, gallery.EvaluatedArtifactId);
        var compatibilityContext = Assert.IsType<PromptGalleryConsumerContext>(gallery.CompatibilityContext);
        Assert.Equal(PromptGalleryConsumer.Chat, compatibilityContext.Consumer);
        Assert.Equal(PromptGalleryCompatibilityPurpose.Selection, compatibilityContext.Purpose);
        Assert.Equal(provider, compatibilityContext.Provider);
        Assert.Equal(model, compatibilityContext.Model);
        Assert.Equal("Keep the answer concise.", selectedContent);
    }

    [Fact]
    public async Task Incompatible_selection_opens_dialog_and_cancel_does_not_emit_content()
    {
        var gallery = new RecordingPromptGalleryService
        {
            CompatibilityResult = new PromptCompatibilityResult(
            [
                new PromptCompatibilityIssue(
                    PromptCompatibilityIssueCode.ConsumerNotSupported,
                    PromptCompatibilitySeverity.Error,
                    "This Gallery item does not support chat.",
                    IsSuppressible: false,
                    IsSuppressed: false)
            ])
        };
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IPromptGalleryService>(gallery);
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        string? selectedContent = null;
        var cut = context.Render<PromptGalleryChatComposerButton>(parameters => parameters
            .Add(component => component.ContentSelected, content => selectedContent = content));
        var picker = cut.FindComponent<PromptGalleryPickerButton>();

        var selectionTask = cut.InvokeAsync(() =>
            picker.Instance.Selected.InvokeAsync(CreateSelection("Should not be inserted.")));

        host.WaitForElement("[data-testid='prompt-gallery-chat-compatibility-dialog']");
        var cancel = Assert.Single(
            host.FindAll("[data-testid='prompt-compatibility-warning-dialog'] button"));
        Assert.Contains("Cancel", cancel.TextContent, StringComparison.Ordinal);
        cancel.Click();
        await selectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Null(selectedContent);
        Assert.Empty(dialogService.Dialogs);
    }

    private static PromptGallerySelection CreateSelection(string content)
        => new(
            Guid.NewGuid(),
            VersionId: null,
            VersionNumber: null,
            "Reusable chat prompt",
            "A prompt for chat.",
            PromptGalleryItemKind.FullPrompt,
            content,
            Tags: [],
            SupportedModels: [],
            Recommendations: new PromptModelRecommendations());

    private sealed class RecordingPromptGalleryService : IPromptGalleryService
    {
        public PromptCompatibilityResult CompatibilityResult { get; init; } = new([]);

        public Guid? EvaluatedArtifactId { get; private set; }

        public PromptGalleryConsumerContext? CompatibilityContext { get; private set; }

        public Task<Result<PromptCompatibilityResult>> EvaluateCompatibilityAsync(
            Guid promptArtifactId,
            PromptGalleryConsumerContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EvaluatedArtifactId = promptArtifactId;
            CompatibilityContext = context;
            return Task.FromResult(Result<PromptCompatibilityResult>.Success(CompatibilityResult));
        }

        public Task<PromptGalleryPage<PromptGallerySearchItem>> SearchAsync(
            PromptGalleryQuery query,
            CancellationToken cancellationToken = default)
            => throw Unused();

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

        public Task<Result> SetWarningSuppressionAsync(
            Guid promptArtifactId,
            PromptGalleryConsumer consumer,
            PromptCompatibilityIssueCode issueCode,
            bool suppressed,
            CancellationToken cancellationToken = default)
            => throw Unused();

        private static NotSupportedException Unused()
            => new("This dependency member is not used by the composer button selection test.");
    }
}
