# P1-04 Selection-Panel Decomposition And Lazy Expensive Support Surfaces

## Status
- Lifecycle status: `Ready`

## Objective
- Reduce page-level Razor recomputation by decomposing heavy selection and support surfaces without dropping behavior.

## Covered Inputs
- Audit recommendation to keep overlays in HTML and Blazor but make them cheaper and more focused.
- Feature preservation items `F07`, `F08`, `F09`, `F11`, `F12`, `F14`, `F15`, `F16`, `F17`, `F18`, `F19`, `F20`, `F22`, `F23`, and `F25`.

## Prerequisites
- `P0-06` completed with trusted runtime-shell cleanup proof.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.SelectionPanel.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Workflows.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables
- Selection-panel decomposition or equivalent recomputation reduction.
- Lazy rendering of expensive support sections where appropriate.
- Full preservation of modal, preview, transcript, runtime-launch, and summary behavior.

## Dependency Impact
- Largest feature-preservation surface in the bundle.
- Later browser regression suite work depends on these flows staying stable and more localizable.

## Validation Depth
- Broad targeted component coverage across affected selection and dialog flows.
- Browser proof for representative single-select, multi-select, modal, preview, and runtime-launch surfaces.
- Screenshot review for selection window, modal, preview, and provider confirmation states.

## Implementation Steps
- Identify which overlay sections recompute unnecessarily today.
- Extract or defer only the sections that materially reduce recomputation while keeping state explicit.
- Preserve action ordering and type-specific behavior.

## Do Not Do
- Do not move overlay logic into the renderer hot path.
- Do not silently remove advanced detail or modal flows because they are expensive.

## Acceptance Checklist
- Selection UI remains feature-complete.
- Unrelated viewport changes do not force large overlay recomputation.

## Proof Required
- Targeted ProjectStructure component tests for selection and modal flows.
- Playwright proof for representative overlay workflows.
- Screenshots for each visible surface materially touched by the change.

## Browser Validation Logging
- Route: ProjectStructure structure route.
- Viewport: large-screen first, narrower-width follow-up if layout shifts.
- Record overlay states, screenshot paths, and result in `reviews/01-execution-report.md`.

## Progression Gate
- Do not start final platform modularization or closure work until selection and dialog behavior is both preserved and measurably less coupled to viewport interaction.

## Suggested Agent Prompt
- Decompose or lazy-render only the expensive selection and support surfaces that materially reduce page recomputation, then prove that all mapped modal and detail workflows still behave correctly.
