# P07 Process policy admission and governed execution

Source review 2026-10-19, after signed checkpoint `623686202`. This refines
[the runtime-policy design](P04-P07-runtime-policy.md) and
[the nested decision review](../reviews/P04-P07-nested-decision-review.md).
No Process or Workflow policy activation is claimed by this design.

The first admission/storage foundation now passes [68 focused cases](../validation/2026-10-19-process-policy-admission.md).
Both current module executors explicitly reject non-null admission before child work.
The following governed-execution, producer and continuation requirements remain open.

## Concrete incompatibilities and responsibility split

`AgentFrameworkProcessStepExecutor` currently requests auto-approved tool calls and
the `ProcessStepOutcomeResult` structured contract. `AgentPlanningCoordinator` requires
an interactive chat, explicit publication review and its own output/material contract.
Setting `PlanningOwner` on the ordinary Process request is invalid. Removing those
guards would weaken approval and produce an incompatible Process result.

Keep ordinary Process execution unchanged until an explicit governed planning executor
can provide its own reviewed-result adapter and durable waiting/continuation behavior.
Process definitions/steps may eventually select the policy, but new preferences must
never be silently accepted by a legacy executor. Do not expose a selectable Process
policy control before the actual executor, publication fence and resume path pass.

| Responsibility | Existing owner and change boundary |
| --- | --- |
| Shared policy vocabulary/resolution | SharedKernel; reuse its immutable policy, overrides, mandate and budget intersection. No database or product references. |
| Immutable Process policy admission | Process Runtime data contract, carried on the immutable step assignment. Keep source settings, definition version, step identity and resolved value together. |
| Preference capture | Process Application launch preparation via a narrow owner-owned input port; composition supplies workspace preferences. Do not reference Workspace, MAF or Workbench implementations from Process Runtime/Application. |
| Actual Agent/Workflow child dispatch | Module adapters, which already reference those runtimes. Use an explicit supported execution contract; no fabricated chat or automatic approval. |
| Publication/supersession | Process Persistence, using the existing enlisted root transaction lock and exact claim/readiness/source checks. Keep current decision generation through the effect commit. |
| Material basis and effect receipt | The business owner (initially Workbench task planning), not Process/Agent runtime. Preserve exact saved basis and historical receipt. |
| Durable pause/resume | Existing Process manager/runtime continuation ownership, extended with a typed decision reference. A status string or UI card alone is insufficient. |

No new runtime partial or global coordinator is justified. Policy serialization and
validation must be independently testable without constructing the large launch service.
Launch integration should delegate capture/resolution rather than add policy logic to
its existing partial cluster. Workflow gets a separate occurrence-owner integration;
its definition version alone does not fence a child publication.

## First foundation: immutable admission and compatibility

Add a versioned, bounded Process admission value containing workspace namespace/head
revision, immutable Process definition version, logical step identity, workspace policy,
definition/step/authorized-invocation overrides, owner capabilities and enclosing budget
ceilings. Recompute the resolved value from this retained provenance and reject a
mismatch. These values are supplied by the admission owner, not trusted from an API body.
Use the existing typed Process IDs and SharedKernel policy; no Agent-model dependency.

An optional assignment property represents legacy omission only. Omit it when writing
null so historical serialized initial commits keep their bytes. A new non-null admission
uses an explicit schema/hash domain, strict known/required/unique JSON members and a
small UTF-8 bound. Prepared-launch decoding validates definition/step bindings. Assignment
repair cannot add, remove or change it. Persist it in a nullable bounded text column;
legacy rows remain null. Never populate old rows from today's workspace settings.

This foundation may be committed with runtime activation closed. Its acceptance is
real serializer, immutable-assignment store and PostgreSQL migration/reload proof,
including unchanged legacy prepared-launch fingerprints. It is not proof of a working
Process policy setting or a producer path. The actual launch producer is a separate
required successor, and must be proven before any activation claim.

Do not add public definition/step preferences under a format that older executors
silently ignore. Activation requires an explicit supported executor/format handshake
and a rollout rule excluding older runtime writers; compatibility scanning alone is
not an execution fence. This must be resolved before exposing preferences or enabling
new admitted policy values through product entry points.

## Runtime semantics and lock constraints

RunSnapshot admits one bounded business scope before dependent provider work and shares
that exact material with enrolled children. StepSnapshot captures at each new logical
decision, not on transport retry or claim rotation. Retrying the current Process
run/step continues to use its existing same-source Agent reservation. Semantic replan
requires a persisted successor dimension in Agent source reservation, Workflow child
lookup, idempotency key and intent fingerprint together. Never derive it from a new
claim token or caller-chosen GUID.

The current `EfProcessExecutionMutationGuard` acquires the root advisory transaction
lock and rechecks runtime/assignment rows using the effect owner's enlisted context.
Supersession must take this same root fence and reconcile every affected enrolled child
before replacing a generation. The detailed lock order must preserve the existing
Project lifetime, dataset and catalog read leases; do not hold a SQL transaction over
model work or acquire a non-reentrant Project lease twice. An Agent file-journal update
and SQL publication are separate commits, so uncertain old effects prevent replanning
until their original receipts are reconciled.

Persist work/replan charges on the decision lineage. Resumes and child retries cannot
reset ceilings. Pause releases claims/resources and stores a typed resumable decision;
manager refresh creates a new basis and approval intent only after no-effect or exact
receipt reconciliation. Historical accepted work remains history. Current permission,
source lifetime and capability checks remain mandatory even for retained snapshots.

## Required proof and performance

First prove exact legacy payload/fingerprint compatibility, strict malformed/adversarial
policy decoding, immutable repair rejection, prepared identity binding, PostgreSQL
upgrade/reload and clean-model migration consistency. Then prove actual launch admission
with changing workspace/definition settings, same-source recovery, real provider input
equal to the admitted basis, durable manager pause/restart, and both supersession versus
child publication orderings. Include mixed unsupported executor rejection before any
provider call. Workflow proof must inspect its persisted occurrence and child lookup.

Bound policy JSON and hierarchy depth. Read workspace/definition preferences once per
admission, retain them, and avoid per-tool settings reads or full-catalog serialization
inside Process SQL locks. Root contention is scoped to one Process lineage; benchmark
unrelated roots during blocked publication and measure capture/provider/admission work
separately. Whole-catalog Agent revisions and full retained operation history are not
substitutes for narrow Process decision revisions. No sustained capacity claim follows
from the preceding external-Agent or native-chat checkpoints.
