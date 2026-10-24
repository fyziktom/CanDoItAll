# Concurrent mutations implementation bundle

Current work: [atomic refresh delivery persistence](validation/2026-10-24-process-refresh-delivery-queue.md)
passes its 33 new queue, wake and migration cases. Admission stages delivery atomically;
capture Return wakes it without losing a concurrent park or disturbing a live lease.
All builds and final portability pass, with six reviewed generated metadata allowances.
The 36 existing admission/journal regressions and final resource closure are still running.
Worker activation, source revocation, legacy rollout, unknown-work resolution, retry and
capacity remain open. SPA stays on **codex/concurrent-mutations** for colleague review.
Historical cleanup denials remain, including the rejected user-authorized junction retry.

Preceding work: [refresh delivery continuation](validation/2026-10-23-process-refresh-delivery.md)
passes all 110 distinct selected cases across a verified test-only repair. Original receipts resume the
stop/account/capture flow, cancellation drains known work, and unknown work stays funded.
Final portability, source/output identity, secret review and new resource reconciliation
pass without a baseline change. Durable queue/wake delivery, source revocation, unknown-work resolution,
explicit retry, capacity and public activation remain open. SPA stays untouched on
**codex/concurrent-mutations** for colleague review. Historical cleanup denials remain,
including the exact user-authorized junction retry rejected by automatic review.

Preceding work: [actual older-producer retention](validation/2026-10-23-process-refresh-journal-compatibility.md)
passes all ten cross-binary cases. The previous producer preserves all 36 mapped Process
tables across five states in both snapshot modes. Normal outputs are restored and final
portability passes without a baseline change. Cleanup of the temporary build junction and
checkout remains blocked: automatic review rejected the exact user-authorized retry too.
Delivery, unknown-work resolution, explicit retry and public refresh/Replan remain open.
SPA stays untouched on **codex/concurrent-mutations** for colleague review.

Preceding work: the [durable capture journal](validation/2026-10-23-process-refresh-capture-journal-storage.md)
passes all 124 distinct selected cases across a verified test-only repair. Reservation,
first invocation, returned work and exact settlement survive interruption through the
original owners. Full portability, source/output and new resource checks pass. Actual
older-binary retention, unknown-work resolution, explicit retry, delivery, public refresh/
Replan and engine E2E remain open. SPA stays on **codex/concurrent-mutations** for colleague
review; historical cleanup denials remain recorded.

Preceding work: the [pure capture invocation journal](validation/2026-10-23-process-refresh-capture-journal.md)
passes all 35 selected cases. It distinguishes never-invoked cancellation from unknown
work, binds immutable returned material/work, and checks exact budget settlement. Final
portability, source/output and owned resource checks pass. Storage and registered producer integration are next; no
durable recovery or public refresh activation is claimed. SPA remains on its colleague-review
branch, and historical cleanup denials remain recorded.

Preceding work: [registered refresh capture producer](validation/2026-10-23-process-refresh-capture-producer.md)
passes 65 distinct selected cases across a verified test-only repair. Both snapshot modes
capture and advance through the original owners; known failure/cancellation settles actual
work, and restart can inherit completed root material. Full portability, source/output
identity and new resource reconciliation pass. Unknown-work recovery, explicit retry,
delivery and engine E2E remain open in the new recovery design. SPA stays clean on
**codex/concurrent-mutations** for colleague review; historical cleanup denials remain.

Preceding work: [refresh capture preparation queries](validation/2026-10-23-process-refresh-capture-query.md)
passes all 33 selected cases. Original-input/funding selection and the one-query progress
projection are registered; the successor shares the same reconciliation check. Final
portability, source/output identity, secret review and new resource reconciliation pass.
Capture orchestration, interrupted-work recovery, delivery and engine E2E remain open.
SPA stays clean on **codex/concurrent-mutations** for colleague review. Historical cleanup
denials remain recorded.

Preceding work: [original refresh successor](validation/2026-10-23-process-refresh-successor.md)
passes all 129 selected cases. Both snapshot modes preserve original history and attempts,
replay without another settlement and enroll the next scheduler attempt with replacement
material. Shared inheritance waits for root work; independent step snapshots can proceed.
Final portability, source/output identity and new resource reconciliation pass. Durable
capture orchestration, interrupted-work recovery, delivery and engine E2E remain open.
SPA remains clean on **codex/concurrent-mutations** for colleague review; historical
cleanup denials remain recorded.

Preceding work: [original refresh reconciliation accounting](validation/2026-10-23-process-refresh-accounting.md)
passes all 275 selected cases across the verified test-only work assertion repair.
Original stopped work settles once; an already accepted effect completes its original
result, while unknown/open work stays reserved. Full portability, source/output identity
and new resource reconciliation pass. Successor and capture decisions are recorded in
P28; full refresh/replan and engine E2E remain open. SPA stays on
**codex/concurrent-mutations** for colleague review. Historical cleanup denials remain.

Preceding work: [original-child refresh stop](validation/2026-10-23-process-refresh-child-stop.md)
passes all 73 selected cases across a verified test-only cursor assertion repair. Registered
Workbench/Core stopping requires the exact durable refresh and preserves original work,
effects and reserved grants. Final portability, source/output and new resource checks pass.
Reconciliation accounting is next; full P28 refresh/replan and engine E2E remain open.
SPA stays clean on **codex/concurrent-mutations** for colleague review. Historical cleanup
denials remain recorded.

