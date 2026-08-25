# SB03 CodeAnalytics baseline

| Fact | Value |
| --- | --- |
| Snapshot | `snap-20260824235022-a4b340a8` |
| Solution | `C:\\repositories\\CanDoItAll\\CanDoItAll.slnx` |
| Captured UTC | `2026-08-24T23:50:22Z` |
| Scoped product projects | 13 |
| Scoped source documents | 717 |
| Modules | 34 |
| Dependency edges | 4,849 |
| Direct product `ProjectReference` edges | 31 |
| Project-level cycles | 0 |
| Other reported cycles | 2 module-level, 1 nested-type |
| Error findings | 0 |

This force-refreshed pre-SB03 snapshot adds Composition to the prior CP-02 scope so the planned
descriptor-only integration wiring has an exact baseline. No SB03 product edit had been applied.

The authorized product delta is one new zero-SDK `CanDoItAll.SharedProviders.Http` descriptor
project with `Http -> Abstractions` and outer `Composition -> Http` wiring. Workspace must retain
only its Abstractions edge; Web endpoints may use existing Workspace/Abstractions edges and must
not dispatch upstream. The after snapshot must have zero project cycles and the same three
pre-existing internal cycles.

