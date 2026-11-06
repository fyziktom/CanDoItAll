using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Projects.Pages;

public sealed class ProjectDefinitionComparisonCoordinator(ProjectNativeDefinitionOperations definitions,
    Func<CancellationToken, ValueTask> authorizeRead, Func<ProjectEditorDraft, bool> isActive, ILogger logger) : IDisposable {
    private CancellationTokenSource? reading;
    private bool disposed;
    public ProjectEditorDraft? Reading { get; private set; }

    public async Task<bool> ReadAsync(ProjectEditorDraft target, CancellationToken cancellationToken) {
        if (disposed || !isActive(target) || target.Model.Id is not { } projectId || ReferenceEquals(Reading, target) ||
            !(target.CanCompare || target.Conflict is not null)) {
            return false;
        }
        Retire();
        using var read = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        reading = read;
        Reading = target;
        target.Definition.Reading = true;
        var hint = target.Definition.HintRevision;
        if (target.Conflict is not null) {
            target.BeginConflictRead();
        }
        try {
            await authorizeRead(read.Token);
            var result = await definitions.ReadCurrentAsync(projectId,
                target.Scope ?? throw new InvalidOperationException("The editor has no original dataset binding."), read.Token);
            await authorizeRead(read.Token);
            if (disposed || !isActive(target) || read.IsCancellationRequested) {
                return false;
            }
            if (result.IsFailure) {
                if (result.Errors.Any(error => error.Code == ProjectErrorCodes.NotFound)) {
                    target.Definition.Unavailable();
                }
                Fail(target, string.Join(" ", result.Errors.Select(error => error.Message)));
                return false;
            }
            if (target.Conflict is not null) {
                target.CompleteConflictRead(result.Value!, hint);
            } else {
                target.InspectCurrent(result.Value!, hint);
            }
            return true;
        } catch (OperationCanceledException) when (read.IsCancellationRequested) {
            return false;
        } catch (Exception exception) {
            logger.LogError(exception, "Current Project definition comparison failed for {ProjectId}.", projectId);
            if (!disposed && isActive(target)) {
                Fail(target, "Unable to read the current definition. Your draft and comparison are retained; retry the current read.");
            }
            return false;
        } finally {
            if (ReferenceEquals(reading, read)) {
                target.Definition.Reading = false;
                reading = null;
                Reading = null;
            } else if (!ReferenceEquals(Reading, target)) {
                target.Definition.Reading = false;
            }
        }
    }

    public async Task BeginAsync(ProjectEditorDraft target, CancellationToken cancellationToken) {
        if (disposed || !isActive(target) || !target.CanCompare) {
            return;
        }
        if (target.HasPendingInput) {
            FailInput(target, "Complete or clear invalid dates before comparing this draft. Your unfinished input is preserved.");
            return;
        }
        if (target.Definition.Review?.HasPendingInput == true) {
            FailInput(target, "Complete or clear unfinished comparison results before rebuilding them. Your result input is retained.");
            return;
        }
        if ((!target.HasCurrentDefinition || target.Definition.CurrentIsStale || target.Definition.Review is not null) &&
            !await ReadAsync(target, cancellationToken)) {
            return;
        }
        if (disposed || !isActive(target) || !target.CanCompare || target.Definition.CurrentIsStale) {
            return;
        }
        try {
            target.BeginMerge();
            target.Message = null;
            target.IsError = false;
        } catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) {
            FailInput(target, exception.Message);
        }
    }

    public async Task ApplyAsync(ProjectEditorDraft target, CancellationToken cancellationToken) {
        if (disposed || !isActive(target) || !target.CanApplyMerge) {
            return;
        }
        var review = target.Definition.Review!;
        if (!await ReadAsync(target, cancellationToken) || disposed || !isActive(target) || !ReferenceEquals(review, target.Definition.Review)) {
            return;
        }
        var current = target.Conflict?.Current ?? target.InspectedDefinition;
        if (current?.Expectation != review.Current.Expectation) {
            if (target.CanCompare && !target.HasPendingInput && target.Definition.Review?.HasPendingInput != true) {
                target.BeginMerge();
            }
            FailInput(target, "The saved definition changed again. Review the updated comparison; your draft is unchanged.");
            return;
        }
        if (!target.CanApplyMerge) {
            FailInput(target, "Your input or the saved definition changed during verification. Read current again; your draft is retained.");
            return;
        }
        target.ApplyMerge();
    }

    public void Retire() {
        reading?.Cancel();
        reading = null;
        Reading = null;
    }

    public void Dispose() {
        disposed = true;
        Retire();
    }

    private static void Fail(ProjectEditorDraft target, string error) {
        target.Definition.ReadFailed();
        if (target.Conflict is not null) {
            target.FailConflictRead(error);
        }
        FailInput(target, error);
    }

    private static void FailInput(ProjectEditorDraft target, string error) {
        target.Message = error;
        target.IsError = true;
    }
}
