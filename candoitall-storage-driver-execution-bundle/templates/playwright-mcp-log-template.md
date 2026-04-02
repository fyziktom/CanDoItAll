
# Playwright MCP Log Template

## Surface Identity

- Route:
- Surface / dialog / overlay name:
- Owning phase:
- Owning workstream:
- Workbook touchpoint ids:

## Viewports

- Desktop: `1900x1200`
- Narrow follow-up: `1366x900` or nearest practical width

## Required Actions

1. Navigate to the target route in a headed browser.
2. Open every changed dialog, dropdown, overlay, preview panel, and wizard step.
3. Trigger both supported and unsupported actions when applicable.
4. Capture screenshots in open state and stable state.
5. Record any retries, broken steps, or blockers honestly.

## Assertions

- Interaction succeeded without console-breaking UI errors.
- The correct actions are enabled, disabled, or hidden based on storage capabilities.
- No text clipping, overflow, overlap, or inaccessible controls were observed.
- No overlay, modal, or preview pane escaped the viewport bounds.
- The surface remained readable at both required widths.

## Artifacts

- Desktop screenshot path:
- Narrow screenshot path:
- Extra open-state screenshot path(s):
- Optional console/network note path:

## Findings

- Visual review findings:
- Behavior findings:
- Blockers or reopen triggers:
- Final result:
