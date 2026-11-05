using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workbench.Planning.UI;

namespace CanDoItAll.Modules.Workbench.Pages;

internal sealed class ProjectTaskDialogCloseOwner : IDisposable {
    private DialogReference? reference;
    private Func<PlanningTaskCloseRequest?>? readCurrent;
    private Func<Task<bool>>? confirmLeave;
    private PlanningTaskCloseRequest? accepted;
    private readonly Func<Task<bool>> approve;
    private bool disposed;

    public ProjectTaskDialogCloseOwner() {
        approve = ApproveAsync;
    }

    public void Attach(DialogReference? current, Func<PlanningTaskCloseRequest?> read, Func<Task<bool>> confirm) {
        if (disposed) {
            return;
        }
        if (!ReferenceEquals(reference, current)) {
            Detach();
            if (current?.BeforeClose is not null) {
                throw new InvalidOperationException("The task dialog already has a close owner.");
            }
            reference = current;
            if (reference is not null) {
                reference.BeforeClose = approve;
            }
        }
        readCurrent = read;
        confirmLeave = confirm;
    }

    public async Task<bool> CloseAcceptedAsync(DialogReference original, PlanningTaskCloseRequest? submitted, object result) {
        if (disposed || submitted is null || !ReferenceEquals(reference, original) || !MatchesInput(submitted)) {
            return false;
        }
        accepted = submitted;
        try {
            await original.CloseAsync(result);
            return original.Result.IsCompleted;
        } finally {
            accepted = null;
        }
    }

    private Task<bool> ApproveAsync() => disposed ? Task.FromResult(false) :
        accepted is { } submitted && MatchesInput(submitted) ? Task.FromResult(true) :
        confirmLeave?.Invoke() ?? Task.FromResult(false);

    private bool MatchesInput(PlanningTaskCloseRequest submitted) => readCurrent?.Invoke() is { } current &&
        current.OpeningId == submitted.OpeningId && current.Input == submitted.Input;

    private void Detach() {
        if (reference?.BeforeClose == approve) {
            reference.BeforeClose = null;
        }
        reference = null;
    }

    public void Dispose() {
        disposed = true;
        Detach();
    }
}
