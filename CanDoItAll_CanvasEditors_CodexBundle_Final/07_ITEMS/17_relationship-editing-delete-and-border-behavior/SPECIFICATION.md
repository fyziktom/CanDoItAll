
# Specification

## Item identity

- **Item ID:** I17
- **Title:** Relationship editing, delete behavior, and borders
- **Origin:** docx
- **Dependencies:** I01

## Objective

Make structural editing safer and more expressive when users reconnect nodes, delete them, or organize them into named borders.

## Normalized scope

Add unconnect and reconnect workflows, sensible delete confirmation rules, drag-to-border behavior, and border naming.

### In scope

- Unconnect and reconnect behavior.
- Delete confirmation rules by node complexity.
- Drag-and-drop onto borders or group frames.
- Border naming and display.

### Out of scope

- A full graph-history UI beyond existing undo or redo support.

## Key implementation decisions

- Differentiate simple-note deletion from higher-risk deletion that deserves confirmation.
- Treat borders as named grouping constructs rather than anonymous drop zones.
- Keep relationship editing explicit so reconnection is visible and reversible.

## Implementation tasks

- Add clear unconnect and reconnect actions.
- Implement complexity-aware delete confirmation behavior.
- Enable drag-and-drop into named borders or frames and surface border naming controls.

## Risks to control

- Graph corruption if reconnection updates parent-child relationships incompletely.

## Covered original notes

- N129 — Unconnect node and connect it to some different node
- N130 — Delete (simple note without confirmation, more complex with confirmation)
- N131 — Drag and drop node to some border with other nodes
- N132 — Name for borders
