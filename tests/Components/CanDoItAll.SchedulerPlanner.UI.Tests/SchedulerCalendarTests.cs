using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.SchedulerPlanner.Presentation;
using CanDoItAll.SchedulerPlanner.UI;
using CanDoItAll.SchedulerPlanner.UiSandbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace CanDoItAll.Tests.Components.SchedulerPlanner;

public sealed class SchedulerCalendarTests : BunitContext {
    public SchedulerCalendarTests() {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exact_state_identity_opens_once_even_when_event_payload_is_null(bool doubleClickFirst) {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        var cut = Render<SchedulerCalendar>(parameters => parameters.Add(item => item.Workspace, workspace).Add(item => item.Surface, workspace.Data!.CalendarSurface));
        var calendar = cut.FindComponent<CanvasCalendar>().Instance;
        var selected = workspace.Data!.CalendarSurface.Events.First();
        await cut.InvokeAsync(() => cut.Instance.BeginCalendarPointer(1));
        if (doubleClickFirst) {
            await cut.InvokeAsync(() => cut.Instance.OnCalendarItemDoubleClickedAsync(1));
        }
        await cut.InvokeAsync(() => calendar.OnSelectionChanged("null", "{}"));
        await cut.InvokeAsync(() => calendar.OnStateChanged("{}", selected.Id, "2026-09-28", "week", "week", "UTC"));
        if (!doubleClickFirst) {
            await cut.InvokeAsync(() => cut.Instance.OnCalendarItemDoubleClickedAsync(1));
        }
        await cut.InvokeAsync(() => cut.Instance.OnCalendarItemDoubleClickedAsync(1));
        Assert.Equal(SchedulerScenarioStore.PlanA, workspace.EditDraft!.Values.Id);
        Assert.Equal(1, store.EditorReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_selection_cannot_reopen_a_closed_dialog_or_successor_tab(bool changeTab) {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        var cut = Render<SchedulerCalendar>(parameters => parameters.Add(item => item.Workspace, workspace).Add(item => item.Surface, workspace.Data!.CalendarSurface));
        var calendar = cut.FindComponent<CanvasCalendar>().Instance;
        await cut.InvokeAsync(() => cut.Instance.BeginCalendarPointer(1));
        await cut.InvokeAsync(() => cut.Instance.OnCalendarItemDoubleClickedAsync(1));
        if (changeTab) {
            workspace.Tab = SchedulerTab.History;
        } else {
            workspace.CloseEdit();
        }
        await cut.InvokeAsync(() => calendar.OnSelectionChanged("null", "{}"));
        await cut.InvokeAsync(() => calendar.OnStateChanged("{}", workspace.Data!.CalendarSurface.Events.First().Id, "2026-09-28", "week", "week", "UTC"));
        Assert.Null(workspace.EditDraft);
        Assert.Equal(0, store.EditorReads);
    }

    [Fact]
    public async Task Import_finishing_after_retirement_never_attaches_and_disposes_module() {
        ComponentFactories.AddStub<CanvasCalendar>();
        var runtime = new HeldImport();
        Services.AddSingleton<IJSRuntime>(runtime);
        using var workspace = new SchedulerWorkspace(new SchedulerScenarioStore());
        await workspace.InitializeAsync();
        var cut = Render<SchedulerCalendar>(parameters => parameters.Add(item => item.Workspace, workspace).Add(item => item.Surface, workspace.Data!.CalendarSurface));
        var retiring = cut.Instance.DisposeAsync().AsTask();
        var module = new CalendarModule();
        runtime.Result.SetResult(module);
        await retiring;
        Assert.Empty(module.Calls);
        Assert.Equal(1, module.Disposals);
    }

    [Fact]
    public async Task Disconnected_detach_still_disposes_owned_module() {
        ComponentFactories.AddStub<CanvasCalendar>();
        var runtime = new HeldImport();
        var module = new CalendarModule { Disconnect = true };
        runtime.Result.SetResult(module);
        Services.AddSingleton<IJSRuntime>(runtime);
        using var workspace = new SchedulerWorkspace(new SchedulerScenarioStore());
        await workspace.InitializeAsync();
        var cut = Render<SchedulerCalendar>(parameters => parameters.Add(item => item.Workspace, workspace).Add(item => item.Surface, workspace.Data!.CalendarSurface));
        await cut.Instance.DisposeAsync();
        Assert.Equal(new[] { "attach", "detach" }, module.Calls);
        Assert.Equal(1, module.Disposals);
    }

    private sealed class HeldImport : IJSRuntime {
        public TaskCompletionSource<IJSObjectReference> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            Assert.Equal("import", identifier);
            return (TValue)await Result.Task;
        }
    }

    private sealed class CalendarModule : IJSObjectReference {
        public List<string> Calls { get; } = [];
        public int Disposals { get; private set; }
        public bool Disconnect { get; init; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            Calls.Add(identifier);
            if (Disconnect && identifier == "detach") {
                throw new JSDisconnectedException("Test circuit disconnected.");
            }
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask DisposeAsync() {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }
}
