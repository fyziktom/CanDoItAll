using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Prompts.UI.Editor;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Prompts;

public sealed class PromptGalleryItemEditorHostTests
{
    [Fact]
    public async Task First_save_then_failed_re_read_then_second_save_updates_the_same_item()
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        var token = DateTimeOffset.UnixEpoch.AddHours(1);
        gallery.SaveDraft = draft => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(
            new PromptDraftSaveReceipt(draft.Id ?? savedId, token)));
        gallery.GetItem = _ => Task.FromException<Result<PromptGalleryItemDetails>>(new IOException("Re-read failed."));
        var commits = new List<PromptGalleryEditorCommit>();
        using var context = CreateContext(gallery);
        var cut = context.Render<PromptGalleryItemEditorHost>(parameters => parameters
            .Add(component => component.Committed, EventCallback.Factory.Create<PromptGalleryEditorCommit>(this, commits.Add)));

        cut.Find("[data-testid='prompt-gallery-editor-title']").Change("First save");
        cut.Find("[data-testid='prompt-gallery-editor-content']").Change("Body");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() => Assert.Equal(savedId, cut.Instance.CurrentTarget));
        Assert.Equal(new PromptGalleryEditorCommit(savedId, PromptGalleryEditorCommitKind.DraftSaved), Assert.Single(commits));
        Assert.NotNull(cut.Find("[data-testid='prompt-gallery-editor-warning']"));
        Assert.Equal("First save", cut.Find("[data-testid='prompt-gallery-editor-title']").GetAttribute("value"));
        Assert.Contains("Saved Gallery item", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='prompt-gallery-editor-title']").Change("Second save");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() => Assert.Equal(2, gallery.SavedDrafts.Count));
        Assert.Null(gallery.SavedDrafts[0].Id);
        Assert.Equal(savedId, gallery.SavedDrafts[1].Id);
        Assert.Equal(token, gallery.SavedDrafts[1].ExpectedUpdatedAtUtc);
        Assert.Equal("Second save", gallery.SavedDrafts[1].Title);
        Assert.Equal(2, commits.Count);
    }

    [Fact]
    public async Task Owner_echo_of_the_adopted_identity_neither_reloads_nor_resets_the_draft()
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        gallery.SaveDraft = _ => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(savedId, DateTimeOffset.UnixEpoch)));
        using var context = CreateContext(gallery);
        var cut = context.Render<PromptGalleryItemEditorHost>();

        cut.Find("[data-testid='prompt-gallery-editor-title']").Change("Echo proof");
        cut.Find("[data-testid='prompt-gallery-editor-content']").Change("Body");
        await cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => Assert.Equal(savedId, cut.Instance.CurrentTarget));
        var readsAfterSave = gallery.GetItemCalls.Count;
        cut.Find("[data-testid='prompt-gallery-editor-title']").Change("Unsaved edit");

        cut.Render(parameters => parameters.Add(component => component.ItemId, savedId));

        Assert.Equal(readsAfterSave, gallery.GetItemCalls.Count);
        Assert.Equal("Unsaved edit", cut.Find("[data-testid='prompt-gallery-editor-title']").GetAttribute("value"));
        Assert.Equal(savedId, cut.Instance.CurrentTarget);
    }

    [Fact]
    public async Task Finalize_partial_success_reports_the_saved_draft_and_the_failed_version()
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        gallery.SaveDraft = _ => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(savedId, DateTimeOffset.UnixEpoch)));
        gallery.CreateVersion = (_, _) => Task.FromResult(Result<PromptVersionSnapshot>.Failure(
            Error.Failure("Archived Gallery items cannot create new versions.", "prompts.version.artifact-archived")));
        var commits = new List<PromptGalleryEditorCommit>();
        using var context = CreateContext(gallery);
        var notifications = context.Services.GetRequiredService<NotificationService>();
        var cut = context.Render<PromptGalleryItemEditorHost>(parameters => parameters
            .Add(component => component.Committed, EventCallback.Factory.Create<PromptGalleryEditorCommit>(this, commits.Add)));

        cut.Find("[data-testid='prompt-gallery-editor-title']").Change("Finalize me");
        cut.Find("[data-testid='prompt-gallery-editor-content']").Change("Body");
        await cut.Find("[data-testid='prompt-gallery-editor-finalize']").ClickAsync();

        cut.WaitForAssertion(() => Assert.Equal(savedId, cut.Instance.CurrentTarget));
        Assert.Equal(new PromptGalleryEditorCommit(savedId, PromptGalleryEditorCommitKind.DraftSaved), Assert.Single(commits));
        Assert.DoesNotContain(notifications.Messages, message => message.Summary == "Prompt draft saved");
        var failure = Assert.Single(notifications.Messages, message => message.Summary == "Final version was not created");
        Assert.Contains("The draft was saved, but", failure.Detail, StringComparison.Ordinal);
        Assert.False(cut.Instance.Presentation.IsBusy);
        Assert.NotNull(cut.Find("[data-testid='prompt-gallery-item-editor']"));
    }

    [Fact]
    public async Task Two_editor_instances_keep_independent_drafts_and_identities()
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        gallery.SaveDraft = _ => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(savedId, DateTimeOffset.UnixEpoch)));
        using var context = CreateContext(gallery);
        var first = context.Render<PromptGalleryItemEditorHost>();
        var second = context.Render<PromptGalleryItemEditorHost>();

        first.Find("[data-testid='prompt-gallery-editor-title']").Change("First draft");
        first.Find("[data-testid='prompt-gallery-editor-content']").Change("Body");
        second.Find("[data-testid='prompt-gallery-editor-title']").Change("Second draft");
        await first.Find("form").SubmitAsync();

        first.WaitForAssertion(() => Assert.Equal(savedId, first.Instance.CurrentTarget));
        Assert.Null(second.Instance.CurrentTarget);
        Assert.Equal("Second draft", second.Find("[data-testid='prompt-gallery-editor-title']").GetAttribute("value"));
        Assert.Equal(PromptGalleryEditorPhase.New, second.Instance.Presentation.Phase);
        Assert.Single(gallery.SavedDrafts);
    }

    [Fact]
    public void Missing_item_shows_a_failed_state_with_retry_rather_than_a_new_draft()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        gallery.GetItem = _ => Task.FromResult(Result<PromptGalleryItemDetails>.Failure(
            Error.Failure("Prompt Gallery item was not found.", "prompts.gallery.not-found")));
        using var context = CreateContext(gallery);

        var cut = context.Render<PromptGalleryItemEditorHost>(parameters => parameters.Add(component => component.ItemId, itemId));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-editor-retry']")));
        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-item-editor']"));
        Assert.Equal(itemId, cut.Instance.CurrentTarget);
        Assert.Contains("was not found", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='prompt-gallery-editor-retry']").Click();
        cut.WaitForAssertion(() => Assert.Equal(2, gallery.GetItemCalls.Count));
    }

    [Fact]
    public async Task Foreign_change_before_an_own_archive_is_surfaced_as_a_conflict_and_reload_accepts_the_latest_revision()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        var loaded = DateTimeOffset.UnixEpoch.AddHours(1);
        var foreign = loaded.AddMinutes(5);
        var archived = foreign.AddMinutes(1);
        var foreignContent = new PromptGalleryEditorSubmission(
            "Changed by someone else",
            "Foreign summary",
            PromptGalleryItemKind.FullPrompt,
            "workflow",
            "Foreign content",
            ["workflow"],
            [new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true)],
            [PromptGalleryConsumer.Workflow],
            new PromptModelRecommendations(0.2, 800, 0.9));
        var persistedToken = loaded;
        var persistedContent = (PromptGalleryEditorSubmission?)null;
        var persistedArchived = false;
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(
            ScriptedPromptGalleryService.Details(id, persistedToken, isArchived: persistedArchived, content: persistedContent)));
        gallery.Archive = (_, value) =>
        {
            persistedArchived = value;
            persistedToken = archived;
            return Task.FromResult(Result.Success());
        };
        gallery.SaveDraft = draft => draft.ExpectedUpdatedAtUtc == persistedToken
            ? Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(itemId, persistedToken.AddMinutes(1))))
            : Task.FromResult(Result<PromptDraftSaveReceipt>.Failure(Error.Failure("The item changed elsewhere.", "prompts.gallery.concurrency-conflict")));
        using var context = CreateContext(gallery);
        var notifications = context.Services.GetRequiredService<NotificationService>();
        var cut = context.Render<PromptGalleryItemEditorHost>(parameters => parameters.Add(component => component.ItemId, itemId));
        cut.WaitForAssertion(() => Assert.Equal("Loaded prompt", cut.Find("[data-testid='prompt-gallery-editor-title']").GetAttribute("value")));
        cut.Find("[data-testid='prompt-gallery-editor-title']").Change("My unsaved edit");

        // Someone else saves a newer revision, then this editor archives: the read-back carries foreign content.
        persistedToken = foreign;
        persistedContent = foreignContent;
        await cut.Find("[data-testid='prompt-gallery-editor-archive']").ClickAsync();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-editor-conflict']")));
        Assert.Equal("My unsaved edit", cut.Find("[data-testid='prompt-gallery-editor-title']").GetAttribute("value"));
        Assert.Contains("Restore", cut.Find("[data-testid='prompt-gallery-editor-archive']").TextContent, StringComparison.Ordinal);

        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() => Assert.Single(gallery.SavedDrafts));
        Assert.Equal(loaded, gallery.SavedDrafts[0].ExpectedUpdatedAtUtc);
        Assert.Contains(notifications.Messages, message => message.Summary == "Prompt draft was not saved");
        Assert.NotNull(cut.Find("[data-testid='prompt-gallery-editor-conflict']"));

        cut.Find("[data-testid='prompt-gallery-editor-reload']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Changed by someone else", cut.Find("[data-testid='prompt-gallery-editor-title']").GetAttribute("value")));
        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-editor-conflict']"));
        cut.Find("[data-testid='prompt-gallery-editor-title']").Change("Merged edit");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() => Assert.Equal(2, gallery.SavedDrafts.Count));
        Assert.Equal(archived, gallery.SavedDrafts[1].ExpectedUpdatedAtUtc);
        Assert.Equal("Merged edit", gallery.SavedDrafts[1].Title);
        Assert.Contains(notifications.Messages, message => message.Summary == "Prompt draft saved");
    }

    private static BunitContext CreateContext(ScriptedPromptGalleryService gallery)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IPromptGalleryService>(gallery);
        return context;
    }
}
