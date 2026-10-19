using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Projects.Pages;

public partial class ProjectsPage {
    [Inject] private ProjectNativeLifecycleOperations NativeLifecycle { get; set; } = default!;
    [Inject] private IProjectLifecyclePendingStore LifecycleStore { get; set; } = default!;
    [Inject] private IProjectLifecycleRecoveryCodec LifecycleCodec { get; set; } = default!;
    [Inject] private ProjectLifecyclePendingStoreLimits LifecycleLimits { get; set; } = default!;
    private readonly Dictionary<ProjectLifecycleOperation, ProjectLifecyclePageOperation> lifecycleOperations = [];
    private IReadOnlyList<ProjectLifecyclePendingMetadata> lifecycleRows = [];
    private ProjectLifecycleWriteContext? lifecycleContext;
    private ProjectLifecyclePageOperation? selectedLifecycle;
    private bool lifecycleReady;
    private bool lifecycleLoading;
    private bool lifecycleActionBusy;
    private string? lifecycleError;
    private string? lifecycleReadError;
    private int lifecycleVisible;
    private long lifecycleSelection;

    private IReadOnlyList<ProjectLifecyclePendingMetadata> VisibleLifecycleRows => lifecycleRows.Take(lifecycleVisible).ToArray();

    private bool LifecycleBlocks(Guid? projectId) => projectId is { } id &&
        (lifecycleRows.Any(row => row.TargetProjectIds.Contains(id) && row.Stage != ProjectPendingStage.ClosedAbsent) ||
        lifecycleOperations.Values.Any(operation => !operation.Cleared && ProjectLifecyclePendingTargets.Capture(operation.Pending.Frozen.Submission.Command).Contains(id)));

    private ProjectLifecyclePendingView? LifecycleView {
        get {
            if (selectedLifecycle is not { Cleared: false } operation) {
                return null;
            }
            var pending = operation.Pending;
            var record = pending.Record;
            var canRetry = !pending.IsRetired && !pending.NeedsStorageRepair && pending.KnownAccepted is null &&
                pending.LastLookup is ProjectLifecycleLookup.NotObserved { State: ProjectOperationObservationState.Open } &&
                record?.Stage is ProjectPendingStage.Dispatching or ProjectPendingStage.Unconfirmed;
            var message = pending.KnownAccepted is not null
                ? pending.NeedsStorageRepair
                    ? "This operation was accepted. Browser evidence needs repair; keep this reference and check the original outcome."
                    : operation.Applied ? "The deletion was committed and the original editor was closed. Cleanup is tracked separately."
                    : "An accepted result is retained. It does not certify that deletion cleanup finished or authorize changes to a newer editor."
                : pending.Problem == ProjectPendingProblem.StorageFailed ? "Browser evidence could not be retained safely. Keep this original reference; no replacement operation will be sent."
                : pending.Problem == ProjectPendingProblem.ContextChanged ? "The original workspace or caller is no longer selected. This operation has not been rebound."
                : pending.LastOutcome is ProjectLifecycleOutcome.Refused && record?.Stage != ProjectPendingStage.ClosedAbsent
                    ? "This attempt was refused. Another attempt at this same original operation may still complete; keep the reference and check its outcome."
                : record?.Stage switch {
                    ProjectPendingStage.Prepared => "This exact operation is retained before dispatch. It can be cancelled while it remains undispatched.",
                    ProjectPendingStage.Rejected => "A local refusal is retained, but the original operation is unresolved. Check its outcome before another write.",
                    ProjectPendingStage.ClosedAbsent => "The owner confirms that this original operation was not accepted. Your draft has not been refreshed.",
                    _ => "Completion is unconfirmed. Check this original reference. Retry is available only after the owner explicitly permits the same operation."
                };
            return new(pending.Frozen.Submission.Operation, message, lifecycleLoading || lifecycleActionBusy || pending.IsBusy,
                canRetry, CanApplyLifecycle(operation), record?.Stage == ProjectPendingStage.Prepared && !pending.NeedsStorageRepair,
                !pending.NeedsStorageRepair && (record?.Stage == ProjectPendingStage.ClosedAbsent ||
                pending.KnownAccepted is not null && pending.HasOwnerAcknowledgement && !pending.NeedsStorageRepair &&
                (operation.Applied || !IsOriginalLifecycleEditor(operation))));
        }
    }

