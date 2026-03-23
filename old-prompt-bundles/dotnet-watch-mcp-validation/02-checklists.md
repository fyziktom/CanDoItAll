# Checklists

## Repair checklist

- [x] Implement `ContinueIfSafe` or remove it from the public surface and pack.
- [x] Make `app_start(waitFor=...)` propagate unsatisfied wait outcomes instead of returning plain success.
- [x] Harden stale cleanup with ownership checks: workspace, command, arguments, owner kind/id, and server instance identity.
- [x] Implement graceful-then-force shutdown with Windows and Unix-specific tree terminators.
- [x] Complete wait support: `Ready`, optional `RestartCompleted`, stable-success health, correct quiet baseline, and quiet plus health coordination.
- [x] Complete `workspace_info`: relative paths, optional history, and actual redaction of configuration snapshots.
- [x] Complete `tests_run`: request `environmentOverlay`, real `Auto` runner detection, artifact reporting, and no runner field for build operations.
- [x] Enforce config-backed behavior such as log rotation and operational defaults instead of leaving them as unused options.

## Validation checklist

- [x] Add VAL-001 and VAL-024 stdout-discipline tests.
- [x] Add VAL-002 invalid `SolutionPath` bootstrap test.
- [x] Add VAL-006 and VAL-007 session reuse/conflict tests.
- [x] Add VAL-008 plus VAL-029 and VAL-030 real process-tree kill coverage.
- [x] Add VAL-011 quiet-wait-after-change coverage.
- [x] Add VAL-015 build-failure-plus-resume coverage.
- [x] Add VAL-016 proof that `tests_run` executes `dotnet test`, never `dotnet watch test`.
- [x] Add VAL-020 safe stale-cleanup coverage with ownership verification.
- [x] Add VAL-025 correlation consistency coverage.
- [x] Add VAL-031 actionable lock-holder / busy-workspace coverage.

## Release gate checklist

- [x] All P0 scenarios from `CanDoItAll.Mcp.DotNetWatch.CodexPack/12-validation-matrix.csv` are automated and green.
- [x] Public MCP tool contracts match real tool behavior.
- [x] Stale cleanup cannot kill unrelated processes.
- [x] stdout discipline is verified under live MCP traffic.
- [x] Build/test preemption and resume behavior are proven under both success and failure paths.
- [x] Docs and prompts are updated to match the repaired implementation.
