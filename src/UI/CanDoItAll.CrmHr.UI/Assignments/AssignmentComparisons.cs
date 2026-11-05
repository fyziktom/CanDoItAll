using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.CrmHr.UI.Assignments;

public sealed class AssignmentComparisons(AssignmentDrafts drafts) {
    private readonly long[] hints = new long[2];
    private readonly Dictionary<AssignmentEditorKind, long> outdated = [];
    private long read;
    private bool refreshRequired;
    private long opening;
    private long input;
    private long comparedHint;
    private ProjectPartyAssignmentUpsertRequest? assignmentLocal;
    private StaffingRequestEditorModel? staffingLocal;
    private ProjectPartyAssignmentUpsertRequest? assignmentUndo;
    private StaffingRequestEditorModel? staffingUndo;
    private ProjectPartyAssignmentUpsertRequest? assignmentApplied;
    private StaffingRequestEditorModel? staffingApplied;
    private long undoOpening;
    private long undoInput;
    private ProjectAssignmentObservation? undoAssignmentOriginal;
    private StaffingRequestObservation? undoStaffingOriginal;
    private readonly Dictionary<Guid, string> names = [];
    private AssignmentEditorKind? undoEditor;
    public AssignmentEditorKind? Editor { get; private set; }
    public AssignmentReview? Assignment { get; private set; }
    public StaffingRequestReview? Staffing { get; private set; }
    public bool Reading { get; private set; }
    public bool KeepIncompleteInput { get; set; }
    public bool HasReview => Assignment is not null || Staffing is not null;
    public bool IsStale => HasReview && (refreshRequired || Editor is { } editor && (comparedHint != hints[Area(editor)] || !MatchesLocal(editor)));
    public bool HasPendingInput => Editor is { } editor && (editor == AssignmentEditorKind.Staffing
        ? drafts.Staffing.HasPendingInput : State(editor).HasPendingInput);
    public bool HasPendingSelection => Editor is { } editor && (editor == AssignmentEditorKind.Staffing
        ? Enum.GetValues<StaffingField>().Any(inputField => inputField is not (StaffingField.Allocation or StaffingField.Start or StaffingField.End) && drafts.Staffing.IsPending(inputField))
        : Enum.GetValues<AssignmentField>().Any(inputField => inputField is not (AssignmentField.Allocation or AssignmentField.Start or AssignmentField.End) && State(editor).IsPending(inputField)));
    public bool CanApply => Editor is { } editor && HasReview && !Reading && !IsStale &&
        !HasPendingSelection && (!HasPendingInput || KeepIncompleteInput) && (Assignment?.CanApply ?? Staffing?.CanApply ?? false) &&
        !drafts.IsUnknown && !Saving(editor);
    public bool CanUndo => undoEditor is { } editor && !drafts.IsUnknown && !Saving(editor) && Editor is null && !HasReview && !Reading &&
        (editor == AssignmentEditorKind.Staffing
            ? drafts.Staffing.Opening == undoOpening && drafts.Staffing.InputRevision == undoInput && drafts.Staffing.Draft.Original == undoStaffingOriginal && staffingApplied is { } staffing && AssignmentDrafts.Same(staffing, drafts.Staffing.Draft) && !drafts.Staffing.HasPendingInput
            : State(editor).Opening == undoOpening && State(editor).InputRevision == undoInput && State(editor).Draft.Original == undoAssignmentOriginal && assignmentApplied is { } assignment && AssignmentDrafts.Same(assignment, State(editor).Draft) && !State(editor).HasPendingInput);

    public bool CanUndoFor(AssignmentEditorKind editor) => CanUndo && undoEditor == editor;
    public string PartyName(Guid? id) => id is { } value
        ? $"{names.GetValueOrDefault(value, "Unavailable party")} ({value.ToString("N")[..8]})" : "None";
    public IEnumerable<Guid> ReferencedParties() => Assignment is { } assignment
        ? new[] { assignment.Party.Original, assignment.Party.Local, assignment.Party.Current }.Distinct()
        : Staffing is { } staffing
            ? new[] { staffing.Requester.Original, staffing.Requester.Local, staffing.Requester.Current,
                staffing.DeliveryUnit.Original, staffing.DeliveryUnit.Local, staffing.DeliveryUnit.Current }
                .OfType<Guid>().Distinct()
            : [];
    public void SetNames(long request, IReadOnlyDictionary<Guid, string> values) {
        if (request != read || Editor is null) {
            return;
        }
        names.Clear();
        foreach (var value in values) {
            names.Add(value.Key, value.Value);
        }
    }

