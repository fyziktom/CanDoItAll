# Speed Comparison And Evidence

## Scope

I compared two real workflows against the same dashboard page:

1. MCP-managed `dotnet watch` live editing.
2. Plain PowerShell stop/start flow with `dotnet run`, `taskkill`, and endpoint polling.

The edited file was:

- `src/CanDoItAll.Web/Components/Pages/Home.razor`

The changed text was the dashboard description string.

## MCP Watch: Cold Start

Observed from app logs:

- `2026-03-21T16:13:23.345Z`: started `dotnet watch`
- `2026-03-21T16:14:58.806Z`: `Now listening on: https://localhost:7271`
- `2026-03-21T16:14:59.257Z`: health marked ready

Approximate time to ready:

- about `95.9s`

Notable contributors in logs:

- build elapsed: `54.99s`
- watch evaluation: `14.1s`
- projects loaded: `10.8s`

## MCP Watch: Razor UI Edit

### Edit 1: Add marker

- Baseline status time: `2026-03-21T16:15:23.326Z`
- File update log: `2026-03-21T16:15:26.405Z`
- Browser refresh confirmed new text afterward.

Observed detection time:

- about `3.1s`

### Edit 2: Remove marker

- Baseline status time: `2026-03-21T16:15:59.913Z`
- File update log: `2026-03-21T16:16:06.259Z`
- Browser refresh confirmed reverted text afterward.

Observed detection time:

- about `6.3s`

### Takeaway

For simple Razor edits, the managed watch flow is dramatically faster than a full restart flow.

## MCP Watch: C# Change Requiring Watch Work

Temporary file:

- `src/CanDoItAll.Web/McpRestartProbe.cs`

### Add file

- Baseline status time: `2026-03-21T16:16:24.705Z`
- `16:16:29.461Z`: `File added`
- `16:16:29.462Z`: `Evaluating projects ...`
- `16:16:38.960Z`: evaluation completed
- `16:16:49.511Z`: `Hot reload succeeded.`

Observed end-to-end watch work:

- about `24.8s`

But `QuietSinceCursor` returned early:

- returned success at `16:16:32.765Z`
- this was before the later evaluation/loading/hot-reload-success logs

### Delete file

- `16:19:53.323Z`: `File deleted`
- `16:19:53.596Z`: `Restart is needed to apply the changes.`
- `16:19:54.005Z`: child app exited
- `16:19:54.070Z`: build started
- build output continued through at least `16:20:32.214Z`

But `Healthy` wait returned success at:

- `2026-03-21T16:20:27.820Z`

This was a false-positive readiness result.

## Plain PowerShell Manual Flow

### Manual control characteristics observed

- Needed process-tree killing with `taskkill /T /F`.
- Recorded parent PID was not enough to reason about the actual listening child process.
- One teardown left a listener alive briefly, requiring extra cleanup logic.

### Manual restart after the same small UI edit

Manual method:

- stop process tree
- start `dotnet run --project src/CanDoItAll.Web/CanDoItAll.Web.csproj --configuration Debug --launch-profile https`
- poll `http://localhost:5032/_dev/runtime` until `isReady == true`

Measured ready time:

- `86412ms`

Browser verification confirmed the `QA baseline marker.` text after the restart.

## Comparison Summary

| Scenario | Approx time to usable result | Trust level |
| --- | ---: | --- |
| MCP watch, tiny Razor edit | `3-6s` | Good for simple Razor edits |
| MCP watch, C# add/delete flow | `25s` actual watch work | Current waits are not trustworthy |
| Manual PowerShell restart after tiny edit | `86.4s` | Slow and operationally noisy |

## Bottom Line

The MCP server already proves its value for live UI work.

The missing piece is not speed. The missing piece is trustworthy synchronization for anything beyond the simplest hot-reload case.
