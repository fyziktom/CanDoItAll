# Root cancellation quiescence and admission fence

Entry: Pass after signed 94a4d1ac9 and 235 passing selected executions. Behavioral foundation; implementation and validation follow this reviewed design.

## Problem and boundary

ProcessRuntimeOperatorApplicationService currently starts the root barrier, notifies
observers, cancels descendants in up to five unbounded passes, then finalizes the root.
Notification failures only become diagnostics. EfProcessRuntimeUnitOfWork protects the
monotonic cancellation status but does not establish that child decision work and budgets
have closed. Selected Agent stop is therefore not enough to call a root Cancelled.

Make the persisted root finalization predicate a small Process persistence owner. It
receives the existing DbContext and exact root identity inside the existing root lock
and transaction. No Agent/Workbench dependency, provider access, file wait, new schema,
interface, partial class or project reference is required. The broad unit of work
delegates the query and retains transaction/CAS/receipt ownership. Application remains
responsible for arranging recovery; persistence enforces that it cannot be bypassed.

Reuse CodeAnalytics persistence snapshot snap-20261022033809-531e33b7: 179 types,
1373 members, 204 dependency edges, zero cycles, 62 findings. Ten WS0002 package-advisory
workspace diagnostics are retained; scoped facts and successful Release builds are
separate evidence. Exact CommitCoreAsync is lines 243–373 before this change. Query
structure and actual project references must be checked again after implementation.

## Commit position and invariants

Keep original-command receipt lookup first. A historical accepted command remains a
replay even if the current root is cancelled. Apply the new predicate after current-state
CAS and the monotonic status guard, before ReplaceState or staged decision changes, for
any existing root newly becoming Cancelled. Ordinary child cancellation and repeated
already-Cancelled accounting are unchanged. The dedicated FinalizeRootCancellation
transition still requires CancelRequested. Existing generic direct cancellation may
close a quiescent root atomically, but receives the same persistence predicate and
cannot evade it; do not impose a new barrier requirement on harmless empty legacy roots.

Return an explicit runtime rejection with an actionable pending category and unchanged
state; do not swallow database errors. Rejection must leave events, receipts, outbox,
accounting, material and runtime revision unchanged. A command trying to settle the last
item and finalize in one request must first settle through its original owner, then
finalize in a separate short transaction. This avoids judging unpersisted staged rows.

Under the same root fence, require:

- No descendant runtime outside Completed, Failed or Cancelled. CancelRequested is still
  pending. Root enrollment already takes the root fence and refuses a stopped root.
- Every current decision generation is Completed or Cancelled, with no unclosed child.
  A Superseded current head is not an idle terminal generation. All retained generations
  must have no child without its exact retained closure; counters alone are insufficient.
- No root-associated budget with nonzero reserved work/replans and no allocation lacking
  its original owner settlement, including zero-work replan grants and legacy unfunded allocations.
  Joining through budgets/root IDs includes descendant steps and historical generations.
- Every shared run scope belonging to the root is Completed or Cancelled, with no pending
  capture fields. Material payloads are immutable history, not a reason to delete data.

Use EXISTS over root-indexed heads/budgets/material scopes and child primary/foreign keys.
Do not deserialize journals, load the root aggregate again, enumerate IDs, or sum arbitrary
unbounded sets in memory. Prefer a small fixed number of SQL queries with typed categories;
measure emitted SQL and ensure early exit cannot skip integrity needed for a final pass.
Do not call these queries from a long-held cross-store transaction. No latency claim is
made without actual query/host evidence.

## Recovery transitions that must remain legal

Decision BeginReconciliation accepts Active/WaitingForReview; Cancel requires Reconciling
and all children closed. TerminalAccounting already applies Cancel after the final exact
closure and budget settlement when no other work remains. Empty decisions need these same
legal phase transitions. Shared scope BeginReconciliation accepts Active; Finish(cancelled)
accepts Reconciling/AwaitingCapture and requires complete membership and root allocations.
Capturing requires original capture completion/abort and measured-work settlement first.
Never assign phase directly as a recovery shortcut.

