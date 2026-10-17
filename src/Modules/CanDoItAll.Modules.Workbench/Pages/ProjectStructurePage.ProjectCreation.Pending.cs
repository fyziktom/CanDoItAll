using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    [Inject] private ProjectNativeDefinitionOperations ProjectCreateDefinitions { get; set; } = default!;
    [Inject] private IProjectPendingSubmissionStore ProjectCreatePendingStore { get; set; } = default!;
    [Inject] private ProjectPendingStoreLimits ProjectCreatePendingLimits { get; set; } = default!;
    private readonly Dictionary<ProjectEditorDraft, ProjectEditorOperationSession> projectCreateSessions = [];
    private ProjectDefinitionWriteContext? projectCreateContext;
    private IReadOnlyList<ProjectPendingSubmissionMetadata> projectCreatePending = [];
    private string? projectCreateRecoveryNotice;
    private readonly HashSet<ProjectEditorOperationSession> projectCreateRecoveryActions = [];

    private ProjectEditorOperationSession? ProjectCreateSession => projectCreateSessions.GetValueOrDefault(projectCreateDraft);
    private bool ProjectCreateSubmissionReady => projectCreateContext is not null && !ProjectCreateBlocked(projectCreatePending);

    private bool ProjectCreateBlocked(IReadOnlyList<ProjectPendingSubmissionMetadata> rows)
        => rows.Any(row => row.TargetProjectId is null && row.Stage is not ProjectPendingStage.Rejected and not ProjectPendingStage.ClosedAbsent) ||
           projectCreateSessions.Values.Any(session => session.Submitted.Model.Id is null);

    private bool IsLiveProjectCreate(ProjectStructureAuthoringOpening opening, ProjectEditorOperationSession session)
        => !session.IsRetired && showProjectCreateModal && ReferenceEquals(session.Draft, projectCreateDraft) &&
           IsCurrentAuthoring(opening, projectCreateOpening);

    private async Task<(ProjectDefinitionWriteContext Context, IReadOnlyList<ProjectPendingSubmissionMetadata> Pending)> ReadProjectCreatePendingAsync(ProjectPendingScope scope) {
        var before = await ProjectCreateDefinitions.GetContextAsync(deferredCompletionCts.Token);
        if (ProjectNativeDefinitionOperations.Scope(before) != scope) {
            throw new InvalidOperationException("The dataset or caller changed after the editor opened.");
        }
        await ProjectCreatePendingStore.InitializeAsync(deferredCompletionCts.Token);
        var rows = new List<ProjectPendingSubmissionMetadata>();
        var seen = new HashSet<ProjectDefinitionOperation>();
        ProjectDefinitionOperation? after = null;
        do {
            var page = await ProjectCreatePendingStore.ListAsync(scope, ProjectCreatePendingLimits.MaximumPageSize, after, deferredCompletionCts.Token);
            foreach (var row in page.Items) {
                if (!scope.Contains(row.Operation) || !seen.Add(row.Operation) || rows.Count >= ProjectCreatePendingLimits.MaximumRecordCount) {
                    throw new InvalidOperationException("The retained-save inventory is invalid or exceeds its bounded scope.");
                }
                rows.Add(row);
            }
            after = page.Next;
            if (after is not null && (page.Items.Count == 0 || after != page.Items[^1].Operation)) {
                throw new InvalidOperationException("The retained-save continuation does not follow its page.");
            }
        } while (after is not null);
        var context = await ProjectCreateDefinitions.GetContextAsync(deferredCompletionCts.Token);
        if (ProjectNativeDefinitionOperations.Scope(context) != scope) {
            throw new InvalidOperationException("The dataset or caller changed while retained saves were read.");
        }
        return (context, rows);
    }

    private bool ValidateProjectCreateCapture(ProjectEditorDraft target) {
        if (!ReferenceEquals(target, projectCreateDraft) || !ProjectCreateSubmissionReady) {
            target.Message = "A retained root creation must be resolved in Projects before another creation.";
            target.IsError = true;
            projectCreateMessage = target.Message;
            return false;
        }
        try {
            ProjectDefinitionInputBounds.Validate(target.Model, projectCreateContext!.Limits);
            return true;
        } catch (ArgumentException exception) {
            target.Message = exception.Message;
            target.IsError = true;
            projectCreateMessage = target.Message;
            return false;
        }
    }

    private ProjectPendingEditorView? ProjectCreatePendingView {
        get {
            if (ProjectCreateSession is not { } session) {
                return null;
            }
            var pending = session.Pending;
            var record = pending.Record;
            var retry = !session.IsRetired && pending.KnownAccepted is null && projectCreateContext is { } context &&
                pending.Problem is not ProjectPendingProblem.ContextChanged and not ProjectPendingProblem.RecoveryUnavailable &&
                pending.LastLookup is not ProjectDefinitionLookup.NotObserved { State: ProjectOperationObservationState.Draining } &&
                context.Namespace == session.Operation.Namespace && context.CallerBinding == session.Operation.CallerBinding &&
                context.Epoch == session.Operation.Epoch && DateTimeOffset.UtcNow < context.AdmissionDeadlineUtc &&
                (record is null || record.Stage is ProjectPendingStage.Prepared or ProjectPendingStage.Dispatching or ProjectPendingStage.Unconfirmed);
            var status = pending.Problem switch {
                ProjectPendingProblem.StorageFailed => "Browser persistence failed. The original save identity is retained; no replacement creation will be sent.",
                ProjectPendingProblem.ContextChanged => "The original dataset, caller or epoch changed. Check this save's retained outcome.",
                ProjectPendingProblem.RecoveryUnavailable => "The original outcome is unavailable. Retain its reference; current data does not prove rollback.",
                ProjectPendingProblem.RecordChanged => "Another tab changed this save. Check its retained outcome before continuing.",
                ProjectPendingProblem.ReconciliationFailed => "The project was accepted, but local reconciliation failed. Its exact result remains retained.",
                _ => pending.IsReconciled ? "The accepted definition was reconciled. Finish recovery before another creation."
                    : record?.Stage == ProjectPendingStage.Prepared ? "This exact creation is retained before dispatch."
                    : "Check this retained save before another write. Recovery never repeats starter-object effects."
            };
            return new(session.Operation, status, pending.IsBusy || projectCreateRecoveryActions.Contains(session), retry, pending.CanReconcile,
                record?.Stage == ProjectPendingStage.Prepared && !session.HadPreparationFailure,
                pending.IsReconciled || record?.Stage is ProjectPendingStage.Rejected or ProjectPendingStage.ClosedAbsent,
                session.Draft.AcceptedOutcome is not null, session.Submitted.Seeds.Count > 0);
        }
    }

    private async Task SaveProjectCreateAsync(ProjectStructureAuthoringOpening? opening, ProjectEditorDraft target,
        ProjectEditorSubmission submitted, bool openStructure = false) {
        if (opening is null || !showProjectCreateModal || !ReferenceEquals(target, projectCreateDraft) ||
            !target.CanMutate || !target.Validate() || !ValidateProjectCreateCapture(target) ||
            !TryBeginAuthoring(opening, projectCreateOpening)) {
            return;
        }
        var parent = projectCreateParentOpening;
        var dialog = projectHierarchyDialogBeforeCreate;
        ProjectEditorOperationSession? session = null;
        Exception? failure = null;
        target.Mutation = ProjectEditorMutationState.Saving;
        try {
            var (context, pending) = await ReadProjectCreatePendingAsync(target.Scope!);
            if (!IsCurrentAuthoring(opening, projectCreateOpening) || !ReferenceEquals(target, projectCreateDraft)) {
                return;
            }
            projectCreateContext = context;
            projectCreatePending = pending;
            if (ProjectCreateBlocked(pending)) {
                throw new InvalidOperationException("A retained root creation must be resolved before another creation.");
            }
            var frozen = ProjectCreateDefinitions.Freeze(submitted, target.Scope!, context);
            session = new(target, submitted, new(ProjectCreatePendingStore, frozen, submitted.Seeds.Count > 0));
            projectCreateSessions.Add(target, session);
            await session.PrepareAsync(deferredCompletionCts.Token);
            await session.Pending.DispatchAsync(context, DateTimeOffset.UtcNow, false, ProjectCreateDefinitions.DispatchAsync);
            session.RememberAcceptance();
            if (!IsLiveProjectCreate(opening, session)) {
                target.Mutation = ProjectEditorMutationState.PendingRecovery;
                return;
            }
            if (session.Pending.KnownAccepted is not ProjectDefinitionAcceptedReceipt.Full accepted) {
                target.Mutation = ProjectEditorMutationState.PendingRecovery;
                target.Message = session.Pending.Record?.Stage == ProjectPendingStage.Rejected &&
                    session.Pending.LastOutcome is ProjectPendingDispatchOutcome.Full { Outcome: ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected rejected }
                    ? string.Join(" ", rejected.Errors.Select(error => error.Message))
                    : "Creation is not confirmed. Check its original save reference before continuing.";
                return;
            }
            session.Reconcile();
            if (session.CanSeed) {
                if (ProjectWorkbenchSeedService is not IAdmittedProjectWorkbenchSeedService seeds) {
                    throw new InvalidOperationException("Starter objects require their original project-lifetime admission.");
                }
                session.BeginSeedAttempt();
                try {
                    await seeds.SeedProjectObjectsAsync(accepted.Result.Acknowledgement.Project, submitted.Seeds.Select(seed => seed.Value).ToArray());
                    session.CompleteSeedAttempt();
                } catch (ProjectWriteAdmissionRejectedException) {
                    session.RefuseSeedAttempt();
                    target.Mutation = ProjectEditorMutationState.AdmissionRefused;
                    target.Message = "Project saved. Starter objects were refused before writes; inspect the current project explicitly.";
                    target.IsError = true;
                    await ClearProjectCreateEvidenceAsync(session);
                    return;
                }
            }
            await ClearProjectCreateEvidenceAsync(session);
            if (!IsLiveProjectCreate(opening, session)) {
                return;
            }
            target.Message = $"{accepted.Result.Acknowledgement.Name} was created. Select it below to connect it.";
            target.IsError = false;
            target.Mutation = ProjectEditorMutationState.AcceptedReadOnly;
            if (target.EditRevision != submitted.EditRevision) {
                target.Message += " Newer input remains in this draft. Open Projects to continue editing the created project.";
                return;
            }
            showProjectCreateModal = false;
            projectCreateOpening = null;
            if (openStructure) {
                projectHierarchyDialogBeforeCreate = null;
                CloseProjectHierarchyDialog();
                OpenStructure(accepted.Result.Acknowledgement.Project.ProjectId);
            } else {
                try {
                    await RestoreProjectHierarchyAsync(opening, parent, dialog, accepted.Result.Acknowledgement.Project.ProjectId);
                } catch (Exception exception) {
                    target.Message = "Project saved. The hierarchy selection could not be refreshed; reopen the hierarchy without repeating creation.";
                    target.IsError = true;
                    Logger.LogWarning(exception, "Accepted Workbench project {ProjectId} could not refresh hierarchy selection.",
                        accepted.Result.Acknowledgement.Project.ProjectId);
                }
            }
        } catch (Exception exception) {
            failure = exception;
            if (session is null) {
                target.Mutation = ProjectEditorMutationState.AdmissionRefused;
                target.Message = "Creation could not be prepared in its original context. Preserve the draft and open Projects recovery.";
                target.IsError = true;
            } else {
                RecordProjectCreateFailure(session, exception);
            }
        } finally {
            opening.IsBusy = false;
            session?.RememberAcceptance();
            if (session is not null) {
                var accepted = (session.Pending.KnownAccepted as ProjectDefinitionAcceptedReceipt.Full)?.Result.Acknowledgement;
                var kind = accepted is null ? session.Pending.Record?.Stage == ProjectPendingStage.Rejected
                        ? ProjectStructureAuthoringResultKind.Rejected : ProjectStructureAuthoringResultKind.Unconfirmed
                    : failure is not null || session.SeedOutcomeUnknown ? ProjectStructureAuthoringResultKind.PartialCommit
                    : ProjectStructureAuthoringResultKind.Committed;
                RecordAuthoringOutcome(opening, new(opening.Id, session.Operation.Id, opening.Context.Admission,
                    ProjectStructureAuthoringOperation.CreateProject, kind,
                    target.Message ?? "The original creation reference is retained. Open Projects recovery.") {
                    TargetProjectId = accepted?.Project.ProjectId,
                    Editor = accepted,
                    Failure = failure
                }, IsCurrentAuthoring(opening, projectCreateOpening));
            }
            if (ReferenceEquals(target, projectCreateDraft) && IsCurrentAuthoring(opening, projectCreateOpening)) {
                projectCreateMessage = target.Message;
            }
            if (!deferredCompletionCts.IsCancellationRequested &&
                (projectCreateSessions.ContainsKey(target) || target.IsError || target.EditRevision != submitted.EditRevision)) {
                projectCreateRecoveryNotice = "A project creation or its later input requires review. Close the editor and open Projects recovery; no save or starter effect is retried automatically.";
            }
            await RenderAuthoringOutcomeAsync();
        }
    }

    private async Task HandleProjectCreatePendingAsync(ProjectStructureAuthoringOpening? opening, ProjectPendingEditorIntent action) {
        if (opening is null || ProjectCreateSession is not { } session || session.Operation != action.Operation ||
            !IsLiveProjectCreate(opening, session) || session.Pending.IsBusy || !projectCreateRecoveryActions.Add(session)) {
            return;
        }
        try {
            switch (action.Action) {
                case ProjectPendingEditorAction.Check:
                    await session.Pending.ObserveLookupAsync(await ProjectCreateDefinitions.LookupAsync(session.Operation, deferredCompletionCts.Token));
                    session.RememberAcceptance();
                    break;
                case ProjectPendingEditorAction.Retry:
                    var context = await ProjectCreateDefinitions.GetContextAsync(deferredCompletionCts.Token);
                    var lookup = await ProjectCreateDefinitions.LookupAsync(session.Operation, deferredCompletionCts.Token);
                    if (!IsLiveProjectCreate(opening, session)) {
                        break;
                    }
                    await session.Pending.ObserveLookupAsync(lookup);
                    session.RememberAcceptance();
                    if (lookup is not ProjectDefinitionLookup.NotObserved { State: ProjectOperationObservationState.Open }) {
                        break;
                    }
                    if (session.Pending.Record is null) {
                        await session.PrepareAsync(deferredCompletionCts.Token);
                    }
                    await session.Pending.DispatchAsync(context, DateTimeOffset.UtcNow,
                        session.Pending.Record?.Stage != ProjectPendingStage.Prepared, ProjectCreateDefinitions.DispatchAsync);
                    session.RememberAcceptance();
                    break;
                case ProjectPendingEditorAction.ApplyAccepted:
                    session.Reconcile();
                    session.Draft.Message = "The exact accepted creation was applied. Newer input is preserved. Read the current definition separately; recovery does not create starter objects.";
                    break;
                case ProjectPendingEditorAction.ReadCurrent:
                    if (session.Draft.AcceptedOutcome is { } accepted) {
                        var current = await ProjectCreateDefinitions.ReadCurrentAsync(accepted.Accepted.Project.ProjectId, session.Draft.Scope!, deferredCompletionCts.Token);
                        if (IsLiveProjectCreate(opening, session) && current.IsSuccess) {
                            session.Draft.InspectCurrent(current.Value!);
                        } else if (current.IsFailure) {
                            throw new InvalidOperationException("The current project definition is unavailable.");
                        }
                    }
                    break;
                case ProjectPendingEditorAction.CancelPrepared:
                    if (session.Pending.Record is { Stage: ProjectPendingStage.Prepared } prepared && !session.HadPreparationFailure) {
                        RequireProjectCreateCleared(await ProjectCreatePendingStore.CancelPreparedAsync(prepared, deferredCompletionCts.Token));
                        projectCreateSessions.Remove(session.Draft);
                        session.Dispose();
                        session.Draft.Mutation = ProjectEditorMutationState.AdmissionRefused;
                        session.Draft.Message = "The undispatched save was canceled. Close this draft and explicitly start again when ready.";
                    }
                    break;
                case ProjectPendingEditorAction.ClearResolved:
                    await ClearProjectCreateEvidenceAsync(session);
                    session.Draft.Mutation = session.Pending.KnownAccepted is null
                        ? ProjectEditorMutationState.AdmissionRefused : ProjectEditorMutationState.AcceptedReadOnly;
                    session.Draft.Message = session.SeedOutcomeUnknown
                        ? "Definition recovery finished. Starter-object completion remains unconfirmed; inspect Structure."
                        : "Resolved creation recovery finished. Preserve any newer input and open Projects before another edit.";
                    break;
            }
        } catch (Exception exception) {
            RecordProjectCreateFailure(session, exception);
        } finally {
            projectCreateRecoveryActions.Remove(session);
            if (IsLiveProjectCreate(opening, session)) {
                projectCreateMessage = session.Draft.Message;
            }
            await RenderAuthoringOutcomeAsync();
        }
    }

    private async Task ClearProjectCreateEvidenceAsync(ProjectEditorOperationSession session) {
        if (session.Pending.Record is not { } record ||
            !(session.Pending.IsReconciled || record.Stage is ProjectPendingStage.Rejected or ProjectPendingStage.ClosedAbsent)) {
            throw new InvalidOperationException("An unresolved creation cannot be cleared.");
        }
        RequireProjectCreateCleared(await ProjectCreatePendingStore.ClearResolvedAsync(record, deferredCompletionCts.Token));
        projectCreateSessions.Remove(session.Draft);
        projectCreatePending = projectCreatePending.Where(row => row.Operation != session.Operation).ToArray();
    }

    private static void RequireProjectCreateCleared(ProjectPendingStoreChange change) {
        if (change.Status != ProjectPendingStoreStatus.Applied || change.Record is not null) {
            throw new InvalidOperationException("Another tab changed the retained creation. Check it before clearing.");
        }
    }

    private void RecordProjectCreateFailure(ProjectEditorOperationSession session, Exception exception) {
        session.RememberAcceptance();
        session.Draft.Mutation = session.SeedOutcomeUnknown ? ProjectEditorMutationState.SeedOutcomeUnknown
            : session.Pending.KnownAccepted is not null ? ProjectEditorMutationState.SaveReconciliationFailed : ProjectEditorMutationState.PendingRecovery;
        session.Draft.Message = session.SeedOutcomeUnknown
            ? "Project saved. Starter-object completion is unconfirmed; inspect Structure without repeating these plans."
            : session.Pending.KnownAccepted is not null ? "The definition was accepted. Its exact result is retained; check recovery before continuing."
            : "The original creation reference is retained. Check recovery before another write.";
        session.Draft.IsError = true;
        Logger.LogWarning(exception, "Workbench project creation recovery failed for operation {OperationId}.", session.Operation.Id);
    }

    private void RetireProjectCreateSession() {
        ProjectCreateSession?.Dispose();
        projectCreateContext = null;
    }

    private void OpenProjectCreateRecovery() {
        RetireProjectCreateSession();
        projectCreateOpening = null;
        showProjectCreateModal = false;
        Navigation.NavigateTo("/projects");
    }
}