Preceding work: [atomic Process refresh admission](validation/2026-10-23-process-refresh-admission.md)
passes all 343 selected backend cases, eight actual older/current reader checks and final
portability. It freezes one original local/shared generation and preserves
exact action replay without stopping the child, spending its grant or exposing a public
refresh switch. Source/output identity and new resource reconciliation pass; prior cleanup
denials remain. Real child stop/accounting/capture is next. Full P28 refresh/replan and engine E2E
remain open. SPA stays clean on **codex/concurrent-mutations** for colleague review.

Preceding work: [Process basis-change failure](validation/2026-10-23-process-basis-change-failure.md)
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
passes all 173 selected cases, 11 Release builds and full portability. The old synchronous
root cascade is retired. Real MAF cancellation now distinguishes local proposals from
hosted-tool uncertainty using immutable original checkpoint evidence. Source/output and
resource reconciliation pass; both failed candidates remain retained. Manager-chat
concurrency, capacity/default activation, remaining owners/policies and final E2E stay
open. SPA remains on its dedicated branch for colleague review.

Preceding [native cancellation and Workflow recovery](validation/2026-10-23-native-workflow-cancellation.md)
qualifies the native action, exact Workflow boundary cancellation, coverage-version upgrade
and both held-boundary browser scenarios. The latest passing topic selection contains
244 cases across explicit repair boundaries, not one clean aggregate run. The final
494 source pins and 100 outputs remain unchanged. A separate manager-chat workspace
revision conflict is retained as open; its unchanged rerun is not a repair. Full
portability and rendering review pass. Legacy API cutover, capacity/activation, remaining
owners/policies and final E2E stay open. SPA stays on its dedicated branch for colleague
review. Historical denied cleanup remains recorded.

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




Current work resumed on 2026-10-18 under the [resumption record](validation/2026-10-18-resumption.md).
The original end-to-end outcome remains active. SPA work stays on its own
`codex/concurrent-mutations` branch and must not be merged into SPA main.

Latest qualified checkpoint: [cancellation barrier and accounting replay](validation/2026-10-21-cancellation-barrier-and-replay.md).
All 84 selected cases pass, including 14 new cases. CancelRequested and Cancelled cannot
be reactivated by rework or direct persisted mutations. Ordinary failed-run retry stays
supported. Historical accounting recovers with current runtime after legitimate rework,
without another charge or effect. Seven Release builds, full portability, exact source/
runtime identity and all five observed commands pass with zero new resource residue.
Selected Agent stopping and durable quiescence/recovery are next; refresh, activation,
finite read latency, sustained capacity and final integrated closure remain open.

Preceding checkpoint: [terminal-child accounting](validation/2026-10-21-terminal-child-accounting.md).
Its later rework-replay finding is corrected by the checkpoint above.
All 178 selected cases pass, including 61 new cases. Exact closed work settles after a
Process step stops while its business outcome and accepted effects remain unchanged.
Both snapshot modes, competing requests, lost acknowledgements and SQL rollback pass.
Source/runtime identity, full portability and all six observed commands pass with zero
new resource deltas. Parent stop/recovery, snapshot refresh and producer activation
remain gated. Finite read latency and sustained capacity are still open.

Preceding checkpoint: [closed-child accounting recovery](validation/2026-10-21-closed-child-accounting.md).
All 134 selected cases pass (131 correctness/compatibility and three diagnostic profiles),
including all 55 new cases. Lost original approval requests can be recovered without
another Agent/provider/effect operation. Source/runtime identity, full portability and
all 12 observed commands pass with zero new resource deltas. The retained initial
parser assertion failure required only a test correction. Current read profiles still
exceed one second in two samples; construction and manager checks are not dominant.
Parent stop/refresh, database/Agent read diagnosis and producer activation remain next.

Preceding checkpoint: [scoped catalog normalization](validation/2026-10-21-scoped-catalog-normalization.md).
Bounded current-input reuse reduces median measured process allocation from about
79–81 MB to 43–46 MB. All 192 preceding correctness cases pass; across unchanged
source, the four approval/restart cases now pass under bounded request diagnostics.
Earlier approval timeouts remain evidence. The measured six-shape read repeat still
fails StepSnapshot/1024 at 1.25 seconds with low observed host CPU. Existing tracing
shows only 18–43 ms in catalog comparisons; seed construction/input validation
also account for only 58–105 ms. P10 subsequently measured construction/current authority. Failed attempts and all samples remain
retained. Downstream activation and complete E2E closure remain open.

Latest fully qualified prerequisite: [Process planning review HTTP](validation/2026-10-21-process-review-http.md).
All 76 cases pass, including both snapshot modes, exact retained requests after
host restart, invalid input without intent, accounting refusal and lost commit
acknowledgement. Full portability, source/runtime identity and all seven observed
commands pass, with zero new resource deltas. The initial host crash and its
policy-denied cleanup remain recorded. Saved-intent compatibility, selected-read
capacity, parent reconciliation and executor activation remain next.

