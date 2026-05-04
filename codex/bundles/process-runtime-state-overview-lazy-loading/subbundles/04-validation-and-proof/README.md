# validation-and-proof

## Status

- `Completed`

## Objective

Run final tests, browser proof, raw-note closure, and bundle completion sync.

## Covered Inputs

- R009, R010
- All raw notes N001-N007 through closure audit.

## Prerequisites

- Subbundles 01, 02, and 03 completed or honestly blocked with documented proof.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\process-runtime-state-overview-lazy-loading\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\codex\bundles\process-runtime-state-overview-lazy-loading\README.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`

## Deliverables

- Targeted tests/build results recorded.
- Browser validation analytics completed or blocker documented.
- Raw-note closure table completed.
- Bundle README and execution report synchronized.
- Final validator run recorded.

## Dependency Impact

- This closes the whole bundle. Weak proof here reopens the affected earlier subbundle.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Run targeted integration tests.
2. Run build or affected project build.
3. Use browser proof against processes page if local app is available.
4. Update execution report with commands, browser analytics, gate results, raw-note closure, and residual risks.
5. Run completed bundle validator.

## Scope Exceptions

- If local app/browser cannot reach `https://localhost:7271/`, document the exact blocker and rely on tests/build for non-visual proof.

## Do Not Do

- Do not mark browser proof as passed without actual navigation/assertions.
- Do not leave raw notes pending.

## Acceptance Checklist

- Commands are recorded with pass/fail.
- Browser validation analytics are complete or honestly blocked.
- Raw notes N001-N007 are `Solved`, `Partially solved`, or `Not solved` with proof.
- Bundle status matches implementation state.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter ProcessesServiceIntegrationTests`
- `dotnet build CanDoItAll.slnx`
- Browser proof or documented blocker.
- `validate_bundle.py --stage completed`

## Browser Validation Logging

- Route: `https://localhost:7271/processes`.
- Viewport: large desktop first.
- Actions/assertions: inspect badges, open Runs tab, inspect blocked stop action if data exists, verify no Blazor error UI.
- Screenshots: record under `output/playwright/process-runtime-state-overview/` when available.

## Progression Gate

- The bundle can close only when code, tests/build, browser analytics or explicit blocker, raw-note closure, and final validator agree.

## Suggested Agent Prompt

```text
Implement subbundle 04 only: run validation, collect browser proof if available, update the execution report, close raw notes, and run final bundle validation.
```
