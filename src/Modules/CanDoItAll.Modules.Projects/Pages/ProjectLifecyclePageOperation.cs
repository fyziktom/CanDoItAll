using CanDoItAll.Modules.Projects.Pages.Components;

namespace CanDoItAll.Modules.Projects.Pages;

internal sealed class ProjectLifecyclePageOperation : IDisposable {
    private bool disposed;

    internal ProjectLifecyclePageOperation(ProjectLifecyclePendingSession pending, ProjectEditorDraft? target, long editorIntent) {
        ArgumentNullException.ThrowIfNull(pending);
        if (target is not null && (pending.Frozen.Submission.Command is not ProjectLifecycleCommand.Delete deletion ||
                target.Model.ExpectedDefinition != deletion.Expected || target.Model.Id != deletion.Expected.Project.ProjectId)) {
            throw new InvalidOperationException("The deletion lifetime must retain its displayed original definition.");
        }
        Pending = pending;
        Target = target;
        EditorIntent = editorIntent;
    }

    internal ProjectLifecyclePendingSession Pending { get; }
    internal ProjectEditorDraft? Target { get; }
    internal long EditorIntent { get; }
    internal bool Applied { get; private set; }
    internal bool Cleared { get; set; }
    internal bool EditorRetired { get; private set; }
    internal CancellationTokenSource DispatchLifetime { get; } = new();

    internal bool IsOriginalEditor(ProjectEditorDraft current, long intent, bool visible) => !disposed && !EditorRetired &&
        Target is { } target && visible && ReferenceEquals(current, target) && intent == EditorIntent &&
        Pending.Frozen.Submission.Command is ProjectLifecycleCommand.Delete deletion && target.Model.ExpectedDefinition == deletion.Expected;

    internal bool CanApply(ProjectEditorDraft current, long intent, bool visible) => !Applied &&
        IsOriginalEditor(current, intent, visible) && Pending.CanReconcile;

    internal bool TryApply(ProjectEditorDraft current, long intent, bool visible,
        Action<ProjectEditorDraft, long, ProjectLifecycleAccepted> apply) {
        ArgumentNullException.ThrowIfNull(apply);
        if (!CanApply(current, intent, visible)) {
            return false;
        }
        Pending.Reconcile(accepted => {
            if (accepted.Acknowledgement is not ProjectLifecycleAcknowledgement.DeletionCommitted) {
                throw new InvalidDataException("The original deletion lacks a committed deletion acknowledgement.");
            }
            Applied = true;
            apply(Target!, EditorIntent, accepted);
        });
        return true;
    }

    internal void RetireEditor() {
        if (!EditorRetired) {
            EditorRetired = true;
            DispatchLifetime.Cancel();
        }
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        try {
            RetireEditor();
        } finally {
            Pending.Dispose();
            DispatchLifetime.Dispose();
        }
    }
}