The preceding [workspace and Process manager review](validation/2026-10-21-manager-review-integration.md)
qualifies the action owner.
All 172 cases pass, including both snapshot modes, approve/reject, exact retry,
renewed credentials, lost acknowledgements, late revocation/cancellation and sibling
progress with retained accounting receipts. Source/runtime identity, full portability
and all 13 observed commands pass, with zero new resource deltas. Implementation is
signed commit `528d29dbd`. Public review transport now qualifies; parent reconciliation
and executor activation remain required. Earlier cleanup denials remain unresolved.

The preceding [durable manager review owner](validation/2026-10-21-manager-review-owner.md)
qualifies persisted Agent intent.
All 147 cases pass, including exact intent replay, permission changes during lock
waits, cancellation after effects, Project rollback and Process completion regressions.
Frozen source/runtime identity, portability and resource reconciliation pass.
Workspace and Process integration now qualify; public review integration remains next.

The preceding [manager publication guard](validation/2026-10-21-manager-run-authority.md)
qualifies the selected file publication boundary.
All 56 selected cases pass, including permission changes during lock waits and
unrelated publication during authority observation. Frozen inputs, portability and
resource reconciliation pass.

The preceding [waiting-result completion](validation/2026-10-21-process-review-completion.md)
qualifies the produced Process child.
All 331 final selected cases pass, including real Project-scoped publication in both
snapshot modes, root cancellation, exact settlement replay, rollback and restart.
The corrected root fixture and observer are validated; all earlier failures remain
retained. Frozen source/runtime, full portability and observed resource reconciliation
pass. Current manager actions and the actual adapter remain next, under the
[reviewed manager design](plan/P07-current-manager-actions.md). Earlier cleanup
denials remain unresolved; SPA stays on its separate branch.

The preceding [original step requirements](validation/2026-10-21-original-step-requirements.md)
qualify original input and execution accounting.
All 128 original-input cases and the final 29 affected workspace scenarios pass.
Versioned input, migration interruption and indexed scaling checks pass. The shared
execution clock preserves exact work and deadline enforcement; earlier failures remain
retained. Source/runtime identity, final portability and resource reconciliation pass.

The preceding [manager review admission](validation/2026-10-21-manager-review-admission.md)
qualifies 74 distinct behaviors, including actual saved publication after the Process
claim closes. The test-only disclosure correction and observer-sharing incident remain
retained. Production source/binary identity, full portability and resource proof pass.
Process waiting-result completion now qualifies. Current manager actions and the actual
adapter remain next; executor activation stays gated.

The preceding [pooled preparation and original-policy authority](validation/2026-10-21-process-capacity-repair.md)
qualifies 183 distinct behaviors across the retained 180/183 candidate and nine-case
test-only correction. Warm output allocation is close to baseline; cold allocation and
clearing time remain explicit. Removing a current policy cannot downgrade original
governed authority. Full portability and source/runtime/resource proof pass.
Manager continuation and the actual adapter remain required.

The preceding [payload bounds and finite root capacity](validation/2026-10-21-process-capacity-bounds.md)
passes all 179 cases, with explicit preparation/projection limits and finite completion
at the 256-allocation ceiling. Sustained concurrent capacity remains required.

The preceding [closed startup failure](validation/2026-10-21-closed-startup-failure.md)
qualifies 202 distinct behaviors, including replay after acknowledgement loss and a full
host restart without live failure evidence. Both test-only failures are retained; all
production source and 392 DLL copies remain unchanged across the corrections. Full
portability and observed resource reconciliation pass. Capacity and Process continuation
still gate executor activation.

The preceding [original accepted Process input](validation/2026-10-21-original-process-input.md)
qualifies 96 distinct behaviors. Atomic original input, indexed selection, migration,
rollback and recovery pass. Selected reads stay at one query and about 5.3 KB through
1,024 steps; initial acceptance still requires capacity work. The failed SQL-text
assertion candidate is retained, and its test-only correction leaves production unchanged.
Release builds, full portability and observed resource reconciliation pass.

The preceding [initial registered business binding](validation/2026-10-21-initial-business-binding.md)
qualifies 177 distinct behaviors across documented test-only corrections. Both Process
snapshot modes reach the real inherited workspace; read-only assignments are refused and
waiting children retain their original input without another provider call. Production
sources and assemblies are identical across the correction candidates. Full portability,
final documentation/whitespace checks and observed resource reconciliation pass.
The [original execution input design](plan/P07-original-process-input.md) is implemented
and validated; Process activation remains gated.

The preceding [selected Agent authority](validation/2026-10-20-selected-agent-authority.md)
has 54 passing affected behaviors across the policy/recovery and final source-lifetime
corrections. Selected Agent keys permit unrelated edits while preserving revocation
fences; retained Project bindings no longer trigger redundant SQL. Conflicting source
and target lifetimes are refused before native mutation. Both failed candidates remain
recorded. Initial business binding is validated; Process activation still requires original
input, manager continuation, cancellation and capacity closure.

The [initial and recovered startup accounting](validation/2026-10-20-inherited-startup-accounting.md)
passes 189 distinct behaviors after an exact test-only correction. Initial work
uses the original child grant, and recovery cannot create a missing journal.
Recovered startup runs inside measured child work. Release builds, full portability,
source/runtime identities and resource reconciliation pass. The additional
[budget/cancellation boundary](validation/2026-10-20-inherited-startup-boundary.md)
passes all 40 affected integration cases. Selected Agent authority locking now passes,
so actual business binding can avoid carrying a workspace-wide lock across SQL.
Business binding and manager continuation still gate Process activation.

