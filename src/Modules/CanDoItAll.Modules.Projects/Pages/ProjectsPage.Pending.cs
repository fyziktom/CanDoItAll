using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Projects.Pages;

public partial class ProjectsPage {
    [Inject] private ProjectNativeDefinitionOperations NativeDefinitions { get; set; } = default!;
    [Inject] private IProjectPendingSubmissionStore PendingStore { get; set; } = default!;
    [Inject] private ProjectPendingStoreLimits PendingLimits { get; set; } = default!;
    private readonly Dictionary<ProjectEditorDraft, ProjectEditorOperationSession> editorOperations = [];
    private IReadOnlyList<ProjectPendingSubmissionMetadata> pendingSaves = [];
    private ProjectDefinitionWriteContext? pendingContext;
    private bool pendingReady;
    private bool pendingLoading;
    private bool pendingActionBusy;
    private string? pendingError;
    private int pendingVisible = 16;
    private int pendingClears;
    private long pendingRecoveryGeneration;

    private IReadOnlyList<ProjectPendingSubmissionMetadata> VisiblePendingSaves => pendingSaves.Take(pendingVisible).ToArray();

    private bool PendingBlocks(Guid? projectId) => pendingSaves.Any(item =>
        item.Stage is not ProjectPendingStage.Rejected and not ProjectPendingStage.ClosedAbsent &&
        (projectId.HasValue ? item.TargetProjectId == projectId : item.TargetProjectId is null)) ||
        editorOperations.Values.Any(session => !session.IsEvidenceDiscarded && session.Pending.Record?.Stage is not ProjectPendingStage.Rejected and not ProjectPendingStage.ClosedAbsent &&
            (projectId.HasValue ? session.Draft.Model.Id == projectId || session.Draft.AcceptedOutcome?.Accepted.Project.ProjectId == projectId
                : session.Submitted.Model.Id is null));

    private ProjectPendingEditorView? PendingEditorView {
        get {
            if (!editorOperations.TryGetValue(draft, out var session) || session.IsEvidenceDiscarded) {
                return null;
            }
            var pending = session.Pending;
            var record = pending.Record;
            var canRetry = !session.IsRetired && pending.KnownAccepted is null && !pending.IsBusy && pendingContext is { } context &&
                context.Namespace == session.Operation.Namespace && context.CallerBinding == session.Operation.CallerBinding &&
                context.Epoch == session.Operation.Epoch && DateTimeOffset.UtcNow < context.AdmissionDeadlineUtc &&
                (record is null || record.Stage is ProjectPendingStage.Prepared or ProjectPendingStage.Dispatching or ProjectPendingStage.Unconfirmed);
            var status = pending.Problem switch {
                ProjectPendingProblem.StorageFailed => "Browser persistence failed. The original save identity is retained; no replacement save will be sent.",
                ProjectPendingProblem.RecordChanged => "Another tab changed this save. Check the retained outcome before continuing.",
                ProjectPendingProblem.ContextChanged => "The dataset, caller or admission epoch changed. The original save remains available for outcome checks.",
                ProjectPendingProblem.RecoveryUnavailable => "The saved outcome cannot currently be established or disclosed. Retain this reference; reading current data is not proof of rollback.",
                ProjectPendingProblem.ReconciliationFailed => "The definition was accepted, but local reconciliation failed. Your draft and exact accepted result remain retained.",
                _ => record?.Stage switch {
                    ProjectPendingStage.Prepared => "This exact definition is retained before dispatch.",
                    ProjectPendingStage.Dispatching => "This save was dispatched. Check its outcome before another write.",
                    ProjectPendingStage.Unconfirmed => "Save completion is unconfirmed. Check or explicitly retry this same save; its identity will not change.",
                    ProjectPendingStage.Accepted => pending.IsReconciled
                        ? "This definition was accepted and reconciled. Finish recovery to release its retained reference."
                        : "A historical accepted result is retained. Check its authorized receipt, then explicitly apply it to this draft.",
                    ProjectPendingStage.Rejected => "The sole first attempt was rejected without a committed definition effect.",
                    ProjectPendingStage.ClosedAbsent => "The closed retained epoch proves this save was not accepted. Finish recovery, then start editing explicitly.",
                    _ => "The frozen definition is retained in this editor. Browser preparation must succeed before dispatch."
                }
            };
            return new(session.Operation, status, pending.IsBusy || pendingLoading || pendingActionBusy, canRetry, pending.CanReconcile,
                record?.Stage == ProjectPendingStage.Prepared && !session.HadPreparationFailure,
                pending.IsReconciled || record?.Stage is ProjectPendingStage.Rejected or ProjectPendingStage.ClosedAbsent,
                draft.Model.Id.HasValue || draft.AcceptedOutcome is not null,
                record?.HadStarterPlan == true || session.Submitted.Seeds.Count > 0);
        }
    }

