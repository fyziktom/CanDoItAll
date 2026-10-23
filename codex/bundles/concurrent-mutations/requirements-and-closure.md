# Requirements and closure ledger

Current work: [Process basis-change failure](validation/2026-10-23-process-basis-change-failure.md)
qualifies Fail and PauseForReview for both snapshot modes, immutable child response grants,
and SPA selection/recovery. All 247 distinct backend cases, eight historical-reader checks,
152 SPA tests and sixteen rendered states pass. Final portability and new resource
reconciliation pass. SPA signed checkpoint **1139939** stays on **codex/concurrent-mutations**.
One interrupted-run directory remains blocked by automatic approval review; historical
cleanup blockers remain. Parent refresh/replan, native/publication UI, broader lifecycle,
authored defaults, capacity, remaining owners and final engine E2E are still open.

Preceding work: [Project Process launch client](validation/2026-10-23-project-process-launch-client.md)
qualifies scoped definition reads, original-intent lookup and the SPA selection/review/
acceptance flow. Nine backend cases, 135 SPA tests and fourteen rendered states pass;
full portability and new resource reconciliation pass. SPA remains on
**codex/concurrent-mutations** for colleague review. Native UI, definition defaults,
broader lifecycle/policies, capacity, remaining owners and final engine E2E remain open.

Preceding work: [Process policy discovery](validation/2026-10-23-process-policy-discovery.md)
qualifies server-owned execution identities and supported policy ceilings, with 56 distinct
backend cases across an exact test/baseline repair and 103 SPA tests. The generated schema
retains all existing contracts; full portability and resource reconciliation pass. SPA
remains on **codex/concurrent-mutations** for colleague review. Process launch UI,
definition defaults, broader policies/lifecycle, capacity, remaining owners and engine E2E
remain open.

Preceding work: [Workspace policy client](validation/2026-10-23-workspace-policy-client.md)
qualifies the SPA policy editor, exact-duration conversion and conditional V2 writes.
The backend schema collision is repaired; all ten HTTP cases, 100 SPA tests, full
portability and ten rendered states pass. Source/output and runtime cleanup checks pass.
SPA stays on **codex/concurrent-mutations** for colleague review. Process launch UI,
broader lifecycle/policies, durable recovery, capacity, remaining owners and engine E2E
remain open.

Preceding work: [public Process policy producer](validation/2026-10-23-process-policy-producer.md)
passes 251 distinct cases across verified test-source repairs, full portability and final
source/output/resource checks. Public RunSnapshot/StepSnapshot selection reaches queued
MAF planning, HTTP review, one task publication and terminal accounting. The SPA client
passes 62 tests and its typecheck/build; signed commit `0fa527a` stays on
**codex/concurrent-mutations** for colleague review. Selection UI, shared refresh/replan,
broader lifecycle, capacity, remaining owners
and final engine E2E remain open.

Preceding work: [chat-session publication and client integration](validation/2026-10-23-chat-session-publication.md)
qualifies conditional title changes, selected session creation and late-run title retention.
There are 365 distinct passing backend cases across the recorded test-only repair, full
portability passes unchanged, and the SPA has 58 passing tests, a full typecheck/build and
five rendered states on its own branch. New bulk-scan cleanup is explicitly blocked by
automatic approval review. Capacity/activation, durable same-source identity, remaining
owners/policies and final E2E remain open. SPA is not merged or pushed to main.

Preceding work: [legacy root cancellation cutover](validation/2026-10-23-legacy-cancellation-cutover.md)
qualifies the Application/HTTP cutover and the real MAF provider-wait repair with all 173
selected cases, 11 Release builds, unchanged source/output evidence and full portability.
Native and Workflow coverage was qualified in P17/P18. Manager-chat concurrency,
capacity/default activation, remaining mutable owners/policies and final E2E remain open.
SPA stays on its dedicated branch for colleague review. No whole-engine closure is claimed.

Preceding [finite root cancellation admission](validation/2026-10-22-cancellation-admission.md)
passes all 61 selected cases, eight Release builds, architecture and full portability.
The 464 source pins and 54 outputs remain unchanged through nine observed test commands;
resource reconciliation finds zero new runtime residue. Concurrent SQL requests preserve
one barrier, and real HTTP admission returns while an Agent child is locked before the
registered worker completes original accounting in both snapshot modes. Native/legacy
client cutover and worker activation remain next. Remaining owners/policies, sustained
capacity and final E2E remain open. SPA stays on its separate branch.

