# Workbench ProjectStructurePage refactor and validation

## Status

- `Completed`

## Objective

- Refactor `ProjectStructurePage` and its related workbench surfaces so the non-canvas overlays, toolbox, detail preview, and support panels rely on shared components and shared Tailwind-backed styles instead of large raw markup blocks, while preserving the existing canvas-owned behavior.

## Covered Inputs

- `REQ-03`, `REQ-04`, `REQ-12`, `REQ-13`, `REQ-16`, `REQ-17`, `REQ-18`
- Follow-up input: `ProjectStructurePage.razor` must replace raw `div` / `button` / `span` / `p` markup with shared components and prepared styles where reasonable.
- Follow-up input: `Project Structure Toolbox` must be rebuilt around a common treeview component with one-row items (`icon + text + tooltip`).
- Follow-up input: the page must be split into logical subparts or components.
- Follow-up input: node-data preview should be improved through a typed or reflection-backed detail factory based on attributed model properties.

## Prerequisites

- Subbundles `01`, `02`, `03`, and `04` remain valid.
- The current repeated-pattern cleanup wave remains trusted because this page should consume the same shared layout and typography primitives.

## Exact Source References

- `C:\repositories\CanDoItAll\solution-style-unification-bundle-v1\inputs\01-project-structure-page-follow-up.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.ToolWindows.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.SelectionPanel.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructureSelectionPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureNodeDescriptor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables

- A shared treeview primitive added to BaseLib and used by the `Project Structure Toolbox`.
- `ProjectStructurePage.razor` materially shortened through logical component extraction.
- Non-canvas workbench overlays and support panels migrated toward shared wrappers and prepared styles.
- Improved node preview rendering that uses typed metadata display helpers instead of only a flat fact list when richer metadata exists.
- Measured progress for the page split, wrapper uptake, and repeated-markup reduction.

## Dependency Impact

- This phase reopens a previously deferred workbench surface. Weak proof here would leave one of the largest remaining style hotspots outside the shared system.
- The final closure audit cannot honestly claim broader non-canvas style unification progress without this page because it is one of the largest remaining raw-markup workbench surfaces.

## Validation Depth

- `UI, component-test, build, and browser-proof`

## Implementation Steps

1. Inventory the main raw-markup hotspot families inside `ProjectStructurePage` and the linked selection panel or overlay surfaces.
2. Add any missing shared components required for the refactor, starting with a reusable BaseLib treeview primitive.
3. Split the page into logical workbench components that align with the existing code-behind seams: toolbar, toolbox, selection or detail support, and overlay dialogs.
4. Improve node preview rendering with a typed metadata or reflection-backed display layer so important node properties surface through reusable preview components instead of manual fact rows alone.
5. Update or extend component tests for the changed toolbox and detail surfaces.
6. Build and browser-validate the workbench route with the toolbox, selection panel, preview dialogs, and detail windows on large and narrower viewports.

## Scope Exceptions

- Do not change `CanDoItAll.Components.CanvasLib` internals or drawing behavior. The refactor may compose around canvas-hosted content but must not rewrite the canvas substrate itself.
- If some page-local overlay behavior is too coupled to the workbench surface for BaseLib extraction, that logic may stay module-local, but the markup and styling should still move into focused reusable components.

## Do Not Do

- Do not leave the toolbox as a long stacked catalog with multi-line raw item cards.
- Do not hide metadata preview complexity behind another large inline markup wall in `ProjectStructurePage.razor`.
- Do not regress selection syncing, grouped create actions, or workbench dialog behavior while chasing style cleanup.

## Acceptance Checklist

- `ProjectStructurePage.razor` is materially shorter and delegates major surfaces to focused components.
- The toolbox uses a shared treeview primitive with one-row entries, icon, label, and tooltip behavior.
- The node detail preview is richer than the previous flat fact wall and is driven by reusable display helpers.
- Shared wrappers or prepared styles replace the obvious raw non-canvas markup families in the touched workbench surfaces.
- Tests, builds, and browser validation pass with saved screenshots and no overlay clipping or horizontal overflow.

## Proof Required

- `npm run build` from `C:\repositories\CanDoItAll\Tailwind`
- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Focused test run for `ProjectStructurePageTests`
- Browser proof for `http://127.0.0.1:5501/projects/{projectId}/structure` covering toolbox, selection/detail windows, and support dialogs
- Updated metrics and closure notes in the execution report and workbook if the census materially changes

## Browser Validation Logging

- Target route: `/projects/{projectId}/structure`
- Required viewports: `1600x1200`, `1280x900`, `390x960`
- Required Playwright actions: open the workbench page, open the toolbox, expand or collapse tree nodes, filter the toolbox, open selection-driven detail content, and open at least one relevant support dialog or preview
- Required screenshot findings: toolbox rows stay single-line and readable, detail content is not clipped, floating windows layer correctly, dialog content wraps correctly, and the canvas safe zones remain intact

## Progression Gate

- This subbundle passes only when the shared treeview exists, the page split is real, the detail preview improvement is shipped, focused tests pass, and browser proof exists for the changed workbench surfaces.

## Suggested Agent Prompt

```text
Refactor ProjectStructurePage onto shared wrappers and prepared styles, add a reusable treeview primitive for the toolbox, split the page into logical workbench components, improve node-detail preview rendering through reusable metadata display helpers, and prove the result with tests, builds, and browser screenshots.
```

## Completion Notes

- `ProjectStructurePage.razor` line count moved from `1775` at `HEAD` to `1464`.
- `8` focused workbench components were added under `Pages\Components\ProjectStructure`.
- The page-file case-sensitive raw-tag census after refactor is `0` for `<div`, `<button`, `<span`, and `<p `.
- BaseLib gained shared `TreeView`, `TreeViewNodeRow`, and `TreeViewPrimitives`.
- The workbench metadata display now flows through `ProjectStructurePreviewFieldAttribute`, `ProjectStructureNodeDetailFactory`, and `ProjectStructureNodeDetailPreview`.
- Browser proof found one real narrow-width overflow in the outline support panel; the fix landed in shared treeview sizing plus `ProjectStructureSupportPanels`, and the final narrow-width proof measured `bodyScrollWidth 405` and `docScrollWidth 405` at `innerWidth 420`.
