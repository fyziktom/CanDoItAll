# 02-02-catalog-expansion-and-type-mutation-flows

## Status

- `Completed`

## Objective

- Expand the standard block catalog and add maintainable block-type mutation flows for common blocks so new presets such as computer, router, and WiFi participate in the same create and change-type experiences.

## Covered Inputs

- `N006`
- `N007`
- `N009`
- `RQ-06`
- `RQ-07`
- `RQ-09`

## Prerequisites

- `01-01-visual-profile-and-palette-foundation` is completed.
- The unified preset contract already drives rendered colors for existing node categories.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.RichDefinitions.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Workflows.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructureSelectionPanel.razor
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs

## Deliverables

- New common catalog entries for computer, router, and WiFi-oriented blocks or variants.
- A supported UI flow to change block type for common catalog-backed blocks.
- Mutation logic that preserves compatible state while re-resolving the destination preset through the unified visual pipeline.
- Automated proof that the new catalog items are searchable, creatable, and mutable through browser-visible flows.

## Dependency Impact

- `03-03-inline-note-multiline-and-note-conversion` depends on this phase because note-to-block conversion should reuse the same typed mutation flow.
- `05-05-subtree-to-subproject-transfer` depends on this phase indirectly because moved descendants must retain valid block kinds after transfer.
- Weak proof here leaves later browser validation unable to distinguish whether broken block visuals or broken mutation logic caused the regression.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Extend the catalog definitions with new computer, router, and WiFi-related entries that follow the unified preset model.
2. Add the change-type workflow for common blocks in the existing project-structure UI surface.
3. Ensure type mutation preserves compatible metadata and refreshes the runtime node without forcing a destructive recreate where avoidable.
4. Add or update tests for catalog registration and mutation behavior.
5. Prove the create and change-type flows in Playwright and capture screenshots.

## Do Not Do

- Do not add new block kinds without wiring them into the same preset and mutation infrastructure as existing common blocks.
- Do not implement block-type change as delete and recreate if compatible metadata can be preserved.
- Do not hide the new blocks in a secondary or inconsistent toolbox location.

## Acceptance Checklist

- Computer, router, and WiFi-related presets appear in the standard block catalog and are searchable.
- A selected common block can change to another supported common block type through an explicit UI action.
- Changed blocks retain compatible content and adopt the correct destination preset.
- Browser proof demonstrates both new creation flows and type mutation flows.

## Proof Required

- Run focused automated coverage for catalog registration and type mutation behavior.
- Run a Playwright pass that searches for the new common blocks, creates them, and changes a supported block from one common type to another.
- Capture screenshots for toolbox discovery and post-mutation rendered state.
- Record the exact created block kinds and mutation path in `reviews/01-execution-report.md`.

## Browser Validation Logging

- Route under test: `/projects/{projectId}/structure`
- Required viewports: `1600x1000` large-screen proof and `1280x800` follow-up
- Required Playwright evidence: open the standard blocks toolbox, search for computer and router terms, create representative blocks, invoke the change-type flow, and assert the updated labels and visuals
- Required screenshots: `02-toolbox-common-blocks.png`, `02-change-block-type-result.png`
- Screenshot review questions: are the new presets discoverable, visually consistent, and clearly updated after the type change

## Progression Gate

- Downstream note conversion work may continue only after the shared change-type flow is proven for common blocks and the new catalog presets are visible and working in the browser.

## Suggested Agent Prompt

```text
Implement subbundle 02-02-catalog-expansion-and-type-mutation-flows only. Add the new common block presets, wire change-type support for common blocks through the existing project-structure surfaces, and produce the required automated and Playwright proof.
```
