# P28: parent-owned Process refresh and replan

Status: atomic admission passes its [343-case qualification](../validation/2026-10-23-process-refresh-admission.md),
including migration, historical readers and portability. Original-child stop passes its
[73-case qualification](../validation/2026-10-23-process-refresh-child-stop.md). Reconciliation
accounting is next; full refresh activation remains gated by the owner/delivery checkpoints below.
Prerequisite P27 is
signed backend `8b11d7d1c` and SPA `1139939`; both worktrees were clean at entry.
Behavioral proof tier. The current public contract still supports Fail/Pause only.
This design must be qualified through the actual producer before advertising Replan.

## Outcome and ownership

An operator can explicitly replace the material of the existing single-activity planner;
an admitted automatic policy can later use the same mechanism after verified changed
material. The parent owns generation changes, the finite budget and retirement of its old
child. The Agent continues using its original material until its owner closes that work.
No background subscription mutates provider input, and no child independently spends a
parent replan. Keep the original Process, source authority and accepted preparation.

The existing `ProjectTaskPlanProcessChildStopper` requires a durable root cancellation
barrier. Refresh must not manufacture that barrier: it would cancel the business Process
and prevent a successor. Existing terminal accounting likewise requires a terminal step
and must not be weakened to account for an active refresh. Introduce explicit refresh
authority and reconciliation accounting at the existing owner boundaries.

| Responsibility | Existing owner and planned change |
| --- | --- |
| Exact generation, claims and phase rules | Processes.Runtime: independent typed refresh admission and pure transition; no engine partial growth |
| Admission, bounded progress and replay | Processes.Application: cohesive refresh coordinator using original accepted input/result and receipt queries |
| Atomic fence, durable intent and accounting | Processes.Persistence: existing runtime UOW and decision/scope stores; no independent transaction in a collaborator |
| Original child stop and accepted-effect evidence | Workbench adapter plus existing Agent Core stop/outcome owners; no Process reference in Core |
| Authentication and explicit action transport | Existing Web/Workbench review boundary after the producer is qualified |
| Rendering and retained request | Existing SPA review session and controls, on `codex/concurrent-mutations` only |

Application must not reference Workbench. Persistence implements Application contracts;
Runtime remains provider neutral. Reuse the real child owner rather than adding a generic
workflow engine or giving SQL a callback into Agent journals. No new project reference,
service locator or business partial is planned. A separate reconciliation-stop contract
is justified only if it preserves the current cancellation contract and its negative tests;
share the Workbench stop mechanics after each path verifies its own durable authority.

## Required state sequence

1. Admit one exact refresh intent against the original waiting result, child, decision,
   local budget and, for RunSnapshot, shared scope. Verify accepted single-activity input,
   current caller/source/profile, nonterminal root and completed original dispatch claim.
   Manual refresh requires a review-capable policy. Automatic refresh additionally needs
   the original Replan policy, a verified changed-basis outcome and available replan credit.
2. Under the existing root fence, atomically retain the intent and move the local decision
   to Reconciling. For RunSnapshot, move the original shared scope to Reconciling in the
   same commit. Stop new child enrollment, joins, claims and publication before calling
   any child owner. Do not reserve a replacement grant at this point.
3. Outside SQL, stop/reconcile the exact old child through its owner. Authority must bind
   the original root/run/step, decision generation, child and admitted refresh, plus the
   shared material generation when present. Recheck it inside the Agent dispatch lease.
   A generic root-is-stopped check, manager identity alone or expired claim is insufficient.
4. Read the retained owner outcome. Missing journal, active work, lease contention,
   unresolved external effects and lost acknowledgement stay pending with their original
   grants reserved. A closed proposal-only child can settle its actual work once while
   the Process step remains blocked and the decision remains Reconciling.
5. Reenter the root fence and prove complete quiescence: exact bounded memberships,
   original children and allocations, no live claims, no unknown work, no terminal root
   and no concurrent cancellation. Counters or a truncated list alone cannot authorize it.
6. Reserve one bounded capture grant and persist its original identity before reading
   business data outside SQL. RunSnapshot captures once at the root; StepSnapshot captures
   for the step. Complete capture with exact closed usage and original content identity.
   Manual refresh charges work but no automatic replan. Automatic RunSnapshot refresh
   charges one root replan; local inheritance charges zero. StepSnapshot charges one
   linked local/root replan. Preserve overruns and unknown capture outcomes explicitly.