The [selected step preparation and enrollment](validation/2026-10-20-process-selected-step-producer.md)
passes 187 distinct behaviors under an exact test-only correction. It reads bounded
original source, reuses shared RunSnapshot or captures StepSnapshot, and atomically
enrolls one original child under the exact claim and assignment contract. Lost responses
never authorize repeated capture. Full portability, Release builds, architecture and
source/runtime/resource checks pass. The original 185/186 result remains retained.
Actual business binding and manager continuation remain next.

The [initial root snapshot production](validation/2026-10-20-process-root-material-producer.md)
passes 192 distinct behaviors. Required dispatcher preparation captures real Project
material before claims, invokes one capture under contention, preserves unknown work and
atomically settles closed work. Root source reads stay at three rows / 6,612 serialized
scalar bytes for both 1 and 128 steps. Original Agent/operator source, lifetime and
profile checks are proved; Release builds, full portability and resource checks pass.
The initial test setup/assembly failures remain recorded. Selected step production is
now available; actual child binding and manager continuation remain activation gates.

The [Process capture ownership prerequisite](validation/2026-10-20-process-capture-ownership.md)
has 127 distinct passing behaviors under an exact test-only correction. Durable replay
cannot grant fresh capture ownership, and exact dispatch attachment rejects expired,
replaced or cancelled authority, including rollback after SQL flush. The initial test
candidate exposed and corrected the design ordering: RunSnapshot capture precedes claims;
StepSnapshot capture uses its claim. Production scope admission was preserved.
Builds, full portability, architecture and source/runtime/resource checks pass.
Actual step/child producers and manager continuation remain next.

The [Process result/accounting integration](validation/2026-10-20-process-result-accounting.md)
has 369 distinct passing behaviors: 111 final cases and 258 unaffected passes retained
under an exact source comparison. Dispatcher and interrupted-execution recovery now use
original child evidence and atomically commit runtime results with work accounting.
Receipt serialization and optional legacy joins are repaired; both failed candidates
remain retained. Release builds, full portability, architecture and source/runtime/resource
checks pass. StepSnapshot capture, actual child binding, manager continuation, Workflow, settings/SPA,
sustained capacity and broad Stable closure remain open.

The [asynchronous child-binding checkpoint](validation/2026-10-19-asynchronous-child-binding.md)
passes all 192 selected cases. Original target checks run inside exact new-only admission;
cancellation and source revocation during the await prevent initial save. The concrete
Process/Workbench path remains the next integration work.

The [selected Process source query](validation/2026-10-19-selected-child-dispatch.md)
passes 86 selected cases, including exact child and original policy checks, retained
history and bounded metadata-only SQL. Legacy reservation/mutation routes remain closed.

The [root funding checkpoint](validation/2026-10-19-process-run-budget-funding.md) passes
361/361 selected cases after correcting the legacy optional-parent SQL projection.
Local and descendant work now share one genuine root budget, with atomic reservations,
settlement, original receipts and cancellation/overrun publication checks. Root funding is
signed as `bed62fc21`; prior pending-signature records remain historical.

The [active shared scope checkpoint](validation/2026-10-19-process-shared-run-scope.md)
passes all 68 repair cases, covering every failure in the retained initial 397/414 run.
Root capture and accounting commit atomically; descendants inherit original material, and
parent reconciliation freezes joins and publication. Eight Release builds, model consistency
and full portability enforcement pass.
The [inherited Agent checkpoint](validation/2026-10-19-inherited-agent-planning.md)
now has 242 distinct passing behaviors across its explicit test-only repair bridge.
Schema 7 retains parent material/time/grant, rejects local refresh, and supplies original
background input through the real MAF path. Actual Process/Workflow producer, Workbench
publication, manager continuation and settings activation remain open.

The [exact child admission checkpoint](validation/2026-10-19-exact-inherited-admission.md)
now has 73 distinct passing selected behaviors across an exact test-only bridge. It
resolves the stable execution before preparing new input, recovers interrupted initial
saves and preserves original work/grants without scanning unrelated execution history.
This prerequisite does not activate Process/Workflow producers.

The [Agent policy receipt checkpoint](validation/2026-10-19-agent-policy-receipts.md)
passes 40/40 cases. Policy and historical receipt commit together; read-only lookup
recovers the original result after lost acknowledgement without replaying PUT.
Receipt publication keeps only the affected Agent's lock. [Native retained-save recovery](validation/2026-10-19-agent-policy-native-recovery.md)
now has 39 passing selected behaviors, including actual two-tab dispatch exclusion,
browser reload and historical recovery with later typing preserved. The
[deleted-Agent recovery checkpoint](validation/2026-10-19-agent-policy-deleted-recovery.md)
closes native entry after deletion and actual owned-host restart, with 58 distinct
selected passing behaviors across its explicit repair bridges. Wider E2E work continues.

