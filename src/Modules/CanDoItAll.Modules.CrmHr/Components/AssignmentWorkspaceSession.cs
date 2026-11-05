using CanDoItAll.CrmHr.UI.Assignments;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

public enum AssignmentWriteArea { Assignments, Staffing }
public sealed record AssignmentPendingDelete(ProjectPartyAssignmentDetail? Assignment, StaffingRequestItemModel? Staffing);

public sealed class AssignmentWorkspaceSession(ProjectPartyIntegrationService assignments, IStaffingRequestEditing staffing,
    ProjectAssignmentChanges assignmentChanges, CrmHrChanges crmChanges, EditingChangeFeed feed, IEditingUiAccess access,
    ILogger<EditingObservation> observationLog, ILogger<AssignmentWorkspaceSession> logger) : IAsyncDisposable {
    private readonly Dictionary<AssignmentWriteArea, EditingObservation> observations = [];
    private readonly Queue<Guid> operations = new();
    private long generation;
    private bool disposed;
    private bool ready;
    private Guid editorId;
    private long reviewedUnknownOpening = -1;
    private ProjectWriteAdmission? project;
    public AssignmentDrafts Drafts { get; } = new();
    public string? Message { get; private set; }
    public bool RequiresAttention { get; private set; }
    public bool AccessDenied { get; private set; }
    public bool IsDeleting { get; private set; }
    public AssignmentPendingDelete? UnknownDelete { get; private set; }
    public bool IsUnknown => Drafts.IsUnknown || UnknownDelete is not null;
    public bool CanDiscardReviewedUnknown => IsUnknown && HasAccess && reviewedUnknownOpening == generation;
    public bool IsBusy => IsDeleting || Drafts.Relationship.IsSaving || Drafts.Allocation.IsSaving || Drafts.Staffing.IsSaving;
    public bool HasAccess => project is not null && !AccessDenied && ready;
    public required Func<Func<Task>, Task> Dispatch { get; init; }
    public required Action Changed { get; init; }
    public required Func<AssignmentWriteArea, Task> RecordsChanged { get; init; }
    public required Func<ProjectPartyAssignmentUpsertRequest, CancellationToken, Action<ProjectAssignmentSaveReceipt>, Task<Result<Guid>>> SaveAssignment { get; init; }

    public void Retire() {
        generation++;
        project = null;
        Drafts.Comparisons.Clear();
        ready = false;
        AccessDenied = true;
    }

    public async Task OpenAsync(ProjectWriteAdmission? target) {
        var opening = ++generation;
        project = target;
        Drafts.Comparisons.Clear();
        ready = false;
        reviewedUnknownOpening = -1;
        editorId = Guid.NewGuid();
        Message = null;
        RequiresAttention = AccessDenied = IsDeleting = false;
        UnknownDelete = null;
        var previous = observations.Values.ToArray();
        observations.Clear();
        foreach (var source in previous) {
            await source.DisposeAsync();
        }
        if (!Current(opening) || target is null) {
            return;
        }
        try {
            await BindAsync(AssignmentWriteArea.Assignments, EditingOwner.ProjectAssignmentRecords, assignmentChanges.Scope);
            await BindAsync(AssignmentWriteArea.Staffing, EditingOwner.CrmStaffingRecords, crmChanges.Scope);
            if (Current(opening)) {
                ready = true;
            }
        } catch (Exception exception) {
            logger.LogWarning(exception, "Assignment access could not open for project {ProjectId}.", target.ProjectId);
            if (Current(opening)) {
                Deny();
            }
        }

        async Task BindAsync(AssignmentWriteArea area, EditingOwner owner, EditingScope scope) {
            if (!Current(opening)) {
                return;
            }
            var source = new EditingObservation(feed, access, new(scope, owner, target.ProjectId), observationLog);
            observations.Add(area, source);
            await source.StartAsync(batch => Dispatch(async () => {
                if (!Current(opening) || !batch.RequiresResync && batch.Changes.All(change =>
                        change.OriginEditorId == editorId || operations.Contains(change.OperationId))) {
                    return;
                }
                reviewedUnknownOpening = -1;
                Drafts.Comparisons.Hint(area == AssignmentWriteArea.Staffing);
                await RecordsChanged(area);
                if (Current(opening)) {
                    Changed();
                }
            }), () => Dispatch(() => {
                if (Current(opening)) {
                    Deny();
                }
                return Task.CompletedTask;
            }));
            if (!Current(opening)) {
                await source.DisposeAsync();
            }
        }
    }

    public Task<bool> SaveRelationshipAsync(CancellationToken token) => SaveAsync<ProjectPartyAssignmentUpsertRequest, AssignmentField, ProjectAssignmentSaveReceipt>(AssignmentEditorKind.Relationship, Drafts.Relationship, AssignmentWriteArea.Assignments,
        (model, receipt) => SaveAssignment(model, token, receipt),
        (state, submitted, receipt) => AssignmentDrafts.Accept(state, submitted, From(receipt, submitted.Model)), token);

    public Task<bool> SaveAllocationAsync(CancellationToken token) => SaveAsync<ProjectPartyAssignmentUpsertRequest, AssignmentField, ProjectAssignmentSaveReceipt>(AssignmentEditorKind.Allocation, Drafts.Allocation, AssignmentWriteArea.Assignments,
        (model, receipt) => SaveAssignment(model, token, receipt),
        (state, submitted, receipt) => AssignmentDrafts.Accept(state, submitted, From(receipt, submitted.Model)), token);

    public Task<bool> SaveStaffingAsync(CancellationToken token) => SaveAsync<StaffingRequestEditorModel, StaffingField, StaffingRequestSaveReceipt>(AssignmentEditorKind.Staffing, Drafts.Staffing, AssignmentWriteArea.Staffing,
        (model, receipt) => staffing.SaveAsync(model, token, receipt),
        (state, submitted, receipt) => AssignmentDrafts.Accept(state, submitted, receipt.Accepted), token);

    private async Task<bool> SaveAsync<T, F, R>(AssignmentEditorKind editor, RecordDraftState<T, F> state, AssignmentWriteArea area,
        Func<T, Action<R>, Task<Result<Guid>>> save, Action<RecordDraftState<T, F>, RecordSubmission<T, F>, R> accept,
        CancellationToken token) where T : class where F : struct, Enum {
        if (!HasAccess || IsBusy || IsUnknown || !Drafts.Comparisons.CanSave(editor)) {
            return false;
        }
        var submitted = state.Begin();
        if (submitted is null) {
            return false;
        }
        var opening = generation;
        var accepted = false;
        var started = false;
        Message = null;
        RequiresAttention = false;
        bool Active() => Current(opening) && state.IsCurrent(submitted);
        async ValueTask Authorize(CancellationToken cancellation) {
            if (!Active()) {
                throw new UnauthorizedAccessException("The assignment editor opening retired before commit.");
            }
            await AuthorizeAsync(area, opening, write: true, cancellation);
            if (!Active()) {
                throw new UnauthorizedAccessException("The assignment editor opening retired during authorization.");
            }
        }
        try {
            await Authorize(token);
            using var authority = EnterAuthority(area, Authorize);
            using var origin = EnterOperation(area);
            started = true;
            var result = await save(submitted.Model, receipt => {
                accepted = true;
                if (Active()) {
                    accept(state, submitted, receipt);
                }
            });
            if (!Active()) {
                return accepted;
            }
            if (result.IsFailure) {
                if (result.Errors.Any(error => error.Code is ProjectAssignmentEditingErrors.Conflict or
                        ProjectAssignmentEditingErrors.PrimaryConflict or StaffingRequestEditingErrors.Conflict)) {
                    Drafts.Comparisons.MarkOutdated(editor);
                }
                Message = string.Join(" ", result.Errors.Select(error => error.Message));
                RequiresAttention = true;
                return false;
            }
            if (!accepted) {
                throw new InvalidOperationException("The assignment owner returned success without its committed receipt.");
            }
            Message = state.IsDirty ? "Saved. Later input is retained in this editor." : "Saved.";
            return true;
        } catch (Exception exception) {
            logger.LogWarning(exception, "Assignment {Area} save continuation failed; accepted {Accepted}.", area, accepted);
            if (Active()) {
                if (started && !accepted && exception is not (UnauthorizedAccessException or ProjectWriteAdmissionRejectedException)) {
                    state.Unknown(submitted);
                }
                Message = state.IsUnknown ? "The save outcome is unknown. Your submission and later input are retained. Refresh saved records before resolving this operation; saving again is blocked."
                    : accepted ? "The change committed. Follow-up failed; it will not be submitted again."
                    : "The change was refused before commit. Your input is retained; reopen with current project and write access.";
                RequiresAttention = true;
            }
            return accepted;
        } finally {
            state.Complete(submitted);
            if (Current(opening)) {
                Changed();
            }
        }
    }

    public async Task CompareAsync(AssignmentEditorKind editor, CancellationToken token) {
        if (!HasAccess || IsBusy || IsUnknown || project is not { } expected) {
            return;
        }
        var opening = generation;
        var comparison = Drafts.Comparisons;
        var request = comparison.BeginRead(editor);
        Changed();
        try {
            bool observed;
            if (editor == AssignmentEditorKind.Staffing) {
                var id = Drafts.Staffing.Draft.Id ?? throw new InvalidOperationException("Save this new request before comparing it.");
                var current = await ReadAsync(() => staffing.ReadAsync(id, token), token);
                observed = current is not null && current.ExpectedProjectAdmission == expected && comparison.Observe(request, current);
            } else {
                var model = editor == AssignmentEditorKind.Relationship ? Drafts.Relationship.Draft : Drafts.Allocation.Draft;
                var id = model.AssignmentId ?? throw new InvalidOperationException("Save this new assignment before comparing it.");
                var current = await ReadAsync(() => assignments.ReadAssignmentForEditingAsync(expected, id, token), token);
                observed = current is not null && comparison.Observe(request, AssignmentReviewModels.From(current, expected));
            }
            if (observed) {
                var ids = comparison.ReferencedParties().Where(id => id != Guid.Empty).ToArray();
                var names = await ReadAsync(async () => {
                    var values = new Dictionary<Guid, string>();
                    foreach (var id in ids) {
                        if (await assignments.GetPartyOptionAsync(id, token) is { } party) {
                            values.Add(id, $"{party.DisplayName} · {party.PartyTypeLabel}");
                        }
                    }
                    return values;
                }, token);
                comparison.SetNames(request, names);
            }
            if (!Current(opening)) {
                return;
            }
            if (!observed) {
                comparison.ReadFailed(request);
                Message = "This saved record is unavailable, or the draft changed during the read. Your input is retained; refresh the comparison before applying.";
                RequiresAttention = true;
            }
        } catch (Exception exception) {
            logger.LogWarning(exception, "Assignment comparison could not read {Editor} in project {ProjectId}.", editor, expected.ProjectId);
            if (Current(opening)) {
                comparison.ReadFailed(request);
                Message = "The current record could not be read. Your draft and comparison choices are retained.";
                RequiresAttention = true;
            }
        }
        if (Current(opening)) {
            Changed();
        }
    }

    public Task ApplyComparisonAsync(CancellationToken token) => RunComparisonActionAsync(Drafts.Comparisons.Apply,
        "Comparison applied to your draft. Save is a separate action.", token);

    public Task UndoComparisonAsync(CancellationToken token) => RunComparisonActionAsync(Drafts.Comparisons.Undo,
        "Your earlier local values were restored. Save will still check for newer saved changes.", token);

    private async Task RunComparisonActionAsync(Func<bool> action, string message, CancellationToken token) {
        var opening = generation;
        if (!HasAccess || IsBusy || IsUnknown) {
            return;
        }
        try {
            foreach (var area in Enum.GetValues<AssignmentWriteArea>()) {
                await AuthorizeAsync(area, opening, write: true, token);
            }
            if (Current(opening) && action()) {
                Message = message;
                RequiresAttention = false;
                Changed();
            }
        } catch (Exception exception) {
            logger.LogWarning(exception, "Assignment draft comparison could not apply in the current authorized opening.");
            if (Current(opening)) {
                Message = "Current assignment access could not be confirmed. The draft and comparison are retained.";
                RequiresAttention = true;
                Changed();
            }
        }
    }

    public async Task<bool> ReviewPrimaryAsync(ProjectPartyAssignmentUpsertRequest draft, CancellationToken token) {
        var opening = generation;
        var expected = draft.ExpectedProjectAdmission;
        var role = draft.Role;
        var node = draft.NodeKey.Trim();
        if (expected != project || expected is null || IsUnknown) {
            return false;
        }
        try {
            await AuthorizeAsync(AssignmentWriteArea.Assignments, opening, write: false, token);
            var basis = await assignments.ReadPrimaryObservationAsync(expected, role, node, token);
            await AuthorizeAsync(AssignmentWriteArea.Assignments, opening, write: false, token);
            if (!Current(opening) || draft.ExpectedProjectAdmission != expected || draft.Role != role || draft.NodeKey.Trim() != node) {
                return false;
            }
            draft.PrimaryOriginal = basis;
            return true;
        } catch (Exception exception) {
            logger.LogWarning(exception, "Assignment primary observation failed for project {ProjectId}.", expected.ProjectId);
            if (Current(opening)) {
                Message = "The current primary could not be read. Your input is retained; try reviewing the primary again.";
                RequiresAttention = true;
                Changed();
            }
            return false;
        }
    }

    public Task<bool> DeleteAssignmentAsync(ProjectPartyAssignmentDetail row, CancellationToken token) =>
        DeleteAsync(AssignmentWriteArea.Assignments, new(row, null), row.ProjectId, row.ProjectLifetimeId, async receipt => {
            await assignments.DeleteAssignmentAsync(row.Id, token,
                new(assignmentChanges.Scope.Id, row.ProjectId, row.ProjectLifetimeId), row.Original, _ => receipt());
            return Result.Success();
        }, token);

    public Task<bool> DeleteStaffingAsync(StaffingRequestItemModel row, CancellationToken token) =>
        DeleteAsync(AssignmentWriteArea.Staffing, new(null, row), row.ProjectId, row.ProjectLifetimeId,
            receipt => staffing.DeleteAsync(row.Id, row.Original, token, _ => receipt()), token);

    private async Task<bool> DeleteAsync(AssignmentWriteArea area, AssignmentPendingDelete pending, Guid? projectId,
        Guid? projectLifetimeId, Func<Action, Task<Result>> delete, CancellationToken token) {
        if (pending.Assignment is { Original: null } || pending.Staffing is { Original: null }) {
            Message = "Read the displayed row before deleting it. Your current input is retained.";
            RequiresAttention = true;
            Changed();
            return false;
        }
        if (!HasAccess || IsBusy || IsUnknown || projectId != project!.ProjectId || projectLifetimeId != project.LifetimeId) {
            return false;
        }
        var opening = generation;
        var accepted = false;
        var started = false;
        IsDeleting = true;
        Message = null;
        RequiresAttention = false;
        try {
            await AuthorizeAsync(area, opening, write: true, token);
            using var authority = EnterAuthority(area, cancellation => AuthorizeAsync(area, opening, write: true, cancellation));
            using var origin = EnterOperation(area);
            started = true;
            var result = await delete(() => accepted = true);
            if (!Current(opening)) {
                return accepted;
            }
            if (result.IsFailure) {
                Message = string.Join(" ", result.Errors.Select(error => error.Message));
                RequiresAttention = true;
                return false;
            }
            if (!accepted) {
                throw new InvalidOperationException("The assignment owner returned deletion success without its committed receipt.");
            }
            Message = "Deleted.";
            return true;
        } catch (Exception exception) {
            logger.LogWarning(exception, "Assignment {Area} delete continuation failed; accepted {Accepted}.", area, accepted);
            if (Current(opening)) {
                if (started && !accepted && exception is not (UnauthorizedAccessException or ProjectAssignmentConflictException)) {
                    UnknownDelete = pending;
                }
                Message = exception is ProjectAssignmentConflictException ? "This row changed. Refresh its displayed information before deleting it."
                    : UnknownDelete is not null ? "The delete outcome is unknown. The observed row is retained; repeating deletion is blocked. Refresh saved records to review it."
                    : accepted ? "Deletion committed. Follow-up failed; the delete will not be repeated."
                    : "Deletion was refused before commit. Reopen with current access.";
                RequiresAttention = true;
            }
            return accepted;
        } finally {
            if (Current(opening)) {
                IsDeleting = false;
                Changed();
            }
        }
    }

    private async ValueTask AuthorizeAsync(AssignmentWriteArea area, long opening, bool write, CancellationToken token) {
        if (!Current(opening) || AccessDenied || !observations.TryGetValue(area, out var source)) {
            throw new UnauthorizedAccessException("Current assignment access is required.");
        }
        await source.AuthorizeAsync(write, token);
        if (!Current(opening) || AccessDenied) {
            throw new UnauthorizedAccessException("The assignment opening retired during authorization.");
        }
    }

    public async Task<T> ReadAsync<T>(Func<Task<T>> read, CancellationToken token) {
        var opening = generation;
        foreach (var area in Enum.GetValues<AssignmentWriteArea>()) {
            await AuthorizeAsync(area, opening, write: false, token);
        }
        var result = await read();
        foreach (var area in Enum.GetValues<AssignmentWriteArea>()) {
            await AuthorizeAsync(area, opening, write: false, token);
        }
        return result;
    }

    private IDisposable EnterAuthority(AssignmentWriteArea area, Func<CancellationToken, ValueTask> authorize) =>
        area == AssignmentWriteArea.Assignments ? assignmentChanges.EnterUiWrite(authorize) : crmChanges.EnterUiWrite(authorize);

    private IDisposable EnterOperation(AssignmentWriteArea area) {
        var operation = new EditingOperationIdentity(editorId, Guid.NewGuid());
        operations.Enqueue(operation.OperationId);
        while (operations.Count > 32) {
            operations.Dequeue();
        }
        return area == AssignmentWriteArea.Assignments ? assignmentChanges.Enter(operation) : crmChanges.Enter(operation);
    }

    private bool Current(long opening) => !disposed && generation == opening;
    public void EditorOpened(AssignmentEditorKind editor) {
        Drafts.Comparisons.Opened(editor);
        if (!IsBusy && !IsUnknown) {
            Message = null;
            RequiresAttention = false;
        }
    }

    public void ReportReadFailure() {
        Message = "Saved records could not be refreshed. Your current input is retained; retry refreshing the records.";
        RequiresAttention = true;
        Changed();
    }

    public void MarkUnknownRecordsReviewed() {
        if (IsUnknown && HasAccess) {
            reviewedUnknownOpening = generation;
            Changed();
        }
    }

    public void InvalidateUnknownReview() => reviewedUnknownOpening = -1;

    public void DiscardReviewedUnknown(Action discardDrafts) {
        if (!CanDiscardReviewedUnknown || IsBusy) {
            throw new InvalidOperationException("Read current saved records before discarding a local pending operation.");
        }
        discardDrafts();
        UnknownDelete = null;
        reviewedUnknownOpening = -1;
        Message = "The local pending operation was discarded. Its server outcome remains unconfirmed; no write was repeated.";
        RequiresAttention = true;
        Changed();
    }
    private void Deny() {
        ready = false;
        AccessDenied = true;
        Message = "Current assignment access is required. Input is retained; reopen with current access before saving.";
        RequiresAttention = true;
        Changed();
    }

    private static ProjectPartyAssignmentUpsertRequest From(ProjectAssignmentSaveReceipt receipt, ProjectPartyAssignmentUpsertRequest submitted) {
        var row = receipt.Accepted;
        if (receipt.Id != row.Id || row.ProjectId != submitted.ProjectId || row.ProjectLifetimeId != submitted.ExpectedProjectAdmission?.LifetimeId ||
            submitted.AssignmentId is { } expectedId && expectedId != row.Id) {
            throw new InvalidOperationException("The accepted assignment receipt does not belong to the submitted identity and project lifetime.");
        }
        return new() {
            AssignmentId = row.Id, Original = row, ProjectId = row.ProjectId, ExpectedProjectAdmission = submitted.ExpectedProjectAdmission,
            PartyId = row.PartyId, PartyAffiliationId = row.PartyAffiliationId, Role = row.Role, NodeKey = row.NodeKey,
            IsPrimary = row.IsPrimary, AllocationPercent = row.AllocationPercent, Source = row.Source, Notes = row.Notes,
            StartsOn = row.StartsAtUtc is { } start ? DateOnly.FromDateTime(start.UtcDateTime) : null,
            EndsOn = row.EndsAtUtc is { } end ? DateOnly.FromDateTime(end.UtcDateTime) : null
        };
    }

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        generation++;
        foreach (var source in observations.Values.ToArray()) {
            await source.DisposeAsync();
        }
        observations.Clear();
    }
}
