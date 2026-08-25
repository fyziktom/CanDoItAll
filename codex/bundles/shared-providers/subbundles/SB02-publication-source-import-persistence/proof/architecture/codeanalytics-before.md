# SB02 CodeAnalytics baseline

| Fact | Value |
| --- | --- |
| Snapshot | `snap-20260824213007-c65710b4` |
| Solution | `C:\\repositories\\CanDoItAll\\CanDoItAll.slnx` |
| Captured UTC | `2026-08-24T21:30:07Z` |
| Scoped product projects | 12 |
| Scoped source documents | 677 |
| Modules | 32 |
| Dependency edges | 4,632 |
| Direct product `ProjectReference` edges | 24 |
| Project-level cycles | 0 |
| Other reported cycles | 2 module-level, 1 nested-type |

This is the force-refreshed SB01 after snapshot and therefore the exact pre-SB02 production
baseline. It includes the zero-dependency SharedProviders Abstractions project and only the
authorized `Web -> Abstractions` edge.

SB02 may add one production edge only:
`CanDoItAll.Modules.Workspace -> CanDoItAll.SharedProviders.Abstractions`. It must not add a
Workspace-to-Http edge, move entities to Foundation, or change an inner AgentFramework project.
The after snapshot must report zero project cycles and must classify the same pre-existing
module/nested-type cycles as unchanged.
