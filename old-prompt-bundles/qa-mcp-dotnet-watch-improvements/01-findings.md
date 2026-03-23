# Findings

## P0: `app_wait` can return false readiness during active watch rebuild/restart work

### What happened

During a temporary C# file add/delete experiment, the server returned successful waits before the watched app had finished processing the change.

- Baseline before file add: `candoitall_app_status` at `2026-03-21T16:16:24.705Z`, cursor `352`.
- Watch log sequence `353-358` showed:
  - `16:16:29.461Z`: `File added: .\McpRestartProbe.cs`
  - `16:16:29.462Z`: `Evaluating projects ...`
  - `16:16:38.960Z`: `Evaluation completed in 9.5s.`
  - `16:16:49.511Z`: `Hot reload succeeded.`
- `candoitall_app_wait(condition=QuietSinceCursor, cursor=352, quietPeriodMs=3000)` returned success at `16:16:32.765Z`, before sequences `355-358` happened.

The problem repeated on file delete:

- Watch log sequence `359-365` showed:
  - `16:19:53.323Z`: `File deleted: .\McpRestartProbe.cs`
  - `16:19:53.596Z`: `Restart is needed to apply the changes.`
  - `16:19:54.005Z`: `[CanDoItAll.Web (net10.0)] Exited`
  - `16:19:54.070Z`: `Building ...`
  - `16:20:02.061Z`: restore/build still in progress
- `candoitall_app_wait(condition=Healthy)` returned success at `16:20:27.820Z`.
- App logs were still emitting build output through at least `16:20:32.214Z` (`sequence 379`).

### Why this is severe

This breaks the main promise of the server: an agent can be told "the change propagated" while the runtime is still in motion. That can cause premature browser refreshes, bad screenshots, flaky follow-up edits, and misleading diagnoses.

### Root cause

This is a combined state-model and wait-algorithm defect:

- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs`
  - `QuietSinceCursor` only checks whether the most recent log entry has been quiet long enough at the moment of polling.
  - It does not require a final watch-generation completion signal.
  - `EvaluateHealthAsync` short-circuits when session state is already `Healthy`.
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs`
  - Session state is not reset when a watch-triggered rebuild/restart starts.

### Required fix direction

- Invalidate `Healthy` immediately on file-change/restart-needed/build-start signals.
- Re-probe health after any watch generation that touches code.
- Make waits generation-aware instead of log-gap-aware.
- Do not let `Healthy` short-circuit until the current watch generation is confirmed.

## P0: Watch lifecycle parser does not match real `dotnet watch` behavior

### What happened

Observed live log lines included:

- `dotnet watch : File updated: .\Components\Pages\Home.razor`
- `dotnet watch : File added: .\McpRestartProbe.cs`
- `dotnet watch : Evaluating projects ...`
- `dotnet watch : Loading projects ...`
- `dotnet watch : Projects loaded in 8.3s.`
- `dotnet watch : [CanDoItAll.Web (net10.0)] Hot reload succeeded.`
- `dotnet watch : Restart is needed to apply the changes.`
- `dotnet watch : [CanDoItAll.Web (net10.0)] Exited`
- `dotnet watch : Waiting for changes`

Current parser logic in `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs` looks for:

- `Restarting`
- `Waiting for a file to change`
- `Hot reload enabled`

### Impact

- `SessionVersion` stayed `1` across the live watch rebuild experiments.
- `LastRestartUtc` never changed.
- `RecentEvents` never reflected the actual watch generation transitions.
- `RestartCompleted` is not a dependable wait condition for real watch flows.

### Required fix direction

- Parse the real messages emitted by your current SDK/tooling on Windows.
- Track separate concepts:
  - watcher process state
  - child runtime generation state
  - hot reload success/failure
  - restart-needed / restart-completed

## P1: `app_wait Healthy` uses stale health after the first successful start

### What happened

After the first healthy start, subsequent waits reused the old healthy state even during later rebuild activity.

Evidence:

- `AppStatusData.health.lastSuccessUtc` stayed at `2026-03-21T16:14:59.257Z` long after later file changes.
- `candoitall_app_wait(condition=Healthy)` returned immediately during a rebuild window because session state was still `Healthy`.

### Root cause

