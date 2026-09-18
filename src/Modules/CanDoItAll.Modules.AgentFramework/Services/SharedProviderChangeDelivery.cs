using CanDoItAll.Modules.AgentFramework.ProviderManagement;

namespace CanDoItAll.Modules.AgentFramework;

public enum SharedProviderDeliveryDisposition { Acknowledged, Pending, InProgress, NotCurrent }

public sealed class SharedProviderChangeDelivery(Guid attemptId, SharedProviderChange change) {
    private readonly object gate = new();
    private Task? reconciliation;
    private bool acknowledged;

    public Guid AttemptId { get; } = attemptId;
    public SharedProviderChange Change { get; } = change;
    public bool IsAcknowledged {
        get {
            lock (gate) {
                return acknowledged;
            }
        }
    }
    internal bool InProgress { get; set; }

    public Task ReconcileAsync(Func<Task> reconcile) {
        ArgumentNullException.ThrowIfNull(reconcile);
        TaskCompletionSource completion;
        lock (gate) {
            if (acknowledged) {
                return Task.CompletedTask;
            }
            if (reconciliation is not null) {
                return reconciliation;
            }
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            reconciliation = completion.Task;
        }
        _ = CompleteReconciliationAsync(reconcile, completion);
        return completion.Task;
    }

    private async Task CompleteReconciliationAsync(Func<Task> reconcile, TaskCompletionSource completion) {
        try {
            await reconcile();
            lock (gate) {
                acknowledged = true;
                reconciliation = null;
                completion.SetResult();
            }
        } catch (OperationCanceledException exception) {
            lock (gate) {
                reconciliation = null;
                completion.SetCanceled(exception.CancellationToken);
            }
        } catch (Exception exception) {
            lock (gate) {
                reconciliation = null;
                completion.SetException(exception);
            }
        }
    }
}
