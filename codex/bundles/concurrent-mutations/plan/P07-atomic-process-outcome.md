# Atomic Process child completion

Status: implemented and validated after the signed inherited participant checkpoint
fd93e4e46; [validation](../validation/2026-10-20-atomic-process-outcome.md) records 100
distinct passing behaviors and the fixture-only correction boundaries. This is the
transactional outcome prerequisite of P07; it does not
activate a production producer, manager or Workflow.

## Problem and invariant

The current runtime commit already contains the step mutation, claim/result receipt,
budget settlement and one decision command. Closing a child decrements its open count
but leaves its generation Active. Completing the generation requires a second command.
A crash between those commands can leave a terminal step with an active decision.

A single explicit child-completion command must settle the exact original allocation,
retain the owner's exact work revision/effect receipt, close the last open child, set the
generation phase and commit the terminal step and claim release in one transaction.
Unknown work/effects cannot be converted to a no-effect closure to satisfy this invariant.
A receipt read alone never proves interrupted work accounting.

## Ownership and dependency gate

Scoped CodeAnalytics snap-20261020160906-8e205a26 covers Runtime, Persistence and
Application: 221 documents, 728 types, 6,651 members, 208 findings and 189 existing
diagnostics. Three scoped project edges contain no cycle. Direct csproj reads confirm
Runtime already consumes Drivers.Abstractions; passing runtime decision commands through
driver contracts would create a reverse dependency. Application will later read typed
child outcome evidence after adapter execution. No Agent/Core/UI reference belongs here.

Keep the current owners:
- Runtime command validates a typed optional completion.
- Pure generation transition checks exact expectation, phase and all children closed.
- Persistence decision store checks exact allocation/owner revision and proposed runtime
  state, inside the existing root transaction.
- Existing receipt codec versions the new operation shape and rejects changed replay.

No new project, interface, runtime partial, service resolver, global lock or background
worker is needed. The 1,116-line unit of work need not grow: it already stages the
decision/budget/runtime records together. The 318-line decision store owns this rule.
The codec's private wire records remain transport details, not architecture boundaries.

## Transition and compatibility contract

Add optional ProcessDecisionCompletion (Complete or Cancel) to ProcessDecisionClosure.
Null keeps existing closure behavior and canonical serialized bytes. Only new completion
commands write receipt version 4; versions 1-3 remain exact and cannot carry completion.

Complete is allowed from Active or WaitingForReview and closes the generation as
Completed. Its runtime step must be Completed or Failed: generation completion means
work is closed, not that business execution succeeded. Earlier accepted effects remain
retained even if later execution fails. Cancel is allowed only from Reconciling, writes
Cancelled and requires a Cancelled runtime step.

The last unclosed child must be the original selected child. Closure plus final phase is
one revision increment, with exact idempotent replay and unchanged material, budget and
receipt identity. Already-closed ordinary children do not silently gain completion;
their existing separate phase transition remains the explicit path.

Neither a live step claim nor a retained Claimed/LeaseRenewed/Reclaimed claim record may
remain. The original runtime mutation, child closure, phase, accounting and command
receipt roll back together. A lost acknowledgement replays the same command and cannot
reserve work or charge it twice. Changing/removing completion on replay is a conflict.

The codec validates the exact version, command kind, completion, resulting phase, closed
child count and revision. Unknown versions, null/missing completion in v4, completion in
legacy versions and mixed material/scope/completion shapes are rejected. Old binaries
explicitly reject v4 rather than interpreting it as an ordinary closure. No database
migration is needed; producer rollout must still account for older workers.

## Concurrency and cost

Reuse the existing ordered root fence and SQL transaction. Provider work and material
capture remain outside it. Final completion validates the bounded retained child set
(maximum 64) once, in addition to the exact indexed child/allocation lookup. It does not
decode accepted launch documents, scan runtime events, copy material or serialize an
entire workflow. Different roots retain independent progress.

## Validation and progression

Proof tier: Governed, using the bundle's existing plan/validation/retained-JSON shape.
Unit tests cover phase combinations, accepted/no-effect closure, one revision, exact
replay, changed identity, remaining children and invalid completion. Codec tests cover
legacy bytes, v4 round trip, downgrade/shape corruption and changed command replay.
SQL tests cover terminal step/claim matching, exact owner/allocation, multiple children,
rollback after flush and lost acknowledgement with unchanged budget/effect identity.
Use actual persistence transactions and the existing isolated PostgreSQL fixture.

Build Runtime/Persistence through their affected standard Release project/test outputs,
serialize shared-output builds, discover the exact focused selection before execution,
run bounded groups, enforce full portability without rewriting its baseline, compare
architecture and verify input/resource identities. The protocol change invalidates its
own codec/store/generation contract; targeted legacy, replay and dependent SQL proof
bound that scope. No unrelated unfiltered suite is selected. The earlier full Stable
failures remain recorded, not treated as green.

Entry gate: Pass for this outcome prerequisite. No product producer is enabled.
Closure requires code, exact discovery, all selected tests, architecture review, full
static enforcement and resource reconciliation to agree. Then connect the real
Application outcome reader and selected Process producer; manager approval/retirement,
durable capture and refresh, Workflow, settings/SPA and capacity remain required.
