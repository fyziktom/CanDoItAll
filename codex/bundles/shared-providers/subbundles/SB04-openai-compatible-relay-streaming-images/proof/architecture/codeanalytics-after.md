# SB04 CodeAnalytics after implementation

State: `PASS`

| Fact | Value |
| --- | --- |
| Snapshot | `snap-20260825051057-300644c7` |
| Scoped product projects | 14 |
| Scoped source documents | 752 |
| Modules | 35 |
| Dependency edges | 5,158 |
| Direct product `ProjectReference` edges | 34 |
| Project-level cycles | 0 |
| Other reported cycles | 2 module-level, 1 nested-type |
| Error findings | 0 |

Relative to the SB04 before snapshot `snap-20260825012213-a17e36ed`, the scoped product count and
module count are unchanged. The source-document count rises from 736 to 752, dependency edges
from 4,954 to 5,158, and direct product references from 33 to 34. The single reference delta is
the authorized outer `CanDoItAll.Modules.AgentFramework ->
CanDoItAll.SharedProviders.Abstractions` edge required by the neutral image/usage contracts.

There is no project cycle and no error finding. The same two pre-existing module cycles and one
pre-existing nested-type cycle remain; SB04 neither created nor expanded them. Source inspection
also confirms that Abstractions has no outgoing product reference, Http still points only to
Abstractions, Workspace does not point to Http, and inner MAF/provider projects gained no outer
dependency.

This force refresh follows the SB04 semantic repairs. Relative to the superseded interim refresh,
it contains one additional scoped source document and five additional dependency facts while
preserving the 14-project scope, 35 modules, 34 direct product references, the same cycle set, and
zero error findings.
