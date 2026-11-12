# Waiting Process result completion — design review

Status: implemented and [qualified with 331 final cases](../validation/2026-10-21-process-review-completion.md).
Original compiled-step requirements, execution accounting and manager-review admission
are qualified prerequisites. This work does not activate the executor or expose a manager endpoint.

## Problem and evidence

The real manager-publication test reaches a committed Project receipt with the original
Process step still blocked. The first NeedsManager receipt has correctly closed the
dispatch claim. ProcessRuntimeEngine.PrepareStrategyResult requires Running plus a
matching unexpired claim; constructing a synthetic Running state or renewed claim would
manufacture authority and destroy the original history.

The reusable baseline is scoped snapshot snap-20261021090234-c953f4c6 over Process
Application, Persistence, Runtime, Builder and PostgreSQL Migrations. Its six in-scope project
references and zero cycles are unchanged by the test-only clock repair. The retained
189 existing collector diagnostics remain a limit; direct affected builds pass.
No new project reference, Runtime partial, journal schema or HTTP flag is justified.

## Responsibility inventory

- Runtime owns a pure waiting-to-terminal transition, exact original result identity,
  result/event/outbox history and terminal-run calculation.
- Application owns original child selection, stable receipt replay, reading the actual
  child's closed effect/work outcome and assembling one atomic accounting request.
- Persistence owns current root/step/generation/budget checks under the existing root
  transaction and exact child closure plus budget settlement.
- Workbench owns Project task-plan outcome evidence. Its current outcome reader remains
  authoritative; the caller must not assert success or measured work.
- The later manager action owner owns current caller authorization and exact displayed
  Agent/Process expectations. This result seam is not itself a public manager endpoint.

## Bounded contract

Introduce a typed review-completion request naming the original run/step, original waiting
result key, child execution identity, displayed decision expectation, budget expectation
and new idempotency key. Completion derives terminal outcome and effect/work evidence
from the existing registered child outcome reader. The request contains no trusted
terminal-success boolean and cannot substitute a child or new generation.

The shared Application validator supports the registered single-activity contract with no
required or produced artifact slots, runtime tool or host requirements, subprocess
mappings, branch gates or branch outcome obligations. It requires Activity kind without
StartsSubprocess and checks both indexed original requirements and current runtime fields.
Process Application does not reference a Workbench contract constant; the original
policy execution contract must match the selected registered child contract. Unsupported contracts must refuse before child enrollment in the
executor and again at completion. Do not silently drop artifact or host obligations.
A future richer contract must extend this owner explicitly and gain matching evidence.

Runtime can check obligations retained on ProcessRuntimeStepState. Branch and assignment
obligations absent from that snapshot must be checked through the existing indexed
IProcessAcceptedExecutionInputReader in the review application entry. Use original accepted
input, not the mutable assignment. This is one bounded selected read for the uncommon
completion action; it adds no query to ordinary dispatch or every live-work checkpoint.
Sharing the same explicit supported-contract validator with executor preflight prevents
the two entries from accepting different obligations.

The pure transition must require:

- a nonterminal, non-cancelling run and the exact waiting/blocked executable step;
- no active claim token and no open claim for that step, including an expired open claim;
- exactly one completed claim retaining the original waiting key and step attempt;
- the latest result for that step is the specified original NeedsManager receipt,
  with the same selected execution and strategy;
- a new completion key distinct from the waiting key; no contradictory receipt replay;
- supported terminal disposition derived from verified child evidence;
- strictly retained earlier receipts, claims, artifacts and unrelated steps.

Append one terminal result receipt with a digest derived from original waiting identity,
selected child, terminal disposition, exact child closure and measured work. Emit step and, when applicable,
run terminal events/outbox entries. Do not emit another DispatchClaimCompleted event.
Preserve the original NeedsManager receipt and its original claim unchanged.

Share terminal-run calculation with ordinary strategy result completion by moving the
existing method into its existing terminal-state owner. It has no I/O and should be tested
directly. Share the bounded event/outbox construction where it removes actual duplicated
logic; do not add a generalized pipeline, new interface or large facade.

Concrete placement: extract the existing ProcessRuntimeTerminalStates type from
ProcessRuntimeState.cs into its own file and move CompleteRunIfTerminal out of the
Engine partial into it. Add Applied/Duplicate construction to ProcessRuntimeMutation,
which already owns Rejected construction. Existing Engine helpers delegate to these
factories. A small Runtime event factory can take the existing state/context and retain
the existing envelope validation; Core must not reference Runtime. The independent
ProcessDecisionReviewCompletion owner uses these shared primitives and does not call
back into ProcessRuntimeEngine. This avoids extracting the entire generic strategy-result
helper cluster merely to support the narrower reviewed terminal path.

## Application accounting

Resolve the exact original selection and derive a separate versioned review command identity.
Bind immutable review input, caller audit identity and original child/material/budget; do not
include mutable current head revisions in place of the displayed request values.
Check its durable receipt before requiring the child to still be open, so lost acknowledgements
can recover after settlement and restart. Validate both original waiting and completion receipt,
run/step/child/material/allocation and exact closure/work identity on replay.

