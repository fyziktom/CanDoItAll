using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Prompts.UI.Editor;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Prompts;

public sealed class PromptGalleryConcurrentReviewTests {
    [Fact]
    public async Task Reading_R2_preserves_review_work_and_requires_a_new_choice_if_R2_changes_the_same_field() {
        var id = Guid.NewGuid();
        var remote = ScriptedPromptGalleryService.Details(id);
        var gallery = new ScriptedPromptGalleryService { GetItem = _ => Task.FromResult(Result<PromptGalleryItemDetails>.Success(remote)) };
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(id);
        var form = PromptGalleryEditorForm.FromSource(session.Presentation.Source!);
        form.Summary = "Parent local draft";
        var local = Capture(form);
        await session.ReadReviewAsync(local);
        session.Review!.Fields.Summary.SetResult("Work typed in review");
        remote = remote with { Summary = "Third editor summary", UpdatedAtUtc = remote.UpdatedAtUtc.AddSeconds(1) };
        await session.ObserveAsync(true);
        await session.ReadReviewAsync(local);
        Assert.Equal("Work typed in review", session.Review!.Fields.Summary.Local);
        Assert.False(session.Review.Fields.Summary.IsResolved);
        Assert.Equal("Parent local draft", session.Review.OriginalLocal.Values.Summary);
        await session.CancelReviewAsync();
        Assert.Equal(DateTimeOffset.UnixEpoch, session.AcceptedToken);
        Assert.Equal("Parent local draft", form.Summary);
        Assert.Empty(gallery.SavedDrafts);
    }
    [Fact]
    public async Task Apply_combines_B_L_R_in_the_same_draft_and_adopts_R_without_saving() {
        var id = Guid.NewGuid();
        var remote = ScriptedPromptGalleryService.Details(id);
        var gallery = new ScriptedPromptGalleryService { GetItem = _ => Task.FromResult(Result<PromptGalleryItemDetails>.Success(remote)) };
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(id);
        var seed = session.Presentation.Source!;
        var form = PromptGalleryEditorForm.FromSource(seed);
        form.Summary = "My local summary";
        form.Touch(PromptDraftField.Summary);
        var local = Capture(form) with { Temperature = "-" };
        remote = remote with { Title = "Foreign name", UpdatedAtUtc = remote.UpdatedAtUtc.AddSeconds(1) };
        await session.ObserveAsync(true);
        await session.ReadReviewAsync(local);
        var review = session.Review!;

        Assert.True(await session.ApplyReviewAsync(review, form.Revision, patch => form.Patch(patch.Values)));

        Assert.Same(seed, session.Presentation.Source);
        Assert.Equal("Foreign name", form.Title);
        Assert.Equal("My local summary", form.Summary);
        Assert.Equal("-", review.Fields.Result().Temperature);
        Assert.Equal(remote.UpdatedAtUtc, session.AcceptedToken);
        Assert.Empty(gallery.SavedDrafts);
        Assert.True(form.IsDirty);
        Assert.False(session.Presentation.HasExternalChange);
    }

    [Fact]
    public async Task Draft_ABA_and_target_replacement_reject_old_review_without_patching() {
        var id = Guid.NewGuid();
        var gallery = new ScriptedPromptGalleryService();
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(id);
        var form = PromptGalleryEditorForm.FromSource(session.Presentation.Source!);
        var local = Capture(form);
        await session.ReadReviewAsync(local);
        var review = session.Review!;
        form.Touch(PromptDraftField.Title);
        form.Touch(PromptDraftField.Title);
        Assert.False(await session.ApplyReviewAsync(review, form.Revision, _ => throw new InvalidOperationException("Must not patch.")));
        await session.SetTargetAsync(Guid.NewGuid());
        Assert.False(await session.ApplyReviewAsync(review, local.Revision, _ => throw new InvalidOperationException("Must not patch.")));
        Assert.Empty(gallery.SavedDrafts);
    }

    [Fact]
    public async Task Hint_during_read_invalidates_Apply_and_retry_uses_a_new_pre_read_witness() {
        var gallery = new ScriptedPromptGalleryService();
        var id = Guid.NewGuid();
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(id);
        var local = Capture(PromptGalleryEditorForm.FromSource(session.Presentation.Source!));
        var pending = new TaskCompletionSource<Result<PromptGalleryItemDetails>>(TaskCreationOptions.RunContinuationsAsynchronously);
        gallery.GetItem = _ => pending.Task;
        var reading = session.ReadReviewAsync(local);
        await session.ObserveAsync(true);
        pending.SetResult(Result<PromptGalleryItemDetails>.Success(ScriptedPromptGalleryService.Details(id)));
        await reading;
        Assert.False(session.IsReviewCurrent);
        gallery.GetItem = value => Task.FromResult(Result<PromptGalleryItemDetails>.Success(ScriptedPromptGalleryService.Details(value)));
        await session.ReadReviewAsync(local);
        Assert.True(session.IsReviewCurrent);
        await session.ObserveAsync(false);
        Assert.True(session.IsReviewCurrent);
    }

