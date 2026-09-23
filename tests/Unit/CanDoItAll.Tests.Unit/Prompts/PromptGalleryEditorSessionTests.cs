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

    private static readonly PromptGalleryEditorSubmission ForeignSubmission = Submission with
    {
        Title = "Changed by someone else",
        Content = "Foreign content"
    };

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
        Assert.Equal(token, session.AcceptedToken);
        Assert.Contains("could not be re-read", session.Presentation.Warning, StringComparison.Ordinal);
        Assert.False(session.Presentation.HasExternalChange);
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
        Assert.True(session.Presentation.HasExternalChange);
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
        var receiptToken = DateTimeOffset.UnixEpoch.AddMinutes(1);
        gallery.SaveDraft = _ => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(savedId, receiptToken)));
        gallery.CreateVersion = (_, _) => Task.FromResult(Result<PromptVersionSnapshot>.Failure(
            Error.Validation("Prompt content is required before creating a version.", "prompts.version.content-required")));
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(
            ScriptedPromptGalleryService.Details(id, receiptToken, content: Submission)));
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
        Assert.Equal(receiptToken, gallery.VersionRequests[0].Request.ExpectedUpdatedAtUtc);
        var notice = Assert.Single(notices);
        Assert.Equal("Final version was not created", notice.Summary);
        Assert.Contains("The draft was saved, but", notice.Detail, StringComparison.Ordinal);
        Assert.Contains("content is required", notice.Detail, StringComparison.Ordinal);
        Assert.Equal(savedId, session.Presentation.Target);
        Assert.Equal(receiptToken, session.AcceptedToken);
        Assert.Null(session.Presentation.Warning);
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
        // Finalization advanced the token without touching editable content: the read-back is this editor's own revision.
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(
            ScriptedPromptGalleryService.Details(id, refreshedToken, currentVersionNumber: 1, content: Submission)));
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
        Assert.False(session.Presentation.HasExternalChange);
        Assert.Equal(refreshedToken, session.AcceptedToken);

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
        Assert.False(session.Presentation.HasExternalChange);

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(2), Assert.Single(gallery.SavedDrafts).ExpectedUpdatedAtUtc);
    }

    [Fact]
    public async Task Own_archive_after_a_foreign_content_change_keeps_the_accepted_token_and_flags_the_conflict()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        var loadedToken = DateTimeOffset.UnixEpoch.AddMinutes(1);
        var archivedToken = DateTimeOffset.UnixEpoch.AddMinutes(3);
        var reads = 0;
        // Read 1: the revision this editor loads. Read 2 (after the own archive): another actor's content at a newer token.
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(++reads == 1
            ? ScriptedPromptGalleryService.Details(id, loadedToken, content: Submission)
            : ScriptedPromptGalleryService.Details(id, archivedToken, isArchived: true, content: ForeignSubmission)));
        gallery.SaveDraft = draft => Task.FromResult(draft.ExpectedUpdatedAtUtc == archivedToken
            ? Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(draft.Id!.Value, archivedToken.AddMinutes(1)))
            : Result<PromptDraftSaveReceipt>.Failure(Error.Failure("The Prompt Gallery item changed after it was loaded. Reload it before saving.", "prompts.gallery.concurrency-conflict")));
        var commits = new List<PromptGalleryEditorCommit>();
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            Committed = commit => { commits.Add(commit); return Task.CompletedTask; },
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(itemId);
        var source = session.Presentation.Source;

        await session.ApplyAsync(new PromptGalleryEditorIntent.ToggleArchive(session.Presentation.Generation));

        Assert.True(session.Presentation.IsArchived);
        Assert.True(session.Presentation.HasExternalChange);
        Assert.Equal(loadedToken, session.AcceptedToken);
        Assert.Same(source, session.Presentation.Source);

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));

        var draft = Assert.Single(gallery.SavedDrafts);
        Assert.Equal(loadedToken, draft.ExpectedUpdatedAtUtc);
        Assert.Equal(new[] { PromptGalleryEditorCommitKind.Archived }, commits.Select(commit => commit.Kind));
        Assert.Contains(notices, notice => notice.Summary == "Prompt draft was not saved");
        Assert.Same(source, session.Presentation.Source);
        Assert.True(session.Presentation.HasExternalChange);
        Assert.Single(gallery.SavedDrafts);
    }

    [Fact]
    public async Task Foreign_change_between_the_save_receipt_and_the_read_back_keeps_the_receipt_token()
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        var receiptToken = DateTimeOffset.UnixEpoch.AddMinutes(1);
        var foreignToken = DateTimeOffset.UnixEpoch.AddMinutes(2);
        gallery.SaveDraft = draft => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(draft.Id ?? savedId, receiptToken)));
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(
            ScriptedPromptGalleryService.Details(id, foreignToken, content: ForeignSubmission)));
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(null);

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));

        Assert.Equal(receiptToken, session.AcceptedToken);
        Assert.True(session.Presentation.HasExternalChange);
        Assert.Equal(savedId, session.Presentation.Target);

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));

        Assert.Equal(2, gallery.SavedDrafts.Count);
        Assert.Equal(receiptToken, gallery.SavedDrafts[1].ExpectedUpdatedAtUtc);
        Assert.NotEqual(foreignToken, gallery.SavedDrafts[1].ExpectedUpdatedAtUtc);
    }

    [Fact]
    public async Task Own_finalize_and_archive_without_a_foreign_change_adopt_each_newer_token()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        var token = DateTimeOffset.UnixEpoch;
        // Every read-back returns this editor's own content at whatever token the last own command produced.
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(
            ScriptedPromptGalleryService.Details(id, token, content: Submission)));
        gallery.SaveDraft = draft =>
        {
            token = token.AddMinutes(1);
            return Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(draft.Id!.Value, token)));
        };
        gallery.CreateVersion = (id, _) =>
        {
            token = token.AddMinutes(1);
            return Task.FromResult(Result<PromptVersionSnapshot>.Success(ScriptedPromptGalleryService.Snapshot(id, 1)));
        };
        gallery.Archive = (_, _) =>
        {
            token = token.AddMinutes(1);
            return Task.FromResult(Result.Success());
        };
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(itemId);

        await session.ApplyAsync(new PromptGalleryEditorIntent.CreateVersion(session.Presentation.Generation, Submission));
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(2), session.AcceptedToken);
        await session.ApplyAsync(new PromptGalleryEditorIntent.ToggleArchive(session.Presentation.Generation));
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(3), session.AcceptedToken);
        await session.ApplyAsync(new PromptGalleryEditorIntent.ToggleArchive(session.Presentation.Generation));
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(4), session.AcceptedToken);
        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));

        Assert.False(session.Presentation.HasExternalChange);
        Assert.Equal(
            [DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(4)],
            gallery.SavedDrafts.Select(draft => draft.ExpectedUpdatedAtUtc));
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(5), session.AcceptedToken);
    }

    [Fact]
    public async Task Reload_after_a_conflict_discards_the_draft_and_accepts_the_latest_revision()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        var latestToken = DateTimeOffset.UnixEpoch.AddMinutes(9);
        var reads = 0;
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(++reads == 1
            ? ScriptedPromptGalleryService.Details(id, DateTimeOffset.UnixEpoch, content: Submission)
            : ScriptedPromptGalleryService.Details(id, latestToken, content: ForeignSubmission)));
        await using var session = new PromptGalleryEditorSession(gallery);
        await session.SetTargetAsync(itemId);
        var staleSource = session.Presentation.Source;
        await session.ApplyAsync(new PromptGalleryEditorIntent.ToggleArchive(session.Presentation.Generation));
        Assert.True(session.Presentation.HasExternalChange);

        await session.ApplyAsync(new PromptGalleryEditorIntent.Retry(session.Presentation.Generation));

        Assert.False(session.Presentation.HasExternalChange);
        Assert.NotSame(staleSource, session.Presentation.Source);
        Assert.Equal("Changed by someone else", session.Presentation.Source?.Title);
        Assert.Equal(latestToken, session.AcceptedToken);

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, ForeignSubmission));
        Assert.Equal(latestToken, Assert.Single(gallery.SavedDrafts).ExpectedUpdatedAtUtc);
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

    [Fact]
    public async Task Committed_callback_failure_after_a_valid_receipt_keeps_the_saved_identity_and_reports_only_the_effect()
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        var receiptToken = DateTimeOffset.UnixEpoch.AddMinutes(1);
        gallery.SaveDraft = _ => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(savedId, receiptToken)));
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(
            ScriptedPromptGalleryService.Details(id, receiptToken, content: Submission)));
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            Committed = _ => throw new InvalidOperationException("Owner refresh exploded."),
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(null);

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));

        Assert.Equal(savedId, session.Presentation.Target);
        Assert.Equal(receiptToken, session.AcceptedToken);
        Assert.Equal(PromptGalleryEditorPhase.Ready, session.Presentation.Phase);
        Assert.Null(session.Presentation.Warning);
        Assert.False(session.Presentation.IsBusy);
        Assert.Equal(["Prompt draft saved", "Editor update failed"], notices.Select(notice => notice.Summary));
        Assert.Contains("Owner refresh exploded.", notices[1].Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(notices, notice => notice.Summary == "Final version request failed");
        Assert.Empty(gallery.VersionRequests);
        Assert.Single(gallery.SavedDrafts);
    }

    [Fact]
    public async Task Owner_callback_failure_after_a_successful_version_keeps_the_version_known()
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        var receiptToken = DateTimeOffset.UnixEpoch.AddMinutes(1);
        gallery.SaveDraft = _ => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(savedId, receiptToken)));
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(
            ScriptedPromptGalleryService.Details(id, receiptToken.AddMinutes(1), currentVersionNumber: 1, content: Submission)));
        var commits = new List<PromptGalleryEditorCommit>();
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            Committed = commit =>
            {
                commits.Add(commit);
                return commit.Kind == PromptGalleryEditorCommitKind.VersionCreated
                    ? throw new InvalidOperationException("Catalog refresh exploded.")
                    : Task.CompletedTask;
            },
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(null);

        await session.ApplyAsync(new PromptGalleryEditorIntent.CreateVersion(session.Presentation.Generation, Submission));

        Assert.Equal(
            [PromptGalleryEditorCommitKind.DraftSaved, PromptGalleryEditorCommitKind.VersionCreated],
            commits.Select(commit => commit.Kind));
        Assert.Single(session.Presentation.Versions);
        Assert.Single(gallery.VersionRequests);
        Assert.Equal(receiptToken.AddMinutes(1), session.AcceptedToken);
        Assert.Null(session.Presentation.Warning);
        Assert.Equal(["Final version created", "Editor update failed"], notices.Select(notice => notice.Summary));
        Assert.False(session.Presentation.IsBusy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Version_result_failure_and_thrown_version_request_are_distinct_outcomes(bool thrown)
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        var receiptToken = DateTimeOffset.UnixEpoch.AddMinutes(1);
        gallery.SaveDraft = _ => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(savedId, receiptToken)));
        gallery.CreateVersion = (_, _) => thrown
            ? Task.FromException<Result<PromptVersionSnapshot>>(new IOException("Connection dropped during finalization."))
            : Task.FromResult(Result<PromptVersionSnapshot>.Failure(Error.Failure("Archived Gallery items cannot create new versions.", "prompts.version.artifact-archived")));
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(
            ScriptedPromptGalleryService.Details(id, receiptToken, content: Submission)));
        var commits = new List<PromptGalleryEditorCommit>();
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            Committed = commit => { commits.Add(commit); return Task.CompletedTask; },
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(null);

        await session.ApplyAsync(new PromptGalleryEditorIntent.CreateVersion(session.Presentation.Generation, Submission));

        Assert.Equal([PromptGalleryEditorCommitKind.DraftSaved], commits.Select(commit => commit.Kind));
        Assert.Equal(savedId, session.Presentation.Target);
        Assert.Equal(receiptToken, session.AcceptedToken);
        Assert.Single(gallery.VersionRequests);
        var notice = Assert.Single(notices);
        if (thrown)
        {
            Assert.Equal("Final version request failed", notice.Summary);
            Assert.Contains("unknown result", session.Presentation.Warning, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal("Final version was not created", notice.Summary);
            Assert.Contains("The draft was saved, but", notice.Detail, StringComparison.Ordinal);
            Assert.Null(session.Presentation.Warning);
        }
    }

    [Fact]
    public async Task Target_transition_during_a_suspended_commit_callback_issues_no_further_write()
    {
        var gallery = new ScriptedPromptGalleryService();
        var savedId = Guid.NewGuid();
        gallery.SaveDraft = _ => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(savedId, DateTimeOffset.UnixEpoch.AddMinutes(1))));
        var suspendedCommit = new TaskCompletionSource();
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            Committed = _ => suspendedCommit.Task,
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(null);
        var replacement = Guid.NewGuid();

        var finalize = session.ApplyAsync(new PromptGalleryEditorIntent.CreateVersion(session.Presentation.Generation, Submission));
        await session.SetTargetAsync(replacement);
        var noticesAtTransition = notices.Count;
        suspendedCommit.SetResult();
        await finalize;

        Assert.Empty(gallery.VersionRequests);
        Assert.Equal(replacement, session.Presentation.Target);
        Assert.Equal(noticesAtTransition, notices.Count);
        Assert.False(session.Presentation.IsBusy);
        Assert.Equal([replacement], gallery.GetItemCalls);
    }

    [Fact]
    public async Task Reentrant_target_change_during_the_busy_publication_cancels_the_command_before_any_write()
    {
        var gallery = new ScriptedPromptGalleryService();
        PromptGalleryEditorSession session = null!;
        var replacement = Guid.NewGuid();
        var redirected = false;
        session = new PromptGalleryEditorSession(gallery)
        {
            Changed = async () =>
            {
                if (!redirected && session.Presentation.IsBusy)
                {
                    redirected = true;
                    await session.SetTargetAsync(replacement);
                }
            }
        };
        await session.SetTargetAsync(null);

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, Submission));

        Assert.Empty(gallery.SavedDrafts);
        Assert.Equal(replacement, session.Presentation.Target);
        Assert.Equal(PromptGalleryEditorPhase.Ready, session.Presentation.Phase);
        await session.DisposeAsync();
    }
}
