# Requirements and closure ledger

Current capture ownership evidence: [127 distinct passing behaviors](validation/2026-10-20-process-capture-ownership.md).
Original Applied receipts cannot grant another capture invocation; the new exact claim
precondition protects StepSnapshot attachment. Shared RunSnapshot preparation retains
its existing root owner and must precede dispatch. This closes the commit/claim
prerequisite only. Registered producer capture, manager actions and full R5 remain open.

Current Process result/accounting evidence: [369 distinct passing behaviors](validation/2026-10-20-process-result-accounting.md),
including 111 final cases and 258 unaffected cases under an exact repair comparison.
Normal dispatch and interrupted recovery share original-child reads, stable result
identity, atomic runtime/accounting acceptance and retained unknown work. This closes
the result-submission prerequisite; R5 and end-to-end activation remain open for actual
producer capture, manager actions, Workflow and settings. Full portability passes;
sustained capacity and the historical incomplete broad Stable run remain open.

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
The [shared-content checkpoint](validation/2026-10-19-process-shared-content.md) passes
265/265 selected cases for mixed-executor reuse and exact historical compatibility.
The [root funding checkpoint](validation/2026-10-19-process-run-budget-funding.md) now passes
361/361 selected cases, implementing genuine root accounting and atomic descendant funding.
The [active shared scope](validation/2026-10-19-process-shared-run-scope.md) now passes
68 focused repair cases, including all failures from the retained initial 397/414 run.
Atomic capture, original inherited membership and parent publication fencing are implemented.
The [inherited Agent checkpoint](validation/2026-10-19-inherited-agent-planning.md)
has 242 distinct passing behaviors across its test-only repair bridge, including original
material/time/grant, current source denial, restart/unknown work and actual MAF input.
Actual Process/Workflow producers, Workbench publication and manager continuation remain
open. These prerequisites do not close R5 or R6. Grant sizing/fair scheduling and workload
capacity still require proof.

The [exact inherited admission checkpoint](validation/2026-10-19-exact-inherited-admission.md)
adds 73 distinct passing selected behaviors across an exact test-only bridge. Stable child
lookup precedes preparation; initial-save recovery, source reads, original metadata and
unknown work are preserved. This is an admission prerequisite; actual producer and
publication integration remain open.

The [Process admission foundation](validation/2026-10-19-process-policy-admission.md)
is complemented by the [shared inherited planning coordinator](validation/2026-10-19-inherited-planning-coordinator.md),
which passes all 170 selected cases after repairing the SDK options construction boundary.
Both origins share provider iterations and usage accounting; inherited project history,
original material and duplicate-start ownership have actual transport proof. Canonical
Process producer/parent, publication and manager integration remain open.

The Process admission foundation
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

The [canonical child/material reader](validation/2026-10-19-canonical-child-material.md)
adds 169 passing selected cases across an exact test-only repair. It exposes canonical
originals through the Process Application boundary with three bounded SQL reads. The
[actual inherited workspace lifecycle](validation/2026-10-19-inherited-workspace-lifecycle.md)
adds 185 passing selected cases, including credentials, history, usage, competing callers
and retained execution reads. Producer activation, inherited Workbench publication and
manager paths remain open under R5/R6; historical read is not dispatch authority.


The [Process publication claim-lifetime repair](validation/2026-10-19-publication-claim-lifetime.md)
passes all 106 selected cases. Dispatch now requires the exact active claim and attempt
with unexpired lifetime after the root lock; historical read and manager review remain
separate. Concrete producer/publication integration and R5/R6 closure remain open.

The [asynchronous child binding](validation/2026-10-19-asynchronous-child-binding.md) passes all 192 selected cases. It enables original database target/context reads inside exact new-only admission, with cancellation and current source checks after the await. It does not close R5/R6 or activate Process/Workflow producers.

The [selected Process dispatch query](validation/2026-10-19-selected-child-dispatch.md) passes 86 cases. It binds original child/grant/policy/material and exact current claim without loading the material payload. Producer routing, inherited business effects and manager continuation remain open.