The preceding [bounded observation](validation/2026-10-22-cancellation-observation.md)
qualified selected reads, privacy and progress semantics with all 32 cases. Observation
never advances recovery or equates runtime cancellation with verified cleanup.

Previous implementation: [shared local workspace admission](validation/2026-10-22-local-coordination.md)
qualifies the repair with all 90 focused cases, eight Release builds, source/output
identity, architecture, full portability and zero new observed runtime residue. All six
previously contended recovery cases now pass without relaxing deadlines; 600 writes are
preserved. History-sized publication cost and separate-process fairness remain open.
Next is [cancellation observation and bounded admission](plan/P17-cancellation-observation-and-admission.md).
Worker activation, remaining owners/policies and final E2E remain open. SPA stays on its
separate branch. Earlier cleanup denials are retained; no whole-bundle cleanup is claimed.

The [initial contention failures](validation/2026-10-22-recovery-contention.md)
and diagnostic remain preserved as the repair's before evidence.

Previous implementation checkpoint: [selection publication reuse](validation/2026-10-22-process-selection-reuse.md).
All 82 focused cases, four actual historical-binary handoffs and full portability pass.
Three alternating cost pairs reduce median throughput loss to 3.35%; original thresholds
pass and 120 repeated JSON publications disappear. Four real-owner contention cases also
preserve every revision, but 32-writer p95 still reaches 15.06 seconds. Approved separate
cleanup removed both temporary probe sources and Release was refreshed. Automatic review
still blocks the two handoff folders and baseline junction; their ownership is retained.
Worker activation, contended capacity, remaining writers/policies and final E2E stay open.
SPA remains on its dedicated branch.

Preceding cost checkpoint: [controlled index investigation](validation/2026-10-22-process-index-controlled-cost.md).
Six alternating native cases preserve 600 writes and 600 reads, but reproduce a 12.714%
median throughput loss. Two final listener diagnostics identify 123 additional durable
JSON publications; serialization itself is small. The failed first listener and build
attempts remain retained. Full portability and all three resource reconciliations pass.
Implement the reviewed selection-publication reuse design next; capacity, activation and
final E2E remain open. The owned baseline worktree is retained for that comparison. SPA
stays on its separate branch.

Preceding behavioral checkpoint: [native backlog progression](validation/2026-10-22-recovery-backlog.md).
Both Integration rows pass on unchanged production 91ce8b113. One/two independent service
providers preserve 3:1 opportunities across 32 live and eight historical roots; all 39
valid roots progress while corrupt original evidence is deferred intact. Source/output
identity, full portability and zero new residue pass. This is finite backlog progression,
not a capacity or global latency guarantee. Whole-journal cost, HTTP activation and final
E2E remain open; Enabled remains false. SPA stays on its separate branch.

Preceding behavioral checkpoint: [finite worker and OS restart](validation/2026-10-22-recovery-worker.md).
All 64 selected cases pass (50 Unit, 14 Integration). The scoped worker preserves finite
3:1 opportunities, pacing and cancellation. Four actual termination/restart rows preserve
original actions and receipts and charge real inherited work once across both snapshot
modes. All eight child identities, 56 output identities, architecture, full portability
and zero new resource residue qualify. Native backlog fairness, capacity, HTTP activation
and final E2E remain open; Enabled remains false. SPA stays on its separate branch.

Preceding behavioral checkpoint: [cooperative recovery deadlines](validation/2026-10-22-recovery-deadlines.md).
All 53 selected cases pass (41 Unit, 12 Integration). Linked deadlines propagate through
claim, owner and checkpoint without detached work or cleanup writes; native File/SQL
waits preserve original intent through lease takeover. Seven production builds, 44 output
identities, unchanged architecture/full portability and zero new resource residue pass.
Hosted fairness/restart, capacity, HTTP activation and final E2E remain open. SPA stays
on its separate branch.