This guard can qualify before the durable worker, with activation still gated. The next
worker must resolve pending categories fairly and durably. Missing journals, unknown
effects and interrupted measured work remain pending with an operational resolution path;
neither zero work nor elapsed wall time can replace original owner evidence.

## Proof plan

Test the persistence predicate directly with real PostgreSQL queries independently of the
large unit of work, then prove the actual commit rejects/accepts under the root fence.
Use controlled real owner transitions for normal records; direct malformed/legacy records
must be explicitly labelled fixture cases and remain constraint-valid.

Cover empty roots, active descendants, open/waiting/reconciling generations, historical
unclosed children, unsettled allocations even with inconsistent zero legacy reserves,
zero-work replan grants, reserved root/funded child budget,
pending shared capture, finished shared scope and exact settled success. Assert no mutation
residue on rejection and retain original receipts after successful cancellation replay.
Use two contexts and an interception barrier to prove late enrollment cannot race final
checking; also prove a pending accounting commit can complete while the root stays stopped.

The selected-stop test for an already-cancelled historical root must deliberately arrange
legacy persisted state once this guard is installed; using new production finalization to
create an invalid old root would contradict the new invariant.

Run the owning predicate/commit tests, existing cancellation barrier tests, root/step
accounting and shared-scope recovery, plus the ten selected-stop adapter cases. Refresh
standard Release persistence and owning test outputs, discover exact case counts, run
full portability-static enforcement and documentation checks. Preserve the original
selected-stop proof; record the new source/runtime identities and observed resource
closure separately. No full Stable or SPA build is justified by this persistence slice.

## Additional admission fence to settle before implementation

A finalization predicate alone is insufficient: a completed/failed descendant could be
reworked after its root stops. Original-command receipt lookup must remain first, but
new work on any existing descendant needs the same persisted root fence. Inspect and
classify new/renewed claims (including same-token expiry extension), reactivation,
unfunded as well as funded budget reservation, decision start/enrollment/refresh and
shared capture start. Completion/abort of already-recorded work, reconciliation and exact
settlement must remain legal. Do not use the current assignment as historical authority.

EfProcessDecisionRunScopeStore.RequireWorkAdmissionAsync currently computes newly active
claim tokens/claimed-step pairs, then returns when no shared scope exists. Its shared-
scope gate does not establish a universal stopped-root check and does not detect a
same-token lease extension. If this predicate is reused, extract a small pure Runtime
predicate only for actual shared use; do not copy or silently widen scope semantics.

Root status changes already acquire the existing advisory root transaction lock. Place
both the new-work rejection and final quiescence check under that fence after receipt
replay/current-state CAS and before ReplaceState. Base root identity on persisted state,
not on a caller-modified mutation. Verify terminal accounting still works and accepted
historical commands still replay. A bounded quiescence query must also conservatively
refuse a missing current decision generation, not lose it through an inner join.

The complete entry-path audit resolves the initial root-identity concern without another
production guard. ValidateCommitIdentity runs before lock selection and already requires
the mutation's run/root/plan/hash to match OriginalState. EnsureCurrentStateMatchesOriginal
then compares that original with the persisted state before ReplaceState. Preserve both.
Add regression cases for mutation-only substitution and substituting both snapshots of
an existing run, so future changes cannot bypass the original root lock. New child
creation remains the separate lineage-validated path. Historical receipt replay remains
first inside the transaction and cannot admit a new mutation.

## Concrete source review, 2026-10-22

Existing ProcessRuntimeDispatchGuard protects commands carrying a dispatch precondition;
it does not cover every ordinary claim renewal/rework/accounting command. The decision
and budget stores check the local run's original/next status, not every descendant's
persisted root. Thus the new root-level admission check fills a specific uncovered path.
Shared scope CompleteCapture already refuses a stopped root; recovery must use the
existing measured-work AbortCapture transition instead of activating captured material.

