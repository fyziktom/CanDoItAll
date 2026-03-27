# 01-compact-node-path-and-file-presentation

## Status

- `Ready`

## Objective

- Replace noisy full-path lead text with a compact path affordance and promote file names on file-backed nodes so project-structure cards stay readable without hiding the full path from the user.

## Covered Inputs

- `N001` path-backed nodes should show one compact path button, hover the full path, copy on click, and show a temporary check icon.
- `N002` when the path ends with a file name, the node should display that file name on the node itself.
- `R001`
- `R002`
- `R003`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureNodeDescriptor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\CanvasWorkbenchContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`

## Deliverables

- Additive typed node metadata for compact path presentation.
- Shared canvas rendering for the compact path button, tooltip, and copied-state indicator.
- File-name promotion for file-backed nodes.
- Focused automated proof and browser screenshots for long-path presentation.

## Implementation Steps

1. Extend the Workbench node descriptor and adapter to emit typed compact-path data without teaching JavaScript how to infer semantics from plain strings.
2. Promote the file name onto the node card when the path clearly ends with a file-like leaf.
3. Update shared canvas rendering and CSS to show a single compact path button with:
   - compact visible label
   - full-path hover text
   - click-to-copy behavior
   - transient copied-state success icon
4. Preserve legacy lead-text rendering for nodes that do not carry path presentation metadata.
5. Add focused proof for the rendered node content and copied-state behavior.

## Scope Exceptions

- Do not redesign the full node-card visual hierarchy beyond the path-related changes required by the feedback.
- Do not change preview or double-click behavior in this phase.

## Do Not Do

- Do not parse raw lead-text strings in JavaScript to guess whether a value is a path.
- Do not introduce a separate clipboard service or broader node metadata refactor.
- Do not add unrelated typography or layout restyling to other node content.

## Acceptance Checklist

- A long path-backed node no longer renders the full path as generic card text.
- The node exposes one compact path affordance.
- Hover exposes the full path.
- Clicking the affordance copies the full path and shows visible success feedback for about two seconds.
- A file-backed path promotes the file name on the card itself.

## Proof Required

- Add a focused automated test that exercises the new path-presentation mapping or rendered output.
- Run a maximized browser pass on the project-structure surface and save a screenshot showing the compact path presentation.
- Confirm via browser proof that the copied-state icon appears and then resets.
- If layout changes materially, add a narrower-width confirmation pass and record the result in the execution report.

## Suggested Agent Prompt

```text
Implement feedback7 subbundle 01 only.

Keep the change inside the existing project-structure descriptor, adapter, and shared canvas rendering path. Introduce typed path-presentation data instead of string parsing, render a single compact path control with tooltip and copied-state feedback, and promote the file name when the path points at a file.
```
