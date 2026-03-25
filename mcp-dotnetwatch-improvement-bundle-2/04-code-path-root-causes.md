# Code-Path Root Causes

## Watch Launch Shape

| File | Lines | Finding | Impact |
| --- | --- | --- | --- |
| `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs` | `1268-1279` | `SourceWatch` launches `dotnet watch` with `--artifacts-path` | primary hot-reload regression |
| `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs` | `1376-1380` | watch env disables browser refresh and disables MSBuild server | secondary watch/build regression |

## False Positive Wait Semantics

| File | Lines | Finding | Impact |
| --- | --- | --- | --- |
| `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs` | `816-823` | `MarkWatchChangeApplied` clears `_watchPendingChange` immediately on `Hot reload succeeded.` | wait can finish before visible result is proven |
| `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs` | `938-945` | `Revision` for `SourceWatch` is derived from watch iteration and `pending=false` | normal in-process hot reload can look "confirmed" without a new revision |
| `src/CanDoItAll.Web/Program.cs` | `82-103` | `/_dev/runtime` reports `DOTNET_WATCH_ITERATION` and PID only | no in-process hot reload generation token exists |
| `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs` | `330-335` | `RevisionConfirmed` trusts the current revision object | false confirmation path |
| `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs` | `419-441` | `WatchSettled` relies on `pendingChange` and quiet period | same false-positive family |

## Managed Build Shape

| File | Lines | Finding | Impact |
| --- | --- | --- | --- |
| `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs` | `690-712` | build/test operations always use isolated artifacts output and disable MSBuild server | slower warm builds and forced restore behavior |

## Backend Ownership And Dedupe

| File | Lines | Finding | Impact |
| --- | --- | --- | --- |
| `src/CanDoItAll.Mcp.DotNetWatch/Backend/BackendConnectionManager.cs` | `27-53` | backend is already launched detached and reused via registration | tray manager is plausible, but not the main hot-reload root cause |
| `src/CanDoItAll.Mcp.DotNetWatch/Bridge/BridgeRepairCoordinator.cs` | `52-68` | a new request id is generated per send when idempotency is enabled | repeated equivalent calls are not logically deduped |
| `src/CanDoItAll.Mcp.DotNetWatch/Backend/BackendRequestReplayStore.cs` | `15-28` | replay protection only works when the same request id is reused | duplicate semantic requests still execute |
| `src/CanDoItAll.Mcp.DotNetWatch/Runtime/WorkspaceExecutionLock.cs` | `24-35` | resource locking serializes backend mutations by resource key | does not serialize raw filesystem edits made outside the backend |
| `src/CanDoItAll.Mcp.DotNetWatch/Runtime/Coordination/ResourceScopePlanner.cs` | `21-28` | operations are scoped by target path and logical app ids | good for backend safety, insufficient for external edit stacking |

## Root Cause Ranking

1. `--artifacts-path` on `SourceWatch`
2. no real hot-reload generation token
3. wait completion keyed off log text instead of end-to-end confirmation
4. isolated build outputs forcing restore behavior and reducing cache reuse
5. MSBuild server disabled
6. repeated edits/requests can still stack because external file edits are not serialized by the backend
