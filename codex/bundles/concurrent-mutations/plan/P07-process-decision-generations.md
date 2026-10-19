# Process decision generations and child closure

Implementation refinement of [the runtime design](P07-process-decision-runtime.md).
This increment does not enable policy-bearing Process or Workflow dispatch. The existing
owner refusals remain until actual producer, material, child journal, publication and
manager-continuation integration is proven.

The pure lifecycle now has [29 passing isolated cases](../validation/2026-10-19-process-decision-generations.md).
Durable generation/child storage and the remaining integration sequence below are still
prospective; the tests do not claim cross-store atomicity or actual owner authorization.

## Responsibility and dependency decision

Process Runtime owns the pure state machine for one logical decision. Persistence will
apply its changes under the existing root transaction and retain exact command receipts.
Application verifies manager authority and child-owner evidence outside that transaction.
The Process module adapts Agent and Workflow owners; neither engine receives a reference
to Process Runtime. The business owner retains material and effect receipts. No new
interface, project reference, runtime partial, clock or service locator is needed for the
state machine. Its tests construct values, not the runtime host.

Use a small current-generation head and separately addressed children. The head carries
decision/generation, CAS revision, phase, immutable material and budget identities, and
enrolled/unclosed counts. Enrollment and closure inspect one indexed child and update
those counts in constant work. Reconciliation separately enumerates at most 64 children.
Do not put children or historical generations into runtime-state JSON. Persistence must
check a complete enrolled set when rebuilding or importing counts; a caller-supplied
count is never evidence of closure.

Keep decision identity independent of transport claims, retry attempts and child execution
identities. A child has an owner-issued occurrence ID, explicit Agent/Workflow owner and
original allocation ID. Its generation is part of its immutable binding. Reservation,
child lookup, provider input and effect authorization must all use that same binding.
The occurrence is reserved before crossing the file/SQL boundary. Recovery asks the
actual child owner about that occurrence even when no execution acknowledgement arrived.

## State and bounded retention

The supported phases are Active, WaitingForReview, Reconciling, Completed, Cancelled and
Superseded. WaitingForReview keeps children and allocations, but ordinary dispatch cannot
continue through it. Review publication requires a separate manager-authorized continuation;
a phase value alone grants no authority. Active publication and review publication have
different explicit state-machine checks.

Active or WaitingForReview can enter Reconciling. From that point, no new enrollment or
publication is permitted. Child closure can still arrive. Unknown, interrupted, missing
or cancelled executions are not closures. A closed child records an exact positive owner
revision and either an accepted operation receipt or a definite no-effect outcome. This
record is immutable. The application must obtain it from the owner, never from HTTP or
model-supplied metadata. Actual work settlement is a separate exact owner record and must
commit with closure before any allowance is released.

Only a Reconciling generation with zero unclosed children may become Superseded or
Cancelled. Completion likewise requires all children closed. Advancing creates generation
N+1, retains the old terminal generation, preserves the same decision/budget identity and
monotonically advances the CAS revision. Material is immutable within a generation. An
owner-authorized refresh supplies a new material identity; a transport retry cannot do so.
Completed and cancelled generations cannot be restarted by a different command. Exact
historical command replay is handled before current-head checks by Persistence.

Initial bounds are 32 generations per decision and 64 enrolled children per generation.
The existing shared ledger additionally caps allocations at 256 across the lineage; a
new generation never resets that limit or its consumed work. These bounds limit retained
rows and reconciliation work; they are not throughput claims. An unsupported launch plan
must fail before work, rather than truncating its children or silently reducing requested
allowance. Automatic replans also require the original policy allowance and an accounted
replan allocation. Manual review cannot reset any allowance.

The pure head validates counts and terminal shape, and all mutations compare the exact
decision, generation and revision. Checked revision arithmetic must fail before returning
a changed value. An exact repeated enrollment or closure at the current revision is a
read-only result; changed owner, allocation or closure evidence under an existing child
identity fails. A historical command retry will return its original receipt, rather than
pretending that a stale expectation still authorizes today's head.

## Integration and proof sequence

1. Prove the pure lifecycle, including both publication/quiescence orderings, stale and
   foreign expectations, changed child bindings, every child remaining open, exact replay,
   bounded capacity, corrupt retained shapes and overflow. Only in-memory values are used.
2. Add normalized head/generation/child storage with indexed exact reads, restrictive
   foreign keys and CAS. Enlist it through one narrow UOW collaborator, alongside budget
   and runtime state, with a strict original-command receipt. Prove rollback, restart,
   same-root races, unrelated-root progress and migration downgrade refusal.
3. Wire owner-verified child reconciliation and accounting. Verify all publication paths
   hold the existing root fence through their effect; preserve source/Project lock order.
   Never read file journals or call providers while holding the SQL root fence.
4. Add the actual governed producer and version handshake, inherited material, semantic
   child occurrence keys and durable manager continuation. Activate settings only after
   that real path and its browser/restart proof pass.

The baseline Process CodeAnalytics snapshot is `snap-20261019173059-d1ce2c09`: four projects,
958 types, 8,768 members, 24 entities, 321 findings, 190 existing workspace diagnostics and
zero dependency cycles. Builds remain necessary because those workspace diagnostics do
not prove compilation. Run affected production builds, count-confirmed owning tests,
the full portability-static scan and architecture/doc checks for each implementation
increment. This plan does not reclassify earlier failures or close the remaining R5 work.
