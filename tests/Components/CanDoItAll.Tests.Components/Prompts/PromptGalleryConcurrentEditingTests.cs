using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Prompts.UI.Editor;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Prompts;

public sealed class PromptGalleryConcurrentEditingTests {
    [Fact]
    public async Task A_hundred_editor_review_close_cycles_release_subscriptions_and_drafts() {
        var id = Guid.NewGuid();
        var feed = new EditingChangeFeed();
        var changes = PromptGalleryObservationTestSupport.Create(feed);
        var drafts = new List<WeakReference>();
        for (var index = 0; index < 100; index++) {
            drafts.Add(await OpenReviewAndCloseAsync());
            Assert.Equal(0, feed.ActiveSubscriptions);
            Assert.Equal(0, feed.ActiveTopics);
        }
        // Leave the final render's continuation before checking collectibility.
        await Task.Yield();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.All(drafts, draft => Assert.False(draft.IsAlive));

        async Task<WeakReference> OpenReviewAndCloseAsync() {
            await using var context = CreateContext(new(), changes);
            var cut = context.Render<PromptGalleryItemEditorHost>(parameters => parameters.Add(component => component.ItemId, id));
            var draft = new WeakReference(cut.Instance.Surface!.CurrentForm!);
            cut.Find("[data-testid='prompt-gallery-editor-summary']").Input("Owned draft for lifetime proof");
            Assert.Equal(1, feed.ActiveSubscriptions);
            var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Services.GetRequiredService<NotificationService>().Notification += _ => notified.TrySetResult();
            changes.Publish(id, EditingChangeKind.EditableContentChanged);
            await notified.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await cut.InvokeAsync(() => cut.Find("[data-testid='prompt-gallery-editor-review']").ClickAsync(new()));
            await cut.InvokeAsync(() => cut.Find("[data-testid='prompt-merge-cancel']").ClickAsync(new()));
            await cut.InvokeAsync(() => Assert.Empty(cut.FindAll("[data-testid='prompt-merge-dialog']")));
            Assert.Equal("Owned draft for lifetime proof", cut.Instance.Surface.CurrentForm!.Summary);
            await context.DisposeRenderedComponentsAsync();
            return draft;
        }
    }