The [inherited publication snapshot](validation/2026-10-20-inherited-publication-snapshot.md) passes 238 corrected cases, including two-Agent ordering, cancellation, current revocation and the selected SQL-wait reference. The initial fixture failures and coarse lease responsiveness failure are retained. R5/R6 remain open: resolve the initial Process Project/root lock inversion before adding the business fence and actual continuation.

## Process initial lock ordering, 2026-10-20

The [lock-order checkpoint](validation/2026-10-20-process-project-lock-order.md) closes the observed Root-to-Project advisory inversion for initial Process commits, including subprocesses and original executable/source admissions. It has 194 distinct passing selected behaviors across an exact two-test-file repair bridge. Scoped replay retains its original receipt after Project retirement through configured lock-only admission. This is a publication prerequisite; R5/R6 and actual inherited producer/publication/manager paths remain open. Root semaphore retention and capacity/fairness remain review items.

The [selected publication fence](validation/2026-10-20-selected-publication-fence.md) adds the neutral selected query port and enlisted complete-source/material/claim validation, with 126 distinct passing cases and unchanged production across its fixture repair. It preserves legacy refusal and separates dispatch from manager review. R5/R6 remain open until actual Workbench publication, Core/MAF continuation, producers, manager authority and atomic parent outcomes are integrated.

The [enlisted task publication checkpoint](validation/2026-10-20-enlisted-task-publication.md) passes all 72 correctness cases, including real Process expiry before staging and after flush. Its one-second selected-guard performance reference failed for two writers (1006.85 and 1066.32 ms); current responsiveness is reopened and is not a green gate. Inherited producer/continuation and wider capacity remain open.

The [workspace path-probe repair](validation/2026-10-20-workspace-path-probe.md) passes all 75 selected cases. Exact internal path comparison removes repeated filesystem case-probe writes while preserving fresh containment/link validation and durable journals. The identical selected SQL-wait reference now has all six writers below one second (162.67–670.72 ms). Earlier negative evidence is retained; sustained capacity and inherited runtime activation remain open.

The [live-parent saved-effect continuation checkpoint](validation/2026-10-20-parent-saved-effect-continuation.md) passes all
201 selected cases on one frozen candidate. Core retains exact original parent authority,
receipt/claim ownership and finite work; MAF accepts an explicit inherited context/request
through its existing governed Process policy pipeline. Both earlier failed candidates
remain recorded. Five direct Release builds and full portability enforcement pass. Actual
Workbench/Process producer, workspace approval lifecycle, manager/historical recovery,
Workflow and capacity remain open; this does not activate inherited product publication.

The [inherited task receipt identity checkpoint](validation/2026-10-20-inherited-task-receipt-identity.md) passes all 75 selected
cases. Explicit Process-Agent caller/source identity preserves ordinary receipt bytes and
partitions; kind 2 requires active work and matching origin. The two-check migration
preserves rows and refuses downgrade with inherited evidence. Actual inherited Workbench
adapter and Process producer remain the next integration; no product activation is claimed.

The [inherited task publication codec](validation/2026-10-20-inherited-task-publication-codec.md)
passes all 124 selected cases. Original parent material stays shared; child-specific write
contexts, strict origin decoding and receipt semantics remain distinct. Two failed wire
candidates are retained and the full selection was repeated. Current source/executor
authority, actual inherited publication/producer and workspace approval remain next.

The [exact original Process source checkpoint](validation/2026-10-20-selected-process-source-authority.md)
passes all 23 selected cases. Source checks select the original launcher from the held
two-Agent catalog without borrowing executor grants or acquiring another lease. The
earlier 22/23 result and exact test-only deletion-intent repair are retained. Current
executor authority, actual inherited publication and producer/approval integration remain next.

The [inherited task publication authority](validation/2026-10-20-inherited-task-publication-authority.md) passes all 67 selected integration cases,
including 28 new cases. It binds original parent/live work, exact selected source/executor,
current profile generation and the enlisted Process fence through actual SQL publication.
The earlier 62/64 fixture result is retained. Historical receipt authorization, producer,
manager/approval activation and atomic Process outcomes remain open.