    [Fact]
    public async Task Cancel_and_failed_current_read_keep_original_token_source_and_raw_local_capture() {
        var gallery = new ScriptedPromptGalleryService();
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(Guid.NewGuid());
        var seed = session.Presentation.Source!;
        var token = session.AcceptedToken;
        var local = Capture(PromptGalleryEditorForm.FromSource(seed)) with { MaxOutputTokens = "-", PendingProvider = "Unfinished" };
        gallery.GetItem = _ => Task.FromException<Result<PromptGalleryItemDetails>>(new IOException("Unavailable"));
        await session.ReadReviewAsync(local);
        Assert.Null(session.Review);
        Assert.Contains("could not be read", session.Presentation.Warning, StringComparison.Ordinal);
        await session.CancelReviewAsync();
        Assert.Same(seed, session.Presentation.Source);
        Assert.Equal(token, session.AcceptedToken);
        Assert.Equal("-", local.MaxOutputTokens);
        Assert.Empty(gallery.SavedDrafts);
    }

    [Fact]
    public async Task Unknown_outcome_is_sticky_across_hints_retry_current_and_programmatic_Save() {
        var gallery = new ScriptedPromptGalleryService { SaveDraft = _ => Task.FromException<Result<PromptDraftSaveReceipt>>(new IOException("Receipt lost")) };
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(Guid.NewGuid());
        var local = Capture(PromptGalleryEditorForm.FromSource(session.Presentation.Source!));
        var generation = session.Presentation.Generation;
        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(generation, local.Values));
        var reads = gallery.GetItemCalls.Count;
        await session.ObserveAsync(true);
        await session.ApplyAsync(new PromptGalleryEditorIntent.Retry(generation));
        await session.ReadReviewAsync(local);
        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(generation, local.Values));
        Assert.Single(gallery.SavedDrafts);
        Assert.Equal(reads, gallery.GetItemCalls.Count);
        Assert.True(session.Presentation.IsOutcomeUnknown);
        Assert.False(session.CanLeave);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Archived_or_changed_binding_does_not_authorize_replacement(bool archived) {
        var gallery = new ScriptedPromptGalleryService();
        var id = Guid.NewGuid();
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(id);
        var local = Capture(PromptGalleryEditorForm.FromSource(session.Presentation.Source!));
        var remote = ScriptedPromptGalleryService.Details(id) with { IsArchived = archived, ProjectId = archived ? null : Guid.NewGuid() };
        gallery.GetItem = _ => Task.FromResult(Result<PromptGalleryItemDetails>.Success(remote));
        await session.ObserveAsync(true);
        await session.ReadReviewAsync(local);
        Assert.False(session.IsReviewCurrent);
        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, local.Values));
        Assert.Empty(gallery.SavedDrafts);
        Assert.Equal(DateTimeOffset.UnixEpoch, session.AcceptedToken);
    }

    [Fact]
    public async Task Foreign_hint_blocks_direct_Save_even_if_the_button_is_bypassed() {
        var gallery = new ScriptedPromptGalleryService();
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(Guid.NewGuid());
        var local = Capture(PromptGalleryEditorForm.FromSource(session.Presentation.Source!));
        await session.ObserveAsync(true);
        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, local.Values));
        Assert.Empty(gallery.SavedDrafts);
    }

    [Fact]
    public void Adapter_merges_membership_and_rejects_two_preferred_models_without_truncating_text() {
        var remote = ScriptedPromptGalleryService.Details(Guid.NewGuid()) with {
            Tags = ["workflow", "foreign"], SupportedModels = [new("Other", "remote", true)]
        };
        var seed = PromptGalleryEditorSource.FromDetails(remote with { Tags = ["workflow"], SupportedModels = [] });
        var form = PromptGalleryEditorForm.FromSource(seed);
        form.Tags = ["local"];
        form.SupportedModels = [new("OpenAI", "local", true)];
        var review = new PromptMergeReview(seed, Capture(form), remote);
        Assert.Equal(["local", "foreign"], review.Tags.Result);
        Assert.NotNull(review.GroupError);
        Assert.False(review.CanApply);
        review.Models.Single(row => row.Key.Model == "REMOTE").Field.UseLocal();
        Assert.True(review.CanApply);
        Assert.Equal(form.Content, review.Result().Values.Content);
    }

    [Fact]
    public void Acknowledgement_preserves_post_submission_and_ABA_edits() {
        var form = PromptGalleryEditorForm.FromSource(PromptGalleryEditorSource.CreateEmpty());
        form.Title = "Original";
        form.Touch(PromptDraftField.Title);
        var submitted = Capture(form);
        form.Title = "Later";
        form.Touch(PromptDraftField.Title);
        form.Title = "Original";
        form.Touch(PromptDraftField.Title);
        form.MarkAccepted(submitted);
        Assert.True(form.IsDirty);
        Assert.Equal("Original", form.Title);
    }

    private static PromptGalleryDraftCapture Capture(PromptGalleryEditorForm form)
        => form.Capture(PromptGalleryDraftCapture.Format(form.Temperature), PromptGalleryDraftCapture.Format(form.MaxOutputTokens),
            PromptGalleryDraftCapture.Format(form.TopP), string.Empty, string.Empty);
}