7. Advance to one successor material/generation under CAS, retaining the retired history.
   Rebind an unfinished RunSnapshot member to the new parent material through its owner.
   Make the step Ready only after old work is settled and the new material is durable.
   Enroll a new deterministic child for that generation and dispatch once. Old proposals,
   manager approvals, claims and receipt identities cannot authorize new publication.

Every step is bounded and restartable. An HTTP request may report admitted/pending without
claiming that new material exists. It must not hold a SQL transaction, Project/source
lease or Agent dispatch lease while waiting for another owner. Existing generation (32),
child (64) and lineage-allocation (256) bounds remain; retries do not create fresh IDs.

## Publication race and cancellation

The first refresh fence can lose a race to an already accepted business publication.
An accepted effect is a historical fact. Never cancel it away, refund its consumed work
or create a second proposal merely because its journal acknowledgement arrived late.
Reconciliation must classify that original result, complete its original accounting and
report that refresh was overtaken by completion. Unknown effect outcomes remain pending.

Current `ProcessDecisionRunScopeTransitions.FinishResult` accepts successful completion
only from Active. A successful effect that committed before the refresh fence can therefore
need completion from Reconciling. Resolve this explicitly before end-to-end activation:
allow only verified original terminal result completion with complete quiescence, without
advancing material or relaxing root cancellation. Add both orderings as real SQL/owner
races. This is not evidence that the current Fail/Pause checkpoint is incorrect; it is a
new lifecycle obligation introduced by refresh.

Root cancellation wins over capture/advance through the existing transaction barrier.
It may use existing cancellation recovery to settle a refresh's old child. The refresh
owner must then observe cancellation and stop. It must never reopen a cancelled root or
reuse cancellation completion as permission to advance a generation.

## Durable identity and compatibility decision

Use the existing command-receipt owner for the immutable refresh admission. Add a typed
admission command rather than reusing a bare BeginReconciliation command: the intent must
bind its action identity, original request, actor, refresh kind and waiting/child/budget/
scope expectations. A changed body under the same action must conflict; hashing a changed
body into a different command ID is not sufficient replay protection.

A small nullable reference from the original generation to its admitted command is the
proposed discovery mechanism for restart/current-review recovery. Exact command lookup
uses the existing index; do not scan event or Agent journal history. Confirm the final
receipt shape and migration against current mappings before implementation. A new table
is unnecessary unless the existing receipt cannot retain and validate the complete intent.
Old generations remain null and preserve their bytes. New receipt fields/command kind
must fail closed in actual older readers. A downgrade must refuse retained new evidence.

Do not implement automatic progress as a timer that scans every run or catalog. Reuse
durable dispatch/outbox delivery only after verifying its semantics for a blocked Process
and exact refresh intent. The admission commit must survive a crash before enqueue, and
delivery must be bounded/idempotent after restart. Worker activation and sustained capacity
remain separate gates; manual continuation alone cannot qualify automatic Replan.

## Implementation checkpoints and proof

First qualify atomic admission, immutable replay and discovery with no public activation.
Then qualify real child stop/reconciliation, exact accounting and successor capture. Finally
wire actual manual/automatic producers and UI, retaining original intent on lost replies.
These are dependency checkpoints within one feature, not substitutes for final behavior.

For admission, pure tests cover identity/actor/body changes, unsupported policies, stale
expectations, unknown values, stopped roots, live or mismatched claims and runtime shapes.
Real PostgreSQL tests cover concurrent admissions, rollback between local/shared changes,
receipt replay, fresh-context recovery, ordinary legacy byte compatibility and old readers.
The existing publication owner must demonstrate that a held publication and admission
serialize under the same root fence. Builds use standard Release outputs serially; reuse
the isolated PostgreSQL 18 server on 53619. Declare and confirm exact topic discovery before
execution. Run full portability enforcement after source edits; no broad Stable run is
justified by a phase boundary alone.

Subsequent owner proof must include both snapshot modes, manager refresh and automatic
replan limits, proposal-only stop, accepted-before-fence completion, unknown effect/capture,
lost stop/settlement/capture/advance acknowledgement, root cancellation at every boundary,
two concurrent actors, replay after later progress, and rejection of every old generation's
publication. Final HTTP/queued MAF/SQL proof must start from a genuine public preparation;
browser proof must cover visible pending/refusal/recovery outcomes and the new generation.

