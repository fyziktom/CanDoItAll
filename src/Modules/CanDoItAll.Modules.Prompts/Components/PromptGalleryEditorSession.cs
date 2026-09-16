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
    private const string RefreshFailureWarning =
        "The saved item could not be re-read. Versions and status may be stale until the editor is reopened.";

    private readonly IPromptGalleryService gallery;
    private readonly CancellationTokenSource lifetime = new();
    private long generation;
    private Guid? target;
    private PromptGalleryEditorPhase phase = PromptGalleryEditorPhase.New;
    private PromptGalleryEditorSource? source = PromptGalleryEditorSource.CreateEmpty();
    private Guid? projectId;
    private Guid? collectionId;
    private DateTimeOffset? expectedUpdatedAtUtc;
    private bool isArchived;
    private IReadOnlyList<PromptGalleryVersionInfo> versions = [];
    private IReadOnlyList<PromptWarningSuppression> warningSuppressions = [];
    private bool isBusy;
    private string? failureMessage;
    private string? warning;
    private bool disposed;

    public PromptGalleryEditorSession(IPromptGalleryService gallery)
    {
        this.gallery = gallery ?? throw new ArgumentNullException(nameof(gallery));
        Presentation = BuildPresentation();
    }

    public PromptGalleryEditorPresentation Presentation { get; private set; }

    public Guid? Target => target;

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
        if (!itemId.HasValue)
        {
            target = null;
            phase = PromptGalleryEditorPhase.New;
            source = PromptGalleryEditorSource.CreateEmpty();
            ResetMetadata();
            return PublishAsync();
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

    private Task RetryAsync(long requested)
    {
        if (!IsCurrent(requested) || isBusy || !target.HasValue)
        {
            return Task.CompletedTask;
        }

        var current = ++generation;
        failureMessage = null;
        warning = null;
        return LoadAsync(target.Value, current);
    }

    private async Task LoadAsync(Guid itemId, long current)
    {
        target = itemId;
        phase = PromptGalleryEditorPhase.Loading;
        source = null;
        ResetMetadata();
        await PublishAsync();
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
                Adopt(item);
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
            await RaiseAsync(PromptGalleryNoticeSeverity.Error, "Prompt item load failed", exception.Message);
        }

        if (IsCurrent(current))
        {
            await PublishAsync();
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
        isBusy = true;
        warning = null;
        await PublishAsync();
        var draftCommitted = false;
        try
        {
            var saved = await gallery.SaveDraftAsync(BuildDraft(submission), lifetime.Token);
            if (!IsCurrent(current))
            {
                return;
            }

            if (!saved.IsSuccess)
            {
                await RaiseAsync(
                    PromptGalleryNoticeSeverity.Warning,
                    "Prompt draft was not saved",
                    PromptGalleryNotices.DescribeErrors(saved.Errors, "The operation failed."));
                return;
            }

            // The receipt is the authority for identity and the concurrency token before any optional re-read.
            var receipt = saved.Value;
            target = receipt.PromptArtifactId;
            expectedUpdatedAtUtc = receipt.UpdatedAtUtc;
            isArchived = false;
            phase = PromptGalleryEditorPhase.Ready;
            draftCommitted = true;
            await PublishAsync();
            if (!finalize)
            {
                // A finalize command reports the draft only through its own outcome notice and the commit callback.
                await RaiseAsync(PromptGalleryNoticeSeverity.Success, "Prompt draft saved", "The canonical Gallery item was updated.");
            }

            await CommitAsync(new PromptGalleryEditorCommit(receipt.PromptArtifactId, PromptGalleryEditorCommitKind.DraftSaved));
            if (!IsCurrent(current))
            {
                return;
            }

            var versionCreated = false;
            if (finalize)
            {
                var version = await gallery.CreateVersionAsync(
                    receipt.PromptArtifactId,
                    new PromptVersionCreateRequest("Ready for reuse", ExpectedUpdatedAtUtc: receipt.UpdatedAtUtc),
                    lifetime.Token);
                if (!IsCurrent(current))
                {
                    return;
                }

                if (version.IsSuccess)
                {
                    versionCreated = true;
                    await RaiseAsync(
                        PromptGalleryNoticeSeverity.Success,
                        "Final version created",
                        "The immutable prompt version is ready for reuse.");
                }
                else
                {
                    await RaiseAsync(
                        PromptGalleryNoticeSeverity.Warning,
                        "Final version was not created",
                        "The draft was saved, but no immutable version was created. " +
                        PromptGalleryNotices.DescribeErrors(version.Errors, "The operation failed."));
                }
            }

            await RefreshMetadataAsync(current);
            if (versionCreated && IsCurrent(current))
            {
                await CommitAsync(new PromptGalleryEditorCommit(receipt.PromptArtifactId, PromptGalleryEditorCommitKind.VersionCreated));
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!IsCurrent(current))
            {
                return;
            }

            if (draftCommitted)
            {
                warning = "The draft was saved, but the final version request failed with an unknown result. Reopen the item to review its versions.";
                await RaiseAsync(PromptGalleryNoticeSeverity.Error, "Final version request failed", exception.Message);
            }
            else
            {
                // Unknown persistence outcome: keep the draft and identity untouched and never replay the write automatically.
                warning = UnknownSaveOutcomeWarning;
                await RaiseAsync(PromptGalleryNoticeSeverity.Error, "Prompt draft save failed", exception.Message);
            }
        }
        finally
        {
            if (IsCurrent(current))
            {
                isBusy = false;
                await PublishAsync();
            }
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
        await PublishAsync();
        try
        {
            var result = await gallery.ArchiveAsync(itemId, archive, lifetime.Token);
            if (!IsCurrent(current))
            {
                return;
            }

            if (!result.IsSuccess)
            {
                await RaiseAsync(
                    PromptGalleryNoticeSeverity.Warning,
                    "Archive state was not changed",
                    PromptGalleryNotices.DescribeErrors(result.Errors, "The operation failed."));
                return;
            }

            isArchived = archive;
            await PublishAsync();
            await RaiseAsync(
                PromptGalleryNoticeSeverity.Success,
                archive ? "Prompt item archived" : "Prompt item restored",
                archive ? "The item is hidden from normal Gallery search." : "The item is available in Gallery search again.");
            await CommitAsync(new PromptGalleryEditorCommit(
                itemId,
                archive ? PromptGalleryEditorCommitKind.Archived : PromptGalleryEditorCommitKind.Restored));
            // Archive and restore advance the concurrency token; re-read it so the next draft save does not conflict.
            await RefreshMetadataAsync(current);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (IsCurrent(current))
            {
                await RaiseAsync(PromptGalleryNoticeSeverity.Error, "Archive state change failed", exception.Message);
            }
        }
        finally
        {
            if (IsCurrent(current))
            {
                isBusy = false;
                await PublishAsync();
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
        await PublishAsync();
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
                await RaiseAsync(
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
                await RaiseAsync(PromptGalleryNoticeSeverity.Error, "Warning preference update failed", exception.Message);
            }
        }
        finally
        {
            if (IsCurrent(current))
            {
                isBusy = false;
                await PublishAsync();
            }
        }
    }

    // Re-reads versions, status and the token after a commit without touching the in-progress draft.
    private async Task RefreshMetadataAsync(long current)
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
                Adopt(item);
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

    private void Adopt(PromptGalleryItemDetails item)
    {
        target = item.Id;
        projectId = item.ProjectId;
        collectionId = item.CollectionId;
        expectedUpdatedAtUtc = item.UpdatedAtUtc;
        isArchived = item.IsArchived;
        versions = item.Versions;
        warningSuppressions = item.WarningSuppressions;
    }

    private void ResetMetadata()
    {
        projectId = null;
        collectionId = null;
        expectedUpdatedAtUtc = null;
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
            ExpectedUpdatedAtUtc: expectedUpdatedAtUtc);

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
            warning);

    private Task PublishAsync()
    {
        Presentation = BuildPresentation();
        return Changed?.Invoke() ?? Task.CompletedTask;
    }

    private Task RaiseAsync(PromptGalleryNoticeSeverity severity, string summary, string detail)
        => NoticeRaised?.Invoke(new PromptGalleryNotice(severity, summary, detail)) ?? Task.CompletedTask;

    private Task CommitAsync(PromptGalleryEditorCommit commit)
        => Committed?.Invoke(commit) ?? Task.CompletedTask;
}
