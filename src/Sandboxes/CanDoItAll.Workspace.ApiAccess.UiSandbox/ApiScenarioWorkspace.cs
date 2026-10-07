using CanDoItAll.Workspace.ApiAccess.UI;

namespace CanDoItAll.Workspace.ApiAccess.UiSandbox;

public sealed class ApiScenarioWorkspace : IDisposable {
    private readonly List<ApiScenarioStore> retired = [];
    private readonly List<ApiScenarioGate> held = [];
    public ApiScenarioWorkspace() {
        Store = new();
        Session = CreateSession(Store);
        Store.Changed += Notify;
    }
    public event Action? Changed;
    public ApiScenarioStore Store { get; private set; }
    public ApiAccessSession Session { get; private set; }
    public IReadOnlyList<ApiScenarioStore> Retired => retired;
    public string? ControlMessage { get; private set; }
    public int HeldCount => held.Count(gate => !gate.IsReleased);

    public async Task ResetAsync(ApiScenario scenario) {
        if (retired.Count == 8) {
            var completed = retired.FindIndex(store => store.PendingWrites == 0);
            if (completed < 0) {
                ControlMessage = "Eight retired stores still own admitted writes. Release them before resetting again.";
                Notify();
                return;
            }
            retired[completed].Changed -= Notify;
            retired.RemoveAt(completed);
        }
        Session.Dispose();
        retired.Add(Store);
        Store = new(scenario);
        Store.Changed += Notify;
        Session = CreateSession(Store);
        ControlMessage = "A new independent fixture is active. Admitted old writes still belong to their original store.";
        Notify();
        await Session.LoadAsync();
    }
    public void Hold(ApiScenarioOperation operation) {
        held.RemoveAll(gate => gate.IsReleased);
        if (held.Count == 16) {
            ControlMessage = "Release held operations before adding more.";
        } else {
            held.Add(Store.HoldNext(operation));
            ControlMessage = $"The next {operation} will wait for Release held operations.";
        }
        Notify();
    }
    public void Release() {
        foreach (var gate in held) {
            gate.Release();
        }
        held.Clear();
        Notify();
    }
    public void Retire() {
        Session.Dispose();
        ControlMessage = "The view retired. Disclosure and passwords are released. Reset to open a new fixture; release pending writes to observe their original store.";
        Notify();
    }
    private static ApiAccessSession CreateSession(ApiScenarioStore store) => new(store, store, store, new());
    private void Notify() => Changed?.Invoke();
    public void Dispose() {
        Session.Dispose();
        Store.Changed -= Notify;
        foreach (var store in retired) {
            store.Changed -= Notify;
        }
        Changed = null;
        Release();
    }
}
