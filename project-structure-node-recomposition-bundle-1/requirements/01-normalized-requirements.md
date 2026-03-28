# Normalized Requirements

| Requirement | Description | Notes |
| --- | --- | --- |
| `R01` | Add a new project structure toolbar button that triggers subtree recomposition from the current selection. | User explicitly requested a toolbar entry point. |
| `R02` | The feature must be manual only. It must not auto-run on load, sync, create, or ordinary node movement. | Preserves user-adjusted layouts. |
| `R03` | The selected node is the root of the recomposition scope and every descendant below it is eligible for repositioning. | Scope is selection-driven, not whole-canvas-only. |
| `R04` | Recomposition changes positions only. It must not reconnect links, reparent nodes, or rewrite graph relationships. | Direct response to the “no reconnection” constraint. |
| `R05` | The recomposed result must use the available space around the selected node more efficiently and avoid the current one-direction growth pattern. | The final layout should feel radial or circular rather than lane-bound. |
| `R06` | The recomposed result must be collision-free for all moved nodes and must not overlap untouched canvas nodes. | This is a hard correctness constraint. |
| `R07` | Recomputed coordinates must persist across reloads and future surface refreshes. | Service-backed persistence is required. |
| `R08` | The recomposition algorithm must be deterministic for the same graph, selection root, and canvas state. | Manual tooling should not feel random. |
| `R09` | Bundle preparation must analyze established layout approaches and document the chosen architecture with explicit tradeoffs. | Required by the user. |
| `R10` | Final proof must include targeted tests plus real browser validation on the project structure page. | UI-only reasoning is insufficient. |
| `R11` | When recomposing from a root, first-layer descendants must be placed in clockwise clock-face order across balanced hour-like slots instead of collapsing into one side. | The user explicitly asked for layer-aware first-ring positioning. |
| `R12` | Deeper descendants must inherit the directional sector of their first-layer branch so each branch reads as a distinct grouped wedge or bubble. | Prevents children from spilling across neighboring branches. |
| `R13` | Readability is more important than squeezing every node onto one screen. Branch groups need deliberate spacing even when that means a larger overall footprint. | Very large mindmaps will still require zoom or panning. |
| `R14` | Branch-group bubbles must not collide with one another. Children from one first-layer branch must not cross into another branch bubble. | The user described this as invisible bubbles that cannot overlap. |
| `R15` | Follow-up browser proof must use the large `project-structure-mcp-validation-1 workbench` project where the first pass still showed left-sided clustering and nodes that were too close together. | Required validation target from the user feedback. |

## Scope Boundaries

- The feature does not change connector routing rules beyond what follows naturally from new node positions.
- The feature does not replace the existing create-time placement policy for new nodes.
- The feature does not introduce background auto-layout or “always tidy” behavior.