    private async Task RefreshPendingSavesAsync() {
        if (disposed || pendingLoading) {
            return;
        }
        pendingLoading = true;
        pendingReady = false;
        try {
            var before = await NativeDefinitions.GetContextAsync(readLifetime.Token);
            await PendingStore.InitializeAsync(readLifetime.Token);
            var scope = ProjectNativeDefinitionOperations.Scope(before);
            var rows = new List<ProjectPendingSubmissionMetadata>();
            var seen = new HashSet<ProjectDefinitionOperation>();
            ProjectDefinitionOperation? after = null;
            do {
                var page = await PendingStore.ListAsync(scope, PendingLimits.MaximumPageSize, after, readLifetime.Token);
                foreach (var item in page.Items) {
                    if (!seen.Add(item.Operation) || rows.Count >= PendingLimits.MaximumRecordCount) {
                        throw new InvalidOperationException("The retained-save listing exceeds its bounded scope.");
                    }
                    rows.Add(item);
                }
                after = page.Next;
            } while (after is not null);
            var current = await NativeDefinitions.GetContextAsync(readLifetime.Token);
            if (scope != ProjectNativeDefinitionOperations.Scope(current)) {
                throw new InvalidOperationException("The dataset changed while retained saves were being read.");
            }
            if (disposed) {
                return;
            }
            pendingContext = current;
            pendingSaves = rows;
            pendingError = null;
            pendingReady = true;
        } catch (OperationCanceledException) when (readLifetime.IsCancellationRequested) {
        } catch (Exception exception) {
            Logger.LogError(exception, "Native pending Project save references could not be read.");
            pendingSaves = [];
            pendingError = "Retained saves could not be read safely. Refresh pending saves before submitting another definition.";
        } finally {
            pendingLoading = false;
        }
    }

    private void ShowMorePendingSaves() => pendingVisible = Math.Min(PendingLimits.MaximumRecordCount, pendingVisible + PendingLimits.MaximumPageSize);

    private bool ValidateDraftCapture(ProjectEditorDraft target) {
        if (!pendingReady || pendingContext is null) {
            target.Message = "Refresh retained saves before preparing another definition.";
            target.IsError = true;
            return false;
        }
        try {
            ProjectDefinitionInputBounds.Validate(target.Model, pendingContext.Limits);
            return true;
        } catch (ArgumentException exception) {
            target.Message = exception.Message;
            target.IsError = true;
            return false;
        }
    }

    private async Task OpenPendingSaveAsync(ProjectDefinitionOperation operation) {
        if (disposed || pendingLoading || pendingClears > 0) {
            return;
        }
        var intent = ++editorIntent;
        var recoveryGeneration = pendingRecoveryGeneration;
        editorRead?.Cancel();
        try {
            var context = await NativeDefinitions.GetContextAsync(readLifetime.Token);
            if (!ProjectNativeDefinitionOperations.Scope(context).Contains(operation)) {
                throw new InvalidOperationException("This retained save belongs to another dataset or caller.");
            }
            var record = await PendingStore.ReadAsync(operation, readLifetime.Token)
                ?? throw new InvalidOperationException("The retained save changed. Refresh the pending list.");
            var after = await NativeDefinitions.GetContextAsync(readLifetime.Token);
            if (disposed || intent != editorIntent) {
                return;
            }
            if (pendingClears > 0 || recoveryGeneration != pendingRecoveryGeneration) {
                throw new InvalidOperationException("The retained save changed while it was being opened. Refresh the pending list.");
            }
            if (!ProjectNativeDefinitionOperations.Scope(after).Contains(operation)) {
                throw new InvalidOperationException("The dataset or caller changed while the retained save was being read.");
            }
            var retained = editorOperations.Values.FirstOrDefault(item => item.Operation == operation);
            RetireEditor();
            draft = retained?.Draft ?? ProjectEditorDraft.Restore(record.Frozen.Submission);
            draft.Mutation = ProjectEditorMutationState.PendingRecovery;
            editorOperations[draft] = new(draft, retained?.Submitted ?? draft.Capture(), new(PendingStore, record), restored: true);
            showProjectModal = true;
            modalMode = ProjectsModalMode.Editor;
            wizardStep = 0;
            draft.Message = retained is null
                ? "Only this frozen submitted definition was restored. Later typing and raw starter-object plans were not persisted."
                : "Your retained live draft was reopened. Check the original save's accepted outcome before continuing.";
        } catch (OperationCanceledException) when (readLifetime.IsCancellationRequested) {
        } catch (Exception exception) {
            if (disposed || intent != editorIntent) {
                return;
            }
            Logger.LogError(exception, "Retained Project save {OperationId} could not be opened.", operation.Id);
            pendingError = "This retained save could not be opened safely. Its evidence has not been removed.";
        }
    }

