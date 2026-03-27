# Target Solution

## Inspector Structure

- Keep one primary summary card for the selected node.
- Show one node title only, followed by the concise lead text and chips.
- Replace the six-equal-tiles treatment with:
  - a compact quick-summary row for Progress, Priority, and Marker
  - an advanced details accordion for Artifact, Kind, Location, and typed fact rows

## Action Rail

- Keep all existing actions, but render them through a more deliberate action model so the button order, icon, and emphasis are explicit.
- Insert `Edit` as a first-class action in the inspector rail.
- Keep `Delete` last regardless of what other contextual actions are available.

## Edit Flow

- Reuse the shared canvas composer instead of building a separate modal.
- Build an edit descriptor from the selected node by:
  - resolving the matching typed create definition for the node kind and subtype
  - hydrating the same dynamic select options already used for create flows
  - preloading request text fields and input values from the current node metadata
- Route the submitted edit request through a dedicated workbench update path that can persist:
  - title
  - subtitle
  - notes
  - start and end timestamps when applicable
  - metadata JSON validated against the existing typed envelope

## Boundaries

- Keep page code responsible for orchestration and inspector rendering.
- Keep metadata parsing and mapping in strongly typed helper code rather than inline Razor logic.
- Do not repurpose create submission as a hidden edit fallback without an explicit action path and update model.
