# SB02 Anti-Stub Audit Transcript

- Invariant ID: `WEB-SB02-001`

Command:

```powershell
rg -n "executorOptionsLoaded|workflowOptionsLoaded|analyticsLoaded|EnsureRuntimeOptionsLoadedAsync|EnsureAnalyticsLoadedAsync|Process_workspace_defers_hidden_runtime_and_analytics_data_until_tabs_need_it" src\CanDoItAll.Modules.Processes tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs -S
```

ExitCode: 0

Output:

```text
tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs:36:    public async Task Process_workspace_defers_hidden_runtime_and_analytics_data_until_tabs_need_it()
tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs:51:        Assert.False(GetPrivateFieldValue<bool>(cut.Instance, "executorOptionsLoaded"));
tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs:52:        Assert.False(GetPrivateFieldValue<bool>(cut.Instance, "workflowOptionsLoaded"));
tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs:53:        Assert.False(GetPrivateFieldValue<bool>(cut.Instance, "analyticsLoaded"));
tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs:66:        Assert.True(GetPrivateFieldValue<bool>(cut.Instance, "executorOptionsLoaded"));
tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs:72:        Assert.True(GetPrivateFieldValue<bool>(cut.Instance, "analyticsLoaded"));
src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Loading.cs:197:    private async Task EnsureRuntimeOptionsLoadedAsync(CancellationToken cancellationToken = default)
src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Loading.cs:255:    private async Task EnsureAnalyticsLoadedAsync(
```

Audit conclusion: no stub-only proof; the assertions cite production deferred-load fields, production ensure methods, and a component test that observes the state transitions.
