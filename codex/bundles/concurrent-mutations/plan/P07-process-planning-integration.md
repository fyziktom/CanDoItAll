# Concrete inherited Process planning integration

Status: architecture refinement after signed exact-admission checkpoint `90ba35186`.
Step 1 now has [170 passing selected cases](../validation/2026-10-19-inherited-planning-coordinator.md).
The shared loop, explicit inherited participant/coordinator and original history scope
are implemented. SDK construction uses the existing compatibility options factory.
Step 2 now has [169 passing selected cases](../validation/2026-10-19-canonical-child-material.md)
for the neutral canonical child/material binding and reader, including bounded real SQL.
Existing Process/Workflow executor guards stay closed until the complete selected path
passes its owner, restart, publication and manager-authority proof.

Step 3 now has [185 passing selected cases](../validation/2026-10-19-inherited-workspace-lifecycle.md).
The registered workspace reuses preparation, credentials, activity and usage persistence
while a one-use capability holds ownership through completion. Retained reads bypass the
provider lease and preserve the saved input and scope. Concrete Process source, Workbench
publication and manager authority still need their product integration.

## Current boundaries and responsibilities

1. Process executors still refuse every non-null DecisionDataPolicy; EfProcessExecutionAuthorityQuery and Workflow authority also refuse. Do not lift until exact execution contract and business participant are recognized.

2. AgentFrameworkProcessStepExecutor preflights dispatch claim, canonical assignment, tool names/host evidence, subprocess/runtime-owned handlers, Agent readiness and tooling, original claimed metadata, parent artifacts, input contract. Insert selected governed path after applicable gates, preserving unsupported routes.

3. Legacy ProcessAgentExecutionAdmission scans source lineage through ExecuteSameSourceRunAsync, auto-approves generic tools and expects ProcessStepOutcomeResult. New bounded task planner must have explicit execution contract and outcome mapping; cannot silently route every Process Agent into it.

4. AgentPlanningCoordinator is interactive: requires governance and chat for prepare/review/actions; GetExecutionRunDetail currently routes any PlanningDecision to that review. Inherited API must avoid fake chat and expose owner review/outcome explicitly.

5. IAgentPlanningParticipant mixes common step prompt/PreparePublication/read with interactive PrepareRun/CaptureBasis. Introduce a common IAgentPlanningStepParticipant and leave existing IAgentPlanningParticipant as interactive extension so inherited participant need not implement throwing interactive methods. MAF runtime contracts should use common interface, registrations remain explicit.

6. Current planning publication lease/expectation and AgentPlanningDecisionOwner.RequirePublication require OriginalChatSession/latestRun. Need explicit inherited schema 7 snapshot mode and canonical parent source fencing, retaining all interactive6 predicates.

7. Current saved-effect dispatch uses ordinary background dispatch claim. Waiting Process manager has no active claim. Need explicit owner-authorized manager continuation, validating current manager rights and exact pending original proposal, not a bypass flag.

8. ProjectTaskPlanPublisher checks authority before its SQL transaction and again inside. Process publication fence requires enlisted ReadCommitted transaction, so add explicit inside-transaction hook instead of invoking fence in pretransaction RequireCurrentAsync.

9. Workbench references Process Application/Abstractions, not Persistence/Process product module or AgentFramework product module. Expose IProcessDecisionChildReader at Application boundary; implement existing bounded Ef query. Process product adapter composes workspace execution without leaking its concrete module factory to Workbench.

10. Process owner material uses neutral content contract and exact scope/reference, immutable timestamp. Core origin opaque parent envelope must be parsed/validated by Process module, payload/target by Workbench. No duplicate current capture or whole-history lookup.

11. Process child closure and budget settlement commit atomically via runtime UOW; unknown provider work never invents duration/refund. Parent refresh needs complete child quiescence; manager review retains allocation for later bounded work.

12. Core step observation now supplies validated inherited metadata-v3 scope to MAF history. The inherited fixture binds scope before source fingerprint admission; actual HTTP history proves project attribution. Interactive history behavior remains unchanged.

## Shared material and per-child context

Existing interactive ProjectTaskPlanAgentBasis contains WriteContext, whose caller binding is child-specific. It CANNOT be used as shared Process material unchanged. The shared content must be plain neutral ProjectTaskPlanBasisCodec bytes. An inherited child stores its own original finite WriteContext in its target envelope during new-only deferred admission, leaving the inherited basis payload/digest/time exactly the shared material. On retry no new WriteContext is prepared. Inherited codec/publication reconstructs the business command from neutral basis plus original target context; do not wrap/modify the basis or recapture current content.