Preceding behavioral checkpoint: [held inherited publication](validation/2026-10-22-held-inherited-publication.md).
All 20 selected Integration cases pass on unchanged production c7e0e8c61. In both snapshot
modes, SQL enrollment keeps recovery pending while the first child journal is held;
after publication, no provider call occurs and the original preparation charge is settled
before root completion. Full portability, 32 output identities and all three observed
runs pass with zero new resource residue. Cooperative deadlines, worker/HTTP activation,
capacity/fairness and final E2E remain open. SPA stays on its separate branch.

Preceding behavioral checkpoint: [held cancellation registration](validation/2026-10-22-held-registration.md).
All 93 selected cases pass (60 Integration, 33 Unit). The real Core/MAF stop-first
race reproduced a saved cancellation overwritten as failure; typed original-session
classification and atomic publication refusal now preserve the entire saved detail.
Foreign cancellation evidence, stale success/failure, original publication fences and
Process completion pass. Seven production builds, 44 output identities, full portability
and zero new resource residue qualify this repair. Inherited publication, worker/HTTP
activation, capacity/fairness and final E2E remain open. SPA stays on its separate branch.

Preceding behavioral checkpoint: [held publication fencing](validation/2026-10-22-held-publication.md).
All 87 selected Integration cases pass. The original held race reproduced late Agent
publication after completed root cancellation; a retained Process transaction now fences
journal/index publication after ordered file keys. Reverse ordering, replay, release
failures and real Agent recovery pass. Nine production builds, 29 output identities,
full portability and zero new resource residue qualify this repair. Held registration
and inherited publication, worker/HTTP activation, capacity/fairness and final E2E remain
open. SPA stays on its separate branch; main is untouched.

Preceding behavioral checkpoint: [indexed recovery selection](validation/2026-10-22-recovery-selection-indexes.md).
All 24 selected Integration cases pass after a retained test-only bulk-seeding timeout.
Both actual readers use bounded composite index plans; exact migration restart/downgrade,
row preservation, concurrent writes and real Agent recovery pass. Full portability,
architecture, source/output identity and resource reconciliation pass. Held publication
races, worker/HTTP activation, capacity/fairness and final E2E remain open. SPA main is
untouched.

Preceding behavioral checkpoint: [cancellation completion stages](validation/2026-10-22-cancellation-completion-stages.md).
All 141 selected cases pass (112 Unit, 29 Integration), including real Agent producers,
measured child accounting, interrupted scope capture and exact receipt recovery after
lost acknowledgement. Nine affected Release builds, 32 output identities, architecture,
full portability and observed resource closure pass. Automatic scheduling/HTTP cutover,
publication races, indexes/capacity/fairness, refresh, remaining writers and final E2E
remain open. SPA stays on codex/concurrent-mutations; main remains untouched.

Preceding behavioral checkpoint: [finite runtime recovery stage](validation/2026-10-22-runtime-recovery-stage.md).
All 87 selected cases qualify: 64 Unit passes plus 23 Integration passes after a test-only
parent-claim fixture repair. Exact source/28-output identity, full portability, architecture
and resource gates pass. Original cancellation receipts recover across new leases;
finite Agent inspection retains unresolved work and guards inherited profile changes.
Decision/scope/quiescence stages, capacity/fairness, refresh, remaining writers and final
E2E remain open. SPA stays on its separate branch.

Preceding behavioral checkpoint: [durable recovery cursors](validation/2026-10-22-recovery-cursors.md).
All 54 selected cases pass with seven Release builds, 24 verified owning/copied outputs,
full portability and zero new resource residue. Selected Process/Agent cursors and sticky
pending-cycle evidence survive takeover; old canonical wire positions remain unchanged.
Lost finalization acknowledgements retain their original receipt-recovery intent.
Actual recovery stages, bounded waits/fairness, capacity, refresh, remaining writers and
final E2E remain open. SPA stays on its separate branch.

Preceding behavioral checkpoint: [original Process stop adapter](validation/2026-10-22-process-stop-adapter.md).
All 55 selected cases pass through the typed port and real Process/Agent owners. Six
Release builds, twenty source/runtime identities, full portability, architecture and
resource closure pass. Exact root/claim/source/profile checks preserve pending unknown
work and support renewed current generations without changing original admission.
Finite recovery stages, capacity/fairness, snapshot refresh, remaining writers and final
E2E remain open. SPA stays on its separate branch.