Initial scoped CodeAnalytics snapshot `snap-20261023144945-e8f8071c` covers Runtime,
Application and Persistence: 296 documents, 893 types, 7,501 members, three intended project
edges and no scoped cycles. There are 189 WS0002 diagnostics and `hasBlockingErrors=true`;
the snapshot is not a clean solution-wide claim. Existing complexity findings remain.
P27's unchanged Core/Workbench snapshot remains supporting evidence until those owners
change. Compact evidence is under `.artifacts/concurrent-mutations/validation/c4-process-refresh-admission`.

The following source-backed refinement resolved the admission mapping before implementation
and identifies the downstream completion/delivery changes. Public refresh remains disabled.

## Admission design refinement from current source

The runtime idempotency table already stores a bounded `DecisionReceiptJson` and
`DecisionRunScopeReceiptJson` independently of runtime history. Its primary key is
`(RunId, CommandId)`. The generic `FindCompletedCommandAsync(CommandId)` first searches
by command alone and then reloads the full current runtime state. Do not add that cost to
refresh discovery. A narrow Application receipt-reader contract should project only the
original admission receipt and shared-scope receipt through the composite key, then verify
their typed identity. It must report historical acceptance separately from current progress.

The existing decision receipt format has versions 1-6. Reserve version 7 for the new
admission command or a generation carrying a refresh-command reference. Preserve the
canonical bytes of legacy receipts. Keep the complete immutable request and trusted actor
inside the admission command; use a stable command identity derived from root/run/action,
without actor or body in that identity. Comparing the stored typed command must reject
changed body or actor under the same action. A small nullable command reference on the
original generation allows current-review/restart discovery without scanning history.
It survives reconciliation/terminal retirement, while a successor starts without it.
The migration must retain old null rows and refuse downgrade while new evidence remains.

The public single-activity producer is the initial limit. Admission must verify its
original NeedsManager result, exactly one original child, a completed matching claim,
no other unsettled local preparation, and an active exact parent scope when RunSnapshot.
The new command freezes the local generation and parent scope in one UOW commit; it does
not publish, stop the Agent, settle work, capture data or make the step Ready. Pure rules
belong in a separate Runtime type, and orchestration belongs in a separate Application
coordinator. Extend the existing decision store at its command dispatch, with cohesive
validation in an owning collaborator if needed; do not expand the runtime engine partials.

Source inspection also establishes that `ProcessRuntimeDispatchQueue` is in-memory. The
existing recovery query walks pages and accumulates run IDs; it is not a bounded durable
refresh work queue. `EfProcessOutboxStore` has no registered delivery caller in the inspected
Process projects, and its existing claim operation has neither lease-expiry recovery nor
an atomic multi-worker claim. Neither mechanism may be described as restart-safe automatic
refresh delivery. Admission and explicit continuation can be qualified first; automatic
activation additionally requires a dedicated indexed outbox subscriber with exact owner
command identity, atomic leased claims, bounded retries and lost-worker recovery. Preserve
legacy projection messages and do not consume their backlog incidentally. Qualify this in
the existing outbox owner instead of mixing refresh with the cancellation worker.

Successful publication before the fence must be completed using its verified original
receipt. The planned scope completion extension may accept Reconciling only when the
existing exact terminal-result and complete-quiescence checks pass. It must not authorize
new publication or capture and must still refuse a cancelling/stopped root. This extension
and durable delivery need their own downstream tests before a public refresh/replan switch
is enabled. The first admission checkpoint deliberately leaves the public capabilities
unchanged and cannot be presented as the complete refresh feature.

### Concrete admission edits and validation boundary

- Runtime: a typed refresh request/intent and BeginRefresh command; a pure admission rule
  that reuses `ProcessDecisionReviewCompletion.ValidatePending`; and an optional admitted
  command reference on `ProcessDecisionGeneration`. Reject ParentRefresh as a user action,
  undefined values and mismatched actor kinds. Manual refresh requires Pause/Replan;
  automatic admission requires original Replan support and finite credit. Bind the exact
  original input and child material digests in the trusted intent, without copying payloads.
- Application: a small admission coordinator that checks original accepted single-activity
  input, selected child/head/budget and absence of unrelated work, derives the stable
  run/action command identity, and compares a retained typed admission before fresh-state
  checks. A bounded receipt-reader port returns original admission without loading runtime
  history. The coordinator neither calls the Agent owner nor dispatches a successor.