The [Process policy admission foundation](validation/2026-10-19-process-policy-admission.md)
passes 68/68 cases, preserving legacy launch fingerprints and storing immutable policy
provenance with migration/restart proof. Actual Process/Workflow executors reject the
new contract until governed execution and durable continuation are implemented.
This foundation does not activate Process/Workflow policy settings.
The [persistence authority checkpoint](validation/2026-10-19-process-policy-authority.md)
adds 58 passing distinct selected behaviors across its exact repair bridge. Unsupported
policies now also block Agent reservation and mapped Workflow admission/continuation
at the owner fence while historical reads remain available. The
[decision runtime design](plan/P07-process-decision-runtime.md) specifies the next work.
The [pure allocation foundation](validation/2026-10-19-process-decision-budget.md)
passes 28 Unit cases for shared allowances, exact replay, settlement and recorded
overruns. Decision fences and actual runtime wiring remain open.
The [shared-boundary checkpoint](validation/2026-10-19-shared-decision-budget.md)
moves the unchanged arithmetic to SharedKernel and repeats all 28 cases, preserving
engine dependency direction before durable integration.
The [durable accounting checkpoint](validation/2026-10-19-process-budget-persistence.md)
passes 41/41 cases. Local step budgets, allocations and original accounting receipts
now commit with runtime state under the root fence. Nested owner settlement, generation
fencing and governed execution remain open.
The [generation lifecycle checkpoint](validation/2026-10-19-process-decision-generations.md)
passes 29/29 isolated cases for bounded generations, immutable children, review and
quiescence. The [atomic generation checkpoint](validation/2026-10-19-process-generation-persistence.md)
passes 101/101 selected cases for normalized history/children, atomic runtime/accounting
changes, replay, restart, rollback and bounded reconciliation. Actual child-owner and
effect-fence integration, material inheritance and governed execution remain open.

The [child publication foundation](validation/2026-10-19-process-child-publication.md)
passes 108/108 selected cases. Stable occurrence identity, one-query canonical lookup
and the enlisted generation condition now have restart, rollback, race and unrelated-root
proof. Actual Agent/Workflow publication must still compose this fence with original
source/manager authority, inherited material and the supported execution contract.

The [versioned execution admission checkpoint](validation/2026-10-19-process-execution-contract.md)
has 114 selected passing behaviours across an exact test-only repair bridge. It preserves
schema 1 bytes, binds schema 2's exact contract into launch/assignment intent and retains
the existing unsupported-execution guards. Actual inherited material and runtime wiring
remain open.

The [original material value checkpoint](validation/2026-10-19-process-inherited-material-value.md)
passes 44 selected cases for scope/content integrity, original capture age and bounded
Unicode payloads. The [canonical captured-material checkpoint](validation/2026-10-19-process-material-persistence.md)
now has 237 distinct selected passing behaviours across an exact test-only repair bridge.
The [shared-content checkpoint](validation/2026-10-19-process-shared-content.md) now
passes 265/265 selected cases, separating mixed-executor business content from exact
dispatch capability while preserving historical formats. Shared active parent
scope/budget and child runtime wiring remain open; these contracts do not activate
delegated execution.

The [external Agent precondition checkpoint](validation/2026-10-19-external-agent-preconditions.md)
passes 54/54 focused cases. External update/archive now compare actual Agent state,
capture mutable requests before storage work and retain historical replay receipts.
Process/Workflow policy activation and the wider E2E gates remain active.

The resumed native chat browser proof now passes: one case covers three submissions,
draft preservation, restore without resend and unknown outcomes across native navigation.
Release build, portability enforcement and documentation/rendering checks pass; see the
[resumption record](validation/2026-10-18-resumption.md). The remaining lifecycle,
runtime-policy and capacity work is still active. The selected publication authority now has
[a passing 72-case checkpoint](validation/2026-10-19-selected-publication-cost.md),
including all six unrelated writers below one second during that historical SQL wait.
The 2026-10-20 enlisted-hook run subsequently failed this reference; the
[workspace path-probe repair](validation/2026-10-20-workspace-path-probe.md) then passed
the unchanged reference after a source optimization. Wider
capacity and actual RunSnapshot/StepSnapshot/Refresh/Replan runtime activation remain open.
The [runtime result codec extraction](validation/2026-10-19-runtime-result-codec.md)
also passes compatibility proof. The [saved-effect runtime adapter](validation/2026-10-19-saved-effect-runtime.md)
passes all 70 focused cases. The [planning decision journal](validation/2026-10-19-planning-decision-journal.md)
passes all 94 focused cases. The [live planning owner](validation/2026-10-19-live-planning-owner.md)
passes all 114 focused cases, including real file-owner work accounting and execution
cancellation. The [actual MAF planning provider](validation/2026-10-19-planning-provider.md)
passes all 103 cases in its repaired lane, with the original 146/158 result and exact
unchanged-production bridge retained. The [active-work publication version](validation/2026-10-19-active-work-publication.md)
passes all 47 contract/PostgreSQL cases in its repaired lane. The
[planning saved-action owner](validation/2026-10-19-planning-saved-actions.md) passes
all 146 cases after a definite-no-effect Refresh repair. The
[registered Workbench planning publication](validation/2026-10-19-workbench-planning.md)
passes 38 selected cases after fixture repairs, with an exact production bridge to
76 earlier passing integration cases. The
[historical planning receipt service](validation/2026-10-19-planning-historical-receipts.md)
passes all 75 selected cases, including the publication and existing journal regressions.
The [persisted planning admission](validation/2026-10-19-planning-admission.md)
passes 60 cases after repairing a real UI/provider service-construction cycle. Its
rejected candidate and blocked abandoned-fixture cleanup remain explicit. The
[atomic start and provider contract](validation/2026-10-19-planning-start-contract.md)
pass 123 selected cases. The [initial product execution path](validation/2026-10-19-product-planning-entry.md)
passes 102 selected cases after the retained fixture/transport repairs. Saved product
actions now pass all ten registered product cases plus 15 activity coordinator cases.
The earlier failed candidates and subsequent repairs remain recorded; user-facing
controls are covered by the subsequent native checkpoint; wider end-to-end gates remain open.

