# User Stories And Layout Findings

## User stories on the projects page

The page has to support these primary flows without losing the project list as working context:

1. Scan many projects quickly by name, current phase, status, and recent update signal.
2. Narrow the board by text search and status filter, then reset filters quickly.
3. Create a new project directly from the page.
4. Open a compact project overview/details surface.
5. Enter the full editor flow for save, delete, phase planning, stack profile, and starter object setup.
6. Jump directly to dashboard, structure, or calendar for a specific project.
7. Keep empty-state creation discoverable when no projects match.

## Layout changes made

- Reduced the top section to a compact command bar instead of a tall summary-first layout.
- Moved "New" and "Dashboard" into the same action band as search and filters.
- Replaced broken icon-only affordances with compact visible labels such as `ACT`, `DRF`, `UPD`, `NOTE`, `DB`, `ST`, and `CAL`.
- Kept the project cards as the primary working surface and pushed the editor into the modal flow only.
- Forced the card region to own its own scroll so the page does not waste viewport height on repeated chrome.
- Preserved all existing operations already present on the page.

## Measured before vs after

Baseline evidence: `artifacts/baseline-before/metrics.json`

Final watch validation: `artifacts/after-fresh-watch-visible-tags/metrics.json`

Final atomic validation: `artifacts/after-atomic-visible-tags/metrics.json`

### Desktop 1440x900

- Baseline `documentHeight`: `16132`
- Final `documentHeight`: `1551`
- Baseline first card top: `600`
- Final first card top: `393`
- Baseline search top: `475`
- Final search top: `252`
- Final board bounds: `top=149`, `bottom=809`, `height=660`

### Mobile 390x844

- Baseline `documentHeight`: `37711`
- Final `documentHeight`: `952`
- Baseline first card top: `1415`
- Final first card top: `693`
- Baseline search top: `1225`
- Final search top: `568`

## Important boundary

The projects board itself fits within the desktop viewport after the redesign. The remaining full-page scroll comes from the shared dev-only `Tuning Mode` panel rendered below the page content in the app shell. That is outside the projects page itself and explains why `documentHeight` still exceeds the viewport even though the board sits entirely inside it.
