# SB02 CodeAnalytics after implementation

| Fact | Value |
| --- | --- |
| Snapshot | `snap-20260824231242-d9fc36b9` |
| Solution | `C:\\repositories\\CanDoItAll\\CanDoItAll.slnx` |
| Scoped product projects | 12 |
| Scoped source documents | 703 |
| Modules | 32 |
| Dependency edges | 4,730 |
| Direct product `ProjectReference` edges | 25 |
| Project-level cycles | 0 |
| Other reported cycles | 2 module-level, 1 nested-type |
| Error findings | 0 |

The force-refreshed graph contains exactly the authorized SB02 edge:
`CanDoItAll.Modules.Workspace -> CanDoItAll.SharedProviders.Abstractions`. No product project
references `SharedProviders.Http`, and Abstractions still has no outgoing project or package edge.
The two baseline module cycles and one nested-type cycle are unchanged.

Relevant analyzer warnings were reviewed. The only shared-provider file-level warnings are the
generated EF migration designer and the two SB01 contract files already reviewed at CP-01.
Workspace shared-provider source files receive information-level member-count observations only;
the largest handwritten production file is 217 lines. There is no error-level architecture
finding and no warning that changes the dependency or ownership decision.