    [Fact]
    public async Task Creation_identity_echo_keeps_the_new_exact_observation_alive() {
        var id = Guid.NewGuid();
        var feed = new EditingChangeFeed();
        var changes = PromptGalleryObservationTestSupport.Create(feed);
        var remote = ScriptedPromptGalleryService.Details(id);
        var gallery = new ScriptedPromptGalleryService {
            GetItem = _ => Task.FromResult(Result<PromptGalleryItemDetails>.Success(remote)),
            SaveDraft = draft => {
                remote = remote with { Title = draft.Title, Summary = draft.Summary, Kind = draft.Kind, Phase = draft.Phase,
                    DraftContent = draft.Content, Tags = draft.Tags!, SupportedModels = draft.SupportedModels!,
                    SupportedConsumers = draft.SupportedConsumers!, Recommendations = draft.Recommendations! };
                return Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new(id, remote.UpdatedAtUtc)));
            }
        };
        await using var context = CreateContext(gallery, changes);
        var cut = context.Render<PromptGalleryItemEditorHost>();
        cut.Find("[data-testid='prompt-gallery-editor-title']").Input("Created name");
        cut.Find("[data-testid='prompt-gallery-editor-content']").Input("Created content");
        await cut.Find("form").SubmitAsync();
        Assert.False(cut.Instance.Presentation.HasExternalChange);
        cut.Render(parameters => parameters.Add(component => component.ItemId, id));
        Assert.Equal(1, feed.ActiveSubscriptions);
        changes.Publish(id, EditingChangeKind.EditableContentChanged);
        cut.WaitForAssertion(() => Assert.True(cut.Instance.Presentation.HasExternalChange));
    }
    [Fact]
    public async Task A_new_unaddressed_draft_does_not_subscribe_to_other_items() {
        var feed = new EditingChangeFeed();
        var changes = PromptGalleryObservationTestSupport.Create(feed);
        var gallery = new ScriptedPromptGalleryService();
        await using var context = CreateContext(gallery, changes);
        var cut = context.Render<PromptGalleryItemEditorHost>();
        cut.Find("[data-testid='prompt-gallery-editor-title']").Input("Unrelated new draft");
        Assert.Equal(0, feed.ActiveSubscriptions);
        changes.Publish(Guid.NewGuid(), EditingChangeKind.EditableContentChanged);
        Assert.False(cut.Instance.Presentation.HasExternalChange);
        Assert.Equal("Unrelated new draft", cut.Instance.Surface!.CurrentForm!.Title);
        Assert.Empty(gallery.GetItemCalls);
    }
    [Fact]
    public async Task Invalid_raw_and_pending_inputs_survive_hint_cancel_and_patch_in_the_same_EditContext() {
        var id = Guid.NewGuid();
        var remote = ScriptedPromptGalleryService.Details(id);
        var gallery = new ScriptedPromptGalleryService { GetItem = _ => Task.FromResult(Result<PromptGalleryItemDetails>.Success(remote)) };
        var changes = PromptGalleryObservationTestSupport.Create();
        await using var context = CreateContext(gallery, changes);
        var cut = context.Render<PromptGalleryItemEditorHost>(parameters => parameters.Add(component => component.ItemId, id));
        var form = cut.Instance.Surface!.CurrentForm;
        var editContext = cut.Instance.Surface.CurrentEditContext;
        cut.Find("[data-testid='prompt-gallery-editor-summary']").Input("Local before blur");
        cut.Find("[data-testid='prompt-gallery-editor-temperature']").Input("-");
        cut.Find("[data-testid='prompt-gallery-editor-provider-to-add']").Input("Pending provider");
        cut.Find("[data-testid='prompt-gallery-editor-tag-input']").Input("Pending tag");
        remote = remote with { Title = "Current name", UpdatedAtUtc = remote.UpdatedAtUtc.AddSeconds(1) };
        changes.Publish(id, EditingChangeKind.EditableContentChanged);
        cut.WaitForAssertion(() => Assert.True(cut.Instance.Presentation.HasExternalChange));
        cut.WaitForElement("[data-testid='prompt-gallery-editor-review']").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='prompt-merge-review']")));
        cut.Find("[data-testid='prompt-merge-cancel']").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='prompt-merge-dialog']")));
        Assert.Equal("-", cut.Find("[data-testid='prompt-gallery-editor-temperature']").GetAttribute("value"));
        cut.WaitForElement("[data-testid='prompt-gallery-editor-review']").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='prompt-merge-review']")));
        cut.Find("[data-testid='prompt-merge-apply']").Click();
        cut.WaitForAssertion(() => Assert.Equal("Current name", form!.Title));
        Assert.Same(form, cut.Instance.Surface.CurrentForm);
        Assert.Same(editContext, cut.Instance.Surface.CurrentEditContext);
        Assert.NotEmpty(editContext!.GetValidationMessages());
        Assert.Equal("Local before blur", form!.Summary);
        Assert.Equal("Pending provider", cut.Instance.Surface.Capture()!.PendingProvider);
        Assert.Equal("Pending tag", cut.Instance.Surface.Capture()!.PendingTag);
        await cut.Find("form").SubmitAsync();
        Assert.Empty(gallery.SavedDrafts);
    }

    [Fact]
    public async Task Two_editors_of_one_principal_receive_each_other_but_suppress_the_exact_own_operation() {
        var id = Guid.NewGuid();
        var remote = ScriptedPromptGalleryService.Details(id);
        var changes = PromptGalleryObservationTestSupport.Create();
        var gallery = new ScriptedPromptGalleryService {
            GetItem = _ => Task.FromResult(Result<PromptGalleryItemDetails>.Success(remote)),
            SaveDraft = draft => {
                remote = remote with { Title = draft.Title, UpdatedAtUtc = remote.UpdatedAtUtc.AddSeconds(1) };
                changes.Publish(id, EditingChangeKind.EditableContentChanged);
                return Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new(id, remote.UpdatedAtUtc)));
            }
        };
        await using var context = CreateContext(gallery, changes);
        var a = context.Render<PromptGalleryItemEditorHost>(parameters => parameters.Add(component => component.ItemId, id));
        var b = context.Render<PromptGalleryItemEditorHost>(parameters => parameters.Add(component => component.ItemId, id));
        a.Find("[data-testid='prompt-gallery-editor-title']").Input("A accepted name");
        b.Find("[data-testid='prompt-gallery-editor-summary']").Input("B retained summary");
        await a.Find("form").SubmitAsync();
        b.WaitForAssertion(() => Assert.True(b.Instance.Presentation.HasExternalChange));
        Assert.False(a.Instance.Presentation.HasExternalChange);
        Assert.Equal("B retained summary", b.Instance.Surface!.CurrentForm!.Summary);
        Assert.Single(gallery.SavedDrafts);
    }

    [Fact]
    public async Task Third_editor_hint_disables_an_open_review_and_retry_keeps_local_input() {
        var id = Guid.NewGuid();
        var changes = PromptGalleryObservationTestSupport.Create();
        await using var context = CreateContext(new(), changes);
        var cut = context.Render<PromptGalleryItemEditorHost>(parameters => parameters.Add(component => component.ItemId, id));
        cut.Find("[data-testid='prompt-gallery-editor-summary']").Input("Local retained");
        changes.Publish(id, EditingChangeKind.EditableContentChanged);
        cut.WaitForAssertion(() => Assert.True(cut.Instance.Presentation.HasExternalChange));
        cut.WaitForElement("[data-testid='prompt-gallery-editor-review']").Click();
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='prompt-merge-apply']").HasAttribute("disabled")));
        changes.Publish(id, EditingChangeKind.LifecycleChanged);
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid='prompt-merge-apply']").HasAttribute("disabled")));
        cut.Find("[data-testid='prompt-merge-retry']").Click();
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='prompt-merge-apply']").HasAttribute("disabled")));
        Assert.Equal("Local retained", cut.Instance.Surface!.CurrentForm!.Summary);
    }

    [Fact]
    public async Task Repeated_close_requests_share_one_decision_and_continue_preserves_unblurred_input() {
        await using var context = CreateContext(new(), PromptGalleryObservationTestSupport.Create());
        var cut = context.Render<PromptGalleryItemEditorHost>();
        cut.Find("[data-testid='prompt-gallery-editor-title']").Input("Unblurred local name");
        Task<bool>? first = null;
        Task<bool>? second = null;
        await cut.InvokeAsync(() => {
            first = cut.Instance.RequestCloseAsync();
            second = cut.Instance.RequestCloseAsync();
        });
        Assert.Single(cut.FindAll("[data-testid='prompt-dirty-close-dialog']"));
        cut.Find("[data-testid='prompt-dirty-continue']").Click();
        Assert.False(await first!);
        Assert.False(await second!);
        Assert.Equal("Unblurred local name", cut.Instance.Surface!.CurrentForm!.Title);
    }

    [Fact]
    public async Task Revoked_grant_detaches_before_hint_delivery_and_hides_values_without_reading() {
        var id = Guid.NewGuid();
        var feed = new EditingChangeFeed();
        var changes = PromptGalleryObservationTestSupport.Create(feed);
        var access = new PromptGalleryUiTestAccess();
        var gallery = new ScriptedPromptGalleryService();
        await using var context = CreateContext(gallery, changes, access);
        var cut = context.Render<PromptGalleryItemEditorHost>(parameters => parameters.Add(component => component.ItemId, id));
        var reads = gallery.GetItemCalls.Count;
        access.Allowed = false;
        changes.Publish(id, EditingChangeKind.EditableContentChanged);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='prompt-gallery-access-retired']")));
        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-editor-title']"));
        Assert.Equal(reads, gallery.GetItemCalls.Count);
        Assert.Equal(0, feed.ActiveSubscriptions);
        Assert.DoesNotContain("Loaded prompt", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prompt_markup_in_review_is_escaped_and_never_becomes_an_element() {
        var id = Guid.NewGuid();
        var current = ScriptedPromptGalleryService.Details(id) with { DraftContent = "<script>secret()</script><img src=x onerror=secret()>" };
        var original = PromptGalleryEditorSource.FromDetails(current with { DraftContent = "Original safe content" });
        var form = PromptGalleryEditorForm.FromSource(original);
        var capture = form.Capture("", "", "", "", "");
        var review = new PromptMergeReview(original, capture, current);
        await using var context = CreateContext(new(), PromptGalleryObservationTestSupport.Create());
        var cut = context.Render<PromptMergeReviewSurface>(parameters => parameters.Add(component => component.Review, review));
        Assert.Empty(cut.FindAll("script"));
        Assert.Empty(cut.FindAll("img"));
        Assert.Contains("&lt;script&gt;", cut.Markup, StringComparison.Ordinal);
    }

    private static BunitContext CreateContext(ScriptedPromptGalleryService gallery, PromptGalleryChanges changes, PromptGalleryUiTestAccess? access = null) {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IPromptGalleryService>(gallery);
        PromptGalleryUiTestAccess.Register(context.Services, changes, access);
        return context;
    }
}