`EvaluateHealthAsync` in `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs` returns satisfied immediately if `status.State == AppLifecycleState.Healthy`.

Because watch-triggered changes do not reliably move the session out of `Healthy`, later health waits become stale-state checks instead of actual readiness checks.

### Required fix direction

- Treat any file change, hot-reload apply, restart-needed, child exit, or rebuild start as health-invalidating events.
- Force a fresh health probe against the current generation before confirming `Healthy`.

## P1: The server cannot reliably test itself while it is live

### What happened

`candoitall_tests_run` against `tests/CanDoItAll.Mcp.DotNetWatch.Tests/CanDoItAll.Mcp.DotNetWatch.Tests.csproj` failed with file-lock errors while the MCP server was running.

Observed operation:

- Operation id: `op_828ee085a33d470d9f8366accd09bcbe`
- State: `Failed`
- Exit code: `1`

Observed errors:

- `MSB3027`
- `MSB3021`
- Locked outputs:
  - `src/CanDoItAll.Mcp.DotNetWatch/bin/Debug/net10.0/CanDoItAll.Mcp.LocalRuntime.dll`
  - `src/CanDoItAll.Mcp.DotNetWatch/bin/Debug/net10.0/CanDoItAll.Mcp.Core.dll`

### Why it matters

This blocks one of the most useful QA loops: asking the live agent to change the server and then run the server's own tests through the same managed flow.

### Root cause

The server process is loaded from the same debug output graph that the test build wants to overwrite.

### Required fix direction

Choose one or combine several:

- Run the MCP server from a shadow-copied output location.
- Build and execute tests from a separate output path/configuration.
- Add `--no-build` pathways only when safe and explicit.
- Isolate self-host validation into an external harness that does not share the running output directory.

## P1: `CanDoItAll.slnx` and runtime test inventory disagree

### What happened

- `CanDoItAll.Mcp.DotNetWatch.settings.json` lists `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests.csproj`.
- `candoitall_workspace_info` exposed that integration test project.
- `CanDoItAll.slnx` contains `tests/CanDoItAll.Mcp.DotNetWatch.Tests/...` but not the integration test project.
- `SharpTool_LoadProject("CanDoItAll.Mcp.DotNetWatch.IntegrationTests")` failed because the project is not in the loaded solution.

### Impact

- Solution-level operations and source navigation do not see the same project set that the MCP settings expose.
- This increases confusion for both humans and agents.

### Required fix direction

- Align `CanDoItAll.slnx`, `CanDoItAll.Mcp.DotNetWatch.settings.json`, and test documentation.

## P1: `lastKnownPid` is the watcher PID, not the actual child runtime PID

### What happened

By design, `AppRuntimeManager` starts `dotnet watch` as the managed process:

- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs`
- `src/CanDoItAll.Mcp.LocalRuntime/Processes/ProcessSupervisor.cs`

That means `AppStatusData.lastKnownPid` identifies the watcher process, not the actual web-app child process that binds the ports.

### Impact

- PID-oriented diagnostics are misleading.
- Agents cannot reason about whether the child runtime changed between watch generations.
- A future "restart completed" feature lacks a trustworthy runtime identity anchor.

### Required fix direction

- Surface both watcher PID and active child runtime PID.
- Track child runtime exits/restarts explicitly.

## P2: `watchIteration` already exists in the app, but the MCP server does not use it end to end

### What happened

- `src/CanDoItAll.Web/Program.cs` exposes `DOTNET_WATCH_ITERATION` through `/_dev/runtime`.
- `src/CanDoItAll.Infrastructure/Readiness/RuntimeReadiness.cs` carries `WatchIteration`.
- `src/CanDoItAll.Mcp.DotNetWatch/Health/HealthServices.cs` parses `WatchIteration` into `HealthSnapshot`.
- But `HealthData` and `AppStatusData` do not expose it back to the agent.
- Wait logic does not compare a baseline generation to a confirmed generation.

### Why this matters

You already have the seed of a robust synchronization mechanism. It is just not completed.

### Required fix direction

- Add watch generation fields to status/wait payloads.
- Make waits compare `expectedWatchIteration` vs `confirmedWatchIteration`.
- Prefer generation confirmation over log heuristics where possible.
