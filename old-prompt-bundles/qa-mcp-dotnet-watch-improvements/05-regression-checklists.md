# Regression Checklists

## Core Correctness Checklist

- Start the default app with `candoitall_app_start(waitFor=Healthy)` and confirm cold start still works.
- Trigger a Razor-only edit and confirm the next generation becomes safe to refresh.
- Trigger a C# add/delete edit and confirm waits do not succeed until watch work is actually done.
- Confirm `sessionVersion` or equivalent generation data changes when real watch work happens.
- Confirm `Healthy` performs a fresh validation for the current generation, not a stale-state shortcut.
- Confirm `QuietSinceCursor` cannot finish before later evaluation/build logs for the same generation.

## Status And Observability Checklist

- `app_status` exposes enough data to distinguish watcher state from child runtime state.
- `app_status` exposes the current confirmed watch generation.
- health payloads preserve `watchIteration` end to end.
- PID fields are documented and no longer misleading.
- recent events reflect real watch messages observed on Windows.

## Testability Checklist

- `candoitall_tests_run` succeeds for `tests/CanDoItAll.Mcp.DotNetWatch.Tests`.
- `candoitall_tests_run` succeeds for `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests`.
- self-host tests do not fail with `MSB3021` or `MSB3027`.
- test artifacts still publish correctly.
- `collectCoverage=true` still works.

## Solution And Config Checklist

- `CanDoItAll.slnx` includes `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests`.
- `workspace_info` test project list matches the solution inventory.
- default targets in `CanDoItAll.Mcp.DotNetWatch.settings.json` are still valid.

## Manual Comparison Checklist

- Repeat the same tiny Razor edit under MCP watch and confirm the cycle is materially faster than a manual restart.
- Repeat the same edit under plain `dotnet run` plus script-managed restart and capture the ready time.
- Confirm MCP is still the faster path after correctness fixes are added.

## Release Gate Checklist

- No false-positive healthy waits under rebuild conditions.
- No false-positive quiet waits under delayed watch evaluation/loading.
- Browser refresh after MCP wait consistently shows the new UI.
- Managed app stop/start/test/build flows still work after lifecycle refactor.