Keep the new pure work-admission classification in Process Persistence, used by the
root guard and existing shared-scope claim gate. No Runtime project change is needed.
An active claim needs fresh admission when it is new, reactivated or changed (including
same-token renewal/expiry extension, owner, attempt or step binding). Existing claims
that become released/expired/completed/cancelled are not admission. Also recognize
terminal run/step reactivation, attempt advancement, newly attached claim tokens,
decision start/enrollment/refresh/review, budget reservation and shared capture start.
Completion, reconciliation, abort and exact settlement remain legal. Shared-scope
renewal protection is an intentional strengthening that requires its own regression.

Use one root-PK projection of four correlated EXISTS groups for quiescence: runtime,
decision, budget and shared-scope work. Runtime work includes nonterminal descendants
and retained active descendant claims. Validate proposed root steps/claims directly;
the normal BeginRootCancellation transition already closes them. Decision work includes
missing current generations and all retained open-child rows, even where legacy counters
are inconsistent. Return a typed flags result so all pending groups remain observable
without loading any payload. Missing root is an explicit refusal. The new owner is an
internal cohesive static persistence class tested through existing InternalsVisibleTo;
do not add a public interface or DI registration solely for these predicates.

Preserve legitimate rework of a Failed root: the existing engine allows it, while
Completed and Cancelled roots are immutable at that engine boundary. For the root's own
mutation, the new admission guard fences persisted/proposed cancellation and proposed
terminal work admission. For descendants it requires the persisted root to exist and
not be terminal or CancelRequested. A Blocked/Waiting root is not silently reclassified
as cancelled; dispatch-precondition commands retain their existing stricter Active check.
Blocked/Waiting-to-Ready rework must be recognized even if it resets rather than
increments AttemptNumber. Release/expiry of an existing claim remains legal.

The root guard returns an explicit Rejected ProcessRuntimeCommitResult with a constant
diagnostic code, exact root identity and typed pending groups. UnitOfWork clears tracked
changes before returning. Database failures propagate. Root failure rework, historical
replays and legitimate terminal accounting are mandatory regressions, not exemptions.

### Selected proof and invalidation

New isolated classification tests exercise renewal, marking running, rework, new
decision/review/capture and safe closure/settlement without constructing UnitOfWork.
New PostgreSQL guard tests exercise each pending group, absent current generations,
legacy mismatched counters, zero-work allocations, exact settlement, query shape,
root identity refusal and serialized admission/finalization. An interceptable final
transaction must demonstrate that another root progresses while a same-root writer
waits. Use existing TestApplication/lease and transaction barriers, not sleeps.

Regression filters target ProcessCancellationBarrierPersistenceTests,
ProcessDecisionBudgetPersistenceTests, ProcessDecisionFundingPersistenceTests,
ProcessDecisionPersistenceTests, ProcessDecisionRunScopePersistenceTests,
ProcessDecisionRunScopeRecoveryTests, ProcessDecisionTerminalAccountingIntegrationTests,
ProcessRuntimeDispatchPreconditionTests and ProjectTaskPlanProcessChildStopperTests.
Relevant Unit cancellation/terminal-accounting/runtime-operator cases use their exact
stable class or method filters. Confirm expected discovery against built source before
execution, refuse zero/mismatched discovery, and retain all attempts. No unfiltered
project/Stable gate is triggered by this focused persistence slice. Final integrated
closure still requires the broader frozen gate.

The candidate must have all focused cases pass, one bounded scalar SQL query for
quiescence (no payload/ID enumeration or N+1), unchanged dependency direction, refreshed
standard Release builds, full portability-static enforcement, exact source/runtime
identity and observed zero new resource delta. This is a Behavioral foundation; do not
add unrelated Governed artifacts or browser proof for a persistence-only predicate.

### Admission comparison must use the persisted row

The existing optimistic comparison checks identity/status/timestamp/counts, not every
claim field. Therefore compare proposed claims/steps with the already loaded persisted
entity, never only with caller-supplied OriginalState. A small immutable classification
result carries Claims and NewWork booleans. Compute it once before ReplaceState, pass it
to the root guard, and pass Claims to the shared-scope check. Remove that store's duplicate
claim sets. Compare only needed fields; do not remap/deserialize the full runtime snapshot
or add another SQL read. This preserves hot-path allocation bounds and prevents a forged
same-count OriginalState from disguising a renewal. Include that regression explicitly.

