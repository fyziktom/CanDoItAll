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

// Normalized editable content of one persisted revision. Two revisions with matching content differ only by
// non-editable commands (archive, restore, finalize), so their token can be adopted without a lost update.
public sealed record PromptGalleryPersistedContent(
    string Title,
    string Summary,
    PromptGalleryItemKind Kind,
    string Phase,
    string Content,
    IReadOnlyList<string> TagKeys,
    IReadOnlyList<string> ModelKeys,
    IReadOnlyList<PromptGalleryConsumer> Consumers,
    PromptModelRecommendations Recommendations)
{
    public static PromptGalleryPersistedContent FromDetails(PromptGalleryItemDetails item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Create(
            item.Title,
            item.Summary,
            item.Kind,
            item.Phase,
            item.DraftContent,
            item.Tags,
            item.SupportedModels,
            item.SupportedConsumers,
            item.Recommendations);
    }

    public static PromptGalleryPersistedContent FromSubmission(PromptGalleryEditorSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        return Create(
            submission.Title,
            submission.Summary,
            submission.Kind,
            submission.Phase,
            submission.Content,
            submission.Tags,
            submission.SupportedModels,
            submission.SupportedConsumers,
            submission.Recommendations);
    }

    public bool Matches(PromptGalleryPersistedContent other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(Title, other.Title, StringComparison.Ordinal) &&
               string.Equals(Summary, other.Summary, StringComparison.Ordinal) &&
               Kind == other.Kind &&
               string.Equals(Phase, other.Phase, StringComparison.Ordinal) &&
               string.Equals(Content, other.Content, StringComparison.Ordinal) &&
               TagKeys.SequenceEqual(other.TagKeys, StringComparer.Ordinal) &&
               ModelKeys.SequenceEqual(other.ModelKeys, StringComparer.Ordinal) &&
               Consumers.SequenceEqual(other.Consumers) &&
               Recommendations.Temperature == other.Recommendations.Temperature &&
               Recommendations.MaxOutputTokens == other.Recommendations.MaxOutputTokens &&
               Recommendations.TopP == other.Recommendations.TopP;
    }

    // Mirrors the persistence normalization: trimmed scalars, case-insensitive distinct tags and provider/model pairs.
    private static PromptGalleryPersistedContent Create(
        string title,
        string summary,
        PromptGalleryItemKind kind,
        string phase,
        string content,
        IReadOnlyList<string> tags,
        IReadOnlyList<PromptProviderModel> models,
        IReadOnlyList<PromptGalleryConsumer> consumers,
        PromptModelRecommendations recommendations)
        => new(
            title.Trim(),
            summary.Trim(),
            kind,
            phase.Trim(),
            content,
            tags.Select(tag => tag.Trim().ToUpperInvariant())
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray(),
            models.Select(model => $"{model.Provider.Trim().ToUpperInvariant()}|{model.Model.Trim().ToUpperInvariant()}|{model.IsPreferred}")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray(),
            consumers.Distinct().OrderBy(consumer => consumer).ToArray(),
            recommendations);
}

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
        "This item was changed elsewhere after your draft was loaded. Saving will be rejected until you reload the latest content; reloading discards your unsaved edits.";
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
        if (!IsCurrent(requested) || isBusy || !target.HasValue)
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
        try
        {
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
        if (!IsCurrent(requested) || isBusy || phase is PromptGalleryEditorPhase.Loading or PromptGalleryEditorPhase.Failed)
        {
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
        catch (Exception exception)
        {
            if (IsCurrent(current))
            {
                // Unknown persistence outcome: keep the draft and identity untouched and never replay the write automatically.
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
        catch (Exception exception)
        {
            if (IsCurrent(current))
            {
                warning = UnknownVersionOutcomeWarning;
                await NotifySafelyAsync(current, PromptGalleryNoticeSeverity.Error, "Final version request failed", exception.Message);
            }

            return false;
        }
    }

    private async Task ToggleArchiveAsync(long requested)
    {
        if (!IsCurrent(requested) || isBusy || !target.HasValue || phase != PromptGalleryEditorPhase.Ready)
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
        catch (Exception exception)
        {
            if (IsCurrent(current))
            {
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
        if (!IsCurrent(requested) || isBusy || !target.HasValue || phase != PromptGalleryEditorPhase.Ready)
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
        var content = PromptGalleryPersistedContent.FromDetails(item);
        if (ownToken.HasValue && item.UpdatedAtUtc == ownToken.Value)
        {
            // The read-back is exactly the revision this editor wrote; its normalized content becomes the baseline.
            AdoptBaseline(item, content);
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
            externalChange);

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
