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

6. Current planning publication lease/expectation and AgentPlanningDecisionOwner.RequirePublication require OriginalChatSession/latestRun. Need explicit inherited schema7 snapshot mode and canonical parent source fencing, retaining all interactive6 predicates.

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