    private async Task<Guid?> SaveCoreAsync(ProjectEditorSubmission submitted) {
        var target = draft;
        if (!showProjectModal || !target.Validate() || !TryAdmitMutation(target, ProjectEditorMutationState.Saving)) {
            return null;
        }
        long intent = ++editorIntent;
        ProjectEditorOperationSession? session = null;
        try {
            var context = await NativeDefinitions.GetContextAsync(readLifetime.Token);
            if (!IsActiveEditor(target, intent)) {
                ReleaseMutation(target);
                return null;
            }
            var frozen = NativeDefinitions.Freeze(submitted, target.Scope ?? throw new InvalidOperationException("Reopen this draft to capture its dataset binding."), context);
            session = new(target, submitted, new(PendingStore, frozen, submitted.Seeds.Count > 0));
            editorOperations.Add(target, session);
            await session.PrepareAsync(readLifetime.Token);
            await session.Pending.DispatchAsync(context, DateTimeOffset.UtcNow, false, NativeDefinitions.DispatchAsync);
            RememberAcceptance(session);
            if (!IsActiveEditor(target, intent) || session.IsRetired) {
                return null;
            }
            return await CompleteLiveSaveAsync(session, intent);
        } catch (Exception exception) {
            if (session is null) {
                ReleaseMutation(target, ProjectEditorMutationState.AdmissionRefused);
                target.Message = "This draft could not be prepared under its original dataset binding. Preserve it and reopen the current project before a new save.";
                target.IsError = true;
                Logger.LogError(exception, "Native Project draft preparation failed for {ProjectId}.", target.Model.Id);
            } else {
                RecordPendingFailure(session, exception);
            }
            return null;
        } finally {
            if (!disposed) {
                await RefreshPendingSavesAsync();
            }
        }
    }

    private async Task<Guid?> CompleteLiveSaveAsync(ProjectEditorOperationSession session, long intent) {
        var target = session.Draft;
        var pending = session.Pending;
        if (pending.KnownAccepted is not ProjectDefinitionAcceptedReceipt.Full full || !pending.CanReconcile) {
            if (pending.Record?.Stage == ProjectPendingStage.Rejected &&
                pending.LastOutcome is ProjectPendingDispatchOutcome.Full { Outcome: ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected rejected }) {
                if (!await ClearTerminalAsync(session)) {
                    return null;
                }
                if (rejected.Errors.Any(error => error.Code == ProjectErrorCodes.DefinitionConflict)) {
                    target.RecordConflict(session.Submitted);
                } else {
                    target.Message = string.Join(" ", rejected.Errors.Select(error => error.Message));
                    target.IsError = true;
                }
            } else {
                target.Mutation = ProjectEditorMutationState.PendingRecovery;
            }
            return null;
        }
        var accepted = full.Result.Acknowledgement;
        mutationTargets[accepted.Project.ProjectId] = target;
        session.Reconcile();
        if (session.Submitted.Model.Id is null && IsActiveEditor(target, intent)) {
            PublishCreatedProject(accepted.Project.ProjectId);
        }
        target.Message = "Project saved.";
        target.IsError = false;
        if (session.CanSeed && IsActiveEditor(target, intent)) {
            session.BeginSeedAttempt();
            try {
                await ProjectWorkbenchSeedService.SeedProjectObjectsAsync(accepted.Project, session.Submitted.Seeds.Select(seed => seed.Value).ToArray());
            } catch (ProjectWriteAdmissionRejectedException exception) {
                if (session.IsEvidenceDiscarded) {
                    return null;
                }
                session.RefuseSeedAttempt();
                if (!await ClearTerminalAsync(session)) {
                    return null;
                }
                target.Mutation = ProjectEditorMutationState.AdmissionRefused;
                target.Message = "Project saved. Starter objects were refused before writes because their original project admission is no longer current. Inspect the current project explicitly.";
                target.IsError = true;
                Logger.LogWarning(exception, "Starter-object admission refused after accepted Project operation {OperationId}.", session.Operation.Id);
                return null;
            }
            if (session.IsEvidenceDiscarded || editorOperations.TryGetValue(target, out var replacement) && !ReferenceEquals(replacement, session)) {
                return null;
            }
            session.CompleteSeedAttempt();
            target.Message = "Project saved and starter objects seeded.";
        }
        if (!IsActiveEditor(target, intent)) {
            await ClearTerminalAsync(session);
            return null;
        }
        if (!await ClearTerminalAsync(session)) {
            return null;
        }
        await RefreshPortfolioAsync();
        if (target.EditRevision != session.Submitted.EditRevision) {
            target.Message += " Newer edits remain unsaved.";
            return null;
        }
        return IsActiveEditor(target, intent) ? accepted.Project.ProjectId : null;
    }