    public void MarkOutdated(AssignmentEditorKind editor) => outdated[editor] = Opening(editor);
    public bool CanSave(AssignmentEditorKind editor) => (!outdated.TryGetValue(editor, out var observedOpening) || observedOpening != Opening(editor)) && Editor != editor;
    public bool IsPending(AssignmentField field) => Editor is { } editor && editor != AssignmentEditorKind.Staffing && State(editor).IsPending(field);
    public bool IsPending(StaffingField field) => drafts.Staffing.IsPending(field);
    public void Opened(AssignmentEditorKind editor) {
        outdated.Remove(editor);
        if (Editor == editor) {
            Cancel();
        }
        if (undoEditor == editor) {
            undoEditor = null;
        }
    }

    public void Hint(bool staffing) {
        hints[staffing ? 1 : 0]++;
        if (staffing) {
            if (drafts.Staffing.Draft.Id.HasValue) {
                MarkOutdated(AssignmentEditorKind.Staffing);
            }
        } else {
            foreach (var editor in new[] { AssignmentEditorKind.Relationship, AssignmentEditorKind.Allocation }) {
                if (State(editor).Draft.AssignmentId.HasValue) {
                    MarkOutdated(editor);
                }
            }
        }
    }

    public long BeginRead(AssignmentEditorKind editor) {
        if (drafts.IsUnknown || Saving(editor)) {
            throw new InvalidOperationException("A pending assignment write cannot be compared.");
        }
        if (Editor != editor) {
            Cancel();
        }
        Editor = editor;
        Reading = true;
        refreshRequired = true;
        KeepIncompleteInput = false;
        comparedHint = hints[Area(editor)];
        if (editor == AssignmentEditorKind.Staffing) {
            opening = drafts.Staffing.Opening;
            input = drafts.Staffing.InputRevision;
            staffingLocal = drafts.Staffing.Draft.Snapshot();
        } else {
            opening = State(editor).Opening;
            input = State(editor).InputRevision;
            assignmentLocal = State(editor).Draft.Snapshot();
        }
        return ++read;
    }

    public bool Observe(long request, ProjectPartyAssignmentUpsertRequest current) {
        if (request != read || Editor is not { } editor || editor == AssignmentEditorKind.Staffing) {
            return false;
        }
        Reading = false;
        if (comparedHint != hints[Area(editor)] || !MatchesLocal(editor) || assignmentLocal is not { Original: { } original, ExpectedProjectAdmission: { } project } local ||
            current.AssignmentId != local.AssignmentId || current.ExpectedProjectAdmission != project) {
            return false;
        }
        var next = new AssignmentReview(AssignmentReviewModels.From(original, project), local, current);
        if (Assignment is { } previous) {
            next.PreserveChoices(previous);
        }
        if (IsPending(AssignmentField.Allocation)) {
            next.Allocation.UseLocal();
        }
        if (IsPending(AssignmentField.Start)) {
            next.Start.UseLocal();
        }
        if (IsPending(AssignmentField.End)) {
            next.End.UseLocal();
        }
        Assignment = next;
        refreshRequired = false;
        return true;
    }

    public bool Observe(long request, StaffingRequestEditorModel current) {
        if (request != read || Editor != AssignmentEditorKind.Staffing) {
            return false;
        }
        Reading = false;
        if (comparedHint != hints[1] || !MatchesLocal(AssignmentEditorKind.Staffing) || staffingLocal is not { Original: { } original, ExpectedProjectAdmission: { } project } local ||
            current.Id != local.Id || current.ExpectedProjectAdmission != project) {
            return false;
        }
        var next = new StaffingRequestReview(AssignmentReviewModels.From(original, project), local, current);
        if (Staffing is { } previous) {
            next.PreserveChoices(previous);
        }
        if (IsPending(StaffingField.Allocation)) {
            next.Allocation.UseLocal();
        }
        if (IsPending(StaffingField.Start)) {
            next.Start.UseLocal();
        }
        if (IsPending(StaffingField.End)) {
            next.End.UseLocal();
        }
        Staffing = next;
        refreshRequired = false;
        return true;
    }

