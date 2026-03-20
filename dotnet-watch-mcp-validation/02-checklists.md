# Checklists

## Repair checklist

- [ ] Implement `ContinueIfSafe` or remove it from the public surface and pack.
- [ ] Make `app_start(waitFor=...)` propagate unsatisfied wait outcomes instead of returning plain success.
- [ ] Harden stale cleanup with ownership checks: workspace, command, arguments, owner kind/id, and server instance identity.
- [ ] Implement graceful-then-force shutdown with Windows and Unix-specific tree terminators.
- [ ] Complete wait support: `Ready`, optional `RestartCompleted`, stable-success health, correct quiet baseline, and quiet plus health coordination.
- [ ] Complete `workspace_info`: relative paths, optional history, and actual redaction of configuration snapshots.
- [ ] Complete `tests_run`: request `environmentOverlay`, real `Auto` runner detection, artifact reporting, and no runner field for build operations.
- [ ] Enforce config-backed behavior such as log rotation and operational defaults instead of leaving them as unused options.

## Validation checklist

- [ ] Add VAL-001 and VAL-024 stdout-discipline tests.
- [ ] Add VAL-002 invalid `SolutionPath` bootstrap test.
- [ ] Add VAL-006 and VAL-007 session reuse/conflict tests.
- [ ] Add VAL-008 plus VAL-029 and VAL-030 real process-tree kill coverage.
- [ ] Add VAL-011 quiet-wait-after-change coverage.
- [ ] Add VAL-015 build-failure-plus-resume coverage.
- [ ] Add VAL-016 proof that `tests_run` executes `dotnet test`, never `dotnet watch test`.
- [ ] Add VAL-020 safe stale-cleanup coverage with ownership verification.
- [ ] Add VAL-025 correlation consistency coverage.
- [ ] Add VAL-031 actionable lock-holder / busy-workspace coverage.

## Release gate checklist

- [ ] All P0 scenarios from `CanDoItAll.Mcp.DotNetWatch.CodexPack/12-validation-matrix.csv` are automated and green.
- [ ] Public MCP tool contracts match real tool behavior.
- [ ] Stale cleanup cannot kill unrelated processes.
- [ ] stdout discipline is verified under live MCP traffic.
- [ ] Build/test preemption and resume behavior are proven under both success and failure paths.
- [ ] Docs and prompts are updated to match the repaired implementation.
