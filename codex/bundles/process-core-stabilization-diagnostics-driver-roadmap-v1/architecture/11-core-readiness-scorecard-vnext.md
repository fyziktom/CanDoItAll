# Core Readiness Scorecard vNext

## Decision
Recommendation: proceed to next narrow Core expansion before any production driver-contract implementation.

## Scorecard

| Area | Status | Evidence | Next action |
| --- | --- | --- | --- |
| Core public API stability | Ready for narrow growth | `bundle://architecture/04-core-public-api-inventory.md`; `Process_core_public_api_surface_is_explicitly_guarded` | Keep API snapshot required for every public Core addition. |
| Core dependency cleanliness | Ready | `bundle://proof/SB024/manifest.md`; `bundle://proof/SB033/manifest.md` | Keep Core limited to contracts plus pure read models/rules. |
| Module Core consumers | Ready with exact allow-list | `bundle://architecture/05-core-consumer-allowed-call-site-map.md` | Add consumers only through explicit map updates and architecture tests. |
| Diagnostics/read models | Ready for next narrow family | `bundle://proof/SB009/manifest.md`; `bundle://proof/SB018/manifest.md`; `bundle://proof/SB021/manifest.md` | Candidate: execution/finalizer evidence descriptors that stay pure and adapter-owned. |
| Runtime side effects | Not Core-ready | `bundle://proof/SB024/semantic-invariants.md`; `bundle://proof/SB033/semantic-invariants.md` | Keep EF, workspace/storage, AgentFramework execution, transition execution, and finalizer application module-local. |
| Driver contract readiness | Proposal-ready only | `bundle://architecture/06-driver-contract-proposal.md`; `bundle://architecture/12-driver-contract-implementation-decision.md` | Do not implement production drivers until permission enforcement, auditing, and runtime ownership are designed in a separate bundle. |

## Exact Next Candidate
The next bundle should extract another narrow, deterministic Core read-model/rule family around execution/finalizer evidence descriptors only if it can satisfy the existing allow-list and dependency scans. It must not move execution, persistence, storage, claim lifecycle, transition execution, or finalizer application into Core.

## Explicit Non-Approval
This scorecard does not approve a production helper-driver project, runtime dispatcher, DI registration, manager command, shell execution driver, Office/Graph connector, or execution-capable helper.

