using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Modules.Prompts.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Prompts;

public sealed class PromptGalleryPageTests
{
    [Fact]
    public void Curator_context_is_active_before_chat_opens_and_is_released_with_the_page()
    {
        var launcher = new RecordingCuratorLauncher();
        using var context = CreateContext(new ScriptedPromptGalleryService(), launcher);

        var cut = context.Render<PromptGalleryPage>();
        var button = cut.WaitForElement("[data-testid='prompt-gallery-prompts-curator-open']");

        Assert.Equal($"Open {launcher.Presentation.Name}", button.GetAttribute("aria-label"));
        Assert.DoesNotContain(launcher.Presentation.Name, button.TextContent, StringComparison.Ordinal);
        var avatarImageUrl = Assert.IsType<string>(launcher.Presentation.AvatarImageUrl);
        Assert.Contains(avatarImageUrl, button.InnerHtml, StringComparison.Ordinal);
        Assert.Equal(1, launcher.ActivateCount);
        Assert.Equal(1, launcher.ContextLease.SynchronizeCount);
        Assert.Equal(1, launcher.PresentationCount);
        Assert.Equal(["activate", "synchronize", "presentation"], launcher.Events);

        var tooltipTarget = Assert.IsAssignableFrom<AngleSharp.Dom.IElement>(button.ParentElement);
        tooltipTarget.TriggerEvent("onmouseenter", new MouseEventArgs { ClientX = 120, ClientY = 80 });

        var tooltip = context.Services.GetRequiredService<TooltipService>().Current;
        Assert.Equal($"Open {launcher.Presentation.Name}", tooltip?.Text);
        Assert.Equal(TooltipPosition.Bottom, tooltip?.Options.Position);
        Assert.Equal("prompt-gallery-prompts-curator-tooltip", tooltip?.Options.TestId);

        button.Click();

        cut.WaitForAssertion(() => Assert.Equal(1, launcher.OpenCount));
        Assert.Equal(["activate", "synchronize", "presentation", "open"], launcher.Events);
        Assert.Contains(
            context.Services.GetRequiredService<NotificationService>().Messages,
            notification => notification.Summary == "Chat ready");

        cut.Render();

        Assert.Equal(1, launcher.ActivateCount);
        Assert.Equal(2, launcher.ContextLease.SynchronizeCount);
        Assert.Equal(1, launcher.PresentationCount);

        cut.Instance.Dispose();

        Assert.Equal(1, launcher.ContextLease.DisposeCount);
    }

