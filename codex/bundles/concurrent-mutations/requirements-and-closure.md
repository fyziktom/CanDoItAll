# Requirements and closure ledger

Work resumed on 2026-10-18; see the [resumption record](validation/2026-10-18-resumption.md).
C4's historical successor passed 706/706. The additional native chat browser proof is
now compiled and passes its one case with three submissions; prior failures remain
recorded. End-to-end implementation continues. SPA work must remain on
`codex/concurrent-mutations` pending the user's consultation with its maintainer.

The [Agent policy receipt backend](validation/2026-10-19-agent-policy-receipts.md)
passes 40/40 cases: atomic receipt publication, historical lookup after lost acknowledgement,
bounded retention and actual HTTP permission/strict-input proof. The repaired lock set
preserves unrelated Agent progress. [Native retained submission recovery](validation/2026-10-19-agent-policy-native-recovery.md)
has 39 passing selected behaviors, with an unchanged-production bridge over two test
repairs. The [deleted-Agent recovery checkpoint](validation/2026-10-19-agent-policy-deleted-recovery.md)
adds native orphaned-ticket entry and actual owned-host restart, with 58 distinct selected
passing behaviors across exact repair bridges. This does not close all of R5 or the
Process/Workflow runtime requirements.

The [versioned execution admission checkpoint](validation/2026-10-19-process-execution-contract.md)
adds 114 selected passing behaviours across its exact test-only repair bridge. Contract
identity/version/owner are immutable launch facts; declaration alone does not activate
governed execution or close R5. Shared inherited material and actual child-owner wiring
remain open.

The [immutable material checkpoint](validation/2026-10-19-process-inherited-material-value.md)
passes 44 selected cases for original scope/content integrity and capture age. It is a
pure prerequisite. [Canonical captured-material storage](validation/2026-10-19-process-material-persistence.md)
now has 237 distinct selected passing behaviours: atomic storage/runtime/accounting,
strict versioned admission, original receipt recovery and migration/downgrade proof.
The active shared parent, genuine run budget/funding, neutral content schema for mixed
executors and actual runtime wiring remain open. This does not close R5 or R6.

The [Process admission foundation](validation/2026-10-19-process-policy-admission.md)
passes 68/68 cases. It adds immutable bounded policy provenance, legacy launch/hash
compatibility, repair rejection and PostgreSQL migration/reload proof. The real launch
producer, governed Process/Workflow execution, generation fences and durable resume
remain open; no settings activation or complete R5 closure is claimed.
The [authority checkpoint](validation/2026-10-19-process-policy-authority.md) now has
58 distinct passing selected behaviors, closing unsupported-policy dispatch and
continuation at the persistence boundary without denying historical reads. It does
not close the governed-execution or generation-lifecycle requirements.
The [pure budget foundation](validation/2026-10-19-process-decision-budget.md) adds 28
passing Unit cases; it does not yet provide durable concurrent allocation or close R5.
The [generation lifecycle foundation](validation/2026-10-19-process-decision-generations.md)
adds 29 passing isolated cases for immutable children, quiescence and bounded successors.
The [atomic generation owner](validation/2026-10-19-process-generation-persistence.md)
now passes 101/101 selected cases. Generation/child records, accounting and runtime state
commit together with original receipts. Actual child journals, effect-generation fencing,
producer settings, inherited basis and Process/Workflow continuation remain open.

The [child publication condition](validation/2026-10-19-process-child-publication.md)
passes 108/108 selected cases for deterministic occurrence identity, indexed canonical
lookup and the enlisted root/generation check. Its effect/quiescence races pass, but
actual child journal, source/manager authority and executor integration remain required
before activation; R5 stays open.

The [external Agent checkpoint](validation/2026-10-19-external-agent-preconditions.md)
passes 54/54 focused cases and closes its cached-binding version and delayed-input
defects. It preserves historical replay, pins postcommit projection to original context
and excludes execution journals from catalog-only dependencies. Full writer coverage,
retention/restore, Process/Workflow activation and capacity gates remain open.

The [P03 lifecycle integration](validation/2026-10-18-lifecycle-integration.md) adds
owner/migration, retained native/API operations, creation cleanup and governed hierarchy
with scoped passing proof. Six actual restart cases recover original Agent/Process
receipts without resuming the old execution. Remaining producer/compensation recovery,
claim fencing and capacity gates are explicit; this does not close R1 or R6 engine-wide.

