# Corrective playbook — workspace and shared-infrastructure reset

## Status

- `Completed`
- `2026-04-13`: not triggered because Gate D passed without corrective work.

## Objective

- Repair any Gate D failure where shared extraction, workspace decomposition, UI proof, or schema hygiene regressed instead of materially improving the architecture.

## Covered Inputs

- `U002` Check duplication across modules.
- `BRQ-012` Cross-module duplication reduction.
- `BRQ-013` Workspace and canvas decomposition.
- `BRQ-014` Schema and model hygiene.
- `BRQ-016` Repeated architecture review gates.
- `BRQ-017` Corrective-first continuation.

## Prerequisites

- Gate D or equivalent late-stage proof has failed.
- Subbundles `12-14` were the most recent implemented phases being reviewed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasSurfaceFactoryTests.cs
- C:\repositories\CanDoItAll\architecture_hardening_bundle\reviews\01-execution-report.md
- C:\repositories\CanDoItAll\architecture_hardening_bundle\reviews\02-architecture-gate-memo-log.md

## Deliverables

- A corrected ownership split for shared helpers and workspace surfaces.
- Refreshed component, browser, and build or schema proof for the repaired scope.
- Updated execution-report and gate-memo records tied to the Gate D rerun.

## Dependency Impact

- Final regression proof depends on Gate D being trustworthy.
- Weak correction here would hide UI or schema regressions behind a nominal decomposition.

## Validation Depth

- `Corrective gate with UI proof`

## Implementation Steps

1. Capture the failing Gate D evidence and isolate whether the defect is ownership drift, duplication, workspace concentration, UI regression, or schema incoherence.
2. Apply the smallest correction that restores clear ownership and the intended UI or schema behavior.
3. Rerun focused component proof for the touched workspace surfaces.
4. Refresh browser proof on `/processes` and any build or schema validation affected by the correction.
5. Rerun Gate D and update the execution report and gate memo before unblocking final closure work.

## Do Not Do

- Do not move shared logic into a dumping ground to claim deduplication.
- Do not close this corrective path without fresh browser proof when the failure is UI-visible.
- Do not leave schema or provider coherence to final closure if Gate D already exposed the problem.

## Acceptance Checklist

- Shared infrastructure has clear ownership and no new dumping-ground extraction.
- Workspace and canvas responsibilities are materially healthier for the corrected scope.
- UI regressions are disproven with fresh browser evidence when relevant.
- Gate D reruns and passes with explicit evidence.

## Proof Required

- Focused component tests for the repaired workspace or canvas surfaces.
- Refreshed Playwright proof on `/processes` when UI behavior changed.
- Build or schema validation when configuration or persistence shape changed.
- Updated `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md`.

## Browser Validation Logging

- Capture `/processes` route, viewport, Playwright actions, screenshots, and visual review answers whenever the corrective work changes a visible workspace or overlay surface.
- If the correction is purely non-UI, record `N/A` explicitly and rely on component or build proof.

## Progression Gate

- Gate D passes with explicit evidence that shared ownership, workspace decomposition, and schema hygiene are acceptable for final closure work.

## Suggested Agent Prompt

```text
Execute only the workspace-and-shared-infrastructure corrective subbundle for a failed Gate D. Repair the ownership or UI/schema regression, rerun focused component and browser proof, rerun Gate D, and keep final closure blocked until the gate passes.
```
