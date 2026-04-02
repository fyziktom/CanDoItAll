# Normalized Requirements

## RQ-01 Shared Hive Packing

- The project-structure context menu must use a true honeycomb-style composition where neighboring hexagons visually sit edge-to-edge or near edge-to-edge instead of being separated by large radial gaps.

## RQ-02 Stable First Ring

- Node context menus must present a stable first ring of common actions around the core so frequent actions are learned spatially.
- The clockwise order for that first ring must be `Blocks`, `Assets`, `Tasks`, `Progress`, `Markers`, plus one deterministic node-specific slot.

## RQ-03 All-Node Consistency

- The standard first-ring composition must apply across node context menus for all node types, with the sixth slot chosen deterministically per node type when the available actions differ.

## RQ-04 Intentional Overflow Grouping

- Remaining actions outside the first ring must be placed in the surrounding hive in a way that remains grouped, readable, and appropriate for the specific node instead of arbitrary spillover.

## RQ-05 Preserve Existing Interaction Model

- Keyboard shortcuts, visible shortcut emphasis, submenu opening, click behavior, and accessible labeling from the existing context menu must continue to work after the composition change.

## RQ-06 Space Efficiency And Visual Quality

- The new composition must use the available overlay space more intentionally than the current loose orbit and should read as more organized and visually coherent in browser screenshots.

## RQ-07 Responsive And Edge Safety

- The reworked hive must stay usable near canvas edges, and nested submenus must avoid clipping, overlap, or off-screen placement at both large-screen and narrower desktop widths.

## RQ-08 Proof And Closure

- Completion requires focused automated proof where practical, large-screen and narrower-width browser screenshots of the open menu, execution-report analytics, and passing prepared/completed bundle validators.