| ID | Requirement | Owner / proof destination | Current closure |
| --- | --- | --- | --- |
| R1 | Prevent concurrent lost edits across human, agent and workflow writers | Owner protocol, enabled writer inventory, SQL/API/tool/browser races; P02/P05/P07 | Partially implemented: Projects original-state and durable-operation/native recovery foundation is committed through `702d0a93`, with focused proof. Workspace W1b and Resource R1 have scoped owner/native/API proof; Resource passed 346 ordinary and four later browser cases under the exact reviewed historical bridge. Complete Projects producer cutover, remaining owners and durable recovery obligations remain open. |
| R2 | Complete architecture/risk analysis before implementation | Maintained architecture, independent reviews, P00 gates | Architecture direction and P01a passed independent review; later owner/runtime implementation gates remain explicit |
| R3 | Preserve module ownership and dependency direction | Boundary/pattern map, compiled boundary guards, evaluated project graph | In progress: lifecycle in Infrastructure, owner receipts in Projects, Web/Workbench adapters at existing boundaries; no Infrastructure-to-Projects dependency. Final integrated guards remain open. |
| R4 | Identify and address performance bottlenecks | Performance plan, baseline/candidate matched measurements | Projects encoding reuse reduced large-definition allocation 7.61% with mixed latency. Journal wire comparison passed 161 correctness cases but only 11/22 performance gates; whole-journal cost remains open. The later [selected publication checkpoint](validation/2026-10-19-selected-publication-cost.md) passes 72 cases, including all six unrelated writers below one second during a real SQL wait. The repaired finite Resource baseline passes 126 batches/2,178 calls and exposes same-project contention. Native planning first review takes about 6.1 seconds including admission/provider/persistence/render. Earlier failed measurements remain retained. Sustained capacity, fairness, maximum-size journal cost and receipt-cleanup qualification remain release gates. |
| R5 | Configurable snapshot/refresh/replan behavior for agents/processes | Policy design, persisted settings, admission/checkpoint/runtime/UI proof; P04/P07 | Partially implemented. Workspace/Agent policy owners and Workspace UI/API are integrated. The real Agent project-task planning path captures persisted policy, records bounded work and generations, fences publication, pauses stale plans and supports explicit refresh/replan, cancellation and reviewed publication through native/API actions. [Native browser proof](validation/2026-10-19-native-planning-review.md) includes another actor, delayed refresh, exact-generation publication and preserved typing. The [Agent policy editor/API](validation/2026-10-19-agent-policy-access.md) has focused 63-case proof plus six final visual cases. [Native retained policy recovery](validation/2026-10-19-agent-policy-native-recovery.md) covers existing-Agent browser reload, two tabs and historical acceptance. [Deleted-Agent recovery and actual host restart](validation/2026-10-19-agent-policy-deleted-recovery.md) now have native proof. Process/Workflow policy inheritance/enforcement, nested basis/claim fencing and remaining owner integrations remain open. |
| R6 | Test complete intended behavior and failure modes | Testability plan, exact discovery/results, portability, browser/host/cleanup proof | Broad Stable remains incomplete: 16,199 executed, 17 failed, 2,444 unexecuted. [Historical correctness](validation/P02b-R1c-historical-correctness-checkpoint.md) retains 133 distinct ordinary rows, both restart Facts and Send browser. [C3](validation/P04-C3-focused-runtime-checkpoint.md) passed 190 application cases, with incomplete independent nine-child observation. D1 retains its first 237-pass/ten-fixture-failure record and the complete [267-case successor](validation/R1d-D1-first-runtime-and-fixture-repair.md), including its periodic-observer limitation. [Resource](validation/P05-R1-resource-catalog-cast-runtime-checkpoint.md) passed 346 ordinary cases, then all four browser Facts with exact historical source/runtime bridge and parent-reviewed closure; the ordinary set was not rerun on that browser candidate. Sandbox auxiliary-Web profile capture and Data Sources external second-profile capture remain incomplete; the latter has actual in-band after-restart HTTP evidence. Earlier failed candidates and cleanup blockers remain explicit. No complete allocation-ledger, broad-suite or end-to-end closure is claimed. |
| R7 | Incremental commits and current documentation | Execution record, scoped commits and maintained design | Signed backend checkpoints include `6123cdc7a` retained native chat, `e5c2d3506` lifecycle/reviewed planning and `59b6f1dd4` authorized planning review/actions. The native planning checkpoint is signed as `bd89b5254`; the subsequent Agent policy checkpoint includes current architecture and proof updates. SPA transport remains signed at `70c65dd` on `codex/concurrent-mutations`; no SPA main merge or push is authorized. |

## Architect input coverage

The [41-case Process accounting checkpoint](validation/2026-10-19-process-budget-persistence.md)
adds durable local step balances, allocations and original-command receipts to R5/R6.
They commit atomically with runtime state and survive rollback/restart tests. Nested
owner settlement, generation fencing and actual Process/Workflow execution remain open;
this checkpoint does not close R5 or the full end-to-end test requirement.

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

Historical runtime evidence: [C4 failures and repaired Resource baseline](validation/P04-C4-first-runtime-and-capacity-calibration.md).
The original703-row C4 attempt remains213 passed/one failed/489 unexecuted. Repaired
C4 discovered704 and executed383:376 passed, seven deletion projection failures,
321 unexecuted. Reservation admission now passes; deletion normalization and legacy
audit writers still require safe cutover. The repaired Resource finite baseline passed
126 batches/2,178 matrix calls plus17 controls and scoped resource review. It exposes
same-project contention; shared-lock implementation and sustained qualification remain
R4/R6 gates. Freeze release `C5F0742B` permits successor edits only. C4 shared TEMP
observation gaps and earlier cleanup blockers remain explicit.

The earlier external-Agent target-version gap is closed by the
[54-case checkpoint](validation/2026-10-19-external-agent-preconditions.md): updates use
original workspace/profile scope, Agent identity and actual configuration version,
freeze the submission before awaiting and preserve historical receipts. R5's Agent
policy owner, selected project-task planning runtime, editor/API and existing-Agent
browser recovery have focused proof. Deleted-Agent recovery entry and owned-host
restart now have the linked checkpoint proof; Process/Workflow policy propagation
remains open. Matching
policy contents after an unknown write cannot substitute for an original operation receipt.

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
