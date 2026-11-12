namespace CanDoItAll.Workspace.StorageCatalog.UiSandbox;

public enum CatalogScenarioStage { CatalogRead, SecretsRead, RoutesRead, EditorRead, Admission, Driver, Persistence, Routing, Activity, ReadBack }

public sealed class CatalogScenarioGate {
    private TaskCompletionSource? held;
    public bool IsHeld => held is not null;
    public bool IsEntered { get; private set; }
    public bool FailNext { get; set; }
    public void Hold() {
        held ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
        IsEntered = false;
    }
    public void Release() {
        var pending = held;
        held = null;
        pending?.TrySetResult();
    }
    public async Task EnterAsync() {
        IsEntered = true;
        if (held is { } pending) {
            await pending.Task;
        }
        if (FailNext) {
            FailNext = false;
            throw new InvalidOperationException("Synthetic stage failure; internal details must stay outside the UI.");
        }
    }
}