The [original inherited Process source read](validation/2026-10-20-inherited-original-source-history.md) has 107 distinct passing cases across
an exact one-test-file repair. It preserves accepted source fingerprints after assignment
repair, expiry, closure and supersession while always denying dispatch. Four exact indexed
reads replace reliance on current assignment; the earlier 100/107 result is retained.
Product schema 7 receipt integration, producer/manager and atomic outcomes remain next.

The [inherited task receipt history](validation/2026-10-20-inherited-task-receipt-history.md) passes all 64 selected cases, including 20 new cases. Registered schema 7 history retains original receipts after expiry/repair/closure, applies current disclosure fences and leaves workspace files unchanged. All 28 publication regressions pass with actual Process metadata. Exact SQL/catalog bounds, source/runtime identity, full portability and observed resource closure pass. Producer/provider activation, manager approval and atomic parent outcomes remain open.

The [root sequence registry repair](validation/2026-10-20-root-sequence-registry.md) passes all 26 selected cases. Completed roots no longer retain semaphores, cancellation preserves waiting callers and disposal is concurrent-idempotent. The first single-mutex cost regression and all baseline/candidate measurements remain recorded; bounded registry partitions restore similar observed independent-root cost while retaining zero idle entries. Full portability and source/runtime checks pass. Probe output cleanup remains explicitly blocked by automatic approval review; sustained capacity is still open.

The [explicit initial inherited source admission](validation/2026-10-20-inherited-initial-source.md) passes all 247 selected cases, including 16 new cases. Core admits the exact parent through an explicit owner observation and constructs schema 7 directly from its authorized session; ordinary source support does not imply inherited support. The initial 38/244 candidate remains retained. Full portability, architecture, source/runtime identity and observed resource checks pass. Registered Process source integration and complete parent-policy comparison are next; producer/manager/Workflow and capacity remain open.

The [registered inherited Process source](validation/2026-10-20-inherited-product-source.md) has 153 distinct passing cases across an exact one-test-file repair. Initial and schema 7 observations use the registered owner and compare the complete original accepted policy; current generation, revocation and cancellation remain fenced. Full portability and source/runtime/resource checks pass. The retained performance review reopens whole accepted-launch decoding cost; qualify it before producer activation. Producer/manager, atomic outcomes, Workflow and capacity remain open.

The [accepted-source scaling baseline](validation/2026-10-20-accepted-source-scaling.md) passes all 15 measurement executions but reopens capacity: a 1024-step source read allocates 31 MB and has a 71.075 ms median. The [persistence design](plan/P07-accepted-source-projection.md) separates immutable original-source evidence from live authority checks, with explicit versions and atomic capture. Producer activation remains gated.

The [accepted-source codec foundation](validation/2026-10-20-accepted-source-codec.md) passes 41 executions. Selected source bytes stay about 2.6 KB and allocations about 32 KB across the measured shapes, including 1024 steps and a 1 MiB prompt. The one-step codec timing tradeoff is retained. No runtime source path is switched yet; atomic storage, migration, actual SQL/capture cost and source-principal variants are next.

The accepted-source persistence checkpoint passes 193 distinct cases across an explicit one-test assertion bridge. Atomic capture, migration, corruption/rollback/retry and registered-source/history paths are verified. At 1024 steps the selected SQL source is 2319 bytes with 2.904 ms median versus 4449292 bytes / 137.054 ms legacy; initial acceptance still takes 2593 ms, so sustained capacity remains open. Actual producer/manager integration, Workflow and legacy conversion remain gated. [Candidate validation](validation/2026-10-20-accepted-source-persistence.md).

The [original Process child outcome reader](validation/2026-10-20-process-owner-outcome.md) has 129 distinct passing behaviors across explicit production and test-only correction boundaries. The reader preserves original work/effects after revocation, assignment repair and claim expiry, without publication locks or file mutation. Full portability, direct Release builds, architecture and source/runtime/resource checks pass. Failed candidates remain retained; two intermediate-directory cleanup attempts were rejected by automatic approval review. Actual result submission, producer/manager activation, Workflow, settings/SPA and sustained capacity remain open. SPA stays on codex/concurrent-mutations.