The [native planning review checkpoint](validation/2026-10-19-native-planning-review.md)
passes all 130 focused cases. The native chat now offers explicit project-task planning,
exact-generation review, refresh, cancellation and publication with draft preservation.
The real native browser journey now also passes after capture-purpose and source-refresh
repairs: another actor pauses the old plan, refresh produces generation two, publication
occurs once and the canvas updates while later typing survives. The final repair passes
26/26; broader focused proof and failed predecessors are retained in that record.
First review took about 6.1 seconds, leaving latency qualification open. The signed
native planning checkpoint is `bd89b5254`; SPA remains isolated on its existing branch.

The [Agent policy editor and HTTP checkpoint](validation/2026-10-19-agent-policy-access.md)
passes 63 focused cases, followed by all six affected native/component browser cases
after a subtitle binding correction. It preserves original versions and drafts,
requires explicit conflict reconciliation, and blocks uncertain policy saves. Recovery
for existing Agents is now covered by the later native checkpoint, and
external-Agent target preconditions by their later checkpoint. Deleted-Agent recovery
entry, production-host restart and Process/Workflow integration remain open, along with
the wider bundle gates.

P03 now includes the lifecycle owner/migration, native and HTTP retained operations,
creation cleanup and governed Agent/Process hierarchy and definition editing. Six
actual-process restart scenarios pass, including recovery of committed reconnects and
created-target edits without saved completion. Original creation provenance is bounded
and current permission checks remain mandatory.
See [the lifecycle integration record](validation/2026-10-18-lifecycle-integration.md)
for the exact proof and remaining caller, runtime and capacity gates.

Historical handoff (2026-10-17): implementation stopped at the user's credit-saving
checkpoint. The final C4 successor passed **706/706**. A new native chat browser test
failed twice and is preserved outside the compiled checkpoint; broader runtime policy,
lifecycle cutover, capacity and cleanup remain open. See the
[checkpoint and exact continuation](validation/2026-10-17-checkpoint-handoff.md).
This current handoff supersedes transient next-step/signing statuses in older records.

Status: end-to-end work remains in progress. The Projects original-state and durable
operation foundation is committed through `702d0a93`; see
[the foundation checkpoint](validation/P02bc-foundation-checkpoint.md).
The [historical correctness checkpoint](validation/P02b-R1c-historical-correctness-checkpoint.md)
has passing evidence for 133 distinct ordinary cases across scoped repairs, both actual
restart Facts and the Send browser Fact. Those controlled configurations do not prove
ordinary background recovery or complete producer cutover.

The [C3 checkpoint](validation/P04-C3-focused-runtime-checkpoint.md) passed all 190
application cases on one source/runtime candidate: 60 Unit, 127 Integration, two
LiveProcess and one LongRunning. Independent nine-child process observation remains
incomplete, and earlier blocked resource cleanup remains outstanding. C3 publication
is dormant; finite workload characterization does not qualify capacity.

