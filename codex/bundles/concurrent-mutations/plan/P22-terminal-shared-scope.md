# P22: finish shared material with the final governed root result

Status: qualified with P21 on 2026-10-23; [all 588 final cases pass](../validation/2026-10-23-process-planning-dispatch.md).
Design was reviewed before implementation. Required by the P21
validation failures; public policy producers remain closed until this protocol and
their own admission tests pass.

## Evidence and invariant

The original 425-case P21 run passed 415 cases. Eight failures were isolated to new
test fixtures. Two existing integration cases failed after an actual Agent owner
had closed: manager rejection and measured startup cancellation, both RunSnapshot.
Their runtime/decision/budget commit left the shared material scope Active. The
post-flush root cancellation guard correctly rejected the transaction.

Successful terminal results also lack a shared-scope completion. A final governed
root result must retain its original child settlement and finish the shared scope
in the same transaction. A descendant result must never close its ancestor's scope.
The cancellation guard, owner verification, exact work accounting, and publication
fence remain mandatory. No publication/provider call belongs in this transaction.

## Owners and minimum change

The existing result query already joins the root scope. Return its typed expectation
alongside the existing pending-work observation; do not add a source read, full launch
decode, registry, interface, project reference, or module dependency.

Both Application result coordinators attach a terminal scope command only for the
original root, a terminal result, and no remaining executable runtime steps. Completed
roots finish the scope; failed/cancelled roots cancel it. Early failure with remaining
steps does not claim quiescence. Mixed-contract last steps and failed-root recovery
are separate producer gates, not silently enabled by this change.

Runtime owns the pure eligibility/command checks and a quiescent terminal transition.
Persistence owns transaction ordering, authoritative root-work reads, and canonical
receipt encoding. The existing scope store remains responsible for shared scope
state; the unit of work only sequences those existing owners. Do not add a business
partial or move the ownership rules into Workbench/MAF.

Append distinct CompleteResult and CancelResult command values. Existing Complete and
Cancel retain their previous standalone reconciliation rules. New result commands
require a matching newly applied terminal strategy receipt, the original root, no
active local claim, no initial admission/material command, and a terminal runtime
whose executable steps are all terminal. Reject incompatible combinations before
staging. A bare status change is insufficient authority to finish the scope.

## Transaction and replay protocol

1. Select the original decision, budget, child, and scope expectation. Existing runtime
   CAS and the cross-process root mutation lock serialize the commit. A stale scope
   expectation fails; there is no automatic replacement or adoption of newer material.
2. Stage runtime, child closure, budget settlement, events and the idempotency record.
   Defer only the new terminal scope command. Existing capture/reconciliation commands
   keep their current ordering and validation.
3. Flush inside the existing SQL transaction. Load the actual persisted runtime and
   apply the existing result-acceptance checks while the scope is still active. This
   retains the original publication/result contract instead of exempting a closed
   scope from those checks.
4. Load the original scope and current root budget after the flush. Verify the exact
   scope expectation, complete membership/accounting sets, and the existing indexed
   root-work aggregate. All claims, decisions, descendants and allocations must be
   closed. An unresolved capture, sibling, local reservation or changed expectation
   rejects the entire transaction.
5. Close the scope and persist its receipt on that same idempotency row with a second
   flush. Run the existing final cancellation guard and expiry checks, then commit.
   Failure before/after either flush rolls back runtime, budget, scope, events and
   receipt together. An already closed external child remains recoverable from its
   original identity and measured outcome on retry.
6. Write version 2 only for new terminal-result scope receipts. Preserve canonical
   version-1 bytes and enum values. Old readers reject version 2; the new reader rejects
   down-versioned new commands and incompatible command/result phases. Exact command
   replay returns the original scope/accounting receipts without another transition.
   Historical completed receipts are read as historical evidence, never retroactively
   rewritten to pretend they included scope closure.

## Cost and concurrency risks

The result query adds two scalar columns to its existing indexed join. Only a final
governed root result adds a second SaveChanges and bounded scope/membership/allocation
reads under the existing root lock. No global/catalog lock or provider wait is added.
Membership and allocation limits remain enforced. The aggregate root-work query is
used only at closure; sustained capacity qualification remains open.

Checking quiescence before flushing would read the old active claim, open child and
reserved work. Overlaying just the selected child would miss other tracked rows and
descendant work. Two flushes in one transaction make the authoritative state explicit
without a second transaction or an eventually consistent success response.

The scope store must not hold stale tracked budget entities across the first flush.
Keep receipt update and final scope staging before commit; clear tracking at the
existing boundary and reload the one idempotency row when needed. Transaction rollback
and fresh-context reads, not mocked SaveChanges counts alone, establish atomicity.

## Required proof

- Pure transition/command tests: terminal success/failure/cancellation, stale scope,
  descendant/nonterminal/missing-result refusal, open claims/members/allocations,
  no material capture and terminal receipt version/canonical-byte compatibility.
- Actual PostgreSQL/MAF/file paths: manager approve/reject and measured startup closure
  in both snapshot modes. Assert scope and child/budget results, duplicate receipts,
  unchanged provider/publication count, and StepSnapshot absence of shared completion.
- Inject SQL failure after the first flush and after the final scope flush. Fresh
  contexts must observe the original runtime, decision, budget, active scope and no
  completion receipt. Retry the same original action once and replay it again.
- Reject stale scope and unclosed sibling/root work without a partial settlement.
  Preserve the two original failed cases and exact source/output identity bridges.
- Direct serial Release builds for Runtime/Application/Persistence and affected hosts,
  build-backed discovery, focused existing cancellation/scope/result/review tests,
  full portability-static enforcement and maintained architecture/validation records.

CodeAnalytics before snapshot: `snap-20261023072639-65134217`, scoped to Runtime,
Application and Persistence. Its collector diagnostics must accompany the dependency
comparison; a limited snapshot is not evidence of a globally healthy solution.
