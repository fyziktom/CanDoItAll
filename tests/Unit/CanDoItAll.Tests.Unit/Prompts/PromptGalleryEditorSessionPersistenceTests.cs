using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Prompts.UI.Editor;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.Prompts;

// The editor session against the real Prompts persistence owner: the token/content reconciliation must hold
// with the service's actual concurrency check, timestamp advancement and normalization.
[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PromptGalleryEditorSessionPersistenceTests
{
    private static readonly PromptGalleryEditorSubmission DraftA = new(
        "Shared prompt",
        "Summary",
        PromptGalleryItemKind.FullPrompt,
        "design",
        "Content written by editor A",
        ["architecture", "review"],
        [new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true)],
        [PromptGalleryConsumer.Chat],
        new PromptModelRecommendations(0.2, 800, 0.9));

    [Fact]
    public async Task Foreign_content_change_before_own_archive_is_not_overwritten_by_the_stale_draft()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(nameof(Foreign_content_change_before_own_archive_is_not_overwritten_by_the_stale_draft));
        var service = PromptGalleryTestSupport.CreateService(factory);
        var seeded = await service.SaveDraftAsync(ToDraft(DraftA, id: null, expected: null));
        Assert.True(seeded.IsSuccess);
        var itemId = seeded.Value.PromptArtifactId;
        var notices = new List<PromptGalleryNotice>();
        await using var editorA = new PromptGalleryEditorSession(service)
        {
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await editorA.SetTargetAsync(itemId);
        Assert.Equal(seeded.Value.UpdatedAtUtc, editorA.AcceptedToken);

        // Another actor saves different editable content against the revision editor A loaded.
        var foreign = await service.SaveDraftAsync(ToDraft(
            DraftA with { Title = "Rewritten by editor B", Content = "Content written by editor B" },
            itemId,
            seeded.Value.UpdatedAtUtc));
        Assert.True(foreign.IsSuccess);

        // Editor A archives: the owner acts on the current entity and advances its timestamp again.
        await editorA.ApplyAsync(new PromptGalleryEditorIntent.ToggleArchive(editorA.Presentation.Generation));
        Assert.True(editorA.Presentation.IsArchived);
        Assert.True(editorA.Presentation.HasExternalChange);
        Assert.Equal(seeded.Value.UpdatedAtUtc, editorA.AcceptedToken);

        // Editor A saves its unchanged stale draft: the owner rejects it and B's content survives.
        await editorA.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(editorA.Presentation.Generation, DraftA));

        var persisted = await service.GetItemAsync(itemId);
        Assert.True(persisted.IsSuccess);
        Assert.Equal("Rewritten by editor B", persisted.Value!.Title);
        Assert.Equal("Content written by editor B", persisted.Value.DraftContent);
        Assert.True(persisted.Value.IsArchived);
        var rejected = Assert.Single(notices, notice => notice.Summary == "Prompt draft was not saved");
        Assert.Contains("Reload it before saving", rejected.Detail, StringComparison.Ordinal);
        Assert.True(editorA.Presentation.HasExternalChange);

        // The explicit reload adopts B's revision; the next save continues from it.
        await editorA.ApplyAsync(new PromptGalleryEditorIntent.Retry(editorA.Presentation.Generation));
        Assert.False(editorA.Presentation.HasExternalChange);
        Assert.Equal("Rewritten by editor B", editorA.Presentation.Source?.Title);
        Assert.Equal(persisted.Value.UpdatedAtUtc, editorA.AcceptedToken);

        await editorA.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(
            editorA.Presentation.Generation,
            DraftA with { Title = "Rewritten by editor B", Content = "Merged by editor A after reload" }));
        var merged = await service.GetItemAsync(itemId);
        Assert.Equal("Merged by editor A after reload", merged.Value!.DraftContent);
        Assert.Contains(notices, notice => notice.Summary == "Prompt draft saved");
    }

    [Fact]
    public async Task Foreign_change_between_own_receipt_and_read_back_is_rejected_on_the_next_save()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(nameof(Foreign_change_between_own_receipt_and_read_back_is_rejected_on_the_next_save));
        var service = PromptGalleryTestSupport.CreateService(factory);
        var seeded = await service.SaveDraftAsync(ToDraft(DraftA, id: null, expected: null));
        var itemId = seeded.Value.PromptArtifactId;
        PromptDraftSaveReceipt? ownReceipt = null;
        var interceptor = new ReadBackInterceptor(service, async () =>
        {
            // Between editor A's receipt and its read-back, editor B commits different content.
            var foreign = await service.SaveDraftAsync(ToDraft(
                DraftA with { Content = "Content written by editor B" },
                itemId,
                ownReceipt!.Value.UpdatedAtUtc));
            Assert.True(foreign.IsSuccess);
        });
        var notices = new List<PromptGalleryNotice>();
        await using var editorA = new PromptGalleryEditorSession(interceptor)
        {
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await editorA.SetTargetAsync(itemId);
        interceptor.OnReceipt = receipt => ownReceipt = receipt;

        await editorA.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(
            editorA.Presentation.Generation,
            DraftA with { Content = "Content written by editor A, second revision" }));

        Assert.NotNull(ownReceipt);
        Assert.Equal(ownReceipt!.Value.UpdatedAtUtc, editorA.AcceptedToken);
        Assert.True(editorA.Presentation.HasExternalChange);

        await editorA.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(
            editorA.Presentation.Generation,
            DraftA with { Content = "Content written by editor A, third revision" }));

        var persisted = await service.GetItemAsync(itemId);
        Assert.Equal("Content written by editor B", persisted.Value!.DraftContent);
        Assert.Contains(notices, notice => notice.Summary == "Prompt draft was not saved");
    }

    [Fact]
    public async Task Own_archive_restore_and_finalize_keep_the_next_save_valid_without_a_foreign_change()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(nameof(Own_archive_restore_and_finalize_keep_the_next_save_valid_without_a_foreign_change));
        var service = PromptGalleryTestSupport.CreateService(factory);
        var seeded = await service.SaveDraftAsync(ToDraft(DraftA, id: null, expected: null));
        var itemId = seeded.Value.PromptArtifactId;
        var notices = new List<PromptGalleryNotice>();
        var commits = new List<PromptGalleryEditorCommit>();
        await using var editor = new PromptGalleryEditorSession(service)
        {
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; },
            Committed = commit => { commits.Add(commit); return Task.CompletedTask; }
        };
        await editor.SetTargetAsync(itemId);

        await editor.ApplyAsync(new PromptGalleryEditorIntent.ToggleArchive(editor.Presentation.Generation));
        await editor.ApplyAsync(new PromptGalleryEditorIntent.ToggleArchive(editor.Presentation.Generation));
        await editor.ApplyAsync(new PromptGalleryEditorIntent.CreateVersion(editor.Presentation.Generation, DraftA));
        await editor.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(
            editor.Presentation.Generation,
            DraftA with { Content = "Content written by editor A after finalization" }));

        Assert.False(editor.Presentation.HasExternalChange);
        Assert.DoesNotContain(notices, notice => notice.Summary == "Prompt draft was not saved");
        Assert.Equal(
            [
                PromptGalleryEditorCommitKind.Archived,
                PromptGalleryEditorCommitKind.Restored,
                PromptGalleryEditorCommitKind.DraftSaved,
                PromptGalleryEditorCommitKind.VersionCreated,
                PromptGalleryEditorCommitKind.DraftSaved
            ],
            commits.Select(commit => commit.Kind));
        var persisted = await service.GetItemAsync(itemId);
        Assert.Equal("Content written by editor A after finalization", persisted.Value!.DraftContent);
        Assert.Equal(1, persisted.Value.CurrentVersionNumber);
        Assert.Equal(persisted.Value.UpdatedAtUtc, editor.AcceptedToken);
    }

    private static PromptGalleryDraft ToDraft(PromptGalleryEditorSubmission submission, Guid? id, DateTimeOffset? expected)
        => new(
            id,
            ProjectId: null,
            CollectionId: null,
            submission.Title,
            submission.Summary,
            submission.Kind,
            submission.Phase,
            submission.Content,
            submission.Tags,
            submission.SupportedModels,
            submission.SupportedConsumers,
            submission.Recommendations,
            ExpectedUpdatedAtUtc: expected);

    // Forwards every call to the real service and runs a hook before the first read-back that follows a save.
    private sealed class ReadBackInterceptor(IPromptGalleryService inner, Func<Task> beforeReadBack) : IPromptGalleryService
    {
        private bool pendingReadBack;

        public Action<PromptDraftSaveReceipt>? OnReceipt { get; set; }

        public async Task<Result<PromptDraftSaveReceipt>> SaveDraftAsync(PromptGalleryDraft draft, CancellationToken cancellationToken = default)
        {
            var result = await inner.SaveDraftAsync(draft, cancellationToken);
            if (result.IsSuccess && OnReceipt is not null)
            {
                OnReceipt(result.Value);
                OnReceipt = null;
                pendingReadBack = true;
            }

            return result;
        }

        public async Task<Result<PromptGalleryItemDetails>> GetItemAsync(Guid promptArtifactId, CancellationToken cancellationToken = default)
        {
            if (pendingReadBack)
            {
                pendingReadBack = false;
                await beforeReadBack();
            }

            return await inner.GetItemAsync(promptArtifactId, cancellationToken);
        }

        public Task<PromptGalleryPage<PromptGallerySearchItem>> SearchAsync(PromptGalleryQuery query, CancellationToken cancellationToken = default)
            => inner.SearchAsync(query, cancellationToken);

        public Task<Result<PromptVersionSnapshot>> CreateVersionAsync(Guid promptArtifactId, PromptVersionCreateRequest request, CancellationToken cancellationToken = default)
            => inner.CreateVersionAsync(promptArtifactId, request, cancellationToken);

        public Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(Guid promptVersionId, CancellationToken cancellationToken = default)
            => inner.GetVersionSnapshotAsync(promptVersionId, cancellationToken);

        public Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(Guid promptArtifactId, int versionNumber, CancellationToken cancellationToken = default)
            => inner.GetVersionSnapshotAsync(promptArtifactId, versionNumber, cancellationToken);

        public Task<Result<IReadOnlyList<PromptVersionSnapshot>>> GetVersionSnapshotsAsync(IReadOnlyCollection<Guid> promptVersionIds, CancellationToken cancellationToken = default)
            => inner.GetVersionSnapshotsAsync(promptVersionIds, cancellationToken);

        public Task<Result<IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot>>> GetCompatibilitySnapshotsAsync(IReadOnlyCollection<Guid> promptArtifactIds, CancellationToken cancellationToken = default)
            => inner.GetCompatibilitySnapshotsAsync(promptArtifactIds, cancellationToken);

        public Task<Result> ArchiveAsync(Guid promptArtifactId, bool archived, CancellationToken cancellationToken = default)
            => inner.ArchiveAsync(promptArtifactId, archived, cancellationToken);

        public Task<Result> SetFavoriteAsync(Guid promptArtifactId, bool favorite, CancellationToken cancellationToken = default)
            => inner.SetFavoriteAsync(promptArtifactId, favorite, cancellationToken);

        public Task<Result<PromptCompatibilityResult>> EvaluateCompatibilityAsync(Guid promptArtifactId, PromptGalleryConsumerContext context, CancellationToken cancellationToken = default)
            => inner.EvaluateCompatibilityAsync(promptArtifactId, context, cancellationToken);

        public Task<Result> SetWarningSuppressionAsync(Guid promptArtifactId, PromptGalleryConsumer consumer, PromptCompatibilityIssueCode issueCode, bool suppressed, CancellationToken cancellationToken = default)
            => inner.SetWarningSuppressionAsync(promptArtifactId, consumer, issueCode, suppressed, cancellationToken);
    }
}