    private async Task RefreshLifecycleAsync() {
        if (disposed || lifecycleLoading) {
            return;
        }
        lifecycleLoading = true;
        lifecycleReady = false;
        try {
            var before = await NativeLifecycle.GetContextAsync(readLifetime.Token);
            await LifecycleStore.InitializeAsync(readLifetime.Token);
            var scope = ProjectNativeLifecycleOperations.Scope(before);
            var rows = new List<ProjectLifecyclePendingMetadata>();
            var seen = new HashSet<ProjectLifecycleOperation>();
            ProjectLifecycleOperation? after = null;
            do {
                var page = await LifecycleStore.ListAsync(scope, LifecycleLimits.MaximumPageSize, after, readLifetime.Token);
                foreach (var row in page.Items) {
                    if (!seen.Add(row.Operation) || rows.Count >= LifecycleLimits.MaximumRecordCount) {
                        throw new InvalidDataException("Retained lifecycle references exceed their bounded scope.");
                    }
                    rows.Add(row);
                }
                after = page.Next;
            } while (after is not null);
            var current = await NativeLifecycle.GetContextAsync(readLifetime.Token);
            if (scope != ProjectNativeLifecycleOperations.Scope(current)) {
                throw new InvalidOperationException("The workspace changed while lifecycle references were being read.");
            }
            if (disposed) {
                return;
            }
            lifecycleContext = current;
            lifecycleRows = rows;
            lifecycleVisible = Math.Max(lifecycleVisible, LifecycleLimits.MaximumPageSize);
            lifecycleReadError = null;
            lifecycleReady = true;
        } catch (OperationCanceledException) when (readLifetime.IsCancellationRequested) {
        } catch (Exception failure) {
            Logger.LogError(failure, "Retained Project lifecycle references could not be read.");
            lifecycleReadError = "Retained deletion and hierarchy operations could not be read. Refresh them before another write.";
        } finally {
            lifecycleLoading = false;
        }
    }

    private void ShowMoreLifecycle() => lifecycleVisible = Math.Min(LifecycleLimits.MaximumRecordCount, lifecycleVisible + LifecycleLimits.MaximumPageSize);

    private async Task DeleteAsync() {
        var target = draft;
        var expected = target.Model.ExpectedDefinition;
        var context = lifecycleContext;
        if (!showProjectModal || expected is null || target.Model.Id != expected.Project.ProjectId || context is null ||
                target.Scope is not { } originalScope || !TryAdmitMutation(target, ProjectEditorMutationState.Deleting)) {
            return;
        }
        var intent = ++editorIntent;
        var operationId = Guid.NewGuid();
        ProjectLifecyclePageOperation? operation = null;
        try {
            var frozen = NativeLifecycle.Freeze(new ProjectLifecycleCommand.Delete(expected),
                new(expected.Project.DatabaseProfileId, originalScope.Namespace, originalScope.CallerBinding), context, operationId);
            operation = new(new(LifecycleStore, LifecycleCodec, frozen), target, intent);
            lifecycleOperations.Add(frozen.Submission.Operation, operation);
            selectedLifecycle = operation;
            await operation.Pending.PrepareAsync();
            if (!IsOriginalLifecycleEditor(operation)) {
                operation.RetireEditor();
                return;
            }
            await operation.Pending.DispatchAsync(context, DateTimeOffset.UtcNow, false, NativeLifecycle.DispatchAsync, operation.DispatchLifetime.Token);
            ApplyLifecycleIfOriginal(operation);
            RememberLifecycleMessage(operation);
        } catch (Exception failure) {
            Logger.LogError(failure, "Native Project deletion reference failed for {ProjectId}, operation {OperationId}.", expected.Project.ProjectId, operationId);
            if (operation is null) {
                ReleaseMutation(target, ProjectEditorMutationState.AdmissionRefused);
                target.Message = "The original deletion could not be prepared. Your draft and original expectation are unchanged.";
                target.IsError = true;
            } else {
                RememberLifecycleMessage(operation);
            }
        } finally {
            if (!disposed) {
                await RefreshLifecycleAsync();
            }
        }
        if (!disposed && operation?.Applied == true) {
            await RefreshPortfolioAsync();
        }
    }