    public void ReadFailed(long request) {
        if (request == read) {
            Reading = false;
        }
    }

    public bool Apply() {
        if (!CanApply || Editor is not { } editor) {
            return false;
        }
        undoEditor = editor;
        undoOpening = opening;
        undoInput = input;
        if (editor == AssignmentEditorKind.Staffing) {
            staffingUndo = drafts.Staffing.Draft.Snapshot();
            AssignmentDrafts.Copy(drafts.Staffing.Draft, Staffing!.Candidate());
            drafts.Staffing.Rebase(Staffing.Current);
            undoStaffingOriginal = Staffing.Current.Original;
            staffingApplied = drafts.Staffing.Draft.Snapshot();
        } else {
            assignmentUndo = State(editor).Draft.Snapshot();
            AssignmentDrafts.Copy(State(editor).Draft, Assignment!.Candidate());
            State(editor).Rebase(Assignment.Current);
            undoAssignmentOriginal = Assignment.Current.Original;
            assignmentApplied = State(editor).Draft.Snapshot();
        }
        outdated.Remove(editor);
        Cancel();
        return true;
    }

    public bool Undo() {
        if (!CanUndo || undoEditor is not { } editor) {
            return false;
        }
        if (editor == AssignmentEditorKind.Staffing) {
            staffingUndo!.Original = drafts.Staffing.Draft.Original;
            staffingUndo.ExpectedProjectAdmission = drafts.Staffing.Draft.ExpectedProjectAdmission;
            staffingUndo.ExpectedSourceProjectReference = drafts.Staffing.Draft.ExpectedSourceProjectReference;
            AssignmentDrafts.Copy(drafts.Staffing.Draft, staffingUndo);
        } else {
            assignmentUndo!.Original = State(editor).Draft.Original;
            assignmentUndo.ExpectedProjectAdmission = State(editor).Draft.ExpectedProjectAdmission;
            assignmentUndo.PrimaryOriginal = null;
            AssignmentDrafts.Copy(State(editor).Draft, assignmentUndo);
        }
        undoEditor = null;
        return true;
    }

    public void Cancel() {
        read++;
        Reading = false;
        Editor = null;
        refreshRequired = false;
        Assignment = null;
        Staffing = null;
        assignmentLocal = null;
        staffingLocal = null;
    }

    public void Clear() {
        Cancel();
        outdated.Clear();
        names.Clear();
        undoEditor = null;
        assignmentUndo = null;
        staffingUndo = null;
    }

    private bool MatchesLocal(AssignmentEditorKind editor) => editor == AssignmentEditorKind.Staffing
        ? drafts.Staffing.Opening == opening && drafts.Staffing.InputRevision == input && staffingLocal is { } staffing &&
            AssignmentDrafts.Same(staffing, drafts.Staffing.Draft)
        : State(editor).Opening == opening && State(editor).InputRevision == input && assignmentLocal is { } assignment &&
            AssignmentDrafts.Same(assignment, State(editor).Draft);
    private long Opening(AssignmentEditorKind editor) => editor == AssignmentEditorKind.Staffing
        ? drafts.Staffing.Opening : State(editor).Opening;
    private bool Saving(AssignmentEditorKind editor) => editor == AssignmentEditorKind.Staffing
        ? drafts.Staffing.IsSaving : State(editor).IsSaving;
    private CanDoItAll.Editing.RecordDraftState<ProjectPartyAssignmentUpsertRequest, AssignmentField> State(AssignmentEditorKind editor) =>
        editor switch {
            AssignmentEditorKind.Relationship => drafts.Relationship,
            AssignmentEditorKind.Allocation => drafts.Allocation,
            _ => throw new ArgumentOutOfRangeException(nameof(editor))
        };
    private static int Area(AssignmentEditorKind editor) => editor == AssignmentEditorKind.Staffing ? 1 : 0;
}
