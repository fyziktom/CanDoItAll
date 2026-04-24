# Automation bridge and proof surface

## Status

- Completed

## Objective

- Add the semantic automation bridge, host debug state, DOM mirror queries, export hooks, and Playwright tests needed to prove WebGL interactions deterministically.

## Covered Inputs

- `IN-12`
- `IN-13`
- `IN-19`
- `RQ-10`
- `RQ-15`
- `RQ-16`
- `RQ-20`

## Prerequisites

- `08-architecture-review-gate-b`

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07-runtime-entry.js
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Playwright/SharedCanvasBrowserTests.cs

## Deliverables

- Semantic runtime helpers such as scene snapshot, diagnostics, export image data, simulate drag, finish interaction, and connection simulation.
- Stable host debug state and DOM mirror data attributes for nodes/ports/edges.
- Focused Playwright tests for screenshot capture and semantic interaction validation.

## Dependency Impact

- Final closure proof depends on this bridge being deterministic and honest about any remaining gaps.
- If automation remains weak here, the concept cannot claim repeatable validation.

## Validation Depth

- Critical
- Focused Playwright + screenshot export + semantic assertions

## Implementation Steps

1. Mirror the current canvas runtime's automation style under `window.CanDoItAll.webglWorkbench`.
2. Expose scene snapshots and node/port lookup information through both JS helpers and DOM mirror anchors.
3. Add focused Playwright coverage for node movement, connection mutation, and image export on the sandbox route.
4. Stabilize test mode so screenshots and camera state remain repeatable across runs.


## Do Not Do

- Do not rely on screenshot-only assertions for authoring changes.
- Do not hide automation helpers in ad-hoc test code that the runtime itself does not own.
- Do not accept non-deterministic camera animation during proof capture.

## Acceptance Checklist

- Playwright can read the scene semantically and drive at least move/connect flows through the automation bridge.
- The runtime can export screenshots directly.
- DOM mirror anchors and host debug state stay stable enough for test assertions.

## Proof Required

- Run focused Playwright tests against the dedicated sandbox route.
- Capture exported screenshots and ordinary browser screenshots for the same scenario.
- Record semantic scene-snapshot deltas before and after move/connect actions.
- Validation commands to run for this subbundle:
- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~WebGl" -v:minimal`

## Browser Validation Logging

- Route: dedicated WebGL sandbox route.
- Actions: use automation bridge to read the scene, move a node, mutate a connection, export an image, and reset.
- Screenshots: captured via normal browser screenshot and via runtime export.
- Review questions: do semantic snapshots agree with screenshots, and are the test helpers stable enough to become the standard proof path?

## Progression Gate

- Final closure may continue only after semantic automation, screenshot export, and focused Playwright proof all pass or are honestly blocked with explicit reasons.

## Suggested Agent Prompt

```text
Implement only subbundle 09. Add the semantic automation bridge and debug state to the WebGL runtime, expose DOM mirror anchors, add focused Playwright coverage for move/connect/export flows, prove deterministic screenshot capture, and stop before final closure work.
```

## Preserved Bundle Notes

### Review questions

- Can Playwright prove semantic state changes without brittle raw pointer hacks?
- Does the runtime-exported image match the visible browser scene closely enough for review?
- Are DOM mirror anchors and debug state appropriately stable?

### Validation commands

- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~WebGl" -v:minimal`

### Corrective trigger

- If this subbundle fails, open `_corrective-automation-and-proof-reset` before continuing downstream.

### Corrective template

- `subbundles/_corrective-automation-and-proof-reset`

### Repository touchpoints (relative)

- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07-runtime-entry.js`
- `src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`
- `tests/CanDoItAll.Tests.Playwright/SharedCanvasBrowserTests.cs`

### Notes

- Treat this subbundle as an isolated execution slice. Do not continue into later numbered work during the same pass.
- Update `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md` as soon as this subbundle either passes, blocks, or triggers a corrective path.
