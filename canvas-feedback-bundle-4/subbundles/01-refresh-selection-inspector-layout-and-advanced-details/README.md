# 01 Refresh Selection Inspector Layout And Advanced Details

## Objective

Make the selected-node inspector denser and more intentional by removing repeated primary information, grouping the quick status signals, and moving secondary metadata into an advanced details section.

## Covered Notes

- `N001`
- `N002`
- `N003`
- `R001`
- `R002`
- `R003`
- `R007`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureNodeDescriptor.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables

- streamlined selected-node summary markup
- compact quick-status presentation for Progress, Priority, and Marker
- advanced details accordion or disclosure section for lower-priority metadata and typed facts

## Implementation Steps

1. Refactor the selected-node inspector markup to remove duplicated title or identity text.
2. Replace the six-equal-tile treatment with a more deliberate quick-summary structure.
3. Move Artifact, Kind, Location, and typed fact rows into an advanced reveal near the end of the inspector.
4. Adjust CSS so the inspector still reads clearly in the floating window.

## Do Not Do

- do not hide status chips or attachment/transcript/runtime sections behind the advanced details control
- do not invent new metadata that is not already present on the node
- do not break the current single-select and multi-select branch split

## Acceptance Checklist

- the lead card shows the node title once
- Progress, Priority, and Marker sit together as one row or band
- Artifact, Kind, Location, and typed details are collapsed into advanced details by default
- the inspector still renders attachment previews and supporting cards in the expected order

## Proof Required

- focused component coverage for the selected-node inspector structure
- execution report updated with the exact validation command and result

## Suggested Agent Prompt

```text
Implement subbundle 01 only.

Refactor the selected-node inspector so it prioritizes one clear identity block, one compact quick-status band, and an advanced details reveal for lower-priority metadata. Keep the page readable inside the floating window and do not disturb the attachment, transcript, or runtime sections.
```
