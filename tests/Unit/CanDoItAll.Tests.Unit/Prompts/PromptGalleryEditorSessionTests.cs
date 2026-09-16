using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Prompts.UI.Editor;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Prompts;

public sealed class PromptGalleryEditorSessionTests
{
    private static readonly PromptGalleryEditorSubmission Submission = new(
        "  Reusable prompt  ",
        "Summary",
        PromptGalleryItemKind.FullPrompt,
        "design",
        "Prompt content",
        ["architecture"],
        [new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true)],
        [PromptGalleryConsumer.Chat],
        new PromptModelRecommendations(0.2, 800, 0.9));

    [Fact]
    public async Task New_target_starts_an_empty_draft_without_reading()
    {
        var gallery = new ScriptedPromptGalleryService();
        await using var session = new PromptGalleryEditorSession(gallery);

        await session.SetTargetAsync(null);

        Assert.Empty(gallery.GetItemCalls);
        Assert.Equal(PromptGalleryEditorPhase.New, session.Presentation.Phase);
        Assert.Null(session.Presentation.Target);
        Assert.NotNull(session.Presentation.Source);
        Assert.False(session.Presentation.IsPersisted);
    }

    [Fact]
    public async Task Missing_item_is_failed_with_its_requested_identity_and_retry_reloads_it()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        var missing = true;
        gallery.GetItem = id => missing
            ? Task.FromResult(Result<PromptGalleryItemDetails>.Failure(Error.Failure("Prompt Gallery item was not found.", "prompts.gallery.not-found")))
            : Task.FromResult(Result<PromptGalleryItemDetails>.Success(ScriptedPromptGalleryService.Details(id)));
        await using var session = new PromptGalleryEditorSession(gallery);

        await session.SetTargetAsync(itemId);

        Assert.Equal(PromptGalleryEditorPhase.Failed, session.Presentation.Phase);
        Assert.Equal(itemId, session.Presentation.Target);
        Assert.Null(session.Presentation.Source);
        Assert.Contains("not found", session.Presentation.FailureMessage, StringComparison.Ordinal);

        // A submit against a failed target is refused rather than creating a second record.
        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));
        Assert.Empty(gallery.SavedDrafts);

        missing = false;
        await session.ApplyAsync(new PromptGalleryEditorIntent.Retry(session.Presentation.Generation));

        Assert.Equal(PromptGalleryEditorPhase.Ready, session.Presentation.Phase);
        Assert.Equal(itemId, session.Presentation.Target);
        Assert.Equal("Loaded prompt", session.Presentation.Source?.Title);
        Assert.Equal([itemId, itemId], gallery.GetItemCalls);
    }

    [Fact]
    public async Task Target_change_during_load_ignores_the_late_result_of_the_previous_target()
    {
        var gallery = new ScriptedPromptGalleryService();
        var pending = new Dictionary<Guid, TaskCompletionSource<Result<PromptGalleryItemDetails>>>();
        gallery.GetItem = id =>
        {
            var source = new TaskCompletionSource<Result<PromptGalleryItemDetails>>();
            pending[id] = source;
            return source.Task;
        };
        await using var session = new PromptGalleryEditorSession(gallery);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var loadFirst = session.SetTargetAsync(first);
        var loadSecond = session.SetTargetAsync(second);

        pending[first].SetResult(Result<PromptGalleryItemDetails>.Success(ScriptedPromptGalleryService.Details(first)));
        await loadFirst;
        Assert.Equal(PromptGalleryEditorPhase.Loading, session.Presentation.Phase);
        Assert.Equal(second, session.Presentation.Target);

        pending[second].SetResult(Result<PromptGalleryItemDetails>.Success(ScriptedPromptGalleryService.Details(second)));
        await loadSecond;
        Assert.Equal(PromptGalleryEditorPhase.Ready, session.Presentation.Phase);
        Assert.Equal(second, session.Presentation.Target);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_ignores_a_late_read(bool fail)
    {
        var gallery = new ScriptedPromptGalleryService();
        var pending = new TaskCompletionSource<Result<PromptGalleryItemDetails>>();
        gallery.GetItem = _ => pending.Task;
        var changes = 0;
        var notices = new List<PromptGalleryNotice>();
        var session = new PromptGalleryEditorSession(gallery)
        {
            Changed = () => { changes++; return Task.CompletedTask; },
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        var itemId = Guid.NewGuid();
        var load = session.SetTargetAsync(itemId);
        var changesBeforeDispose = changes;

        await session.DisposeAsync();
        if (fail)
        {
            pending.SetException(new IOException("Late read failure."));
        }
        else
        {
            pending.SetResult(Result<PromptGalleryItemDetails>.Success(ScriptedPromptGalleryService.Details(itemId)));
        }

        await load;

        Assert.Equal(changesBeforeDispose, changes);
        Assert.Empty(notices);
        Assert.Equal(PromptGalleryEditorPhase.Loading, session.Presentation.Phase);
    }

    [Fact]
    public async Task First_save_adopts_identity_and_token_from_the_receipt_even_when_the_re_read_fails()
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        var token = DateTimeOffset.UnixEpoch.AddHours(2);
        gallery.SaveDraft = draft => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(
            new PromptDraftSaveReceipt(draft.Id ?? savedId, draft.Id is null ? token : token.AddMinutes(5))));
        gallery.GetItem = _ => Task.FromException<Result<PromptGalleryItemDetails>>(new IOException("Re-read failed."));
        var commits = new List<PromptGalleryEditorCommit>();
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            Committed = commit => { commits.Add(commit); return Task.CompletedTask; },
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(null);
        var sourceBeforeSave = session.Presentation.Source;

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));

        Assert.Null(gallery.SavedDrafts[0].Id);
        Assert.Equal("Reusable prompt", gallery.SavedDrafts[0].Title);
        Assert.Equal(savedId, session.Presentation.Target);
        Assert.Equal(PromptGalleryEditorPhase.Ready, session.Presentation.Phase);
        Assert.True(session.Presentation.IsPersisted);
        Assert.Same(sourceBeforeSave, session.Presentation.Source);
        Assert.False(session.Presentation.IsBusy);
        Assert.Contains("could not be re-read", session.Presentation.Warning, StringComparison.Ordinal);
        Assert.Equal(new PromptGalleryEditorCommit(savedId, PromptGalleryEditorCommitKind.DraftSaved), Assert.Single(commits));
        Assert.Contains(notices, notice => notice.Summary == "Prompt draft saved");

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));

        Assert.Equal(2, gallery.SavedDrafts.Count);
        Assert.Equal(savedId, gallery.SavedDrafts[1].Id);
        Assert.Equal(token, gallery.SavedDrafts[1].ExpectedUpdatedAtUtc);
        Assert.Equal(2, commits.Count);
    }

    [Fact]
    public async Task Duplicate_submit_while_a_save_is_in_flight_is_ignored()
    {
        var gallery = new ScriptedPromptGalleryService();
        var pending = new TaskCompletionSource<Result<PromptDraftSaveReceipt>>();
        gallery.SaveDraft = _ => pending.Task;
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(null);

        var first = session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));
        Assert.True(session.Presentation.IsBusy);
        await session.ApplyAsync(new PromptGalleryEditorIntent.CreateVersion(session.Presentation.Generation, Submission));
        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));

        Assert.Single(gallery.SavedDrafts);
        pending.SetResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(Guid.NewGuid(), DateTimeOffset.UnixEpoch)));
        await first;
        Assert.False(session.Presentation.IsBusy);
        Assert.Single(gallery.SavedDrafts);
        Assert.Empty(gallery.VersionRequests);
    }

    [Fact]
    public async Task Concurrency_conflict_keeps_the_draft_identity_and_token_without_overwriting()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        var loadedToken = DateTimeOffset.UnixEpoch.AddDays(1);
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(ScriptedPromptGalleryService.Details(id, loadedToken)));
        gallery.SaveDraft = _ => Task.FromResult(Result<PromptDraftSaveReceipt>.Failure(Error.Failure(
            "The Prompt Gallery item changed after it was loaded. Reload it before saving.",
            "prompts.gallery.concurrency-conflict")));
        var commits = new List<PromptGalleryEditorCommit>();
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            Committed = commit => { commits.Add(commit); return Task.CompletedTask; },
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(itemId);
        var source = session.Presentation.Source;

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));
        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));

        Assert.Empty(commits);
        Assert.All(gallery.SavedDrafts, draft => Assert.Equal(loadedToken, draft.ExpectedUpdatedAtUtc));
        Assert.Same(source, session.Presentation.Source);
        Assert.Equal(itemId, session.Presentation.Target);
        Assert.Single(gallery.GetItemCalls);
        Assert.Equal(2, notices.Count);
        Assert.All(notices, notice =>
        {
            Assert.Equal("Prompt draft was not saved", notice.Summary);
            Assert.Contains("Reload it before saving", notice.Detail, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Finalize_reports_draft_saved_when_version_creation_fails()
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        gallery.SaveDraft = _ => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(savedId, DateTimeOffset.UnixEpoch.AddMinutes(1))));
        gallery.CreateVersion = (_, _) => Task.FromResult(Result<PromptVersionSnapshot>.Failure(
            Error.Validation("Prompt content is required before creating a version.", "prompts.version.content-required")));
        var commits = new List<PromptGalleryEditorCommit>();
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            Committed = commit => { commits.Add(commit); return Task.CompletedTask; },
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(null);

        await session.ApplyAsync(new PromptGalleryEditorIntent.CreateVersion(session.Presentation.Generation, Submission));

        Assert.Equal(new PromptGalleryEditorCommit(savedId, PromptGalleryEditorCommitKind.DraftSaved), Assert.Single(commits));
        Assert.Equal(savedId, Assert.Single(gallery.VersionRequests).ItemId);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(1), gallery.VersionRequests[0].Request.ExpectedUpdatedAtUtc);
        var notice = Assert.Single(notices);
        Assert.Equal("Final version was not created", notice.Summary);
        Assert.Contains("The draft was saved, but", notice.Detail, StringComparison.Ordinal);
        Assert.Contains("content is required", notice.Detail, StringComparison.Ordinal);
        Assert.Equal(savedId, session.Presentation.Target);
        Assert.False(session.Presentation.IsBusy);
        Assert.Equal([savedId], gallery.GetItemCalls);
    }

    [Fact]
    public async Task Finalize_success_commits_draft_then_version_and_adopts_the_re_read_versions_and_token()
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        var receiptToken = DateTimeOffset.UnixEpoch.AddMinutes(1);
        var refreshedToken = DateTimeOffset.UnixEpoch.AddMinutes(2);
        gallery.SaveDraft = _ => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(savedId, receiptToken)));
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(
            ScriptedPromptGalleryService.Details(id, refreshedToken, currentVersionNumber: 1)));
        var commits = new List<PromptGalleryEditorCommit>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            Committed = commit => { commits.Add(commit); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(null);
        var source = session.Presentation.Source;

        await session.ApplyAsync(new PromptGalleryEditorIntent.CreateVersion(session.Presentation.Generation, Submission));

        Assert.Equal(
            [PromptGalleryEditorCommitKind.DraftSaved, PromptGalleryEditorCommitKind.VersionCreated],
            commits.Select(commit => commit.Kind));
        Assert.All(commits, commit => Assert.Equal(savedId, commit.ItemId));
        Assert.Single(session.Presentation.Versions);
        Assert.Same(source, session.Presentation.Source);

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));
        Assert.Equal(refreshedToken, gallery.SavedDrafts[1].ExpectedUpdatedAtUtc);
        Assert.Equal(savedId, gallery.SavedDrafts[1].Id);
    }

    [Fact]
    public async Task Late_save_result_after_a_target_change_is_not_published()
    {
        var gallery = new ScriptedPromptGalleryService();
        var pending = new TaskCompletionSource<Result<PromptDraftSaveReceipt>>();
        gallery.SaveDraft = _ => pending.Task;
        var commits = new List<PromptGalleryEditorCommit>();
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            Committed = commit => { commits.Add(commit); return Task.CompletedTask; },
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(null);
        var save = session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));
        var replacement = Guid.NewGuid();

        await session.SetTargetAsync(replacement);
        pending.SetResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(Guid.NewGuid(), DateTimeOffset.UnixEpoch)));
        await save;

        Assert.Empty(commits);
        Assert.Empty(notices);
        Assert.Equal(replacement, session.Presentation.Target);
        Assert.False(session.Presentation.IsBusy);
        Assert.Equal(PromptGalleryEditorPhase.Ready, session.Presentation.Phase);
    }

    [Fact]
    public async Task Late_save_result_after_dispose_is_not_published_and_the_write_is_not_replayed()
    {
        var gallery = new ScriptedPromptGalleryService();
        var pending = new TaskCompletionSource<Result<PromptDraftSaveReceipt>>();
        gallery.SaveDraft = _ => pending.Task;
        var commits = new List<PromptGalleryEditorCommit>();
        var session = new PromptGalleryEditorSession(gallery)
        {
            Committed = commit => { commits.Add(commit); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(null);
        var save = session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));

        await session.DisposeAsync();
        pending.SetResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(Guid.NewGuid(), DateTimeOffset.UnixEpoch)));
        await save;

        Assert.Empty(commits);
        Assert.Single(gallery.SavedDrafts);
    }

    [Fact]
    public async Task Same_target_echo_is_inert_and_an_explicit_null_target_starts_a_new_draft()
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        gallery.SaveDraft = _ => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(savedId, DateTimeOffset.UnixEpoch)));
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(null);
        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));
        var source = session.Presentation.Source;
        var readsAfterSave = gallery.GetItemCalls.Count;

        await session.SetTargetAsync(savedId);

        Assert.Equal(readsAfterSave, gallery.GetItemCalls.Count);
        Assert.Same(source, session.Presentation.Source);
        Assert.Equal(savedId, session.Presentation.Target);

        await session.SetTargetAsync(null);

        Assert.Equal(PromptGalleryEditorPhase.New, session.Presentation.Phase);
        Assert.Null(session.Presentation.Target);
        Assert.NotSame(source, session.Presentation.Source);
        Assert.Empty(session.Presentation.Versions);
    }

    [Fact]
    public async Task Archive_toggle_commits_and_re_reads_the_token_for_the_next_save()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        var reads = 0;
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(
            ScriptedPromptGalleryService.Details(id, DateTimeOffset.UnixEpoch.AddMinutes(++reads), isArchived: reads > 1)));
        var commits = new List<PromptGalleryEditorCommit>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            Committed = commit => { commits.Add(commit); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(itemId);
        var source = session.Presentation.Source;

        await session.ApplyAsync(new PromptGalleryEditorIntent.ToggleArchive(session.Presentation.Generation));

        Assert.Equal((itemId, true), Assert.Single(gallery.ArchiveWrites));
        Assert.True(session.Presentation.IsArchived);
        Assert.Equal(new PromptGalleryEditorCommit(itemId, PromptGalleryEditorCommitKind.Archived), Assert.Single(commits));
        Assert.Same(source, session.Presentation.Source);

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(2), Assert.Single(gallery.SavedDrafts).ExpectedUpdatedAtUtc);
    }

    [Fact]
    public async Task Warning_suppression_is_persisted_immediately_and_a_rejected_write_keeps_the_previous_state()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        var accept = true;
        gallery.SetWarningSuppression = (_, _, _, _) => Task.FromResult(accept
            ? Result.Success()
            : Result.Failure(Error.Validation("Compatibility issue cannot be suppressed.", "prompts.compatibility.issue-not-suppressible")));
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(itemId);
        var preference = new PromptWarningSuppression(PromptGalleryConsumer.Chat, PromptCompatibilityIssueCode.ItemKindMismatch);

        await session.ApplyAsync(new PromptGalleryEditorIntent.SetWarningSuppression(session.Presentation.Generation, preference, true));
        Assert.Contains(preference, session.Presentation.WarningSuppressions);
        Assert.Empty(gallery.SavedDrafts);

        accept = false;
        await session.ApplyAsync(new PromptGalleryEditorIntent.SetWarningSuppression(session.Presentation.Generation, preference, false));
        Assert.Contains(preference, session.Presentation.WarningSuppressions);
        Assert.Equal("Warning preference was not saved", Assert.Single(notices).Summary);
        Assert.Equal(2, gallery.SuppressionWrites.Count);
    }

    [Fact]
    public async Task Unknown_save_outcome_keeps_the_draft_without_an_identity_and_warns_before_any_retry()
    {
        var gallery = new ScriptedPromptGalleryService();
        gallery.SaveDraft = _ => Task.FromException<Result<PromptDraftSaveReceipt>>(new IOException("Connection dropped."));
        var commits = new List<PromptGalleryEditorCommit>();
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            Committed = commit => { commits.Add(commit); return Task.CompletedTask; },
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(null);
        var source = session.Presentation.Source;

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));

        Assert.Empty(commits);
        Assert.Null(session.Presentation.Target);
        Assert.Same(source, session.Presentation.Source);
        Assert.False(session.Presentation.IsBusy);
        Assert.Contains("known result", session.Presentation.Warning, StringComparison.Ordinal);
        Assert.Equal("Prompt draft save failed", Assert.Single(notices).Summary);
        Assert.Single(gallery.SavedDrafts);
    }

    [Fact]
    public async Task Stale_generation_intents_are_ignored()
    {
        var gallery = new ScriptedPromptGalleryService();
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(null);
        var staleGeneration = session.Presentation.Generation;
        await session.SetTargetAsync(Guid.NewGuid());

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(staleGeneration, Submission));
        await session.ApplyAsync(new PromptGalleryEditorIntent.ToggleArchive(staleGeneration));

        Assert.Empty(gallery.SavedDrafts);
        Assert.Empty(gallery.ArchiveWrites);
    }
}
