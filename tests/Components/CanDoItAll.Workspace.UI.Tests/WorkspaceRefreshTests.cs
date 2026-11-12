using CanDoItAll.Modules.Workspace;
using CanDoItAll.Workspace.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceUi;

public sealed class WorkspaceRefreshTests {
    [Theory]
    [InlineData(WorkspaceOperation.DefaultsRead)]
    [InlineData(WorkspaceOperation.SecretList)]
    [InlineData(WorkspaceOperation.FileList)]
    public async Task Concurrent_refresh_callers_await_the_same_read(WorkspaceOperation operation) {
        var store = new WorkspaceScenarioStore();
        using var defaults = new WorkspaceDefaultsController(store);
        using var secrets = new WorkspaceSecretsController(store);
        using var files = new WorkspaceFilesController(store);
        Func<Task> refresh = operation switch {
            WorkspaceOperation.DefaultsRead => defaults.RefreshAsync,
            WorkspaceOperation.SecretList => secrets.RefreshAsync,
            WorkspaceOperation.FileList => files.RefreshAsync,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        var gate = store.HoldNext(operation);
        var first = refresh();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            var second = refresh();
            Assert.False(second.IsCompleted);
            Assert.Equal(1, store.Calls[operation]);
        } finally {
            gate.Release();
            await first;
        }
    }

    [Fact]
    public async Task Provider_choices_do_not_wait_for_the_defaults_editor_read() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceDefaultsController(store);
        var gate = store.HoldNext(WorkspaceOperation.DefaultsRead);
        var pending = state.RefreshAsync();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            Assert.Equal(1, store.Calls.GetValueOrDefault(WorkspaceOperation.ProvidersRead));
            Assert.Single(state.Providers);
        } finally {
            gate.Release();
            await pending;
        }
    }
}