Move the existing monotonic status check into the new cancellation guard unchanged,
including its exception text, so the UnitOfWork keeps transaction/CAS/receipt ownership
and delegates cancellation policy. Its member count must not grow. The shared-scope
store shrinks as claim classification moves to the shared pure owner. No partial or
runtime-project extraction is needed for two persistence consumers.

## Initial implementation review

Persistence gains the internal cancellation guard, typed pending flags and a pure
immutable work-admission classification. UnitOfWork remains 43 members; the shared-scope
store drops from 15 to 14. The scoped dependency graph stays acyclic, with five internal
type edges and no project-reference changes. There are 32 unchanged entities and 62
unchanged findings. Existing WS0002 package-advisory diagnostics remain explicit; direct
build results are a separate gate. No new schema, public interface, partial or registration.

The root itself may retain planned/ready steps after harmless direct cancellation, since
the persisted root barrier forbids dispatch. Running/claimed steps, live claim tokens and
claims must be closed; terminal descendants with legacy live work also remain pending.
Failed roots can still be reworked. Classification compares only loaded entity fields,
using one pass and indexed claim/step lookups, without another query or full snapshot
deserialization. Measure its time and allocation at 1, 128 and 1024 steps; a fixed number
of SQL statements is not a claim of data-volume-independent work or sustained capacity.

Fifty-one new selected cases cover pure classification, real persisted quiescence,
forged original claim fields, scope renewal, historical runtime residue, race ordering,
safe closure/replay and finite query shapes. Existing cancellation tests now use the real
BeginRootCancellation transition, settle original work, and finalize only afterwards.
Tests explicitly about legacy Cancelled roots seed that historical state directly and
retain their recovery assertions. This intentionally strengthens runtime behavior rather
than relaxing the previous checks. Include changed StepMaterialProducer and ReviewRoot
suites, Unit persistence-owner tests and exact operator cancellation cases in regression.

The reviewed candidate declares 367 selected cases: 51 new and 316 regressions across
18 bounded class/method lanes. Build-backed discovery must match every lane's source
methods and expected count before execution. The manifest reuses the existing resource
families, standard Release outputs and isolated PostgreSQL fixture. No UI, migration,
unfiltered project or Stable lane is claimed by this checkpoint.

## Qualification, 2026-10-22

Behavioral closure: Pass for this persisted guard. All 367 distinct selected cases are
qualified: 287 unchanged initial passes and 80 final passes, including the one corrected
fixture and 79 previously unexecuted cases. All 51 new cases pass. The initial fixture
incorrectly reopened a Cancelled step during reconciliation; it now uses the existing
typed final-child Completion=Cancel command. All other file bytes and production inputs/
binaries are unchanged. One validation-helper checksum was corrected before the first
observer could start; its old/new identity is retained.
Four standard Release builds plus the repaired Integration rebuild, full portability enforcement, scoped architecture review,
exact source/runtime identity and observed resource reconciliation pass. The new guard
adds no schema, project reference, public interface or DI registration. Persistence is
acyclic; ten existing WS0002 package-advisory diagnostics remain separately recorded.

See [validation](../validation/2026-10-22-root-cancellation-fence.md) for SQL/classification
measurements, exact selections and limits. This closes the guard only. Durable recovery,
callback lifetime, original-generation refresh and actual producer integration remain
required; a stopped root with unknown work must remain pending, never receive zero work.

The regression review also exposed a separate recovery prerequisite: generic
BeginReconciliation requires a waiting/blocked step and cannot reopen a terminal step.
Final-child Completion=Cancel already preserves the cancelled outcome and is verified
here. Empty active decisions and already-closed children without final completion need
an explicit terminal reconciliation path before worker activation. Do not weaken this
quiescence predicate to ignore them. Historical child/allocation volume also needs its
own SQL plan/index measurements; the descendant-population timing is not that proof.
