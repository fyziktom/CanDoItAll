# Requirements and closure ledger

Current stop: [2026-10-17 checkpoint and continuation](validation/2026-10-17-checkpoint-handoff.md).
The user requested closure to conserve credits. C4's final successor passed 706/706;
the additional browser proof failed twice and remains outside the compiled checkpoint.
End-to-end completion and full resource closure are not claimed.

| ID | Requirement | Owner / proof destination | Current closure |
| --- | --- | --- | --- |
| R1 | Prevent concurrent lost edits across human, agent and workflow writers | Owner protocol, enabled writer inventory, SQL/API/tool/browser races; P02/P05/P07 | Partially implemented: Projects original-state and durable-operation/native recovery foundation is committed through `702d0a93`, with focused proof. Workspace W1b and Resource R1 have scoped owner/native/API proof; Resource passed 346 ordinary and four later browser cases under the exact reviewed historical bridge. Complete Projects producer cutover, remaining owners and durable recovery obligations remain open. |
| R2 | Complete architecture/risk analysis before implementation | Maintained architecture, independent reviews, P00 gates | Architecture direction and P01a passed independent review; later owner/runtime implementation gates remain explicit |
| R3 | Preserve module ownership and dependency direction | Boundary/pattern map, compiled boundary guards, evaluated project graph | In progress: lifecycle in Infrastructure, owner receipts in Projects, Web/Workbench adapters at existing boundaries; no Infrastructure-to-Projects dependency. Final integrated guards remain open. |
| R4 | Identify and address performance bottlenecks | Performance plan, baseline/candidate matched measurements | Projects encoding reuse reduced large-definition allocation 7.61% with mixed latency. Journal wire comparison passed 161 correctness cases but only 11/22 performance gates; whole-journal cost remains open. C3 recorded 54 samples / 282 attempts with expected outcomes; basis-900 append-one allocated about 19.3 MB. See [C3 characterization and measurement limits](validation/P04-C3-focused-runtime-checkpoint.md). The C4 probe completed its effect/guard/cleanup checks but failed the one-second responsiveness reference; the first narrow-owner run stopped at a real reservation race and the Resource capacity run failed calibration before any matrix batch. Both failures are retained; reviewed repairs require fresh execution. Sustained capacity, fairness and receipt-cleanup qualification remain release gates. |
| R5 | Configurable snapshot/refresh/replan behavior for agents/processes | Policy design, persisted settings, admission/checkpoint/runtime/UI proof; P04/P07 | Partially implemented. Workspace/Agent policy owners and Workspace UI/API are integrated with migration, component, HTTP and four Workspace browser passes; see P07-policy-owner-integration.md. Actual Agent/Process/Workflow policy enforcement remains unimplemented. Durable admission/checkpoint/replan/approval and immutable child-policy propagation proof remain required. |
| R6 | Test complete intended behavior and failure modes | Testability plan, exact discovery/results, portability, browser/host/cleanup proof | Broad Stable remains incomplete: 16,199 executed, 17 failed, 2,444 unexecuted. [Historical correctness](validation/P02b-R1c-historical-correctness-checkpoint.md) retains 133 distinct ordinary rows, both restart Facts and Send browser. [C3](validation/P04-C3-focused-runtime-checkpoint.md) passed 190 application cases, with incomplete independent nine-child observation. D1 retains its first 237-pass/ten-fixture-failure record and the complete [267-case successor](validation/R1d-D1-first-runtime-and-fixture-repair.md), including its periodic-observer limitation. [Resource](validation/P05-R1-resource-catalog-cast-runtime-checkpoint.md) passed 346 ordinary cases, then all four browser Facts with exact historical source/runtime bridge and parent-reviewed closure; the ordinary set was not rerun on that browser candidate. Sandbox auxiliary-Web profile capture and Data Sources external second-profile capture remain incomplete; the latter has actual in-band after-restart HTTP evidence. Earlier failed candidates and cleanup blockers remain explicit. No complete allocation-ledger, broad-suite or end-to-end closure is claimed. |
| R7 | Incremental commits and current documentation | Execution record, scoped commits and maintained design | Architecture `ceb10731e`, cleanup `efbebfc54`, Projects original-state/editor `1452352f0`, validation/capacity `0bdb050c`, durable operations/browser recovery `702d0a93`. Reviewed continuation source is integrated for the next checkpoint. The SPA transport checkpoint is signed as `70c65dd`; signing remains enabled for MAIN checkpoint closure. |

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
| 10 projection/storage/effects | Owner/performance design, P02/P06 | Projects search is enlisted with the accepted definition/receipt; installed external-adapter matrix source-reviewed, with owner-specific implementation and qualification pending |
| 11 performance/capacity | Performance/risk plan, each candidate measurement | No historical numbers accepted as measured |
| 12-15 foundations/phases/pilot/lifecycle | Execution plan, owner design, P01/P02/P08 | Source refreshed; lifecycle/retention cutover gates remain |
| 16-18 tests/decoupling/operations | Testability, dependency map, performance plan | Planned proof only |
| 19 decisions/questions | Decisions and readiness gates in current design/reviews | Proposed defaults assessed; unresolved gates explicit |
| 20 source ledger | Current-state inventory and source commit | Historical source anchors superseded only by recorded current evidence |
| 21 test catalogue | Testability scenario mapping and phase proof | Planned scenarios; no false discovered test counts |
| 22 risk register | Performance/risk and owner/policy/editor design | Risk mitigations specified; execution proof pending |
| P00-P08 phase cards/registers | Current execution plan, retaining dependency intent | No historical phase marked implemented |

End-to-end closure requires all mandatory rows solved with evidence. The original request
still includes every enabled native, API, SPA, agent and workflow writer. Focused owner
or browser proof does not reduce that scope.

Latest runtime evidence: [C4 failures and repaired Resource baseline](validation/P04-C4-first-runtime-and-capacity-calibration.md).
The original703-row C4 attempt remains213 passed/one failed/489 unexecuted. Repaired
C4 discovered704 and executed383:376 passed, seven deletion projection failures,
321 unexecuted. Reservation admission now passes; deletion normalization and legacy
audit writers still require safe cutover. The repaired Resource finite baseline passed
126 batches/2,178 matrix calls plus17 controls and scoped resource review. It exposes
same-project contention; shared-lock implementation and sustained qualification remain
R4/R6 gates. Freeze release `C5F0742B` permits successor edits only. C4 shared TEMP
observation gaps and earlier cleanup blockers remain explicit.

R1 also retains a concrete external-Agent target-version gap: external binding versions
can remain stale after ordinary or policy edits. The successor must compare the
original workspace/profile scope, Agent identity and actual configuration version,
freeze the submitted editor once before awaiting, and preserve historical replay
receipts. A reserved-policy preservation test does not prove this target CAS. R5's
Agent policy owner and Workflow contract checkpoints remain source-reviewed proposals,
without integrated runtime enforcement or lost-ack recovery proof.

Required continuations include complete Projects producer cutover; P03 original deletion,
hierarchy and durable owner reconciliation; Workspace/Agent/Process/Workflow persisted
settings and actual governed execution; Workflow definition, ordinary component and
global-settings original-state/namespace protection with durable client intent and owner
recovery; remaining owner and external-effect waves; migration/restore compatibility;
matched capacity measurements; and final broad/static/resource validation. Source-reviewed
local proposals remain conditional on integration, migration where applicable, build,
static, native/API/browser and restart proof. The Resource browser release at
2026-10-17T11:20:36.0215536Z permits the reviewed next transfer only; it is not end-to-end,
capacity or activation acceptance. Imported architect inputs remain immutable evidence.