    private async Task OpenLifecycleAsync(ProjectLifecycleOperation original) {
        if (disposed || lifecycleLoading || lifecycleActionBusy) {
            return;
        }
        var selection = ++lifecycleSelection;
        lifecycleActionBusy = true;
        lifecycleError = null;
        try {
            var current = await NativeLifecycle.GetContextAsync(readLifetime.Token);
            if (!ProjectNativeLifecycleOperations.Scope(current).Contains(original)) {
                throw new InvalidOperationException("The retained operation belongs to another workspace or caller.");
            }
            var record = await LifecycleStore.ReadAsync(original, readLifetime.Token)
                ?? throw new InvalidOperationException("The retained lifecycle operation is missing.");
            var after = await NativeLifecycle.GetContextAsync(readLifetime.Token);
            if (!ProjectNativeLifecycleOperations.Scope(after).Contains(original)) {
                throw new InvalidOperationException("The workspace changed while the original operation was being opened.");
            }
            if (disposed || selection != lifecycleSelection) {
                return;
            }
            if (!lifecycleOperations.TryGetValue(original, out var operation) || operation.Pending.IsRetired || operation.Cleared) {
                operation = new(new(LifecycleStore, LifecycleCodec, record.Frozen), null, 0);
                lifecycleOperations[original] = operation;
            }
            await operation.Pending.RestoreAsync(readLifetime.Token);
            if (!disposed && selection == lifecycleSelection) {
                lifecycleContext = after;
                selectedLifecycle = operation;
            }
        } catch (OperationCanceledException) when (readLifetime.IsCancellationRequested) {
        } catch (Exception failure) {
            Logger.LogError(failure, "Retained Project lifecycle operation {OperationId} could not be opened.", original.Id);
            lifecycleError = "The retained operation could not be opened safely. Its evidence has not been removed.";
        } finally {
            lifecycleActionBusy = false;
        }
    }

    private async Task HandleLifecycleActionAsync(ProjectLifecyclePendingIntent intent) {
        if (disposed || lifecycleActionBusy || lifecycleLoading || selectedLifecycle is not { Cleared: false } operation ||
                operation.Pending.Frozen.Submission.Operation != intent.Operation || operation.Pending.IsBusy) {
            return;
        }
        lifecycleActionBusy = true;
        lifecycleError = null;
        try {
            var view = LifecycleView!;
            switch (intent.Action) {
                case ProjectLifecyclePendingAction.Check:
                    await operation.Pending.LookupAsync(NativeLifecycle.LookupAsync, readLifetime.Token);
                    break;
                case ProjectLifecyclePendingAction.RetryOriginal when view.CanRetry:
                    var context = await NativeLifecycle.GetContextAsync(readLifetime.Token);
                    await operation.Pending.DispatchAsync(context, DateTimeOffset.UtcNow, true, NativeLifecycle.DispatchAsync);
                    break;
                case ProjectLifecyclePendingAction.ApplyAccepted when view.CanApply:
                    ApplyLifecycleIfOriginal(operation);
                    break;
                case ProjectLifecyclePendingAction.CancelPrepared when view.CanCancel:
                    await ClearLifecycleAsync(operation, cancel: true);
                    break;
                case ProjectLifecyclePendingAction.ClearResolved when view.CanClear:
                    await ClearLifecycleAsync(operation, cancel: false);
                    break;
            }
            RememberLifecycleMessage(operation);
        } catch (OperationCanceledException) when (readLifetime.IsCancellationRequested) {
        } catch (Exception failure) {
            Logger.LogError(failure, "Project lifecycle recovery failed for operation {OperationId}.", intent.Operation.Id);
            lifecycleError = "Recovery could not finish safely. The original operation and any known acceptance remain retained.";
        } finally {
            lifecycleActionBusy = false;
            if (!disposed) {
                await RefreshLifecycleAsync();
            }
        }
        if (!disposed && operation.Applied) {
            await RefreshPortfolioAsync();
        }
    }

