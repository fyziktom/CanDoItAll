# Trim selection panel content and add contextual help hints

## Status

- `Completed`

## Objective

- Reduce the selection-panel content to only the information each node type actually needs, while moving any remaining guidance behind small contextual help affordances instead of always-visible repeated text.

## Covered Inputs

- `R004`
- `R005`
- `R006`
- Raw note `N005`
- Raw note `N006`
- Live finding: the selection panel for a created Excel node contains repeated subtype and upload-related content.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.CreateCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureNodeDescriptor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables

- Node-type-specific selection content with less repeated prose.
- Contextual help affordances only where hints still add value.
- Readable text and hint styling on the light selection surface.

## Implementation Steps

1. Audit the node descriptor and selection-record composition path to identify repeated lead text, facts, and hints by node type.
2. Remove or suppress duplicate selection content for the affected node types.
3. Add a compact contextual help affordance only where explanatory text remains necessary.
4. Update CSS for the help affordance and any resulting spacing or readability changes.
5. Add or update tests for the selection content shaping.
6. Validate the affected node types in the real browser and capture screenshots.

## Scope Exceptions

- Badge color semantics belong to subbundle 03 unless a small style dependency is needed for the help affordance.

## Do Not Do

- Do not redesign the entire selection panel.
- Do not add help icons for content that can simply be removed.
- Do not move file badge semantic logic out of the established profile path.

## Acceptance Checklist

- Representative node types show less text and no unnecessary repeated facts.
- Any retained guidance is available through a small contextual help affordance.
- Help affordances do not reduce readability or clutter the panel.
- Light-surface text and icons remain readable.

## Proof Required

- Browser pass at `1600x1000`.
- Screenshot of the selection panel for representative nodes proving reduced content.
- Screenshot or hover proof for any contextual help tooltip added.
- DOM or assertion proof that removed duplicate text is no longer present.

## Browser Validation Logging

- Route: `http://127.0.0.1:5188/projects/{id}/structure`
- Viewports: `1600x1000`
- Required Playwright MCP actions:
- Select representative nodes, including at least one file node and one non-file node if both are affected.
- Verify repeated facts or labels identified during the audit are no longer rendered.
- Hover or focus each added help affordance and verify its tooltip content.
- Required screenshots:
- `C:\repositories\CanDoItAll\output\playwright\feedback8\subbundle-02-selection-desktop.png`
- `C:\repositories\CanDoItAll\output\playwright\feedback8\subbundle-02-selection-help-tooltip.png` when a help affordance is added

## Completion Notes

- Implemented on the live route `http://127.0.0.1:5188/projects/f95ee2d4-166d-4ace-81ae-8b370730abd5/structure`.
- Final proof is captured in `C:\repositories\CanDoItAll\output\playwright\feedback8\subbundle-02-03-excel-selection-panel.png` and `C:\repositories\CanDoItAll\output\playwright\feedback8\subbundle-02-help-tooltip-zindex-fixed-viewport.png`.
- Live Playwright validation caught three tooltip defects during execution:
  - the tooltip initially rendered below the trigger and clipped out of the selection window
  - the first placement fix still overflowed laterally
  - the tooltip still rendered behind the neighboring floating window until its z-index was raised
- The shipped fix moved tooltip placement into the shared `HelpPopover` component and proved the final overlay state in-browser.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Audit node-type-specific selection content, remove unnecessary repeated text, add help affordances only where the guidance still matters, and prove the result in the real browser with screenshots and execution-report analytics.
```