Preceding behavioral checkpoint: [ordinary Agent parent stopping](validation/2026-10-22-ordinary-parent-stop.md).
All 112 selected cases pass, including exact-session signalling, authority changes,
unknown/legacy/request-scoped provider work and lost publication acknowledgements.
Six Release builds, source/runtime identity, unchanged architecture cycles, full
portability (15670 reviewed) and resource closure pass. The previous 107-case candidate
and two fixture compile failures remain retained. Process receipt/root adapters and
stages are next; capacity, fairness, refresh, remaining writers and final E2E are open.
SPA remains on its separate branch.

Preceding behavioral checkpoint: [compact Process selection index](validation/2026-10-22-process-selection-index.md).
All 66 final cases pass; the four-case signed-binary baseline and one historical fixture
failure are retained. Every index publisher uses its original journal target. At 100,000
history entries, metadata falls from 82.1 MB to 11.2 MB and page samples fall to 103–133 ms.
All 400 candidate writes and 400 reads complete without conflicts or lost revisions.
The tiny-history sample exceeds cost thresholds (13.8% throughput loss, 29.8% write-p95
increase), and global-lock tail waits remain. This is not a capacity or activation pass.
Five production/test Release builds plus a refreshed test build, source/runtime identity,
portability (15670 reviewed), rendering and resource checks pass. Actual Agent/stage
integration, bounded waits/fairness, repeated cost investigation, HTTP/status, refresh,
Workflow/settings, remaining writers and final E2E closure remain open. SPA stays on its
separate branch. The queue/coordinator prerequisites remain `13de74201` and `8cd7ebab8`.




The [cancellation-barrier continuation](validation/2026-10-21-cancellation-barrier-and-replay.md)
passes 84 selected cases, including 14 new cases. It prevents direct or rework-driven
reactivation after cancellation and preserves exact accounting replay after legitimate
failed-step rework. Seven Release builds, full portability, unchanged source/runtime
identity and five fully observed commands pass without new resource residue. Parent
stop/quiescence/recovery, refresh, activation, finite/sustained capacity and final Stable
remain open; R1/R4/R5/R6 are not closed by these focused prerequisite checks.

The [terminal-child accounting checkpoint](validation/2026-10-21-terminal-child-accounting.md)
qualifies exact closure and work settlement for Completed, Failed and Cancelled Process
steps. All 178 cases pass, including 61 new cases and actual SQL/file evidence in both
snapshot modes. No business outcome is reopened and no effect/provider is replayed.
Automatic parent stop, quiescence, refresh and activation remain open. The continuation
above repairs rework that could undo CancelRequested and the later receipt replay issue.
Performance, Workflow/settings/SPA, remaining writer waves and broad closure
are not completed; R1/R4/R5/R6 remain open.

The [closed-child accounting checkpoint](validation/2026-10-21-closed-child-accounting.md)
qualifies recovery after loss of an original approval request, with 131 correctness
cases and three diagnostic profiles passing. Exact work and receipts settle through
the existing Process owner without another Agent mutation or business effect. The
initial assertion failure and full corrected proof remain retained. Current profiles
still exceed the finite latency limit in two samples; parent cancellation/refresh,
producer/Workflow/settings integration, remaining writer waves and broad closure remain
open. R1/R4/R5/R6 are not declared complete by this checkpoint.

The [scoped catalog normalization checkpoint](validation/2026-10-21-scoped-catalog-normalization.md)
implements bounded current-input reuse without caching authorization. Median measured
allocation falls to 43–46 MB. Four actual-host approval/restart cases pass in the
bounded diagnostic after earlier timeouts. The measured read repeat still fails one
latency shape. Seed measurements exclude that work as the dominant remaining cost;
P10 subsequently measures request construction and current manager authority. All 192
preceding correctness passes, failed attempts, exact source/runtime identity and clean
new fixture deltas are retained. R1/R4/R5/R6 and downstream activation remain open.