Dormant D1 saved-effect dispatch has seven passing Release builds and final static
enforcement. Its first 247-case run passed 237 and failed ten fixture arrangements.
Three reviewed test-only repairs passed all 267 cases in the [successor checkpoint](validation/R1d-D1-first-runtime-and-fixture-repair.md),
including 20 existing rows for the second stale constructor helper. Its periodic process
observer stopped on an identity mismatch; final resource checks found no new residue,
with that capture limitation retained. The [C4 publication cost probe](validation/P04-C4-small-coarse-runtime-checkpoint.md)
completed all effect and guard-release checks, then failed its unchanged responsiveness
target: unrelated writers sharing an organization waited five seconds. The reviewed
narrow source-lock protocol is integrated. Its [first runtime candidate](validation/P04-C4-first-runtime-and-capacity-calibration.md)
discovered 703 rows and stopped after 213 passes and one background-reservation race
failure, leaving 489 unexecuted. Its repaired 704-row candidate passed 376 and failed
seven deletion projection cases after 383 executions; 321 remain unexecuted. The
reservation race now passes. The deletion normalization window and legacy audit-writer
publication perimeter require further repair. The corrected Resource finite baseline
passed 126 batches/2,178 matrix calls plus 17 controls, with independent resource review.
Same-project serialization is materially slower in this workload; shared-project
optimization and sustained-capacity proof remain required. Failed candidates and
observation limits are retained. Latest freeze release is `C5F0742B`.
The Resource/deletion candidate is integrated. Its canonical migration, complete model
and generated SQL passed [independent review](validation/P05-R1-resource-canonical-generation-review.md);
all eight standard Release builds and static enforcement passed before the first run.
The [first runtime candidate](validation/P05-R1-resource-first-runtime-checkpoint.md) executed
228 cases: 227 passed and one migration-test inspection query failed. Its reviewed SQL
cast correction passed the complete [346-case successor](validation/P05-R1-resource-catalog-cast-runtime-checkpoint.md),
including actual migration refusal/Down proof and resource closure. Two failed browser
candidates remain preserved. The repaired active-source fixture candidate passed all
four browser Facts: product, sandbox, Data Sources and CRM, with parent-reviewed lane
closure and freeze release at 2026-10-17T11:20:36.0215536Z. The 346 ordinary cases remain
historical proof connected by the exact reviewed source/runtime bridge; they were not
rerun on this browser candidate. The sandbox's auxiliary Web profile observation gap
and Data Sources' external second-profile gap remain explicit; the latter has real
in-band after-restart HTTP evidence. Neither supplement claims a complete allocation
ledger. The original full end-to-end request remains open: persisted policy/runtime
integration, Workflow definition, component and global-settings protection with durable
original-intent recovery, other owners, capacity and final broad validation still require
implementation and proof. The [P07 policy checkpoint](validation/P07-policy-owner-integration.md)
integrates Workspace/Agent policy owners and the Workspace editor/API. Its canonical
migration build, pending-model check, PostgreSQL migration/HTTP cases and two native
restore cases pass. The Workspace integration run passed 52 of 54; its two reviewed
test-only repairs passed separately. The repaired 39-case pure Unit run, 67-case Workspace
component run and ten Agent policy integration cases pass. Original failures and repairs
remain recorded. Fresh static enforcement passes on the policy candidate. All four
Workspace browser Facts pass, with reviewed artifacts and retained Web/sandbox/Chromium
closure; this proves policy editing, not runtime enforcement. The corrected deletion
witness and dormant P03 contracts/codec are integrated. The first C4 successor executed
533 of 706 cases: 530 passed and three competing-publication tests failed. Their reviewed
contract corrections are integrated. The fresh successor executed all 706: 703 passed
and three caller tests failed (external provisioning, bulk catalog reads and the capability
proof fault phase). Its exact source/runtime identities match. Twenty attributed SDK logs
were retained and removed; no owned process/listener or PostgreSQL delta remains. Twenty-seven
unowned temporary-file deltas are preserved, so complete resource closure is not claimed.
Freeze release `5D230E60` permits repairs, not acceptance. The chat-draft retention and
three caller-contract repairs are integrated. The focused 30-case pass was followed by
the full [706-case successor](validation/P04-C4-chat-and-caller-repair.md), which passed
all cases with unchanged source/runtime identities. Its three observer segments cover
execution; complete resource closure remains refused because of unowned TEMP deltas
and earlier blockers. The subsequent native chat browser proof failed twice and was
held outside MAIN for repair. Runtime/deletion activation remains open.

Historical failed and repaired candidates remain in their individual validation records;
none is relabelled as passing. Broad Stable is still incomplete: 16,199 executed,
16,182 passed, 17 failed and 2,444 unexecuted. See the
[wire-comparison checkpoint](validation/P02b-R1bc-journal-wire-candidate.md) for journal
cost findings and links to earlier performance candidates. The SPA transport checkpoint
is signed as `70c65dd`; MAIN checkpoint closure is recorded in the current handoff.

Maintained design: [Concurrent mutations architecture](../../../docs/architecture/concurrent-mutations/README.md).
Work order: [execution plan](plan/execution-plan.md). Input mapping and closure:
[requirements ledger](requirements-and-closure.md). Source identity: [original request](inputs/request.md).

The user requested an end-to-end implementation of safe concurrent data editing by
people, agents and workflows, with detailed architecture and risk analysis before
implementation, configurable agent/process reaction to changed information, meaningful
tests, maintained documentation and incremental commits.

The architect's 2026-09-28 v0.1 package is proposal evidence, not executable instructions
or an approved implementation. Preserve it under `inputs/architect-v0.1`. Its phase
cards and registers are retained as supplied; current decisions and execution status
belong to this bundle and the maintained design under `docs/architecture`.

## Execution contract

- Refresh current owners, enabled callers, dependency edges and test seams before
  accepting a design. Never infer protection from a UI token alone.
- Keep fact-owner policy in its module and provider-neutral execution policy in the
  existing runtime contracts. Preserve authority and project-lifetime fences.
- Complete the architecture review before production edits. Implement dependency-ready
  slices, with affected builds, discovered tests, portability enforcement and scoped
  commits. Do not label a protected owner complete while a writer bypasses it.
- Use standard Debug/Release outputs and serialize builds. Preserve port 5032, ordinary
  databases, installed MCP tools and unrelated work.
- Imported source scripts are reference material and are not executed.

## Resource ownership

This bundle owns `codex/bundles/concurrent-mutations` and may own small validation
artifacts under `.artifacts/concurrent-mutations`. One isolated PostgreSQL 18 test
container and volume are recorded in [resource ownership](resources.md). Reuse them
across related validation lanes and verify cleanup after their last required use.

## Compatibility map