- Persistence: stage BeginRefresh through the existing UOW and decision store. Require
  the exact paired shared-scope fence for RunSnapshot and its absence for StepSnapshot;
  validate against the saved policy and child under the root fence. The mutation may
  advance only its timestamp and decision/scope state, with no business result, settlement,
  capture, new claim or Ready step. Retain the command reference on the old generation.
  Add the nullable column and strict version-7 receipt support, with old byte formats
  unchanged. An exact composite-key projection serves receipt/current-admission discovery.
- Guard later generic refresh commands against bypassing an admitted intent: an advancing
  command must carry the same original admission reference when one is present. A new
  successor drops that reference; the retired generation retains it. Cancellation and
  verified old-effect completion retain historical admission evidence.

The first checkpoint covers admission only, so it stages no unimplemented delivery and
exposes no new public capability. Its acceptance criteria are atomic local/shared fencing,
strict intent replay/body/actor identity, bounded current discovery, no provider/business
effects, original-child and cancellation races, strict malformed/old-reader rejection,
rollback/restart behavior and unchanged legacy receipt bytes. Required existing regression
topics are decision generations/persistence, run scopes, review completion and terminal
accounting; choose exact class filters and confirm their source-derived case counts before
execution. The new unit and integration classes must enumerate their expected cases in the
proof manifest. Migration/rollback tests use the existing isolated PostgreSQL server.
Full portability is mandatory after the actual final source edits. Architecture and
dependent publication checks gate the next child-stop checkpoint.

Implementation refinement: the first admission command accepts only explicit manager
refresh. It rejects AutomaticReplan and ParentRefresh, including a caller claiming a System
actor. A saved Replan policy can permit a manual refresh, but cannot supply the missing
verified changed-basis evidence or durable automatic delivery. Automatic admission remains
gated until its original owner evidence and delivery checkpoint are implemented; it must
not be inferred from a request enum. Shared-scope admission is limited to its original
root run, consistent with the current single-activity public producer.

## Original-child stop implementation boundary

The next dependency checkpoint gives Workbench explicit refresh authority to retire the
old Agent. Reuse `AgentInheritedPlanningStopper` and its existing lease/cancellation/journal
protocol without changing Core or weakening the root-cancellation entry point. The
Workbench adapter will expose distinct cancellation and refresh contracts, each selecting
its own authority callback before sharing the existing original-source/profile/child checks.
An arbitrary manager identity or a historical receipt alone cannot stop a child.

A narrow Application authority port implemented in Persistence will require the durable
admission command, its canonical original-child digest, the current Reconciling generation
and pointer, the exact original child/allocation, no active claim, and a nonterminal root
and step. For RunSnapshot it also requires the original Reconciling scope, material and
membership. Reuse the existing bounded child query and exact command lookup. A small
head/scope projection must distinguish current shared reconciliation from obsolete history;
`IsCurrent` alone deliberately becomes false when the shared scope is frozen. Bound each
authority observation to three indexed metadata queries, with no runtime/event-history or
material-payload read. Recheck before cancellation signalling, inside the dispatch lease
and immediately before the journal write. Hold no SQL transaction while calling Core.

The stop contract returns the existing explicit pending dispositions for open work, busy
lease, missing journal, unresolved effects or accepted effect awaiting completion. Closed
work retains its actual measured cost and receipt, and replay does not mutate it. This
checkpoint cannot settle/refund work, advance material, make a Process step Ready or
enable automatic delivery. Root cancellation remains a separate authority; a refresh
continuation must stop when cancellation or terminal progress has superseded it.

Required proof uses the real registered Workbench/Core owners and PostgreSQL in both read
modes: proposal-only stop and idempotent replay, no refresh/no matching action/wrong child,
changed source/profile, root cancellation or generation change during a held lease,
accepted effect before the fence, missing journal, open work and unresolved effects. Keep
the existing cancellation stopper negative and accounting cases. Confirm exact class
discovery on refreshed Release outputs, rerun full portability, and compare the changed
Workbench and Process dependency scopes. No public capability changes before reconciliation
accounting, capture, successor dispatch and restart delivery are qualified together.

### Reconciliation accounting source findings

