# Validation and closure

## Status

- `Completed`

## Objective

- Prove that the completed stabilization preserved behavior, removed the targeted duplicate surfaces, reduced CanvasLib folder density, and closed the bundle honestly.

## Covered Inputs

- `N003 too large files, too many files in one folder are not ok`
- `N007 assure all functions are always preserved all is working as before`
- `R006 Behavior Preservation`
- `R007 Closure Audit`
- `R008 Scope Discipline`

## Prerequisites

- `subbundles/01-asset-ownership-and-duplicate-retirement` closed
- `subbundles/02-canvaslib-component-topology-reorganization` closed
- `subbundles/03-canvas-graph-and-contracts-decomposition` closed

## Exact Source References

- `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json`
- `C:\repositories\CanDoItAll\tools\canvaslib\build-assets.cjs`
- `C:\repositories\CanDoItAll\tools\canvaslib\verify-assets.cjs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`
- `C:\repositories\CanDoItAll\canvaslib-maintainability-stabilization-bundle-v1\reviews\01-execution-report.md`

## Deliverables

- Final command log
- Final browser analytics table
- Raw-note closure table
- Duplicate and line-count audit results
- Completed bundle synchronization and validator proof

## Dependency Impact

- This is the closure phase. Weak proof here would invalidate the entire stabilization claim and must reopen earlier work instead of being documented as a vague residual risk.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Re-run the asset commands and build or test commands required by the earlier subbundles.
2. Run the browser regression proof that covers the shared canvas surfaces.
3. Run the duplicate, line-count, and folder-density audits.
4. Update the execution report with shipped proof, gate decisions, analytics review, and raw-note closure.
5. Run the bundle validator for `completed` and repair any documentation drift.

## Scope Exceptions

- Any remaining exception must be specific, evidenced, and tied to a concrete follow-up. A generic `future cleanup` note is not acceptable.

## Do Not Do

- Do not hide failed proof behind `manual testing`.
- Do not leave executed subbundles in `Ready` or `In progress`.
- Do not call the duplicate cleanup complete unless the audit actually proves it.

## Acceptance Checklist

- Asset commands pass.
- Web build and component tests pass.
- Browser proof passes on the shared canvas routes.
- Duplicate audit shows the targeted duplicate surfaces are retired or explicitly excepted with evidence.
- CanvasLib line-count and folder-density audits show the structural cleanup.
- Bundle prepared and completed validators both pass.

## Proof Required

- `npm run canvaslib:build-assets`
- `npm run canvaslib:verify-assets`
- `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- Targeted or full Playwright proof from `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`
- Duplicate audit proving the CanvasLib mirror tree decision and the legacy duplicate-project decision
- CanvasLib line-count and folder-density audit
- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\canvaslib-maintainability-stabilization-bundle-v1 --profile initiative --stage completed`

## Browser Validation Logging

- Routes:
  - `/projects/{projectId}/structure`
  - `/prompt-factory`
  - `/projects/{projectId}/calendar`
- Viewports:
  - `1900x1200`
  - `1600x900`
  - `1280x800`
- Required Playwright proof:
  - open the shared workbench shell
  - verify at least one route from each consumer surface loads cleanly
  - capture screenshots or cite existing Playwright artifact paths
- Screenshot review:
  - readable shell and toolbar
  - overlays layer correctly
  - no missing assets or blank canvases

## Progression Gate

- The bundle may close only after all required commands pass, browser analytics are populated, raw-note closure is complete, and the completed validator passes.

## Suggested Agent Prompt

```text
Execute only the final validation and closure phase.
Do not add new feature work here. Prove the stabilization with commands, browser evidence, duplicate and size audits, then synchronize the bundle and pass the completed validator.
```
