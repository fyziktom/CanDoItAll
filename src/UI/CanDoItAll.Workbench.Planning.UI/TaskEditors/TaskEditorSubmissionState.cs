using Microsoft.Extensions.Logging;

namespace CanDoItAll.Workbench.Planning.UI;

internal sealed class TaskEditorSubmissionState : IDisposable {
    private bool disposed;
    public bool IsBusy { get; private set; }
    public PlanningTaskSaveResult? Result { get; private set; }
    public bool CanSubmit => !disposed && !IsBusy && (Result is null || Result.CanRetry);

    public Task SaveAsync(Func<Task<PlanningTaskSaveResult>> save, ILogger logger)
        => CanSubmit ? RunAsync(save, logger) : Task.CompletedTask;

    public Task ReadbackAsync(Func<Task<PlanningTaskSaveResult>> readback, ILogger logger)
        => !disposed && !IsBusy && Result?.RequiresReadback == true ? RunAsync(readback, logger) : Task.CompletedTask;

    private async Task RunAsync(Func<Task<PlanningTaskSaveResult>> action, ILogger logger) {
        IsBusy = true;
        var operation = Guid.NewGuid();
        try {
            var result = await action();
            if (!disposed) {
                Result = result;
            }
        } catch (Exception exception) {
            logger.LogWarning("Task editor operation {OperationId} could not be confirmed; failure type {FailureType}.", operation, exception.GetType().Name);
            if (!disposed) {
                Result = new(PlanningTaskSaveStatus.Unknown,
                    "The operation could not be confirmed. Your draft is retained. Read the original project before attempting another save.",
                    Result?.TaskIds ?? [], Result?.ResourceNodeIds ?? [], Result?.Phases ?? [], true) { OperationId = operation };
            }
        } finally {
            IsBusy = false;
        }
    }

    public void Dispose() {
        disposed = true;
    }
}
