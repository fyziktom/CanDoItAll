# Requirements and closure ledger

| ID | Requirement | Owner / proof destination | Current closure |
| --- | --- | --- | --- |
| R1 | Prevent concurrent lost edits across human, agent and workflow writers | Owner protocol, enabled writer inventory, SQL/API/tool/browser races; P02/P05/P07 | Partially implemented: P02a original-state Projects writes committed; P02b protected receipts and selected producers under validation. Remaining Projects producers and other engine owners are still open. |
| R2 | Complete architecture/risk analysis before implementation | Maintained architecture, independent reviews, P00 gates | Architecture direction and P01a passed independent review; later owner/runtime implementation gates remain explicit |
| R3 | Preserve module ownership and dependency direction | Boundary/pattern map, compiled boundary guards, evaluated project graph | In progress: lifecycle in Infrastructure, owner receipts in Projects, Web/Workbench adapters at existing boundaries; no Infrastructure-to-Projects dependency. Final integrated guards remain open. |
| R4 | Identify and address performance bottlenecks | Performance plan, baseline/candidate matched measurements | Partial measurements retained: shared guards removed the observed disjoint-writer serialization failures. Large-definition tail/allocation and cleanup capacity remain open; the latest replacement workload stopped at a PostgreSQL EXPLAIN JSON parser defect after its uncontended samples. |
| R5 | Configurable snapshot/refresh/replan behavior for agents/processes | Policy design, persisted settings, admission/checkpoint/runtime/UI proof; P04/P07 | Not solved; policy design drafted |
| R6 | Test complete intended behavior and failure modes | Testability plan, exact discovery/results, portability, browser/host/cleanup proof | P01a and P02a focused proof committed; P02b/c owner, HTTP, runtime, transfer and UI lanes in progress. Failures and unrun lanes remain explicit; final static and integrated closure are pending. |
| R7 | Incremental commits and current documentation | Execution record, scoped commits and maintained design | Architecture `ceb10731e`, cleanup `efbebfc54`, Projects original-state/editor checkpoint `1452352f0`. Next coherent protected-operation checkpoint awaits repair validation. |

## Architect input coverage

Preserved package content is proposal evidence. Its stable mutation/test/risk IDs remain
available without rewriting historical statuses. This map covers every narrative and
phase; exact scenario closure is added when each implementation slice runs.

| Input documents | Live design/work destination | Status |
| --- | --- | --- |
| 01-05 recommendation/evidence/invariants/architecture/revisions | Overall design, owner design, current inventory, P00/P02/P05 | Reviewed and updated for current source; implementation pending |
| 06-07 transaction/outcome/idempotency | Owner design, P01/P02 | Reviewed; known cleanup, stale-write and outcome gaps remain |
| 08 UI drafts/conflicts | Editor design/testability, P02 | Reviewed; preserve newer existing draft seam |
| 09 agent bases/approvals/authority | Policy design, P04/P07 | Reviewed; original authority and current basis stay distinct |
| 10 projection/storage/effects | Owner/performance design, P02/P06 | Search gap identified; driver-specific effect review pending |
| 11 performance/capacity | Performance/risk plan, each candidate measurement | No historical numbers accepted as measured |
| 12-15 foundations/phases/pilot/lifecycle | Execution plan, owner design, P01/P02/P08 | Source refreshed; lifecycle/retention cutover gates remain |
| 16-18 tests/decoupling/operations | Testability, dependency map, performance plan | Planned proof only |
| 19 decisions/questions | Decisions and readiness gates in current design/reviews | Proposed defaults assessed; unresolved gates explicit |
| 20 source ledger | Current-state inventory and source commit | Historical source anchors superseded only by recorded current evidence |
| 21 test catalogue | Testability scenario mapping and phase proof | Planned scenarios; no false discovered test counts |
| 22 risk register | Performance/risk and owner/policy/editor design | Risk mitigations specified; execution proof pending |
| P00-P08 phase cards/registers | Current execution plan, retaining dependency intent | No historical phase marked implemented |

End-to-end closure requires all mandatory rows solved with evidence. A green independent
foundation repair does not close Projects or the whole engine.
