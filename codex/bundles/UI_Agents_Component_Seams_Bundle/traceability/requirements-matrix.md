# Requirement and review traceability

| Requirements | Primary owner | Acceptance / architecture | Proof |
|---|---|---|---|
| R-001–R-005 | SB01/SB07 | Authority, shared v2, baseline, source mode and sequential contracts | Source/input records and final manifests |
| R-010–R-013 | All | Scope/invariants, placement and current route/UI compatibility | Diff/graph review, route/browser tests |
| R-020–R-022 | SB02/SB03 | Workspace state, lazy queries and context; B01–B08 | Route/history/context/page/catalog evidence |
| R-023–R-026 | SB04/SB05 | Session/section/host contract; B09–B12/B16/B19/B20 | Real section/lifetime/delayed/instance cases |
| R-030–R-033 | SB02/SB03 | Query/catalog/host ownership; PSR-02/03 | Real operation/DI and public host behavior |
| R-034–R-039 | SB04/SB05/SB07 | Editor operations, minimal ports, subtree/types; PSR-04–08 | Constructor/adapter/child/graph evidence |
| R-040–R-042 | SB03/SB05 | Request/selection/identity/team/result semantics; B06–B08/B15 | Real host/operation interactions |
| R-043–R-046 | SB04/SB05 | Ten sections, settings, version, commit/refresh; B09–B26 | Existing and new behavior/policy/operation cases |
| R-050–R-055 | SB01–SB06 | Exact behavior coverage and progressive public harnesses | Discovery, replacement map and hygiene |
| R-056–R-057 | SB07 | Focused/stable/portability/browser and six readiness dimensions | Governed artifact manifest and gate review |
| R-058–R-059 | SB01/SB03/SB07 | Measured independent sandbox handoff and P/S/U classification; B30 | Timing baseline, concrete graph/scenario plan |

| Accepted finding | Revised contract | Owning phases |
|---|---|---|
| F01 | Independent sandbox path and measured iteration | SB01/SB03/SB07 |
| F02 | Full rendered subtree/service/assets inventory | SB01/SB04/SB05/SB07 |
| F03 | Public DTO assembly audit and justified projections | SB01/SB05/SB07 |
| F04 | Explicit current/future dialog host lifetime | SB04/SB07 |
| F05 | Selection/target/session/draft/version and async ownership | SB03–SB05 |
| F06 | Minimal cohesive real boundaries without quotas | SB02–SB05 |
| F07 | Lazy regions and chat context preservation | SB02/SB03 |
| F08 | Behavioral coverage plus actual production composition | All |
| F09 | Meeting pack remains proposal, URL decisions open | SB01/SB07 and separate navigation child |

Runtime proof must extend this table with actual test/artifact paths. Preparation traceability is not proof of implementation.
