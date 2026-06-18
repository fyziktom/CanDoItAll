# SB32 Validation

## Commands

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProcessProjectionPipelineTests|FullyQualifiedName~ProcessLaunchExecutorResolverTests" --no-restore`
  - Passed: 13 total, 0 failed.
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceShellTests" --no-restore`
  - Passed: 26 total, 0 failed.
- `dotnet build CanDoItAll.slnx --no-restore`
  - Passed: 0 warnings, 0 errors.
- `git diff --check`
  - Passed. Only existing line-ending conversion warnings were emitted.

## Browser Checks

- Desktop Live Processes at `http://localhost:5032/processes/live?processStarted=1`
  - Start notification visible: "Process started. Live run projection is loading."
  - Tabs frame spans the content width. Snapshot tablist box: `100,249,1324,47`.
  - Agents tab badge shows `0`; agent panel shows `0 working agents` and `2 stale claims`.
  - Stale agent cards show `Lease expired` instead of reporting the agents as actively working.
- Detail dialog
  - Opened from the first activity card.
  - Shows stale claim count, active-agent card, recent events, incidents, manager messages, and process-control action.
- Narrow viewport, 390x900
  - Start notification remains visible.
  - Tabs use the available viewport width with horizontal tab overflow for additional tabs.
  - Activity cards remain accessible below the tab list.

## API And Output Folder

- `/api/processes/live?windowMinutes=60&take=50`
  - Returned 3 recent runs: 2 active, 1 completed.
  - No old runs outside the one-hour `LastEventAtUtc` window were returned.
- `C:\programovani\dotnet\output`
  - Exists check completed.
  - Item count: 0.
  - The old Tetris launch has not produced a final app.