    private async Task ClearLifecycleAsync(ProjectLifecyclePageOperation operation, bool cancel) {
        var record = operation.Pending.Record ?? throw new InvalidOperationException("The retained operation has not been read.");
        var changed = cancel ? await LifecycleStore.CancelPreparedAsync(record, readLifetime.Token)
            : await LifecycleStore.ClearResolvedAsync(record, readLifetime.Token);
        if (changed.Status != ProjectPendingStoreStatus.Applied || changed.Record is not null) {
            await operation.Pending.RestoreAsync(readLifetime.Token);
            throw new InvalidOperationException("Another tab changed the lifecycle evidence before it could be cleared.");
        }
        operation.Cleared = true;
        if (operation.Target is { } original) {
            ReleaseMutation(original, cancel ? ProjectEditorMutationState.Ready : ProjectEditorMutationState.AdmissionRefused);
            original.Message = cancel ? "The undispatched deletion was cancelled. Your draft is unchanged."
                : "The operation is resolved. Your draft is unchanged; explicitly reopen or compare the current definition before a new write.";
        }
        operation.Dispose();
        if (ReferenceEquals(selectedLifecycle, operation)) {
            selectedLifecycle = null;
        }
    }

    private bool IsOriginalLifecycleEditor(ProjectLifecyclePageOperation operation)
        => !disposed && operation.IsOriginalEditor(draft, editorIntent, showProjectModal);

    private bool CanApplyLifecycle(ProjectLifecyclePageOperation operation)
        => !disposed && operation.CanApply(draft, editorIntent, showProjectModal);

    private void ApplyLifecycleIfOriginal(ProjectLifecyclePageOperation operation) {
        if (disposed) {
            return;
        }
        operation.TryApply(draft, editorIntent, showProjectModal, (target, intent, _) => {
            RecordDeletion(target, intent);
            projectDeletionNotice = "Project deletion committed. Cleanup completion is tracked separately.";
            projectDeletionNoticeIsError = false;
        });
    }

    private void RememberLifecycleMessage(ProjectLifecyclePageOperation operation) {
        if (operation.Cleared || operation.Applied || operation.Target is not { } target) {
            return;
        }
        target.Mutation = operation.Pending.KnownAccepted is null ? ProjectEditorMutationState.DeleteOutcomeUnknown : ProjectEditorMutationState.AcceptedReadOnly;
        target.Message = operation.Pending.KnownAccepted is null
            ? "The original deletion reference is retained. Review its outcome before another write; your draft has not been refreshed."
            : "Deletion was accepted. Its exact result remains retained; a newer editor will not be closed by this acknowledgement.";
        target.IsError = operation.Pending.KnownAccepted is null || operation.Pending.NeedsStorageRepair;
    }

    private void RetireLifecycleEditor(ProjectEditorDraft target) {
        foreach (var operation in lifecycleOperations.Values.Where(item => ReferenceEquals(item.Target, target))) {
            operation.RetireEditor();
        }
    }

}
