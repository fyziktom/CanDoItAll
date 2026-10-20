# Concurrent mutations implementation bundle

Current work resumed on 2026-10-18 under the [resumption record](validation/2026-10-18-resumption.md).
The original end-to-end outcome remains active. SPA work stays on its own
`codex/concurrent-mutations` branch and must not be merged into SPA main.

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
including all six unrelated writers below one second during a real SQL wait. Wider
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