The existing child-closure command can settle original work while retaining Reconciling
and a blocked step; no terminal-accounting relaxation is needed for a proposal-only stop.
Use a deterministic reconciliation command tied to the admitted action, read the original
owner outcome outside SQL, and commit its exact closure plus local/root settlement once.
Unclosed work, missing evidence and unresolved effects remain pending with the grant held.
Do not interpret the Agent's Cancelled outcome alone as permission to refund or capture.

An accepted effect needs a different completion path. Besides the scoped transition's
Active-only success rule, `ProcessDecisionResultAcceptance` currently rejects a terminal
result when `SharedScopePending` is true. A Reconciling shared scope deliberately sets that
flag. Both checks need an explicit original-refresh completion contract, validated under
the root fence against the retained admission, exact child/material and original waiting
result. Do not broadly ignore the pending flag or permit every Reconciling scope to finish.
Reuse the original result preparation and complete-quiescence checks, retain the accepted
receipt and actual work, and report that refresh was overtaken by terminal completion.

This accounting owner belongs in Application, with SQL preconditions in Persistence and
pure completion rules in Runtime. It must not give Persistence an Agent callback or extend
the existing terminal-only coordinator. Prove no-effect settlement, accepted-before-fence
completion, unknown outcome, root cancellation, competing continuations and lost commit
acknowledgement in both read modes before capture or public activation.

### Concrete reconciliation accounting contract

Use one Application coordinator for the original admitted action. It reads the retained
admission and original binding, then checks the deterministic accounting receipt before
requiring fresh pending state or reading the child owner. The command identity depends
only on the admission identity and a separate accounting domain. Replay must verify the
original closure, allocation, grant, work revision, material and result; it cannot derive
a new command from a changed budget or treat missing evidence as a successful replay.
The retained admission actor owns this continuation's result events.

A typed optional accounting reference on the in-process runtime commit request selects
the strict persistence guard. It adds no stored receipt field: the existing version-7
generation pointer, exact closure/settlement and deterministic command key already retain
the necessary identity. Ordinary commands preserve their serialization and guards. Do
not add a receipt version or migration unless implementation disproves this boundary.

The coordinator reads the registered child's verified outcome outside SQL. An open or
unknown outcome returns its typed pending reason without any write. A verified NoEffect
closure commits its original local/root settlement and Reconcile closure together, with
only a monotonic runtime timestamp change. The step stays blocked and the shared material
scope remains Reconciling. An Accepted closure uses the existing pure review-result
preparation, its actual terminal status and measured work, then completes that exact
original child/result/scope. It reports that original completion overtook refresh.

Under the existing root fence, a dedicated persistence guard rechecks the immutable
admission, accepted input, original child digest, exact admitted generation and shared
scope, original unsettled allocation/budget, live root and completed waiting claim.
It verifies the entire permitted mutation shape, including original result history,
unmodified claims/other runtime fields and exact result events. No capture, reservation,
Ready step, new claim, outbox action or unrelated command may accompany accounting.
The completion path receives the validated intent from this guard; it must not trust the
mere presence of the optional request field. Add a separate pure shared-scope refresh
completion transition requiring the admitted Reconciling scope/material and its original
terminal member, then reuse complete membership/allocation quiescence. Ordinary scope
completion remains unchanged. Root cancellation and stale expectations win atomically.

Reuse existing bounded receipt/child/material queries. The explicit continuation may
load its selected runtime state through the existing idempotency owner; it is not a
polling API or global-history worker. Keep the child outcome read outside the transaction
and avoid serializing runtime history to validate the mutation. No new project reference,
Core change, service locator, business partial or extra transaction owner is planned.

Qualification must prove both snapshot modes for no-effect settlement and replay,
accepted-before-fence completion, concurrent continuations, rollback after staged work,
lost commit acknowledgement, fresh-host receipt recovery without another child read,
and cancellation after the owner read but before SQL commit. Unknown/missing/open work
must retain grants. Negative tests must reject substituted admission/binding/input,
forged mutation/result/scope authority and an unmarked attempt to complete a frozen
scope. Keep existing terminal/review accounting and run-scope transition cases. Record
source-derived discovery before execution; use actual SQL and registered Workbench/Core
owners for the positive and effect-race cases. Rebuild each changed production owner
and both affected test assemblies in Release, run final portability and scoped architecture
comparison, and reconcile the reused fixture before the successor-capture checkpoint.
