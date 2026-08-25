# SB03 CodeAnalytics after implementation

State: `PASS`

| Fact | Value |
| --- | --- |
| Snapshot | `snap-20260825012213-a17e36ed` |
| Solution | `C:\\repositories\\CanDoItAll\\CanDoItAll.slnx` |
| Captured UTC | `2026-08-25T01:22:13Z` |
| Scoped product projects | 14 |
| Scoped source documents | 736 |
| Modules | 35 |
| Dependency edges | 4,954 |
| Direct product `ProjectReference` edges | 33 |
| Project-level cycles | 0 |
| Other reported cycles | 2 module-level, 1 nested-type |
| Error findings | 0 |

The closure run force-refreshed the same 13-project baseline scope used by
`snap-20260824235022-a4b340a8` plus the new Http project. It confirms one new product project and
exactly the two authorized edges `Http -> Abstractions` and `Composition -> Http`, with no
Workspace-to-Http or Abstractions-to-product edge and no new cycle.

The larger snapshot reports one additional warning-class heuristic and seven informational
findings, but still no error finding. The warning delta is neither a project-reference violation
nor a new cycle and does not block the SB03 checkpoint.
