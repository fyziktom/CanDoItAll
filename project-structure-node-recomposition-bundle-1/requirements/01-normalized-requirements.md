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

## Scope Boundaries

- The feature does not change connector routing rules beyond what follows naturally from new node positions.
- The feature does not replace the existing create-time placement policy for new nodes.
- The feature does not introduce background auto-layout or “always tidy” behavior.
