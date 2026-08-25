# SB02 project references before implementation

CodeAnalytics snapshot `snap-20260824213007-c65710b4` reports 12 scoped projects and 24 direct
production references. The complete table is inherited from the governed SB01 after artifact;
the shared-provider-relevant subset is:

| From | To | State before SB02 |
| --- | --- | --- |
| `CanDoItAll.Web` | `CanDoItAll.SharedProviders.Abstractions` | present, SB01-owned |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.SharedProviders.Abstractions` | absent |
| `CanDoItAll.SharedProviders.Abstractions` | any product project | none |
| inner AgentFramework projects | Workspace/Web/SharedProviders.Http | none |

The unchanged 23 pre-SB01 product edges are enumerated in
`bundle://subbundles/SB01-protocol-identities-and-access-context/proof/architecture/project-references-after.md`.
SB02's expected after graph is 12 projects and 25 direct production references with exactly one
new edge from Workspace to Abstractions. Unit/Integration test-project references are test-only
and are not counted in the product graph.
