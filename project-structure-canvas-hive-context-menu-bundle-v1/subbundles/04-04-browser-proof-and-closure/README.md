# 04-browser-proof-and-closure

## Status

- `Ready`

## Objective

- Consolidate focused test proof, browser screenshots, screenshot review, raw-note closure, and validator results so the hive-menu bundle can close honestly.

## Covered Inputs

- `N001` through `N008` as final closure verification.

## Prerequisites

- `01-01-standard-ring-order-and-node-menu-contract` complete with proof.
- `02-02-hive-geometry-and-submenu-packing` complete with browser proof.
- `03-03-visual-polish-and-responsive-tuning` complete with reviewed screenshots.

## Exact Source References

- `C:\repositories\CanDoItAll\project-structure-canvas-hive-context-menu-bundle-v1\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureActionCatalogAdapterTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureCanvasCatalogTests.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\03-interaction-and-state.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench\overlays\05-overlays-and-composer.css`

## Deliverables

- Updated execution report with actual commands, screenshots, browser analytics, and subbundle gate decisions.
- Explicit closure status for `N001` through `N008`.
- Completed-stage validator pass recorded in the execution report.

## Dependency Impact

- This phase closes the bundle. Weak proof here would leave the spatial complaint unresolved and make future menu work inherit the same ambiguity.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run the focused automated tests needed for the reordered catalog contract.
2. Re-run final browser proof on the structure canvas with the finished hive layout.
3. Capture and review the final screenshots.
4. Update the execution report with commands, analytics, raw-note closure, and residual risks.
5. Run the completed-stage validator and repair any closure defects before exiting.

## Scope Exceptions

- Do not introduce new product scope in this phase.

## Do Not Do

- Do not mark the raw notes solved without explicit browser evidence for the visual composition complaint.
- Do not skip the completed-stage validator.

## Acceptance Checklist

- Focused automated tests pass or are honestly documented with a blocker.
- Final browser screenshots show the intended hive composition.
- Raw-note closure explicitly maps each note to proof.
- Completed-stage validator passes.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "ProjectStructureActionCatalogAdapterTests|ProjectStructureCanvasCatalogTests"`
- Final Playwright MCP proof on `/projects/{projectId}/structure`
- Screenshot artifacts:
  - `output/playwright-mcp/hive-context-menu-desktop.png`
  - `output/playwright-mcp/hive-context-menu-narrow.png`
- `python C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\project-structure-canvas-hive-context-menu-bundle-v1 --profile feedback --stage completed`

## Browser Validation Logging

- Route: `/projects/{projectId}/structure`
- Viewports: `1600x1000` and `1280x800`
- Playwright MCP actions: open the node context menu on a representative node, verify first-ring order, inspect honeycomb density, optionally open one submenu, capture final screenshots
- Review questions:
  - Is the menu materially denser and better organized than before?
  - Do the first-ring actions read as a learnable clockwise pattern?
  - Are any labels clipped or any overlays colliding?

## Progression Gate

- Bundle closure is allowed only after raw-note closure is explicit, browser analytics are recorded, and the completed-stage validator passes.

## Suggested Agent Prompt

```text
Implement only subbundle 04 for the project-structure canvas hive context menu bundle.
Run the focused automated tests, capture final browser proof and screenshots, update the execution report with actual outcomes and note-by-note closure, and finish with a passing completed-stage validator.
```
