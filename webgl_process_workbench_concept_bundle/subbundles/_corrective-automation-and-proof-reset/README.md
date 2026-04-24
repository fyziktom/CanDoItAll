# Corrective automation and proof reset

## Status

- Blocked

## Objective

- Repair failures where screenshots were non-deterministic, semantic automation was too weak, or final proof could not verify actual WebGL state changes.

## Covered Inputs

- `RQ-15`
- `RQ-16`
- `RQ-20`

## Prerequisites

- Prepared bundle readiness gate passed.
- No downstream implementation may begin until this subbundle owns the active work item.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07-runtime-entry.js
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Playwright/SharedCanvasBrowserTests.cs

## Deliverables

- A focused corrective that stabilizes test mode, automation helpers, DOM mirror queries, or screenshot export.
- Updated proof notes showing exactly what changed and why the old proof was inadequate.
- Fresh final proof run or refreshed subbundle 09 evidence after the repair.

## Dependency Impact

- Final closure remains blocked until semantic automation and screenshot proof are trustworthy.

## Validation Depth

- Critical corrective
- Focused Playwright + screenshot export retest

## Implementation Steps

1. Pinpoint whether the instability is in camera animation, DOM mirror sync, scene snapshots, screenshot export, or the test harness.
2. Refactor the runtime/test mode or proof capture path so the same scenario can be reproduced deterministically.
3. Refresh Playwright evidence and rerun the blocked proof gate.


## Do Not Do

- Do not hide weak proof behind manual-only screenshots if the bundle promised semantic automation.
- Do not leave timing-sensitive animation enabled during proof capture.

## Acceptance Checklist

- Semantic automation and screenshot capture are repeatable enough for closure.
- The proof narrative honestly states what changed and what is still limited.
- The blocked proof gate can be rerun on fresh evidence.

## Proof Required

- Run the focused WebGL Playwright suite.
- Capture runtime-exported and browser screenshots for the same scenario.
- Update the corrective memo and rerun the blocked gate.
- Validation commands to run for this subbundle:
- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~WebGl" -v:minimal`

## Browser Validation Logging

- Re-run the move/connect/export scenario and compare semantic snapshots with captured screenshots.

## Progression Gate

- Final closure remains blocked until the proof gate passes on fresh evidence after this corrective.

## Suggested Agent Prompt

```text
Execute only the corrective automation and proof reset. Stabilize the automation bridge and screenshot proof path, refresh the focused Playwright evidence, rerun the blocked proof gate, and do not close the bundle until it explicitly passes.
```

## Preserved Bundle Notes

### Review questions

- Is proof now deterministic enough to trust?
- Do semantic snapshots and screenshots agree?
- Can the blocked proof gate now pass honestly?

### Validation commands

- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~WebGl" -v:minimal`

### Corrective trigger

- This subbundle is itself a corrective playbook.

### Corrective template

- Not applicable.

### Repository touchpoints (relative)

- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07-runtime-entry.js`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`
- `tests/CanDoItAll.Tests.Playwright/SharedCanvasBrowserTests.cs`

### Notes

- Treat this subbundle as an isolated execution slice. Do not continue into later numbered work during the same pass.
- Update `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md` as soon as this subbundle either passes, blocks, or triggers a corrective path.
