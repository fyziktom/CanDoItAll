# Assumptions And Risks

## Assumptions

- The request is primarily about the project-structure node context menu, though reusable runtime improvements may also benefit other menu layers.
- The current central hub label can remain as the core, with the “first circle” interpreted as the first ring of six actionable hexagons around that hub.
- `Blocks`, `Assets`, and `Tasks` refer to existing grouped actions rather than adding new command surfaces.
- The design should stay within the existing CanDoItAll visual language: light surfaces, tone-based action colors, existing glyph system, and shortcut affordances.

## Critical Path Risks

- `01-01-standard-ring-order-and-node-menu-contract` is the first critical foundation. If the first-ring ordering is wrong or non-deterministic, all later browser proof about composition becomes misleading because the stable clockwise scan pattern never really exists.
- `02-02-hive-geometry-and-submenu-packing` is the second critical foundation. If the math still leaves awkward gaps or breaks submenu positioning, later polish cannot rescue the core layout.

## Validation Risks

- Browser proof must validate the open overlay state. Static code reasoning is weak proof for a composition complaint.
- The managed watch session is currently in a restart-pending state, so browser validation may require a clean watch-ready session before layout proof can be trusted.
- The request is subjective enough that screenshot review must answer explicit questions about density, spacing, and edge sharing; a passing DOM assertion alone would be too weak.

## Reopen Triggers

- If the first-ring order differs across node types without an intentional documented reason, reopen subbundle 01.
- If desktop screenshots still show visible empty gaps that make the hexes read as separated buttons instead of a hive, reopen subbundle 02.
- If submenu opening, keyboard focus, or shortcut label fit regresses after the composition change, reopen the subbundle that introduced the regression instead of papering it over in closure.
