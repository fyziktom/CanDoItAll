# Reproduction Playbook

Use this playbook to reproduce the most important failures before and after implementation.

## 1. Self-Test Lock Failure

### Goal

Show that the live MCP server cannot currently run tests against its own project graph.

### Steps

1. Ensure the MCP server is running.
2. Call:
   - `candoitall_tests_run`
   - target: `tests/CanDoItAll.Mcp.DotNetWatch.Tests/CanDoItAll.Mcp.DotNetWatch.Tests.csproj`
3. Read:
   - `candoitall_operation_status`
   - `candoitall_operation_logs`

### Current expected result

- operation fails
- logs contain `MSB3027` and `MSB3021`
- locked files include `CanDoItAll.Mcp.LocalRuntime.dll` and `CanDoItAll.Mcp.Core.dll` under `src/CanDoItAll.Mcp.DotNetWatch/bin/Debug/net10.0`

## 2. False Quiet Wait During Watch Work

### Goal

Show that `QuietSinceCursor` can report completion before watch work is finished.

### Steps

1. Start the default app with `candoitall_app_start(waitFor=Healthy)`.
2. Capture baseline with `candoitall_app_status`.
3. Add a temporary C# file under `src/CanDoItAll.Web/`.
4. Immediately call:
   - `candoitall_app_wait(condition=QuietSinceCursor, cursor=<baselineCursor>, quietPeriodMs=3000)`
5. After it returns, read:
   - `candoitall_app_logs(cursor=<baselineCursor>)`

### Current expected result

- quiet wait can return success
- later watch logs still arrive afterward
- examples include `Evaluating projects ...`, `Loading projects ...`, `Hot reload succeeded.`

## 3. False Healthy Wait During Rebuild

### Goal

Show that `Healthy` can succeed while the child app is still rebuilding.

### Steps

1. Start the default app with `candoitall_app_start(waitFor=Healthy)`.
2. Create and then delete a temporary C# file that forces watch work.
3. While rebuild logs are still active, call:
   - `candoitall_app_wait(condition=Healthy)`
4. Read:
   - `candoitall_app_logs`
   - `candoitall_app_status`

### Current expected result

- `Healthy` may return success immediately
- build logs are still being emitted
- status still reports `Healthy`

## 4. Lifecycle Parser Mismatch

### Goal

Show that real watch messages do not update restart/session tracking.

### Steps

1. Start the app with `candoitall_app_start(waitFor=Healthy)`.
2. Trigger a watch-required change by adding/removing a C# file.
3. Read:
   - `candoitall_app_logs`
   - `candoitall_app_status`

### Current expected result

- logs include messages like:
  - `File added`
  - `Restart is needed to apply the changes.`
  - `Exited`
  - `Hot reload succeeded.`
- `sessionVersion` remains unchanged
- `lastRestartUtc` remains null

## 5. Manual Baseline Comparison

### Goal

Measure what a non-MCP agent has to do today.

### Steps

1. Start with plain PowerShell:
   - `dotnet run --project src/CanDoItAll.Web/CanDoItAll.Web.csproj --configuration Debug --launch-profile https`
2. Poll:
   - `http://localhost:5032/_dev/runtime`
3. Edit `Home.razor`.
4. Kill the process tree with `taskkill /T /F`.
5. Start again and poll readiness again.

### Current expected result

- total restart loop is much slower than the watch case
- parent/child PID tracking is awkward
- teardown is brittle and can require extra cleanup

## 6. Solution Inventory Mismatch

### Goal

Show that the repo metadata is inconsistent.

### Steps

1. Inspect `CanDoItAll.Mcp.DotNetWatch.settings.json`.
2. Inspect `CanDoItAll.slnx`.
3. Load the solution in SharpTools and try to load `CanDoItAll.Mcp.DotNetWatch.IntegrationTests`.

### Current expected result

- settings include the integration test project
- solution omits it
- solution-based project load/navigation does not find it
