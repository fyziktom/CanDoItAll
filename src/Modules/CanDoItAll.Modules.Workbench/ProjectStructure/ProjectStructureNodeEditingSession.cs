using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Workbench.Structure.UI;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureNodeEditingSession(Guid openingId, ProjectWriteAdmission project,
    ProjectStructureNode original, ProjectStructureNodeForm form, EditingObservation access,
    Func<CancellationToken, Task<ProjectStructureNode>> readCurrent, Func<bool> isCurrent) : IAsyncDisposable {
    private readonly CancellationTokenSource lifetime = new();
    private ProjectStructureChangeCursor? hinted;
    private ProjectStructureChangeCursor? comparedHint;
    private CanvasWorkbenchComposerDraft? reviewedDraft;
    private CanvasWorkbenchComposerDraft? undoDraft;
    private long undoRevision;
    private bool retired;
    public Guid OpeningId { get; } = openingId;
    public ProjectStructureNode Baseline { get; private set; } = original;
    public ProjectStructureNodeForm Form { get; } = form;
    public StructureNodeFormReview? Review { get; private set; }
    public StructureNodeFormReview? PreviousReview { get; private set; }
    public ProjectStructureNode? Current { get; private set; }
    public string? Message { get; private set; }
    public bool Busy { get; private set; }
    public bool ShowComparison { get; set; }
    public bool CanUndo => undoDraft is not null && IsActive;
    public bool CurrentMayBeStale => Review is not null && hinted != comparedHint;
    private bool IsActive => !retired && isCurrent() && !access.Lifetime.IsCancellationRequested;

    public async Task OpenAsync(Func<Task>? onRetired = null) {
        try {
            await access.StartAsync(_ => Task.CompletedTask, async () => {
                Message = "Structure editing access changed. Your local draft is retained; reopen with current access.";
                RetireCore(stopObservation: false);
                if (onRetired is not null) {
                    await onRetired();
                }
            });
            await AuthorizeAsync(write: false, lifetime.Token);
        } catch {
            Retire();
            throw;
        }
    }

    public void ObserveHint(ProjectStructureChangeCursor? cursor) {
        if (IsActive && cursor is not null) {
            hinted = cursor;
        }
    }

    public ValueTask AuthorizeWriteAsync(CancellationToken cancellationToken) => AuthorizeAsync(write: true, cancellationToken);

    private async ValueTask AuthorizeAsync(bool write, CancellationToken cancellationToken) {
        try {
            if (!IsActive) {
                throw new UnauthorizedAccessException("The original Structure editor is no longer current.");
            }
            await access.AuthorizeAsync(write, cancellationToken);
            if (!IsActive) {
                throw new UnauthorizedAccessException("Structure editing access changed. Reopen with current access.");
            }
        } catch (UnauthorizedAccessException) {
            Retire();
            throw;
        }
    }

    private async Task<ProjectStructureNode> ReadAsync(CancellationToken cancellationToken) {
        await AuthorizeAsync(write: false, cancellationToken);
        var result = await readCurrent(cancellationToken);
        await AuthorizeAsync(write: false, cancellationToken);
        if (result.RecordId != Baseline.RecordId || result.ObjectType != Baseline.ObjectType ||
            result.ObjectSubtype != Baseline.ObjectSubtype || result.ProjectId != project.ProjectId) {
            throw new ProjectStructureEditConflictException("The original node occurrence or kind changed. Your draft is retained.");
        }
        return result;
    }

    public async Task CompareAsync(CanvasWorkbenchComposerDraft draft, Func<Task<CanvasWorkbenchComposerDraft?>> readDraft) {
        if (Busy || !IsActive) {
            return;
        }
        ShowComparison = true;
        if (draft.OpeningId != OpeningId || draft.HasPendingInput || !draft.CanApply || Review?.HasPendingInput == true) {
            Message = "Finish the original input and any comparison result first. Unconfirmed writes must be observed; comparison cannot retry them.";
            return;
        }
        Busy = true;
        var hint = hinted;
        var previous = Review;
        var revision = previous?.InputRevision;
        try {
            var local = Form.Read(draft, Baseline);
            var current = await ReadAsync(lifetime.Token);
            var actual = await readDraft();
            if (!IsActive || !SameDraft(draft, actual) || previous?.InputRevision != revision) {
                Message = "The draft or comparison changed during the current read. Compare again; your entries are retained.";
                return;
            }
            var review = new StructureNodeFormReview(Form.Schema, Form.Values(Baseline), local, Form.Values(current));
            if (previous is not null) {
                review.PreserveChoices(previous);
            }
            PreviousReview = previous;
            Review = review;
            Current = current;
            comparedHint = hint;
            reviewedDraft = draft;
            Message = "Review the proposed result. Apply changes only this draft; Save remains separate.";
        } finally {
            Busy = false;
        }
    }

    public async Task<bool> ApplyAsync(Func<CanvasWorkbenchComposerApplyRequest, Task<CanvasWorkbenchComposerApplyResult>> apply) {
        if (Busy || !IsActive || Review is not { CanApply: true } review || Current is null || reviewedDraft is null) {
            return false;
        }
        Busy = true;
        var revision = review.InputRevision;
        try {
            var candidate = review.Candidate();
            if (!await VerifyCurrentAsync(Current, lifetime.Token) || review.InputRevision != revision) {
                return false;
            }
            await AuthorizeWriteAsync(lifetime.Token);
            var result = await apply(Form.Patch(reviewedDraft, candidate));
            if (result.Status != CanvasWorkbenchComposerApplyStatus.Applied || !IsActive) {
                Message = RefusalMessage(result.Status);
                return false;
            }
            undoDraft = reviewedDraft;
            undoRevision = result.Revision;
            Baseline = Current;
            ShowComparison = false;
            Message = "Comparison applied to the draft. Save is a separate action.";
            return true;
        } finally {
            Busy = false;
        }
    }

    public async Task UndoAsync(Func<CanvasWorkbenchComposerApplyRequest, Task<CanvasWorkbenchComposerApplyResult>> apply) {
        if (Busy || !CanUndo || undoDraft is null) {
            return;
        }
        Busy = true;
        try {
            if (!await VerifyCurrentAsync(Baseline, lifetime.Token)) {
                return;
            }
            await AuthorizeWriteAsync(lifetime.Token);
            var result = await apply(new(OpeningId, undoRevision, undoDraft.CreateRequest, undoDraft.EditRequest));
            if (result.Status != CanvasWorkbenchComposerApplyStatus.Applied || !IsActive) {
                Message = RefusalMessage(result.Status);
                return;
            }
            undoDraft = null;
            Review = null;
            reviewedDraft = null;
            Message = "The previous local draft was restored. The accepted current version and access remain the baseline for a separate Save.";
        } finally {
            Busy = false;
        }
    }

    private async Task<bool> VerifyCurrentAsync(ProjectStructureNode expected, CancellationToken cancellationToken) {
        var current = await ReadAsync(cancellationToken);
        if (ProjectStructureNodeStateGuard.HasSameEditableContent(expected, current)) {
            return true;
        }
        Message = "The saved node changed again. Refresh the comparison; your draft and result entries are retained.";
        ShowComparison = true;
        return false;
    }

    private static string RefusalMessage(CanvasWorkbenchComposerApplyStatus status) => status switch {
        CanvasWorkbenchComposerApplyStatus.InputChanged => "Your input changed during verification. It is retained; compare the current draft again.",
        CanvasWorkbenchComposerApplyStatus.PendingInput => "Finish or clear the unfinished editor input before applying a result. Your raw input is retained.",
        CanvasWorkbenchComposerApplyStatus.OutcomeRequiresReview => "Review the original save result before continuing. A comparison cannot retry an unconfirmed write.",
        CanvasWorkbenchComposerApplyStatus.ShapeChanged => "The result does not fit this editor's current fields. Correct it or copy the draft before reopening the node.",
        _ => "The original editor is no longer current. Your newer editor has not been changed."
    };

    private static bool SameDraft(CanvasWorkbenchComposerDraft expected, CanvasWorkbenchComposerDraft? actual) =>
        actual is not null && actual.OpeningId == expected.OpeningId && actual.Revision == expected.Revision &&
        !actual.HasPendingInput && actual.CanApply;

    public void Retire() => RetireCore(stopObservation: true);

    private void RetireCore(bool stopObservation) {
        if (retired) {
            return;
        }
        retired = true;
        lifetime.Cancel();
        if (stopObservation) {
            access.Stop();
        }
        Review = null;
        PreviousReview = null;
        Current = null;
        ShowComparison = false;
    }

    public async ValueTask DisposeAsync() {
        Retire();
        await access.DisposeAsync();
        lifetime.Dispose();
    }

    public void ReportFailure(string message) => Message = message;
}