The Process material fingerprint includes its scope, schema, reference, timestamp and
payload. Retain that complete stamp in the parent binding. Agent basis digest independently
hashes the exact payload. Validate both against the canonical material; do not substitute
one hash for the other. Enforce the smaller provider input bound, including original
instruction and target overhead, before dispatch.

## Execution lifecycle and duplicate ownership

Concrete lifecycle entry must be designed before adding a request branch. CompletePreparedExecutionRunAsync catches failures and updates run outcome; a duplicate inherited start rejected by journal CAS must never cause that wrapper to terminalize another caller's active run. Do not automatically execute every retained Preparing run. Prefer one lease/capability owned across startup and planning with exact restart/reconciliation disposition, or reuse existing created-only reservation semantics and explicit controlled resume. Resolve this before lifting guards.

Use the existing preparation/provider selection, activity, cancellation, history and
completion owners. Any reuse of `CompletePreparedExecutionRunAsync` must ensure a rejected
competing start cannot enter its terminal-failure path. The live journal lease/capability,
not an in-memory “created” boolean or a copied record, owns provider entry. Lost initial
acknowledgement must resolve the original child before any new work is considered.

First factor the common planning provider loop and usage accumulation out of the
interactive coordinator. Keep interactive admission/review/actions with that coordinator.
The inherited coordinator resolves canonical original data, checks its saved runtime
contract, acquires the explicit inherited live owner and uses exact inherited basis
loading. Both origins use the same loop. Do not make an inherited participant implement
interactive preparation or current capture methods that merely throw.

## Publication and source authority

The Workbench publisher acquires original source/catalog/Project protections before its
effect transaction. Add an explicit inside-transaction authority hook for the Process
root/generation fence; calling that fence in the existing pretransaction check is invalid.

A selected inherited execution must bind the exact admitted execution contract, original
child, material scope/stamp, allocation, profile, namespace and actual Agent. Preserve
legacy source fingerprint bytes. A new source authorization path must reject generic
schema-2 journals; admitting an inherited child must not enable ordinary unbounded tool
execution with fabricated parent metadata.

Manager publication needs current manager authority and the exact waiting proposal,
plus the enlisted root fence. It cannot revive an old dispatch claim or bypass source
policy with a flag. A closed original record may remain readable without permission to
dispatch new work. Original receipts remain the recovery authority after lost acknowledgement.

The [claim-lifetime repair](../validation/2026-10-19-publication-claim-lifetime.md) passes
106 selected cases. The dispatch fence now requires the exact active attempt and unexpired
claim after waiting for the root lock. Historical reading and manager-review claim release
remain distinct predicates.

## Process outcomes and accounting

The adapter's waiting/terminal outcome and the Process decision/budget command must commit
through the runtime owner's existing unit of work. Do not update Process phase separately
inside the adapter and then let the dispatcher overwrite the same runtime state. Awaiting
manager review closes the Agent work interval but retains its unsettled parent allocation.
Unknown provider/effect outcomes do not manufacture duration, refund or permission to replan.

Publication after sibling/parent reconciliation must fail at the root fence. Refresh is a
new parent-issued child only after complete quiescence, with original effects kept historical.
Grant sizing and reservations held during review require measured fairness/capacity evidence;
the current finite arithmetic alone does not establish acceptable throughput.

## Implementation and acceptance order

1. Extract the common planning step interface and loop. Keep the old interactive
   interface as its extension. Test both origins with the real MAF transport boundary,
   original input, finite work and provider failure/usage preservation.
2. Expose the existing indexed child query through a Process Application reader port.
   Implement strict parent binding and canonical material resolution at the integration
   boundary, with no Core-to-Process or Workbench-to-Process-Persistence reference.
3. Wire exact child admission into the actual workspace lifecycle with one live owner.
   Prove competing callers, interrupted initial saves, changed provider settings and
   unknown work cannot create or terminalize another caller's execution.
4. Compose Workbench inherited target/context, response codec and publication authority.
   Prove lost acknowledgement, current read/write revocation, namespace/lifetime changes,
   concurrent source changes, parent freeze and publication/refresh races in real SQL.
5. Carry decision phase/closure/accounting through the dispatcher unit of work and wire
   exact manager continuation. Prove actual restart and no whole-step replay.