    [Fact]
    public void Curator_action_remains_visible_and_retries_a_transient_presentation_failure()
    {
        var launcher = new RecordingCuratorLauncher
        {
            RemainingPresentationFailures = 1
        };
        using var context = CreateContext(new ScriptedPromptGalleryService(), launcher);

        var cut = context.Render<PromptGalleryPage>();

        cut.WaitForAssertion(() => Assert.Equal(1, launcher.PresentationCount));
        var button = cut.Find("[data-testid='prompt-gallery-prompts-curator-open']");
        Assert.False(button.HasAttribute("disabled"));
        Assert.Equal("Open Prompts Curator Agent", button.GetAttribute("aria-label"));
        Assert.Equal(0, launcher.OpenCount);

        button.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, launcher.PresentationCount);
            Assert.Equal(1, launcher.OpenCount);
            Assert.Equal(
                $"Open {launcher.Presentation.Name}",
                cut.Find("[data-testid='prompt-gallery-prompts-curator-open']").GetAttribute("aria-label"));
        });
    }

    [Fact]
    public void Curator_context_activation_failure_does_not_block_the_gallery()
    {
        var gallery = new ScriptedPromptGalleryService();
        var launcher = new RecordingCuratorLauncher { ActivationFailure = new InvalidOperationException("Registry offline.") };
        using var context = CreateContext(gallery, launcher);
        var notifications = context.Services.GetRequiredService<NotificationService>();

        var cut = context.Render<PromptGalleryPage>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-grid']")));
        Assert.Single(gallery.Queries);
        Assert.Contains(notifications.Messages, message => message.Summary == "Unable to prepare Prompts Curator");
        cut.Render();
        Assert.Equal(1, launcher.ActivateCount);
        Assert.Single(notifications.Messages, message => message.Summary == "Unable to prepare Prompts Curator");
    }

    [Fact]
    public void PromptId_request_reopens_after_null_and_after_a_missing_item_but_not_on_an_echo()
    {
        var gallery = new ScriptedPromptGalleryService();
        var knownId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        gallery.GetItem = id => Task.FromResult(id == knownId
            ? Result<PromptGalleryItemDetails>.Success(ScriptedPromptGalleryService.Details(id))
            : Result<PromptGalleryItemDetails>.Failure(Error.Failure("Prompt Gallery item was not found.", "prompts.gallery.not-found")));
        using var context = CreateContext(gallery, new UnavailableCuratorLauncher());
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/prompt-gallery?promptId={knownId:D}");

        var cut = context.Render<PromptGalleryPage>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-editor-title']")));
        Assert.Equal([knownId], gallery.GetItemCalls);

        cut.Find("[data-testid='prompt-gallery-editor-cancel']").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-item-editor']")));

        // An echo of the same request on rerender does not reopen the closed editor.
        cut.Render();
        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-item-editor']"));

        navigation.NavigateTo("/prompt-gallery");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-item-editor']")));

        navigation.NavigateTo($"/prompt-gallery?promptId={knownId:D}");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-editor-title']")));
        Assert.Equal([knownId, knownId], gallery.GetItemCalls);

        // A missing item is a failed target with its own retry, never a new empty draft.
        navigation.NavigateTo($"/prompt-gallery?promptId={missingId:D}");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-editor-retry']")));
        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-item-editor']"));
        Assert.Equal(missingId, cut.FindComponent<PromptGalleryItemEditorHost>().Instance.CurrentTarget);

        navigation.NavigateTo($"/prompt-gallery?promptId={knownId:D}");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-editor-title']")));
        Assert.Equal(knownId, cut.FindComponent<PromptGalleryItemEditorHost>().Instance.CurrentTarget);
    }

    [Fact]
    public async Task Editor_commit_refreshes_the_list_in_place_and_preserves_filters()
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        gallery.SaveDraft = _ => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(savedId, DateTimeOffset.UnixEpoch)));
        using var context = CreateContext(gallery, new UnavailableCuratorLauncher());
        var cut = context.Render<PromptGalleryPage>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-grid']")));
        var searchHost = cut.FindComponent<PromptGallerySearchHost>().Instance;

        cut.Find("[data-testid='prompt-gallery-kind-filter']").Change("Part");
        cut.WaitForAssertion(() => Assert.Equal(2, gallery.Queries.Count));

        cut.Find("button[aria-label='New item']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-editor-title']")));
        cut.Find("[data-testid='prompt-gallery-editor-title']").Change("Created from page");
        cut.Find("[data-testid='prompt-gallery-editor-content']").Change("Body");
        await cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => Assert.Equal(savedId, cut.FindComponent<PromptGalleryItemEditorHost>().Instance.CurrentTarget));
        Assert.Contains("Edit Gallery item", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(2, gallery.Queries.Count);

        cut.Find("[data-testid='prompt-gallery-editor-cancel']").Click();

        cut.WaitForAssertion(() => Assert.Equal(3, gallery.Queries.Count));
        Assert.Equal(PromptGalleryItemKind.Part, gallery.Queries[^1].Kind);
        Assert.Same(searchHost, cut.FindComponent<PromptGallerySearchHost>().Instance);
        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-item-editor']"));
    }

    [Fact]
    public void Cancelling_an_untouched_editor_does_not_reload_the_list()
    {
        var gallery = new ScriptedPromptGalleryService();
        using var context = CreateContext(gallery, new UnavailableCuratorLauncher());
        var cut = context.Render<PromptGalleryPage>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-edit']")));

        cut.Find("[data-testid='prompt-gallery-edit']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-editor-title']")));
        cut.Find("[data-testid='prompt-gallery-editor-cancel']").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-item-editor']")));
        Assert.Single(gallery.Queries);
    }

    private static BunitContext CreateContext(ScriptedPromptGalleryService gallery, IPromptGalleryCuratorLauncher launcher)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IPromptGalleryService>(gallery);
        context.Services.AddSingleton(launcher);
        return context;
    }

    private sealed class UnavailableCuratorLauncher : IPromptGalleryCuratorLauncher
    {
        public bool IsAvailable => false;

        public IPromptGalleryCuratorContextLease ActivateContext() => throw new InvalidOperationException("Unavailable.");

        public Task<PromptGalleryCuratorPresentation> GetPresentationAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unavailable.");

        public Task OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unavailable.");
    }

    private sealed class RecordingCuratorLauncher : IPromptGalleryCuratorLauncher
    {
        public bool IsAvailable => true;

        public int ActivateCount { get; private set; }

        public int OpenCount { get; private set; }

        public int PresentationCount { get; private set; }

        public int RemainingPresentationFailures { get; set; }

        public Exception? ActivationFailure { get; init; }

        public PromptGalleryCuratorPresentation Presentation { get; } = new(
            "Canonical Prompts Curator",
            "/images/agents/canonical-prompts-curator.png");

        public RecordingCuratorContextLease ContextLease { get; } = new();

        public List<string> Events { get; } = [];

        public IPromptGalleryCuratorContextLease ActivateContext()
        {
            ActivateCount++;
            Events.Add("activate");
            if (ActivationFailure is not null)
            {
                throw ActivationFailure;
            }

            ContextLease.Synchronized = () => Events.Add("synchronize");
            return ContextLease;
        }

        public Task<PromptGalleryCuratorPresentation> GetPresentationAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PresentationCount++;
            Events.Add("presentation");
            if (RemainingPresentationFailures > 0)
            {
                RemainingPresentationFailures--;
                throw new InvalidOperationException("Transient curator presentation failure.");
            }

            return Task.FromResult(Presentation);
        }

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            Events.Add("open");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCuratorContextLease : IPromptGalleryCuratorContextLease
    {
        public int SynchronizeCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Action? Synchronized { get; set; }

        public void SynchronizeNavigation()
        {
            SynchronizeCount++;
            Synchronized?.Invoke();
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
