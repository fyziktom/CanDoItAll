# SB06 CodeAnalytics after implementation

State: `PASS`.

The final force-refreshed snapshot is `snap-20260825100508-300644c7`. It was built from
`CanDoItAll.slnx` with the same 14-product-project scope as the SB06 entry snapshot, with DI,
persistence, and risk collection enabled.

| Fact | Before | After |
| --- | ---: | ---: |
| Scoped product projects | 14 | 14 |
| Scoped source documents | 758 | 766 |
| Modules | 35 | 35 |
| Dependency facts | 5,231 | 5,281 |
| Direct product `ProjectReference` edges | 34 | 34 |
| Project-level cycles | 0 | 0 |
| Existing other cycles | 2 module, 1 nested type | unchanged |
| Error findings | 0 | 0 |

A `SharedProvider`-focused findings query reports 14 warnings and 50 informational findings,
zero errors, and zero open questions. The warnings are existing size/complexity heuristics or
reflect the deliberately explicit runtime boundaries. SB06 keeps materialization in the outer
Workspace/AgentFramework adapter, keeps network selection in Composition, and reuses the existing
OpenAI/MAF runtime. No warning identifies a reverse project dependency or a second provider runtime.

The final independent architecture re-audit first found and blocked source-managed audio egress.
The repaired tree now uses a typed source-credential audio policy at both OpenAI driver operations,
filters those profiles from voice settings, and keeps an explicitly ineligible persisted selection
empty instead of rebinding it to a personal provider. The re-audit then returned `PASS` with no
remaining P1/P2 blocker.

The refreshed graph confirms that inner AgentFramework Models, Providers, and MAF projects have no
Workspace, SharedProviders.Http, Web, or UI implementation edge. Composition remains the concrete
HTTP-client and access-context wiring boundary.