The supplied package's documents/registers/phases are retained as input. They lack
current-branch execution evidence and completed-work status, so they cannot serve as
the active execution record. This root records the live request, design decisions,
phase status and closure; it does not rewrite the historical package's proposed status.

## Closure

End-to-end closure requires complete enabled-writer protection, persisted runtime
settings and producer-to-owner proof, measured capacity, final broad validation and
owned-resource cleanup. Current focused proof does not close those requirements.
Separate SPA preparation is committed at `2fe9e8e` and its dormant typed pending-store
adapter at `0411cd3`; generated-client/editor cutover remains open. The
[execution plan](plan/execution-plan.md) and
[requirements ledger](requirements-and-closure.md) track the remaining work.

The [shared inherited planning coordinator checkpoint](validation/2026-10-19-inherited-planning-coordinator.md)
passes all 170 selected cases. The common loop, exact inherited startup, original material
and project history are proved; concrete Process/Workflow activation and publication
remain governed by the [integration design](plan/P07-process-planning-integration.md).

The [canonical child/material reader](validation/2026-10-19-canonical-child-material.md)
has 169 passing selected cases across an exact test-only repair. It resolves original
policy, grant and material through three bounded reads without new module dependencies.
The [actual inherited workspace lifecycle](validation/2026-10-19-inherited-workspace-lifecycle.md)
now passes all 185 selected cases after repairing the retained caller's activity transition.
It retains original input/scope, current authorization and exclusive provider ownership.
Concrete Process/Workflow source, Workbench publication and manager integration continue next.


The [Process publication claim-lifetime repair](validation/2026-10-19-publication-claim-lifetime.md)
passes all 106 selected cases. Dispatch now requires the exact active claim and attempt
with unexpired lifetime after the root lock; historical read and manager review remain
separate. Concrete producer/publication integration and R5/R6 closure remain open.

The [inherited publication snapshot checkpoint](validation/2026-10-20-inherited-publication-snapshot.md) passes 238 corrected selected cases. It holds the executor and optional launching Agent in one ordered key set without retaining the workspace gate. The coarse SQL-wait comparison remains negative evidence. Actual inherited publication and Process/Workflow activation remain open.

The [Process Project/root lock-order repair](validation/2026-10-20-process-project-lock-order.md) has 194 distinct passing selected cases across an exact test-only bridge. Initial and child commits take original Project gates before Process roots; accepted receipts still recover after retirement. All six new ordering/cancellation races pass. Inherited publication and R5/R6 activation remain open.

The [selected Process publication fence](validation/2026-10-20-selected-publication-fence.md) has 126 distinct passing cases across an exact one-test-file bridge. It requires the original saved source and canonical material, protects assignment repair through effect commit, and rechecks claim time after waits. Actual inherited business publication, saved effects, producer and manager activation remain open.

The [enlisted task publication checkpoint](validation/2026-10-20-enlisted-task-publication.md) passes all 72 correctness cases, including real Process expiry before staging and after flush. Its one-second selected-guard performance reference failed for two writers (1006.85 and 1066.32 ms); current responsiveness is reopened and is not a green gate. Inherited producer/continuation and wider capacity remain open.

The [workspace path-probe repair](validation/2026-10-20-workspace-path-probe.md) passes all 75 selected cases. Exact internal path comparison removes repeated filesystem case-probe writes while preserving fresh containment/link validation and durable journals. The identical selected SQL-wait reference now has all six writers below one second (162.67Ă˘â‚¬â€ś670.72 ms). Earlier negative evidence is retained; sustained capacity and inherited runtime activation remain open.

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

The [registered inherited task participant](validation/2026-10-20-inherited-task-participant.md) passes 172 distinct behaviors across explicit correction boundaries: 124 final cases plus 48 unchanged authority/history cases. The single saved tool supports original Process material and interactive planning; schema 7 interrupted receipt recovery preserves work and denies dispatch. Release builds, full portability, exact input checks and observed resource reconciliation pass. Earlier failures and historical cleanup blockers remain recorded. Actual Process producer/manager activation, atomic outcomes, Workflow, settings/SPA and sustained capacity remain open. SPA stays on codex/concurrent-mutations.

The [atomic Process outcome checkpoint](validation/2026-10-20-atomic-process-outcome.md) passes 100 distinct behaviors across exact fixture-only correction boundaries: 18 final SQL cases plus 82 unchanged transition/compatibility cases. Both failed runs are retained. Full portability, architecture, source/runtime identity and observed resource reconciliation pass. Typed final-child completion shares the existing runtime/budget transaction and bounds final child validation. This proof covers the transactional prerequisite; actual producer and manager activation, Workflow, settings/SPA and sustained capacity remain open.

The [original Process child outcome reader](validation/2026-10-20-process-owner-outcome.md) has 129 distinct passing behaviors across explicit production and test-only correction boundaries. The reader preserves original work/effects after revocation, assignment repair and claim expiry, without publication locks or file mutation. Full portability, direct Release builds, architecture and source/runtime/resource checks pass. Failed candidates remain retained; two intermediate-directory cleanup attempts were rejected by automatic approval review. Actual result submission, producer/manager activation, Workflow, settings/SPA and sustained capacity remain open. SPA stays on codex/concurrent-mutations.
