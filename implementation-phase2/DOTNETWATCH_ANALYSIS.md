# DotNetWatch Analysis

Updated on 2026-03-24 after the phase-2 closeout pass.

## Summary

The `CanDoItAll.Mcp.DotNetWatch` problems are not a single failure mode.
Current evidence separates them into three independent concerns:

1. The direct Codex-to-MCP tool bridge is still broken.
2. The backend manager and managed `WatchRun` session are healthy and usable.
3. The server and app runtime already use isolated shadow artifacts, but the
   app runtime is still configured as live-source `WatchRun`.
4. The publish-backed validation folder can be rebuilt, but only after stopping
   any running published host that has the output files locked.

## What failed

Direct MCP tool calls still fail in this session with generic invocation
errors, for example:

- `candoitall_workspace_info`
- `candoitall_app_start`

This failure persisted after the bundle code changes and after successful local
builds, which means the bridge problem is not caused by the current source
edits.

The bootstrap log shows the server shadow host repeatedly building and
launching successfully from `.artifacts\mcp-server-shadow\builds\...`, so the
server process itself is not failing because the solution is being edited in
place.

## What still works

- The persistent backend manager is reachable through
  `C:\repositories\CanDoItAll\.mcp-state\backend\registration.json`
- The manager UI can start, stop, and force-rebuild the default app session
- A manager-started `WatchRun` session reaches `Healthy` and exposes:
  - `https://localhost:7271`
  - `http://localhost:5032`
- Managed app session artifacts are copied under:
  - `C:\repositories\CanDoItAll\.mcp-state\artifacts\app-projects`
  - `C:\repositories\CanDoItAll\.mcp-state\artifacts\app-sessions`
- Playwright can drive the manager UI and the running app pages successfully

## Current runtime model

The default app is still configured in
`C:\repositories\CanDoItAll\CanDoItAll.Mcp.DotNetWatch.settings.json` as:

- project path:
  `src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- mode:
  `WatchRun`

The current server contract only exposes:

- `WatchRun`
- `RunOnce`

There is no published-app / arbitrary executable mode in `AppRunMode` today, so
the MCP-managed app cannot yet be pointed directly at
`.artifacts\bundle-validation\webapp`.

## WatchRun behavior

Historical app logs still show `dotnet watch` overload events like:

- `Too many changes at once in directory`

Concrete examples from `.mcp-state\logs\app-app_*.ndjson` include repeated
watcher errors for:

- `src\CanDoItAll.ComponentKit`
- `src\CanDoItAll.Modules.Factory`
- `src\CanDoItAll.Modules.Workbench`
- `src\CanDoItAll.Web`

That behavior is consistent with large same-solution edit waves. The current
settings already mitigate some of that pressure:

- `DOTNET_USE_POLLING_FILE_WATCHER=1`
- `Process.UsePollingFileWatcher=true`

Even with those settings, the watch session is still better treated as a
source-side validation path, not the safest place to do heavy build/publish
work concurrently.

## Publish-backed runtime

The standardized release-style validation output is:

- `C:\repositories\CanDoItAll\.artifacts\bundle-validation\webapp`

`dotnet publish` succeeds there after stopping any existing published app
process that is locking the target DLLs. During this pass, the blocker was a
running published host:

- `dotnet C:\repositories\CanDoItAll\.artifacts\bundle-validation\webapp\CanDoItAll.Web.dll`

Once that process was stopped, Release publish completed successfully.

This release output is useful for validation and handoff, but it is not the
runtime that the current MCP manager starts.

## Practical closeout flow

1. Rebuild and test from the source tree.
2. Republish to `.artifacts\bundle-validation\webapp`.
3. If publish fails with `MSB3021` / `MSB3027`, stop the running published host
   that is locking the output folder and rerun publish.
4. While the direct MCP bridge remains broken, use the backend manager UI plus
   Playwright for live session control and screenshot capture.

## Evidence

- `C:\repositories\CanDoItAll\CanDoItAll.Mcp.DotNetWatch.settings.json`
- `C:\repositories\CanDoItAll\.mcp-state\backend\registration.json`
- `C:\repositories\CanDoItAll\.mcp-state\logs\mcp-dotnetwatch-bootstrap.log`
- `C:\repositories\CanDoItAll\artifacts\mcp-backend-manager-current.png`
