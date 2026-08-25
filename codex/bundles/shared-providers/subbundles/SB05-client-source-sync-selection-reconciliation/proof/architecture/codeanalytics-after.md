# SB05 CodeAnalytics after implementation

State: `PASS`.

The final force-refreshed snapshot is `snap-20260825070408-300644c7`, built from the same 14
product-project scope as the SB05 entry snapshot with DI, persistence, and risk collection enabled.

| Fact | Before | After |
| --- | ---: | ---: |
| Scoped product projects | 14 | 14 |
| Scoped source documents | 752 | 758 |
| Modules | 35 | 35 |
| Dependency facts | 5,158 | 5,231 |
| Direct product `ProjectReference` edges | 34 | 34 |
| Project-level cycles | 0 | 0 |
| Existing other cycles | 2 module, 1 nested type | unchanged |
| Error findings | 0 | 0 |

A `SharedProvider`-focused findings query reports 13 warnings and 46 informational findings, zero
errors, and zero open questions. The warnings are size/complexity heuristics. The new responsibilities
are already separated into neutral contracts, safe transport, source CRUD, synchronization,
reconciliation planning, and transaction coordination; splitting them further during SB05 would add
indirection without changing ownership. Reopen if one of those files takes on a second owner or a
second transport/reconciliation mode.

The graph confirms the required direction: Workspace depends on neutral ports, Http depends only on
Abstractions, and Composition remains the concrete wiring boundary.
