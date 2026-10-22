# P11 A: Account closed children of terminal Process steps

Status: qualified by [178 passing cases](../validation/2026-10-21-terminal-child-accounting.md)
after signed P10 checkpoint `788da8a6f`. This slice does not activate a producer, root cancellation finalization,
Agent stop, automatic recovery or snapshot refresh.

## Problem and owner

Cancellation can win after an Agent effect commits but before its Process work settles.
The waiting-review coordinator correctly refuses a terminal step. Add a separate narrow
Application coordinator for exact original accounting. Keep the business runtime outcome
unchanged, including an accepted child effect under a cancelled Process step.

Reuse the accepted-input reader, indexed result query, registered outcome readers,
idempotency store and existing root-fenced runtime unit of work. There is no Agent
mutation/provider dependency, new project reference, schema or persistence abstraction.
Register it in the existing Process module composition root. Application baseline
`snap-20261021233051-8c37828d` has 130 documents, 369 types, 4,026 members, 459 edges,
103 findings, zero cycles and zero diagnostics. Actual Application/Process module
project references are eight/26; neither set needs to change.

## Exact request and receipt

The typed request binds original child address, displayed decision and budget revisions,
displayed Completed, Failed or Cancelled step status and a retained action ID. Skipped
steps are unsupported. Require an identified actor. Validate its shape and original
accepted input, assignment, plan, admission and execution contract. Accounting introduces
no business result, branch, artifact or outbox work, so it does not impose the review
coordinator's single-activity business-completion restriction.

Derive a versioned command identity from the request, original accepted input fingerprint,
canonical child binding and actor. Look up that receipt before current phase admission.
Exact retries recover after acknowledgement loss without reading the Agent outcome again.
Another action or actor cannot adopt that receipt. Validate original material, grant,
child closure, allocation settlement, owner revision, terminal status and decision phase.

New accounting requires the selected step to be terminal, its displayed status unchanged,
and no selected live claim. A stopped root alone does not bypass descendant cancellation.
Compare the actual current decision head/reference and revisions. ChildObservation.IsCurrent
also requires an Active shared scope, so it remains a publication predicate and is not
used to prevent accounting during shared reconciliation. Never weaken its existing meaning.

Use the registered outcome reader outside SQL. Missing, active, approval-pending, unknown
or inconsistent work cannot become a settlement. A known closed outcome retains its
actual effect receipt and measured work, even when the Process step is cancelled.

Commit exact closure and settlement together. Advance only the canonical microsecond
runtime concurrency timestamp; preserve all business state, claims, results and events.
If other local work or children remain, retain the decision phase. Otherwise choose
Cancelled for a cancelled step and Completed for a completed/failed step. Shared scope
reconciliation does not block local settlement or become an implicit scope transition.
A previously settled child may close a nonterminal decision with the same retained work;
the ledger must not charge twice. New actions against terminal decisions refuse.

## Concurrency, failure and dependencies

The existing root lock/CAS protects runtime, decision and budget at commit. Provider,
file and authority operations never run while holding it. Lost commit acknowledgement
requires the exact retained request. Cancellation before commit leaves work pending;
after commit it cannot undo a receipt. Concurrent actions cannot both charge. Current
runtime must be reloaded by the future product boundary when displaying a historical
replay; this lower-level owner returns its explicit receipt.

Root cancellation requires a later durable quiescence check inside the root transaction,
plus supported owner stop/recovery before that check is enabled. The old notification
observer alone does not prove quiescence. Live/remote/crashed work cannot be declared
closed from a cancellation signal. Refresh likewise needs a committed publication barrier,
complete original accounting and one successor material scope, with cumulative budgets.
The broader investigation stays in the owned preparation record until its design is ready.

The continuation audit found that rework of a previously Failed step can reactivate a
CancelRequested root. Repair that runtime and persisted barrier before using it as a
permanent Agent-stop capability. This coordinator still preserves its displayed terminal
step and uses runtime CAS in the same SQL commit; the finding does not invalidate its proof.

## Test and release gates

Use isolated existing query/input/outcome/UOW fakes for request shape, terminal statuses,
pending reasons, missing/mismatched original input, current head/budget, live claims,
shared reconciliation, partial accounting, overrun, exact replay, other actor/action and
corrupted receipt. Prove unchanged business state and no duplicate charge.

Use real existing SQL/file fixtures for both snapshot modes, approved/rejected closed
children after parent stop, lost acknowledgement and a fresh scope, competing CAS,
rollback, exact root/local budget charge and sibling preservation. The original provider
and Project effect counts must remain unchanged. Include existing review/result/decision
accounting regressions, with exact source selection and built discovery before execution.

Build changed owners and refresh standard Release Unit/Integration outputs serially.
Run full portability scan/review/final enforcement, rendering/docs, architecture comparison,
frozen source/runtime identity and owned-resource reconciliation. Reuse isolated PostgreSQL;
preserve ordinary port 5032 and historical cleanup-denied resources. No SPA change or merge.

P10 shows service construction and current manager checks are small parts of read latency.
Continue bounded Agent/read and database-connection diagnostics without weakening authority
or relaxing the one-second acceptance limit. This slice does not qualify capacity.