    private async Task HandlePendingActionAsync(ProjectPendingEditorIntent action) {
        if (!showProjectModal || pendingActionBusy || !editorOperations.TryGetValue(draft, out var session) || session.Operation != action.Operation || session.Pending.IsBusy) {
            return;
        }
        pendingActionBusy = true;
        var target = draft;
        var intent = editorIntent;
        try {
            switch (action.Action) {
                case ProjectPendingEditorAction.Check:
                    await session.Pending.ObserveLookupAsync(await NativeDefinitions.LookupAsync(session.Operation, readLifetime.Token), readLifetime.Token);
                    RememberAcceptance(session);
                    break;
                case ProjectPendingEditorAction.Retry:
                    var context = await NativeDefinitions.GetContextAsync(readLifetime.Token);
                    var lookup = await NativeDefinitions.LookupAsync(session.Operation, readLifetime.Token);
                    await session.Pending.ObserveLookupAsync(lookup, readLifetime.Token);
                    if (lookup is not ProjectDefinitionLookup.NotObserved { State: ProjectOperationObservationState.Open } || !IsActiveEditor(target, intent)) {
                        break;
                    }
                    if (session.Pending.Record is null) {
                        await session.PrepareAsync(readLifetime.Token);
                    }
                    await session.Pending.DispatchAsync(context, DateTimeOffset.UtcNow,
                        session.Pending.Record?.Stage != ProjectPendingStage.Prepared, NativeDefinitions.DispatchAsync);
                    RememberAcceptance(session);
                    break;
                case ProjectPendingEditorAction.ApplyAccepted:
                    if (session.Pending.CanReconcile) {
                        session.Reconcile();
                        mutationTargets[target.Model.Id!.Value] = target;
                        target.Message = "The exact accepted result was applied. Newer input is preserved. Current data may have changed again; inspect it separately.";
                        target.IsError = false;
                    }
                    break;
                case ProjectPendingEditorAction.CancelPrepared:
                    if (session.Pending.Record is { Stage: ProjectPendingStage.Prepared } prepared && !session.HadPreparationFailure) {
                        RequireCleared(await PendingStore.CancelPreparedAsync(prepared, readLifetime.Token));
                        ReleaseOperation(session);
                        target.Message = "The undispatched save was canceled. Your draft is preserved.";
                    }
                    break;
                case ProjectPendingEditorAction.ClearResolved:
                    var absent = session.Pending.Record?.Stage == ProjectPendingStage.ClosedAbsent;
                    if (!await ClearTerminalAsync(session)) {
                        break;
                    }
                    target.Message = session.SeedOutcomeUnknown
                        ? "Definition recovery finished. Starter-object outcome remains unconfirmed; attempted plans will not be sent again. Inspect Structure."
                        : "Resolved save recovery finished. Read the current definition before reconciling further changes.";
                    if (absent) {
                        if (target.Model.Id.HasValue) {
                            target.RecordConflict(session.Submitted);
                            target.Message = "This save was not accepted. Read the current definition and begin a current-based draft before another save.";
                        } else {
                            target.Mutation = ProjectEditorMutationState.AdmissionRefused;
                            target.Message = "This creation was not accepted. Your submitted draft remains visible; close it and explicitly start a new project when ready.";
                        }
                    }
                    break;
                case ProjectPendingEditorAction.ReadCurrent:
                    var id = target.Model.Id ?? target.AcceptedOutcome?.Accepted.Project.ProjectId;
                    if (id is { } projectId) {
                        var current = await NativeDefinitions.ReadCurrentAsync(projectId, target.Scope!, readLifetime.Token);
                        if (IsActiveEditor(target, intent) && current.IsSuccess) {
                            target.InspectCurrent(current.Value!);
                        } else if (current.IsFailure) {
                            throw new InvalidOperationException("The current definition is unavailable.");
                        }
                    }
                    break;
            }
        } catch (Exception exception) {
            RecordPendingFailure(session, exception);
        } finally {
            pendingActionBusy = false;
            if (!disposed) {
                await RefreshPendingSavesAsync();
            }
        }
    }

