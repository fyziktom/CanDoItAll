# Process capture ownership and dispatch attachment

Status: accepted entry design after signed result/accounting checkpoint `0ec44539d`.
This prerequisite does not activate the Process or Workflow executor.
Proof tier: Behavioral, with retained exact discovery/results, resource ownership,
source/runtime identities and full portability evidence.

## Observed responsibility and failure

`EfProcessRuntimeUnitOfWork.CommitCoreAsync` returns the original Applied outcome on
durable replay. That is correct historical command behavior, but it cannot authorize a
second external snapshot capture. A read-before-commit check has the same race. Losing
the acknowledgement after a reservation must not start another capture or refund work
whose completion is unknown.

The unit of work also has a parent-step precondition for launching a subprocess. It has
no exact current-dispatch precondition for attaching the current step's captured material.
An unchanged UpdatedAt timestamp does not establish an unexpired lease. Capture happens
outside the root transaction, so claim expiry/replacement and root cancellation must
be checked when its result returns.

The current owner is a 43-member Persistence type, observed through snapshot
`snap-20261020212822-5874c340`. It remains the transaction owner. New claim validation
belongs in a focused top-level Persistence guard, with a small Runtime contract.
No new partial, interface, project reference, provider dependency or composition service
is needed. Existing six scoped project edges and zero cycles remain the baseline.

## Commit observation

Add a nonserialized `CommittedNow` result property, default false. Set it only for the
newly saved result inside the unit of work. It reaches the caller only after the existing
transaction commit and disposal succeed. Historical lookup and duplicate commit return
false, including when the original Outcome remains Applied. Pure FromMutation results
remain false. An exception after commit exposes no fresh observation; lookup can recover
the receipt but cannot grant capture ownership.

This is a local observation of this invocation, not persisted authority or a public API
capability. JSON must omit it and ignore attempts to supply it. A producer must consume
it in the immediate reservation branch and invoke capture at most once; it must never
cache/reconstruct a fresh observation from the original receipt. The later producer
tests must prove that dependency, including two simultaneous reservation callers.

Changing the historical Outcome to Duplicate was rejected because existing callers
and receipts deliberately retain their original outcome. A new distributed capture
service or table is unnecessary for this observation; durable work ownership continues
to be represented by the original budget allocation and, for shared material, the
existing run-scope capture.

## Exact dispatch attachment

Add an optional typed precondition containing StepInstanceId, ClaimToken and OwnerId.
It applies only to a new commit; a historical receipt can still be read after claim expiry
without acquiring work authority. Reject malformed default identities before mutation.
The producer must provide it for capture reservation, material attachment and enrollment.
Existing unguarded command semantics remain unchanged until that caller is integrated.

Under the already-held root lock and transaction, the focused guard checks:

- an existing active runtime and active original root;
- the selected step is claimed or running with the exact token and attempt;
- the original claim has the exact owner, live status, no result and future expiry;
- the proposed mutation preserves that selected step and claim authority;
- any step-specific decision or budget operation addresses the guarded step.

Use the loaded state entity and its already-loaded steps/claims. Descendants need one
indexed scalar root-status read; root calls need none. Do not decode the full runtime
again, load material payload or add a broad child/history scan. No model, file, network
or business snapshot operation runs under this transaction.

Recheck the observed lease deadline after SaveChanges and the existing persisted-state
reload, before committing. A typed precondition exception rolls back all flushed runtime,
budget, material, event and receipt changes. Returning a rejection after flush would be
incorrect because the outer transaction could commit it. A new request cannot extend
its own captured lease or release/change its claim while attaching material. Other
operations own explicit claim renewal and result completion.

Root cancellation and claim replacement are serialized by existing root ownership;
expiry is clock-based. The final check is the admission point, not a guarantee that a
lease stays alive after the commit. Subsequent provider admission and publication still
require their own current checks.

## Producer and manager continuation sequence

The next vertical consumes these primitives through a Process Application capture
coordinator and a registered business capture port implemented by Workbench:

1. Read original immutable accepted-source policy before a child/material exists, with
   current read authority. Do not use the child query circularly or decode a whole launch.
2. Reserve a bounded capture allocation under the exact dispatch precondition. Stable
   command/allocation identity includes original owner and logical generation.
3. Only the invocation observing CommittedNow may capture. Duplicate/incomplete work
   returns a typed pending result and retains the winning claim and reservation.
4. Capture the coherent business basis under the existing bounded Project transaction
   (five-second transaction, two-second statement/lock limits), outside the Process lock.
5. Attach material and settle measured closed work under the exact claim fence. Failure
   or unknown acknowledgement retains work until original evidence can reconcile it;
   do not substitute the granted amount as measured actual work.
6. StepSnapshot uses its own scope. RunSnapshot is owned by the root's existing shared
   scope; descendants inherit active material and cannot capture on the parent's behalf.
7. Enroll the actual child before Core's inherited workspace admission. Bind the candidate
   and authorized background session through the existing inherited participant seam.
   Process Application must not reference Agent Core; Workbench must not reference
   Modules.Processes or Process Persistence.
8. Manager approval uses current manager authorization and the exact displayed proposal
   through the saved-effect continuation owner. It cannot revive an expired dispatch claim.
   Refresh creates a new material/generation/child/proposal after old work is reconciled;
   it requires new approval. Cancellation preserves unresolved reservations.

The existing executor refusal remains until the complete registered path and manager
actions are tested. This record accepts the commit/claim prerequisite only; remaining
producer bindings and manager commands require their concrete owner design before edits.
SPA remains on `codex/concurrent-mutations` without a main merge.

## Validation and invalidation

Build Runtime and Persistence directly, then refresh Unit and Integration in standard
Release, serializing shared outputs. Reuse the isolated PostgreSQL 18 fixture on 53619.
Record ownership before the candidate directory, observe database leases/processes,
verify identities and preserve unrelated resources.

Existing owning selections are 77 ProcessPersistenceStoreTests and 21
ProcessDecisionBudgetPersistenceTests. Add focused contract and SQL precondition cases;
record their exact expected counts before discovery/execution. Test fresh versus replay,
lost acknowledgement, rollback, concurrent same-command ownership, malformed/forged
identity, expired and replaced claims, root cancellation, expiry while waiting for the
root lock, post-flush expiry rollback and valid material attachment. Verify omitted JSON
authority and no change in legacy behavior. A dependent real reservation/material flow
must prove that an unowned replay cannot initiate another capture.

Source, claim semantics, transaction sequencing, guard scope, fixture ownership and
selected test changes invalidate their affected checks. Architecture closure must show
unchanged dependencies and a cohesive independently exercised guard. Full portability
must pass without baseline rewriting; inspect any findings and repair real defects.
Broad Stable is deferred to the named final checkpoint after activation, not run merely
because this prerequisite closes. The historical 17 failures and 2444 unexecuted cases
remain open and are not covered by this focused proof.
