using CanDoItAll.Prompts.UI.Editor;

namespace CanDoItAll.Modules.Prompts.Components;

public enum PromptGalleryEditorCommitKind
{
    DraftSaved,
    VersionCreated,
    Archived,
    Restored
}

// Reports a persisted change together with the identity the owner should adopt.
public sealed record PromptGalleryEditorCommit(Guid ItemId, PromptGalleryEditorCommitKind Kind);

// Owns one editor instance: the editing target, reads, draft/version/archive writes, identity adoption and busy coordination.
public sealed class PromptGalleryEditorSession : IAsyncDisposable
{
    private const string LoadFailureMessage = "The prompt item could not be loaded.";
    private const string UnknownSaveOutcomeWarning =
        "The last save did not complete with a known result. Check the Gallery before saving again.";
    private const string UnknownVersionOutcomeWarning =
        "The draft was saved, but the final version request failed with an unknown result. Reopen the item to review its versions.";
    private const string RefreshFailureWarning =
        "The saved item could not be re-read. Versions and status may be stale until the editor is reopened.";
    private const string ExternalChangeMessage =
        "This item was changed elsewhere after your draft was loaded. Review the current saved revision and combine it with your draft before saving.";
    private const string ConcurrencyConflictCode = "prompts.gallery.concurrency-conflict";

    private readonly IPromptGalleryService gallery;
    private readonly CancellationTokenSource lifetime = new();
    private long generation;
    private Guid? target;
    private PromptGalleryEditorPhase phase = PromptGalleryEditorPhase.New;
    private PromptGalleryEditorSource? source = PromptGalleryEditorSource.CreateEmpty();
    private Guid? projectId;
    private Guid? collectionId;
    // The accepted persisted baseline: the token and normalized content of the revision this draft continues.
    private DateTimeOffset? baselineToken;
    private PromptGalleryPersistedContent? baselineContent;
    private bool isArchived;
    private IReadOnlyList<PromptGalleryVersionInfo> versions = [];
    private IReadOnlyList<PromptWarningSuppression> warningSuppressions = [];
    private bool isBusy;
    private string? failureMessage;
    private string? warning;
    private string? externalChange;
    private bool disposed;
    private bool outcomeUnknown;
    private bool accessRetired;
    private long hintRevision;
    private long reviewRead;
    private PromptGalleryEditorSource? baselineSource;
    public PromptGalleryEditorReview? Review { get; private set; }
    private PromptGalleryEditorReview? retainedReview;
    public bool IsReviewing { get; private set; }
    public Func<bool, CancellationToken, ValueTask>? Authorize { get; set; }
    public Func<PromptGalleryEditorSubmission, Task>? Acknowledged { get; set; }
    public bool CanLeave => !isBusy && !outcomeUnknown;

    public async Task ObserveAsync(bool requiresReview) {
        if (disposed || !requiresReview) {
            return;
        }
        hintRevision++;
        externalChange = ExternalChangeMessage;
        await PublishSafelyAsync(generation);
    }

    public async Task RetireAccessAsync() {
        accessRetired = true;
        retainedReview = null;
        Review = null;
        IsReviewing = false;
        externalChange = "Gallery access changed. Your draft is retained. Reopen with current access before continuing.";
        await PublishSafelyAsync(generation);
    }

