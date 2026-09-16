using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Prompts.UI.Editor;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Prompts;

// Reconciliation of the accepted persisted baseline against read-backs whose header token and collections do not
// come from one revision, and against collection changes that only a structural comparison can tell apart.
public sealed class PromptGalleryEditorSessionReconciliationTests
{
    private static readonly DateTimeOffset SeededToken = DateTimeOffset.UnixEpoch;
    private static readonly DateTimeOffset ReceiptToken = SeededToken.AddMinutes(1);
    private static readonly DateTimeOffset ForeignToken = SeededToken.AddMinutes(2);
    private static readonly DateTimeOffset ArchivedToken = SeededToken.AddMinutes(3);

    private static readonly PromptGalleryEditorSubmission Submission = new(
        "Reusable prompt",
        "Summary",
        PromptGalleryItemKind.FullPrompt,
        "design",
        "Prompt content",
        ["architecture"],
        [new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true)],
        [PromptGalleryConsumer.Chat],
        new PromptModelRecommendations(0.2, 800, 0.9));

    private static readonly PromptGalleryEditorSubmission SecondRevision = Submission with { Content = "Second revision by editor A" };

    [Theory]
    [InlineData("models")]
    [InlineData("tags")]
    [InlineData("consumers")]
    public async Task Own_token_read_back_with_foreign_collections_keeps_the_submitted_baseline_and_the_stale_save_is_rejected(string collection)
    {
        // Editor B keeps every scalar of A's second revision and changes one collection.
        var foreignRevision = collection switch
        {
            "models" => SecondRevision with { SupportedModels = [new PromptProviderModel("Anthropic", "claude-sonnet-5", IsPreferred: true)] },
            "tags" => SecondRevision with { Tags = ["architecture", "review"] },
            _ => SecondRevision with { SupportedConsumers = [PromptGalleryConsumer.Chat, PromptGalleryConsumer.Workflow] }
        };
        var itemId = Guid.NewGuid();
        var persisted = new PersistedState(SeededToken, Submission);
        var gallery = CreatePersistingGallery(persisted);
        // A's read-back after its receipt: the header still carries A's token while the collections already come from
        // B's later revision. Every later read is a consistent read of the current persisted state.
        var reads = new Queue<PromptGalleryItemDetails>();
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(reads.Count > 0
            ? reads.Dequeue()
            : ScriptedPromptGalleryService.Details(id, persisted.Token, isArchived: persisted.IsArchived, content: persisted.Content)));
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(itemId);
        var sourceBefore = session.Presentation.Source;

        reads.Enqueue(ScriptedPromptGalleryService.Details(itemId, ReceiptToken, content: foreignRevision));
        gallery.SaveDraft = draft =>
        {
            var result = persisted.TrySave(draft, ReceiptToken);
            if (result.IsSuccess)
            {
                // B commits between A's receipt and the collection part of A's read-back.
                persisted.Overwrite(ForeignToken, foreignRevision);
            }

            return Task.FromResult(result);
        };
        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, SecondRevision));

        Assert.Equal(ReceiptToken, session.AcceptedToken);
        Assert.True(session.Presentation.HasExternalChange);
        Assert.Same(sourceBefore, session.Presentation.Source);

        // A's own archive advances the token; the consistent read-back of B's collections must not be adopted.
        gallery.Archive = (_, archived) => { persisted.Archive(ArchivedToken, archived); return Task.FromResult(Result.Success()); };
        await session.ApplyAsync(new PromptGalleryEditorIntent.ToggleArchive(session.Presentation.Generation));

        Assert.True(session.Presentation.IsArchived);
        Assert.Equal(ReceiptToken, session.AcceptedToken);
        Assert.True(session.Presentation.HasExternalChange);

        gallery.SaveDraft = draft => Task.FromResult(persisted.TrySave(draft, ArchivedToken.AddMinutes(1)));
        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, SecondRevision));

        Assert.Equal(ReceiptToken, gallery.SavedDrafts[^1].ExpectedUpdatedAtUtc);
        Assert.Single(notices, notice => notice.Summary == "Prompt draft saved");
        Assert.Single(notices, notice => notice.Summary == "Prompt draft was not saved");
        Assert.Equal(ArchivedToken, persisted.Token);
        Assert.Equal(foreignRevision.SupportedModels, persisted.Content.SupportedModels);
        Assert.Equal(foreignRevision.Tags, persisted.Content.Tags);
        Assert.Equal(foreignRevision.SupportedConsumers, persisted.Content.SupportedConsumers);
        Assert.Same(sourceBefore, session.Presentation.Source);
        Assert.Equal(ReceiptToken, session.AcceptedToken);
    }

    [Fact]
    public async Task Own_token_read_back_with_matching_collections_keeps_the_next_own_command_conflict_free()
    {
        var itemId = Guid.NewGuid();
        var persisted = new PersistedState(SeededToken, Submission);
        var gallery = CreatePersistingGallery(persisted);
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(
            ScriptedPromptGalleryService.Details(id, persisted.Token, isArchived: persisted.IsArchived, content: persisted.Content)));
        gallery.SaveDraft = draft => Task.FromResult(persisted.TrySave(draft, persisted.Token.AddMinutes(1)));
        gallery.Archive = (_, archived) => { persisted.Archive(persisted.Token.AddMinutes(1), archived); return Task.FromResult(Result.Success()); };
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(itemId);

        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, SecondRevision));
        await session.ApplyAsync(new PromptGalleryEditorIntent.ToggleArchive(session.Presentation.Generation));
        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, SecondRevision with { Content = "Third revision" }));

        Assert.False(session.Presentation.HasExternalChange);
        Assert.DoesNotContain(notices, notice => notice.Summary == "Prompt draft was not saved");
        Assert.Equal(2, notices.Count(notice => notice.Summary == "Prompt draft saved"));
        Assert.Equal("Third revision", persisted.Content.Content);
        Assert.Equal(persisted.Token, session.AcceptedToken);
    }

    [Fact]
    public async Task External_model_pair_change_that_only_differs_in_delimiter_placement_is_not_adopted_by_an_own_archive()
    {
        var loaded = Submission with { SupportedModels = [new PromptProviderModel("a|b", "c", IsPreferred: false)] };
        var foreign = Submission with { SupportedModels = [new PromptProviderModel("a", "b|c", IsPreferred: false)] };
        var itemId = Guid.NewGuid();
        var persisted = new PersistedState(SeededToken, loaded);
        var gallery = CreatePersistingGallery(persisted);
        gallery.GetItem = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(
            ScriptedPromptGalleryService.Details(id, persisted.Token, isArchived: persisted.IsArchived, content: persisted.Content)));
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGalleryEditorSession(gallery)
        {
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.SetTargetAsync(itemId);
        Assert.Equal(SeededToken, session.AcceptedToken);

        // Editor B replaces the declared pair; the persistence owner stores provider and model separately.
        persisted.Overwrite(ForeignToken, foreign);
        gallery.Archive = (_, archived) => { persisted.Archive(ArchivedToken, archived); return Task.FromResult(Result.Success()); };
        await session.ApplyAsync(new PromptGalleryEditorIntent.ToggleArchive(session.Presentation.Generation));

        Assert.True(session.Presentation.IsArchived);
        Assert.True(session.Presentation.HasExternalChange);
        Assert.Equal(SeededToken, session.AcceptedToken);

        gallery.SaveDraft = draft => Task.FromResult(persisted.TrySave(draft, ArchivedToken.AddMinutes(1)));
        await session.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(session.Presentation.Generation, loaded));

        Assert.Equal(SeededToken, gallery.SavedDrafts[^1].ExpectedUpdatedAtUtc);
        Assert.Contains(notices, notice => notice.Summary == "Prompt draft was not saved");
        Assert.Equal(foreign.SupportedModels, persisted.Content.SupportedModels);
        Assert.Equal(ArchivedToken, persisted.Token);
    }

    private static ScriptedPromptGalleryService CreatePersistingGallery(PersistedState persisted)
    {
        var gallery = new ScriptedPromptGalleryService();
        gallery.SaveDraft = draft => Task.FromResult(persisted.TrySave(draft, persisted.Token.AddMinutes(1)));
        return gallery;
    }

    // Minimal persisted revision: the token check and content replacement of the real owner, without its normalization.
    private sealed class PersistedState(DateTimeOffset token, PromptGalleryEditorSubmission content)
    {
        public DateTimeOffset Token { get; private set; } = token;

        public PromptGalleryEditorSubmission Content { get; private set; } = content;

        public bool IsArchived { get; private set; }

        public Result<PromptDraftSaveReceipt> TrySave(PromptGalleryDraft draft, DateTimeOffset nextToken)
        {
            if (draft.ExpectedUpdatedAtUtc != Token)
            {
                return Result<PromptDraftSaveReceipt>.Failure(Error.Failure(
                    "The Prompt Gallery item was changed by someone else. Reload it before saving.",
                    "prompts.gallery.concurrency-conflict"));
            }

            Token = nextToken;
            Content = new PromptGalleryEditorSubmission(
                draft.Title,
                draft.Summary,
                draft.Kind,
                draft.Phase,
                draft.Content,
                draft.Tags ?? [],
                draft.SupportedModels ?? [],
                draft.SupportedConsumers ?? [],
                draft.Recommendations ?? new PromptModelRecommendations());
            return Result<PromptDraftSaveReceipt>.Success(new PromptDraftSaveReceipt(draft.Id ?? Guid.NewGuid(), Token));
        }

        public void Overwrite(DateTimeOffset nextToken, PromptGalleryEditorSubmission content)
        {
            Token = nextToken;
            Content = content;
        }

        public void Archive(DateTimeOffset nextToken, bool archived)
        {
            Token = nextToken;
            IsArchived = archived;
        }
    }
}