The [review read boundary](validation/2026-10-21-review-read-boundary.md) removes
execution construction from inherited review GETs while preserving current disclosure.
All 67 selected correctness cases pass. The isolated cost successor passes three
component diagnostics but fails all six HTTP latency cases after all 30 samples and
safety assertions. Repeated catalog normalization remains a measured bottleneck;
the earlier contaminated measurements and fixture correction are retained. No new
project references or scoped cycles were introduced. R1/R4/R5/R6 remain open.

The [review compatibility/read-cost checkpoint](validation/2026-10-21-review-compatibility-read-cost.md)
passes nine fixed v1 digest cases and seven HTTP recovery regressions. Its six cost
cases fail: even one-step plans exceed one second and allocate about 80 MB per
request. The failed proof and clean resource delta are retained. Fixed-cost
profiling/repair now blocks capacity qualification and downstream activation.
R1/R4/R5/R6 remain open; no complete E2E claim follows from the compatibility passes.

The [Process review HTTP checkpoint](validation/2026-10-21-process-review-http.md)
now passes all 76 cases. Current access, lossless bounded requests, original-child
reads, restart and exact action/accounting recovery are qualified. It adds no
project reference or scoped cycle. The initial host crash and policy-denied
cleanup remain recorded. Intent compatibility, read scaling, parent transitions,
executor activation, Workflow/settings/UI, remaining writers and final broad
closure remain required; R1/R4/R5/R6 are not closed by this checkpoint.

Latest prerequisites: initial business binding qualifies 177 behaviors, original input
96 and [closed startup failure](validation/2026-10-21-closed-startup-failure.md) 202.
The final restart correction passes with unchanged production. These close those
prerequisites only; bounded capacity, actual adapter activation, manager continuation,
cancellation/refresh, Workflow, settings/SPA, remaining writers and final broad closure
remain required. Earlier "open" entries below are historical checkpoint records.
The [payload/capacity checkpoint](validation/2026-10-21-process-capacity-bounds.md)
passes 179 cases and finite root completion. The [pooled-buffer and original-policy
correction](validation/2026-10-21-process-capacity-repair.md) qualifies 183 distinct
behaviors across a retained test-only correction.
[Manager-review admission](validation/2026-10-21-manager-review-admission.md) now qualifies
74 distinct behaviors, with the disclosure test correction and observer incident retained.
Process [waiting-result accounting](validation/2026-10-21-process-review-completion.md)
now qualifies with 331 final passing cases, including real publication, root fences,
rollback, replay and restart. The scope guard, root fixture and observation failures
remain recorded with their corrections. Current manager actions and the actual adapter
remain next; sustained concurrent capacity stays open.
The [original step requirements and clock](validation/2026-10-21-original-step-requirements.md)
now qualify: 128 original-input prerequisites and all 29 final affected workspace cases.
Legacy inputs explicitly refuse missing compiled obligations. Existing work, deadline
and timestamp guards remain enforced. This does not activate the incomplete executor.

Selected Agent authority and its [corrected source/lifetime regressions](validation/2026-10-20-selected-agent-authority.md)
have 54 distinct passing affected cases. Initial business binding, original input,
manager continuation and full Process activation remain open. The retained failed
candidates and temporary trace-tool cleanup denial remain explicit.

Core startup evidence: [189 distinct passing behaviors](validation/2026-10-20-inherited-startup-accounting.md).
Fresh initial work and recovered preparation consume the original allocation.
Existing-only recovery cannot recreate a missing journal. Initial 171/189 and final
44/44 plus 145 unchanged passes remain retained. The remaining-budget/cancellation
boundary identified in review now passes 40 affected integration cases. The selected
Agent authority capability now precedes business binding to avoid workspace-wide
serialization across Project SQL checks.

Current selected step evidence: [187 distinct passing behaviors](validation/2026-10-20-process-selected-step-producer.md).
Bounded original source, registered root/step capture and exact first-child enrollment
are integrated. Only a fresh enrollment may start a missing child; Core startup now
enforces and accounts for that fact. Both test candidates retain exact source,
runtime, static and resource proof. Actual business binding, manager continuation,
Workflow, persisted settings and broader writer/capacity closure remain open.