6. Activate only registered, supported execution contracts in Process settings and
   producer admission, preserving the ordinary unsupported paths. Then integrate Workflow
   through its own selected contract over the same neutral material.
7. Run API/native/browser proof, measured same/different-root capacity and fairness,
   complete writer coverage and broad/static/resource closure.

This refinement does not replace the full bundle requirements or claim R5/R6 completion.

The [asynchronous binding prerequisite](../validation/2026-10-19-asynchronous-child-binding.md) now passes 192 selected cases. The new-only callback receives the actual candidate and source session, permits asynchronous business reads, then rechecks cancellation and original source authority before save. Retained paths do not renew the binding.

The [selected source-query prerequisite](../validation/2026-10-19-selected-child-dispatch.md) passes 86 cases. It preserves the original policy/Agent/claim and historical fingerprint with bounded metadata SQL, and observes expiry after awaited reads. It does not yet route the Process source policy or activate a producer.

The [inherited catalog/run snapshot](../validation/2026-10-20-inherited-publication-snapshot.md) passes 238 corrected cases and supports one optional launching Agent in the same ordered set as the executor. Inherited effect dispatch stays closed. Before the publisher hook, fix the source-reviewed initial Process root-before-Project path; preserve duplicate receipt recovery before current lifetime validation. If manager authorization requires a third independent Agent, acquire every required Agent in one bounded set rather than nesting another catalog lease.

## Initial Project/root ordering prerequisite

The [Project/root design](P07-process-project-lock-order.md) now has [194 distinct selected passing cases](../validation/2026-10-20-process-project-lock-order.md). Initial Process transactions acquire the complete original Project set before local/root advisory leases, while duplicate receipts remain ahead of current lifetime validation. Actual roots and child commits pass both arrival orders and cancellation races. The inherited task publisher still needs its explicit enlisted fence after Project locks and before business staging; the generic saved-effect and interactive guards remain closed to inherited publication.

The [selected publication fence](../validation/2026-10-20-selected-publication-fence.md) now validates the exact saved source, canonical material and contracts in the effect transaction, with assignment row locks and expiry checks after waits. Its 126 distinct passing cases retain the initial fixture failures and exact repair bridge. The next business adapter must use this selected entry and its narrow Application query port; the bare child fence is insufficient. Manager review retains an open child in the waiting phase without reviving its claim.

The [enlisted task publication checkpoint](../validation/2026-10-20-enlisted-task-publication.md) passes all 72 correctness cases, including real Process expiry before staging and after flush. Its one-second selected-guard performance reference failed for two writers (1006.85 and 1066.32 ms); current responsiveness is reopened and is not a green gate. Inherited producer/continuation and wider capacity remain open.

The [workspace path-probe repair](../validation/2026-10-20-workspace-path-probe.md) passes all 75 selected cases. Exact internal path comparison removes repeated filesystem case-probe writes while preserving fresh containment/link validation and durable journals. The identical selected SQL-wait reference now has all six writers below one second (162.67–670.72 ms). Earlier negative evidence is retained; sustained capacity and inherited runtime activation remain open.

The [live-parent saved-effect continuation checkpoint](../validation/2026-10-20-parent-saved-effect-continuation.md) passes all
201 selected cases on one frozen candidate. Core retains exact original parent authority,
receipt/claim ownership and finite work; MAF accepts an explicit inherited context/request
through its existing governed Process policy pipeline. Both earlier failed candidates
remain recorded. Five direct Release builds and full portability enforcement pass. Actual
Workbench/Process producer, workspace approval lifecycle, manager/historical recovery,
Workflow and capacity remain open; this does not activate inherited product publication.

The [inherited task receipt identity checkpoint](../validation/2026-10-20-inherited-task-receipt-identity.md) passes all 75 selected
cases. Explicit Process-Agent caller/source identity preserves ordinary receipt bytes and
partitions; kind 2 requires active work and matching origin. The two-check migration
preserves rows and refuses downgrade with inherited evidence. Actual inherited Workbench
adapter and Process producer remain the next integration; no product activation is claimed.

The [inherited task publication codec](../validation/2026-10-20-inherited-task-publication-codec.md)
passes all 124 selected cases. Original parent material stays shared; child-specific write
contexts, strict origin decoding and receipt semantics remain distinct. Two failed wire
candidates are retained and the full selection was repeated. Current source/executor
authority, actual inherited publication/producer and workspace approval remain next.