Do not let a caller provide a new generic StrategyResultEnvelope as the authority for
review completion. The Runtime input consists of typed original identities, the new
result key, terminal StrategyOutcome and validated ProcessDecisionChildClosure.
The Application owner maps the registered child's observed terminal status to that
input. Digest and safe receipt text are generated by the completion owner. Failure and
cancellation remain explicit terminal outcomes; neither implies that an already accepted
effect was undone.

Factor the existing outcome-read/protocol-validation responsibility out of
ProcessDecisionResultCoordinator and reuse it from the review coordinator. Keep its explicit
I/O-to-Pending behavior, actionable masked log, original contract selection and closure/work
revision validation. Do not duplicate accounting rules or introduce service location.

For a fresh command require the displayed current decision and budget expectations, a
WaitingForReview head, current original child, no other unresolved local/shared work and
no prior inconsistent settlement. Pending child evidence refuses completion without a
new terminal result or refund. Closed evidence produces ProcessDecisionClosure and
ProcessDecisionBudgetSettlement in the same existing IProcessRuntimeUnitOfWork commit.
Never convert unknown work to a zero-work failure or automatically approve again.

## Transaction and races

Persist the runtime transition, new result receipt, decision closure, budget settlement,
events and outbox atomically. Retain existing ProcessDecisionResultAcceptance checks.
A waiting completion also needs an enlisted root-state check: a child run's unchanged
local snapshot alone must not allow it to finish after root cancellation. Reuse the
existing root lock order; do not take a separate global manager lock.

Competing completion requests must either replay the same original command or lose the
existing state/head/budget comparison. Neither may charge work twice. Refresh or another
manager changing the generation must refuse the stale original expectation. Settling
already committed work after root cancellation belongs to reconciliation and must not
resurrect a cancelled runtime outcome.

Current caller permission must be checked by the manager action owner before access,
after waiting for a lease and in its publication authority; this pure/result accounting
seam cannot manufacture that permission. Keep executor activation blocked until the
actual action and result owner are wired and tested together.

## Validation contract

Pure tests directly construct the waiting state and call the new transition, without
ProcessRuntimeEngine or a database: three terminal dispositions, exact receipt/child/
step/key substitutions, stale latest result, live or expired-open claims, cancellation,
unrelated-step preservation, unsupported obligations, event/outbox semantics and terminal
run aggregation. Ordinary result tests cover shared terminal/event code and existing
artifact finalization behavior.

Application tests exercise unchanged dispatch accounting plus review pending/malformed
child evidence, exact expectation mismatch and receipt-first retry. Real PostgreSQL/
MAF tests approve and publish once, then close the Process child and settle measured work;
preserve both receipts and one provider call. Include competing completions, lost parent
acknowledgement followed by service restart, generation and root cancellation before
commit, and rejection/no-effect closure when the action owner is added.

Select exact methods/counts after implementation, freeze one candidate, build affected
owners then test outputs in Release, run portability enforcement, compare scoped architecture,
observe the existing isolated fixture and retain failures as well as passes. This seam
does not claim Workflow integration, public manager actions, sustained capacity or E2E
activation. Those remain explicit later acceptance requirements.

## Source-confirmed implementation decisions

The pure input includes the full original child address, waiting strategy/key, new result
key, terminal disposition, validated closure and exact work settlement. Its result digest
binds these fields and the waiting receipt hash in a separate versioned domain. This
also detects consistent substitution of work in both the accounting command and allocation.
Caller time does not enter it.
The original waiting result must be the latest by AppliedSequence, not array position.
The new receipt uses the same positive monotonic sequence rule as ordinary results;
move that rule to the existing receipt owner and delegate from the old Engine method.
Use checked arithmetic to refuse sequence exhaustion rather than wrap to an invalid value.

The original accepted input supplies its fingerprint and policy; the Application identity
binds that fingerprint, the request's displayed expectations, original material/allocation
and caller audit identity. It must not derive identity from later mutable head revisions.
The durable receipt is read before open-phase checks or reading current child outcome.
Verify original waiting and completion results plus the exact closure/work and allocation
on replay; mutable current runtime status cannot invalidate an already accepted receipt.

StageClosureAsync and ProcessDecisionTransitions.CloseChild already support a child
whose matching closure/work was settled while its head stayed WaitingForReview. Preserve
that path: exact closure and settlement may repeat while the head advances, without
charging twice. Inconsistent retained closure or settlement remains an error.

Put the enlisted root check in the existing ProcessDecisionResultAcceptance owner. Derive
it from a new governed terminal receipt changing an original waiting/blocked step,
not a caller-supplied permission flag. For a child run, read the root's current status
under the existing root transaction lock and reject cancellation or terminal status.
For the root run itself, use its original state protected by the commit comparison;
the flushed proposed terminal root status must not reject its own valid completion.
Accounting reconciliation that leaves stopped runtime state unchanged remains possible.
This adds at most one root-status read per child review completion, none to ordinary
Running-to-terminal strategy results, and no new lock or polling loop.

Only one selected input, one existing selection, one outcome read and the existing atomic
commit are allowed. No full plan read, whole-run child scan beyond the existing bounded
256-allocation protocol, unbounded retry, new table, or global manager lock is needed.
The application coordinator, pure transition and original requirement validator are
independently testable. The old result coordinator loses its outcome-read/validation
implementation; Runtime Engine loses terminal aggregation and event/outbox construction.