Current root producer evidence: [192 distinct passing behaviors](validation/2026-10-20-process-root-material-producer.md).
Real registered RunSnapshot capture now precedes claims and owns exact reservations,
material and settlement. Original source and coherent Project reads remain bounded;
unknown acknowledgement never authorizes another capture. This closes initial shared
material production only. Selected step production now passes; actual Agent child binding, manager
continuation and full R5 remain open. SPA stays on its own branch.

Current capture ownership evidence: [127 distinct passing behaviors](validation/2026-10-20-process-capture-ownership.md).
Original Applied receipts cannot grant another capture invocation; the new exact claim
precondition protects StepSnapshot attachment. Shared RunSnapshot preparation retains
its existing root owner and must precede dispatch. This closes the commit/claim
prerequisite only. Registered step/child producers, manager actions and full R5 remain open.

Current Process result/accounting evidence: [369 distinct passing behaviors](validation/2026-10-20-process-result-accounting.md),
including 111 final cases and 258 unaffected cases under an exact repair comparison.
Normal dispatch and interrupted recovery share original-child reads, stable result
identity, atomic runtime/accounting acceptance and retained unknown work. This closes
the result-submission prerequisite; R5 and end-to-end activation remain open for actual
step capture, child binding, manager actions, Workflow and settings. Full portability passes;
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
| R1 | Prevent concurrent lost edits across human, agent and workflow writers | Owner protocol, enabled writer inventory, SQL/API/tool/browser races; P02/P05/P07 | Partially implemented: Projects original-state and durable-operation/native recovery foundation is committed through `702d0a93`, with focused proof. Workspace W1b and Resource R1 have scoped owner/native/API proof; Resource passed 346 ordinary and four later browser cases under the exact reviewed historical bridge. P20 additionally qualifies selected chat creation/title races and preserves current titles through late run outcomes. Complete Projects producer cutover, remaining owners and durable recovery obligations remain open. |
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

The [workspace path-probe repair](validation/2026-10-20-workspace-path-probe.md) passes all 75 selected cases. Exact internal path comparison removes repeated filesystem case-probe writes while preserving fresh containment/link validation and durable journals. The identical selected SQL-wait reference now has all six writers below one second (162.67â€“670.72 ms). Earlier negative evidence is retained; sustained capacity and inherited runtime activation remain open.

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

The [workspace and Process manager review integration](validation/2026-10-21-manager-review-integration.md) qualifies all 172 selected cases on signed implementation commit `528d29dbd`. Both snapshot modes preserve exact manager intent, original source authority, child effects and work, and one Process completion through retries, revocation, cancellation and acknowledgement loss. Historical decision/budget receipts are retained while runtime replay reloads current sibling progress. Release builds, full portability and frozen source/runtime/resource verification pass. Public review transport, parent refresh/cancel/reconciliation, executor activation, Workflow/settings/SPA, remaining writers and sustained capacity still gate R5/R6 and full closure. Historical denied cleanup remains unresolved; SPA stays on its separate branch.

## Native cancellation and Workflow recovery, 2026-10-23

The [P17/P18 validation](validation/2026-10-23-native-workflow-cancellation.md) qualifies the focused native client, bounded Workflow owner, coverage-version upgrade and held-boundary browser completion. The latest passing selection by topic is 244 cases across explicit repair boundaries; earlier failed runs remain retained. Final source/output identity, full portability and new runtime resource cleanup pass. R1/R5/R6 remain open: legacy root transport, current manager-chat create/rename revision conflict, capacity/default activation, remaining owners/policies and final E2E. SPA remains on codex/concurrent-mutations for colleague review.

## Chat-session publication, 2026-10-23

[P20 validation](validation/2026-10-23-chat-session-publication.md) closes the selected
creation/title lost-update boundary, including the actual Process manager-chat producer
and native browser regressions. It does not close durable same-source session identity.
The current compiled SPA passes generated-contract, transport, app and rendered-state
checks on its dedicated branch. Its older Project definition/lifecycle producers and
persistent Workspace draft recovery still require their own integration. Signed backend
checkpoints were subsequently signed in their exact frozen order through P21/P22. No
signing requirement was bypassed and no SPA main integration is authorized. Both new
bulk portability files are retained after separate
cleanup denials; source/output/runtime reconciliation passes for the executed P20 lanes.
