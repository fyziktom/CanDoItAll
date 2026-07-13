# 02-runtime-launch-and-watch-restore-tests

## Status

- `Completed`

## Objective

Repair runtime launch path expectations and watch restore-skip semantics so they match the current repository layout and protect `5032` startup.

## Covered Inputs

- RH-003: `ProjectStructureRuntimeLauncherTests` still expect old `src\CanDoItAll.Web` path.
- RH-004: stale referenced-project assets should prevent `--no-restore`.

## Prerequisites

- SB01 is preferred before final closure but not required for local implementation.
- Evidence exists: `bundle://evidence/targeted-failing-tests.txt`.

## Exact Source References

- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProjectStructureRuntimeLauncherTests.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkspaceRuntimeProcessToolsTests.cs`
- `repo://tools/App/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs`
- `repo://src/App/CanDoItAll.Web/CanDoItAll.Web.csproj`

## Deliverables

- Launch plan tests and production path resolution agree on `src\App\CanDoItAll.Web`.
- Watch argument tests model actual project-reference paths from the current solution.
- `BuildWatchArgumentList` includes `--no-restore` only when all relevant restore inputs/assets are fresh.

## Dependency Impact

- SB05 depends on this for reliable `5032` rebuild/start behavior.
- SB04 uses the same build/test environment, so runtime path correctness should be resolved before database order probes are trusted.

## Validation Depth

- Critical runtime foundation.

## Implementation Steps

1. Capture failing-first output for `ProjectStructureRuntimeLauncherTests` and `WorkspaceRuntimeProcessToolsTests`.
2. Verify actual `CanDoItAll.Web.csproj` path and project references.
3. Update obsolete test expectations if production already resolves correctly.
4. Repair the watch restore stale-reference fixture or production project-reference traversal after proving which is wrong.
5. Add a regression assertion that a stale referenced project assets file omits `--no-restore`.

## Scope Exceptions

- Do not redesign the manager watch subsystem.
- Do not change launch semantics for Python, Docker, or script nodes unless a touched test proves a regression.

## Do Not Do

- Do not make all watch launches restore by default; that would hide the performance optimization.
- Do not hardcode only the local developer path outside test fixtures.

## Acceptance Checklist

- [x] `ProjectStructureRuntimeLauncherTests` pass.
- [x] `WorkspaceRuntimeProcessToolsTests` pass.
- [x] Stale referenced project proof uses a real, existing referenced project path.
- [x] No old `src\CanDoItAll.Web\CanDoItAll.Web.csproj` assertion remains unless it is a deliberate backward-compatibility case.

## Proof Required

- Failing-first transcript: `proof/SB02/failing-runtime-launch-watch-tests.txt`.
- Passing transcript: `proof/SB02/passing-runtime-launch-watch-tests.txt`.
- Source assertion for `src/App/CanDoItAll.Web/CanDoItAll.Web.csproj`.
- Source assertion for stale-reference fixture path.

## Browser Validation Logging

- N/A for this subbundle. Live `5032` browser proof is owned by SB05.

## Progression Gate

- SB05 may not start live runtime proof until these targeted tests pass or the execution report documents a non-runtime blocker.

## Suggested Agent Prompt

```text
Implement SB02 only. Repair runtime launch path and watch restore tests with current repository layout and realistic project references.
```