The [exact original Process source checkpoint](../validation/2026-10-20-selected-process-source-authority.md)
passes all 23 selected cases. Source checks select the original launcher from the held
two-Agent catalog without borrowing executor grants or acquiring another lease. The
earlier 22/23 result and exact test-only deletion-intent repair are retained. Current
executor authority, actual inherited publication and producer/approval integration remain next.

The [inherited task publication authority](../validation/2026-10-20-inherited-task-publication-authority.md) passes all 67 selected integration cases,
including 28 new cases. It binds original parent/live work, exact selected source/executor,
current profile generation and the enlisted Process fence through actual SQL publication.
The earlier 62/64 fixture result is retained. Historical receipt authorization, producer,
manager/approval activation and atomic Process outcomes remain open.

The [original inherited Process source read](../validation/2026-10-20-inherited-original-source-history.md) has 107 distinct passing cases across
an exact one-test-file repair. It preserves accepted source fingerprints after assignment
repair, expiry, closure and supersession while always denying dispatch. Four exact indexed
reads replace reliance on current assignment; the earlier 100/107 result is retained.
Product schema 7 receipt integration, producer/manager and atomic outcomes remain next.

The [inherited task receipt history](../validation/2026-10-20-inherited-task-receipt-history.md) passes all 64 selected cases, including 20 new cases. Registered schema 7 history retains original receipts after expiry/repair/closure, applies current disclosure fences and leaves workspace files unchanged. All 28 publication regressions pass with actual Process metadata. Exact SQL/catalog bounds, source/runtime identity, full portability and observed resource closure pass. Producer/provider activation, manager approval and atomic parent outcomes remain open.

The [root sequence registry repair](../validation/2026-10-20-root-sequence-registry.md) passes all 26 selected cases. Completed roots no longer retain semaphores, cancellation preserves waiting callers and disposal is concurrent-idempotent. The first single-mutex cost regression and all baseline/candidate measurements remain recorded; bounded registry partitions restore similar observed independent-root cost while retaining zero idle entries. Full portability and source/runtime checks pass. Probe output cleanup remains explicitly blocked by automatic approval review; sustained capacity is still open.

The [explicit initial inherited source admission](../validation/2026-10-20-inherited-initial-source.md) passes all 247 selected cases, including 16 new cases. Core admits the exact parent through an explicit owner observation and constructs schema 7 directly from its authorized session; ordinary source support does not imply inherited support. The initial 38/244 candidate remains retained. Full portability, architecture, source/runtime identity and observed resource checks pass. Registered Process source integration and complete parent-policy comparison are next; producer/manager/Workflow and capacity remain open.

The [registered inherited Process source](../validation/2026-10-20-inherited-product-source.md) has 153 distinct passing cases across an exact one-test-file repair. Initial and schema 7 observations use the registered owner and compare the complete original accepted policy; current generation, revocation and cancellation remain fenced. Full portability and source/runtime/resource checks pass. The retained performance review reopens whole accepted-launch decoding cost; qualify it before producer activation. Producer/manager, atomic outcomes, Workflow and capacity remain open.

The [accepted-source scaling baseline](../validation/2026-10-20-accepted-source-scaling.md) passes all 15 measurement executions but reopens capacity: a 1024-step source read allocates 31 MB and has a 71.075 ms median. The [persistence design](P07-accepted-source-projection.md) separates immutable original-source evidence from live authority checks, with explicit versions and atomic capture. Producer activation remains gated.

The [accepted-source codec foundation](../validation/2026-10-20-accepted-source-codec.md) passes 41 executions. Selected source bytes stay about 2.6 KB and allocations about 32 KB across the measured shapes, including 1024 steps and a 1 MiB prompt. The one-step codec timing tradeoff is retained. No runtime source path is switched yet; atomic storage, migration, actual SQL/capture cost and source-principal variants are next.

The accepted-source persistence checkpoint passes 193 distinct cases across an explicit one-test assertion bridge. Atomic capture, migration, corruption/rollback/retry and registered-source/history paths are verified. At 1024 steps the selected SQL source is 2319 bytes with 2.904 ms median versus 4449292 bytes / 137.054 ms legacy; initial acceptance still takes 2593 ms, so sustained capacity remains open. Actual producer/manager integration, Workflow and legacy conversion remain gated. [Candidate validation](../validation/2026-10-20-accepted-source-persistence.md).