    private async Task<bool> ClearTerminalAsync(ProjectEditorOperationSession session) {
        if (editorOperations.TryGetValue(session.Draft, out var current) && !ReferenceEquals(current, session)) {
            return false;
        }
        pendingClears++;
        pendingRecoveryGeneration++;
        try {
            return await ClearTerminalCoreAsync(session);
        } finally {
            pendingClears--;
            pendingRecoveryGeneration++;
        }
    }

    private async Task<bool> ClearTerminalCoreAsync(ProjectEditorOperationSession session) {
        if (session.Pending.IsReconciled) {
            if (session.IsRetired && session.Pending.Record is { Stage: ProjectPendingStage.Accepted } accepted) {
                RequireCleared(await PendingStore.ClearResolvedAsync(accepted, readLifetime.Token));
            } else {
                await session.Pending.ClearReconciledAsync(readLifetime.Token);
                if (session.Pending.Record is not null) {
                    throw new InvalidOperationException("Another tab changed the accepted record before clearing.");
                }
            }
        } else if (session.Pending.Record is { Stage: ProjectPendingStage.Rejected or ProjectPendingStage.ClosedAbsent } terminal) {
            RequireCleared(await PendingStore.ClearResolvedAsync(terminal, readLifetime.Token));
        } else {
            throw new InvalidOperationException("An unresolved save cannot be discarded or replaced with a new identity.");
        }
        if (editorOperations.TryGetValue(session.Draft, out var current) && !ReferenceEquals(current, session)) {
            return false;
        }
        ReleaseOperation(session);
        return true;
    }

    private static void RequireCleared(ProjectPendingStoreChange change) {
        if (change.Status != ProjectPendingStoreStatus.Applied || change.Record is not null) {
            throw new InvalidOperationException("The retained save changed in another tab. Check it again before clearing.");
        }
    }

    private void ReleaseOperation(ProjectEditorOperationSession session) {
        if (editorOperations.TryGetValue(session.Draft, out var current) && ReferenceEquals(current, session)) {
            editorOperations.Remove(session.Draft);
            ReleaseMutation(session.Draft);
        }
        session.Dispose();
    }

    private void RememberAcceptance(ProjectEditorOperationSession session) {
        session.RememberAcceptance();
        if (!session.IsEvidenceDiscarded && session.Pending.KnownAccepted is ProjectDefinitionAcceptedReceipt.Full full) {
            mutationTargets[full.Result.Acknowledgement.Project.ProjectId] = session.Draft;
        }
    }

    private void RecordPendingFailure(ProjectEditorOperationSession session, Exception exception) {
        RememberAcceptance(session);
        if (session.IsEvidenceDiscarded) {
            return;
        }
        if (editorOperations.TryGetValue(session.Draft, out var current) && !ReferenceEquals(current, session)) {
            return;
        }
        var target = session.Draft;
        target.Mutation = session.SeedOutcomeUnknown ? ProjectEditorMutationState.SeedOutcomeUnknown
            : session.Pending.KnownAccepted is not null ? ProjectEditorMutationState.SaveReconciliationFailed : ProjectEditorMutationState.PendingRecovery;
        target.Message = session.SeedOutcomeUnknown
            ? "Project saved. Starter-object completion is unconfirmed; inspect Structure. These plans will not be automatically sent again."
            : session.Pending.KnownAccepted is not null
                ? "The definition was accepted. Its exact result and your draft remain retained; check recovery before another save."
                : "This save remains attached to its original reference. Check the pending outcome before another write.";
        target.IsError = true;
        Logger.LogError(exception, "Native Project save recovery failed for operation {OperationId}, project {ProjectId}.", session.Operation.Id, target.Model.Id);
    }
}