    public async Task ReadReviewAsync(PromptGalleryDraftCapture local) {
        if (disposed || isBusy || outcomeUnknown || accessRetired || !target.HasValue || baselineSource is null) {
            return;
        }
        var currentGeneration = generation;
        var read = ++reviewRead;
        var beforeRead = hintRevision;
        var previous = Review ?? retainedReview;
        var comparisonBase = previous is null ? baselineSource : PromptGalleryEditorSource.FromDetails(previous.Fields.Current);
        var originalLocal = previous?.OriginalLocal ?? local;
        var candidate = previous?.Fields.CaptureCandidate() ?? local;
        IsReviewing = true;
        Review = null;
        warning = null;
        await PublishSafelyAsync(currentGeneration);
        try {
            await DemandAsync(write: false);
            var result = await gallery.GetItemAsync(target.Value, lifetime.Token);
            if (!IsCurrent(currentGeneration) || read != reviewRead || !IsReviewing) {
                return;
            }
            if (!result.IsSuccess || result.Value is not { } remote) {
                warning = "The current item is unavailable. Your draft is retained; retry the read or cancel review.";
            } else if (remote.ProjectId != projectId || remote.CollectionId != collectionId) {
                warning = "The item's project or collection changed. Your draft is retained; reopen the item in its current context.";
            } else {
                Review = new(currentGeneration, beforeRead, new(comparisonBase, candidate, remote), originalLocal);
                retainedReview = Review;
            }
        } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
            return;
        } catch (UnauthorizedAccessException) {
            await RetireAccessAsync();
        } catch (Exception) {
            warning = "The current revision could not be read. Your draft is retained; retry the read.";
        }
        await PublishSafelyAsync(currentGeneration);
    }

    public bool IsReviewCurrent => Review is { } review && review.Generation == generation && review.HintRevision == hintRevision &&
        !accessRetired && !outcomeUnknown && !review.Fields.Current.IsArchived;

    public async Task<bool> ApplyReviewAsync(PromptGalleryEditorReview expected, long draftRevision, Action<PromptGalleryDraftCapture> patch) {
        if (!ReferenceEquals(Review, expected) || !IsReviewCurrent || expected.Fields.Local.Revision != draftRevision || !expected.Fields.CanApply) {
            return false;
        }
        await DemandAsync(write: false);
        if (!ReferenceEquals(Review, expected) || !IsReviewCurrent || expected.Fields.Local.Revision != draftRevision) {
            return false;
        }
        patch(expected.Fields.Result());
        AdoptBaseline(expected.Fields.Current);
        AdoptStatus(expected.Fields.Current);
        retainedReview = null;
        Review = null;
        IsReviewing = false;
        await PublishSafelyAsync(generation);
        return true;
    }

    public Task CancelReviewAsync() {
        reviewRead++;
        retainedReview = null;
        Review = null;
        IsReviewing = false;
        return PublishSafelyAsync(generation);
    }

    public async Task RestoreReviewedAsync() {
        if (Review is not { } review || !review.Fields.Current.IsArchived || review.Generation != generation ||
            review.HintRevision != hintRevision || isBusy || outcomeUnknown || accessRetired) {
            return;
        }
        var current = generation;
        var restored = false;
        isBusy = true;
        await PublishSafelyAsync(current);
        try {
            await DemandAsync(write: true);
            var result = await gallery.ArchiveAsync(review.Fields.Current.Id, false, lifetime.Token);
            if (!IsCurrent(current)) {
                return;
            }
            restored = result.IsSuccess;
            if (!restored) {
                warning = PromptGalleryNotices.DescribeErrors(result.Errors, "The item could not be restored.");
            }
        } catch (UnauthorizedAccessException) {
            await RetireAccessAsync();
        } catch (Exception) {
            outcomeUnknown = true;
            warning = "The restore request has an unknown result. Your draft is retained; do not repeat the command.";
        } finally {
            if (IsCurrent(current)) {
                isBusy = false;
                await PublishSafelyAsync(current);
            }
        }
        if (restored && IsCurrent(current)) {
            await ReadReviewAsync(review.Fields.Local);
        }
    }

    private ValueTask DemandAsync(bool write) => Authorize?.Invoke(write, lifetime.Token) ?? ValueTask.CompletedTask;

    public PromptGalleryEditorSession(IPromptGalleryService gallery)
    {
        this.gallery = gallery ?? throw new ArgumentNullException(nameof(gallery));
        Presentation = BuildPresentation();
    }

    public PromptGalleryEditorPresentation Presentation { get; private set; }

    public Guid? Target => target;

    // The concurrency token of the accepted persisted baseline; exposed for tests and diagnostics only.
    public DateTimeOffset? AcceptedToken => baselineToken;

    public Func<Task>? Changed { get; set; }

    public Func<PromptGalleryNotice, Task>? NoticeRaised { get; set; }

    public Func<PromptGalleryEditorCommit, Task>? Committed { get; set; }

    // Explicit target transition by the owner. An echo of the current target (including one adopted after a save) is inert.
    public Task SetTargetAsync(Guid? itemId)
    {
        if (disposed)
        {
            return Task.CompletedTask;
        }

        if (itemId == target && (itemId.HasValue || phase == PromptGalleryEditorPhase.New))
        {
            return Task.CompletedTask;
        }

        var current = ++generation;
        retainedReview = null;
        Review = null;
        IsReviewing = false;
        reviewRead++;
        outcomeUnknown = false;
        accessRetired = false;
        hintRevision = 0;
        isBusy = false;
        warning = null;
        failureMessage = null;
        externalChange = null;
        if (!itemId.HasValue)
        {
            target = null;
            phase = PromptGalleryEditorPhase.New;
            source = PromptGalleryEditorSource.CreateEmpty();
            ResetMetadata();
            return PublishSafelyAsync(current);
        }

        return LoadAsync(itemId.Value, current);
    }

    public Task ApplyAsync(PromptGalleryEditorIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (disposed)
        {
            return Task.CompletedTask;
        }

        return intent switch
        {
            PromptGalleryEditorIntent.SaveDraft save => SaveAsync(save.Generation, save.Submission, finalize: false),
            PromptGalleryEditorIntent.CreateVersion create => SaveAsync(create.Generation, create.Submission, finalize: true),
            PromptGalleryEditorIntent.ToggleArchive toggle => ToggleArchiveAsync(toggle.Generation),
            PromptGalleryEditorIntent.SetWarningSuppression suppression => SetWarningSuppressionAsync(
                suppression.Generation,
                suppression.Preference,
                suppression.Suppressed),
            PromptGalleryEditorIntent.Retry retry => RetryAsync(retry.Generation),
            _ => throw new NotSupportedException($"The editor session does not own the {intent.GetType().Name} intent.")
        };
    }

    public ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return ValueTask.CompletedTask;
        }

        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
        return ValueTask.CompletedTask;
    }

    // Reloads the current target from the server. In the Ready phase this is the explicit "discard my draft" decision.
    private Task RetryAsync(long requested)
    {
        if (!IsCurrent(requested) || isBusy || outcomeUnknown || accessRetired || IsReviewing || !target.HasValue)
        {
            return Task.CompletedTask;
        }

        var current = ++generation;
        failureMessage = null;
        warning = null;
        externalChange = null;
        return LoadAsync(target.Value, current);
    }

    private async Task LoadAsync(Guid itemId, long current)
    {
        target = itemId;
        phase = PromptGalleryEditorPhase.Loading;
        source = null;
        ResetMetadata();
        await PublishSafelyAsync(current);
        var beforeRead = hintRevision;
        try
        {
            await DemandAsync(write: false);
            var result = await gallery.GetItemAsync(itemId, lifetime.Token);
            if (!IsCurrent(current))
            {
                return;
            }

            if (!result.IsSuccess || result.Value is not { } item)
            {
                // A missing or inaccessible record keeps its requested identity and never becomes a new draft.
                phase = PromptGalleryEditorPhase.Failed;
                failureMessage = PromptGalleryNotices.DescribeErrors(result.Errors, LoadFailureMessage);
            }
            else
            {
                AdoptStatus(item);
                AdoptBaseline(item);
                source = PromptGalleryEditorSource.FromDetails(item);
                phase = PromptGalleryEditorPhase.Ready;
                if (hintRevision != beforeRead) {
                    externalChange = ExternalChangeMessage;
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            if (!IsCurrent(current))
            {
                return;
            }

            phase = PromptGalleryEditorPhase.Failed;
            failureMessage = LoadFailureMessage;
            await NotifySafelyAsync(current, PromptGalleryNoticeSeverity.Error, "Prompt item load failed", exception.Message);
        }

        if (IsCurrent(current))
        {
            await PublishSafelyAsync(current);
        }
    }

    private async Task SaveAsync(long requested, PromptGalleryEditorSubmission submission, bool finalize)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (!IsCurrent(requested) || isBusy || phase is PromptGalleryEditorPhase.Loading or PromptGalleryEditorPhase.Failed) {
            return;
        }
        if (!Presentation.CanWrite) {
            await NotifySafelyAsync(generation, PromptGalleryNoticeSeverity.Warning, "Prompt draft was not saved",
                isArchived ? "Restore this archived item explicitly before saving." : externalChange ?? warning ?? "This editor cannot save in its current state.");
            return;
        }

        var current = generation;
        // The complete command input is frozen before any awaited publication can let the target change underneath it.
        var draft = BuildDraft(submission);
        isBusy = true;
        warning = null;
        await PublishSafelyAsync(current);
        if (!IsCurrent(current))
        {
            return;
        }

        try
        {
            var receipt = await SaveDraftStageAsync(draft, current);
            if (receipt is null || !IsCurrent(current))
            {
                return;
            }

            AcceptSaveReceipt(receipt.Value, submission);
            await (Acknowledged?.Invoke(submission) ?? Task.CompletedTask);
            await PublishSafelyAsync(current);
            if (!finalize)
            {
                // A finalize command reports the draft only through its own outcome notice and the commit callback.
                await NotifySafelyAsync(current, PromptGalleryNoticeSeverity.Success, "Prompt draft saved", "The canonical Gallery item was updated.");
            }

            await CommitSafelyAsync(current, new PromptGalleryEditorCommit(receipt.Value.PromptArtifactId, PromptGalleryEditorCommitKind.DraftSaved));
            if (!IsCurrent(current))
            {
                return;
            }

            var versionCreated = false;
            if (finalize)
            {
                versionCreated = await CreateVersionStageAsync(receipt.Value, current);
                if (!IsCurrent(current))
                {
                    return;
                }
            }

            await RefreshBaselineAsync(current, receipt.Value.UpdatedAtUtc);
            if (versionCreated)
            {
                await CommitSafelyAsync(current, new PromptGalleryEditorCommit(receipt.Value.PromptArtifactId, PromptGalleryEditorCommitKind.VersionCreated));
            }
        }
        finally
        {
            if (IsCurrent(current))
            {
                isBusy = false;
                await PublishSafelyAsync(current);
            }
        }
    }

    // Stage 1: the draft write. Only this stage can produce an unknown persistence outcome for the draft.
    private async Task<PromptDraftSaveReceipt?> SaveDraftStageAsync(PromptGalleryDraft draft, long current)
    {
        PromptDraftSaveReceipt receipt;
        try
        {
            await DemandAsync(write: true);
            var saved = await gallery.SaveDraftAsync(draft, lifetime.Token);
            if (!IsCurrent(current))
            {
                return null;
            }

            if (!saved.IsSuccess)
            {
                if (saved.Errors.Any(error => string.Equals(error.Code, ConcurrencyConflictCode, StringComparison.Ordinal)))
                {
                    externalChange = ExternalChangeMessage;
                }

                await NotifySafelyAsync(
                    current,
                    PromptGalleryNoticeSeverity.Warning,
                    "Prompt draft was not saved",
                    PromptGalleryNotices.DescribeErrors(saved.Errors, "The operation failed."));
                return null;
            }

            receipt = saved.Value;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            return null;
        }
        catch (UnauthorizedAccessException) {
            await RetireAccessAsync();
            return null;
        }
        catch (Exception exception)
        {
            if (IsCurrent(current))
            {
                // Unknown persistence outcome: keep the draft and identity untouched and never replay the write automatically.
                outcomeUnknown = true;
                warning = UnknownSaveOutcomeWarning;
                await NotifySafelyAsync(current, PromptGalleryNoticeSeverity.Error, "Prompt draft save failed", exception.Message);
            }

            return null;
        }

        return receipt;
    }

    // Stage 2: the version write. Its unknown outcome never changes the already known draft commit.
    private async Task<bool> CreateVersionStageAsync(PromptDraftSaveReceipt receipt, long current)
    {
        try
        {
            await DemandAsync(write: true);
            var version = await gallery.CreateVersionAsync(
                receipt.PromptArtifactId,
                new PromptVersionCreateRequest("Ready for reuse", ExpectedUpdatedAtUtc: receipt.UpdatedAtUtc),
                lifetime.Token);
            if (!IsCurrent(current))
            {
                return false;
            }

            if (version.IsSuccess)
            {
                await NotifySafelyAsync(
                    current,
                    PromptGalleryNoticeSeverity.Success,
                    "Final version created",
                    "The immutable prompt version is ready for reuse.");
                return true;
            }

            await NotifySafelyAsync(
                current,
                PromptGalleryNoticeSeverity.Warning,
                "Final version was not created",
                "The draft was saved, but no immutable version was created. " +
                PromptGalleryNotices.DescribeErrors(version.Errors, "The operation failed."));
            return false;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            return false;
        }
        catch (UnauthorizedAccessException) {
            await RetireAccessAsync();
            return false;
        }
        catch (Exception exception)
        {
            if (IsCurrent(current))
            {
                outcomeUnknown = true;
                warning = UnknownVersionOutcomeWarning;
                await NotifySafelyAsync(current, PromptGalleryNoticeSeverity.Error, "Final version request failed", exception.Message);
            }

            return false;
        }
    }

    private async Task ToggleArchiveAsync(long requested)
    {
        if (!IsCurrent(requested) || isBusy || outcomeUnknown || accessRetired || IsReviewing || !target.HasValue || phase != PromptGalleryEditorPhase.Ready)
        {
            return;
        }

        var current = generation;
        var itemId = target.Value;
        var archive = !isArchived;
        isBusy = true;
        warning = null;
        await PublishSafelyAsync(current);
        if (!IsCurrent(current))
        {
            return;
        }

        try
        {
            await DemandAsync(write: true);
            var result = await gallery.ArchiveAsync(itemId, archive, lifetime.Token);
            if (!IsCurrent(current))
            {
                return;
            }

            if (!result.IsSuccess)
            {
                await NotifySafelyAsync(
                    current,
                    PromptGalleryNoticeSeverity.Warning,
                    "Archive state was not changed",
                    PromptGalleryNotices.DescribeErrors(result.Errors, "The operation failed."));
                return;
            }

            isArchived = archive;
            await PublishSafelyAsync(current);
            await NotifySafelyAsync(
                current,
                PromptGalleryNoticeSeverity.Success,
                archive ? "Prompt item archived" : "Prompt item restored",
                archive ? "The item is hidden from normal Gallery search." : "The item is available in Gallery search again.");
            await CommitSafelyAsync(current, new PromptGalleryEditorCommit(
                itemId,
                archive ? PromptGalleryEditorCommitKind.Archived : PromptGalleryEditorCommitKind.Restored));
            // Archive and restore advance the token without a receipt; the re-read adopts it only for unchanged content.
            await RefreshBaselineAsync(current, ownToken: null);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (UnauthorizedAccessException) {
            await RetireAccessAsync();

        }
        catch (Exception exception)
        {
            if (IsCurrent(current))
            {
                outcomeUnknown = true;
                warning = "The archive request has an unknown result. Your draft is retained; do not repeat the command.";
                await NotifySafelyAsync(current, PromptGalleryNoticeSeverity.Error, "Archive state change failed", exception.Message);
            }
        }
        finally
        {
            if (IsCurrent(current))
            {
                isBusy = false;
                await PublishSafelyAsync(current);
            }
        }
    }

    private async Task SetWarningSuppressionAsync(long requested, PromptWarningSuppression preference, bool suppressed)
    {
        if (!IsCurrent(requested) || isBusy || outcomeUnknown || accessRetired || IsReviewing || !target.HasValue || phase != PromptGalleryEditorPhase.Ready)
        {
            return;
        }

        var current = generation;
        var itemId = target.Value;
        isBusy = true;
        await PublishSafelyAsync(current);
        if (!IsCurrent(current))
        {
            return;
        }

        try
        {
            await DemandAsync(write: true);
            var result = await gallery.SetWarningSuppressionAsync(
                itemId,
                preference.Consumer,
                preference.IssueCode,
                suppressed,
                lifetime.Token);
            if (!IsCurrent(current))
            {
                return;
            }

            if (!result.IsSuccess)
            {
                await NotifySafelyAsync(
                    current,
                    PromptGalleryNoticeSeverity.Warning,
                    "Warning preference was not saved",
                    PromptGalleryNotices.DescribeErrors(result.Errors, "The operation failed."));
                return;
            }

            warningSuppressions = suppressed
                ? warningSuppressions.Append(preference).Distinct().ToArray()
                : warningSuppressions.Where(item => item != preference).ToArray();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (UnauthorizedAccessException) {
            await RetireAccessAsync();

        }
        catch (Exception exception)
        {
            if (IsCurrent(current))
            {
                await NotifySafelyAsync(current, PromptGalleryNoticeSeverity.Error, "Warning preference update failed", exception.Message);
            }
        }
        finally
        {
            if (IsCurrent(current))
            {
                isBusy = false;
                await PublishSafelyAsync(current);
            }
        }
    }

    // Re-reads status after a commit and reconciles the persisted baseline without touching the in-progress draft.
    private async Task RefreshBaselineAsync(long current, DateTimeOffset? ownToken)
    {
        if (!IsCurrent(current) || !target.HasValue)
        {
            return;
        }

        try
        {
            await DemandAsync(write: false);
            var result = await gallery.GetItemAsync(target.Value, lifetime.Token);
            if (!IsCurrent(current))
            {
                return;
            }

            if (result.IsSuccess && result.Value is { } item)
            {
                Reconcile(item, ownToken);
                return;
            }

            warning = RefreshFailureWarning;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (IsCurrent(current))
            {
                warning = RefreshFailureWarning;
            }
        }
    }

    private void Reconcile(PromptGalleryItemDetails item, DateTimeOffset? ownToken)
    {
        AdoptStatus(item);
        if (item.ProjectId != projectId || item.CollectionId != collectionId) {
            externalChange = "The item's project or collection changed. Your draft is retained; reopen in the current context.";
            return;
        }
        var content = PromptGalleryPersistedContent.FromDetails(item);
        if (ownToken.HasValue && item.UpdatedAtUtc == ownToken.Value)
        {
            // The header carries this editor's own receipt token, but the owner assembles the collections with separate
            // queries, so the header alone does not prove that every returned value belongs to that revision. The
            // accepted baseline therefore stays the content this editor submitted; only the identity metadata is taken
            // from the read-back. Content that differs from the submission can only come from another actor's revision,
            // which makes that conflict visible now instead of at the next command.
            projectId = item.ProjectId;
            collectionId = item.CollectionId;
            if (baselineContent is not null && !content.Matches(baselineContent))
            {
                externalChange = ExternalChangeMessage;
            }

            return;
        }

        if (baselineContent is null || content.Matches(baselineContent))
        {
            // Only non-editable commands separate the baseline from this revision, so the newer token is safe to carry.
            AdoptBaseline(item, content);
            return;
        }

        // Another actor changed editable content. The accepted token stays behind so the next save is rejected by the
        // owner instead of silently overwriting that change; the user decides whether to reload.
        externalChange = ExternalChangeMessage;
    }

    private void AcceptSaveReceipt(PromptDraftSaveReceipt receipt, PromptGalleryEditorSubmission submission)
    {
        target = receipt.PromptArtifactId;
        baselineToken = receipt.UpdatedAtUtc;
        baselineContent = PromptGalleryPersistedContent.FromSubmission(submission);
        baselineSource = new(submission.Title.Trim(), submission.Summary.Trim(), submission.Kind, submission.Phase.Trim(), submission.Content,
            [.. submission.Tags], [.. submission.SupportedModels], [.. submission.SupportedConsumers], submission.Recommendations);
        isArchived = false;
        phase = PromptGalleryEditorPhase.Ready;
        externalChange = null;
    }

    private void AdoptBaseline(PromptGalleryItemDetails item, PromptGalleryPersistedContent? content = null)
    {
        target = item.Id;
        projectId = item.ProjectId;
        collectionId = item.CollectionId;
        baselineToken = item.UpdatedAtUtc;
        baselineContent = content ?? PromptGalleryPersistedContent.FromDetails(item);
        baselineSource = PromptGalleryEditorSource.FromDetails(item);
        externalChange = null;
    }

    private void AdoptStatus(PromptGalleryItemDetails item)
    {
        isArchived = item.IsArchived;
        versions = item.Versions;
        warningSuppressions = item.WarningSuppressions;
    }

    private void ResetMetadata()
    {
        projectId = null;
        collectionId = null;
        baselineToken = null;
        baselineContent = null;
        baselineSource = null;
        isArchived = false;
        versions = [];
        warningSuppressions = [];
    }

    private PromptGalleryDraft BuildDraft(PromptGalleryEditorSubmission submission)
        => new(
            target,
            projectId,
            collectionId,
            submission.Title.Trim(),
            submission.Summary.Trim(),
            submission.Kind,
            submission.Phase.Trim(),
            submission.Content,
            submission.Tags,
            submission.SupportedModels,
            submission.SupportedConsumers,
            submission.Recommendations,
            ExpectedUpdatedAtUtc: baselineToken);

    private bool IsCurrent(long requested) => !disposed && requested == generation;

    private PromptGalleryEditorPresentation BuildPresentation()
        => new(
            generation,
            target,
            phase,
            source,
            isArchived,
            versions,
            warningSuppressions,
            isBusy,
            failureMessage,
            warning,
            externalChange, outcomeUnknown, IsReviewing, accessRetired);

    // Presentation effects are never allowed to change what is known about persistence.
    private async Task PublishSafelyAsync(long current)
    {
        Presentation = BuildPresentation();
        try
        {
            await (Changed?.Invoke() ?? Task.CompletedTask);
        }
        catch (Exception exception) when (!lifetime.IsCancellationRequested)
        {
            await NotifySafelyAsync(current, PromptGalleryNoticeSeverity.Error, "Editor update failed", exception.Message);
        }
    }

    private async Task NotifySafelyAsync(long current, PromptGalleryNoticeSeverity severity, string summary, string detail)
    {
        if (!IsCurrent(current))
        {
            return;
        }

        try
        {
            await (NoticeRaised?.Invoke(new PromptGalleryNotice(severity, summary, detail)) ?? Task.CompletedTask);
        }
        catch (Exception) when (!lifetime.IsCancellationRequested)
        {
            // A failing notifier cannot be reported through itself; the persistence facts are already recorded.
        }
    }

    private async Task CommitSafelyAsync(long current, PromptGalleryEditorCommit commit)
    {
        if (!IsCurrent(current))
        {
            return;
        }

        try
        {
            await (Committed?.Invoke(commit) ?? Task.CompletedTask);
        }
        catch (Exception exception) when (!lifetime.IsCancellationRequested)
        {
            await NotifySafelyAsync(
                current,
                PromptGalleryNoticeSeverity.Error,
                "Editor update failed",
                $"The change was saved, but the owner could not be updated: {exception.Message}");
        }
    }
}
