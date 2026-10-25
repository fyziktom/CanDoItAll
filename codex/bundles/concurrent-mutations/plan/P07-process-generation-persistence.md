# Atomic Process generation persistence

This follows the [29-case lifecycle foundation](../validation/2026-10-19-process-decision-generations.md).
Keep existing policy-bearing executor and effect-owner refusals active throughout this
increment. These internal owner commands do not establish actual child journal authority.

Implemented with [101 passing selected cases](../validation/2026-10-19-process-generation-persistence.md).
The remaining actual-owner integration at the end of this document is still prospective.

## Storage and transaction boundary

Use three normalized tables: one current decision head per run/step, generation history
keyed by decision/generation, and children keyed by decision/generation/occurrence. The
head contains root/run/step and original budget identity, current generation and CAS
revision. Generation rows retain immutable material and capture-allocation identities,
phase, revision and child counters. Children retain owner/allocation and exact optional
closure. Restrictive foreign keys preserve assignment, budget and historical evidence.
Unique allocation/occurrence constraints prevent reusing a child across successors.

The existing runtime UOW acquires source/Project leases before SQL and the root transaction
fence. Add a narrow context-bound decision collaborator, with no SaveChanges, transaction
or lease acquisition of its own. It applies pure transitions and stages rows with runtime
state, accounting and original-command receipt in the same transaction. Existing ungoverned
commits perform no decision queries. No new general-purpose hooks, DI dependencies,
runtime partials or project references are justified.

Review and reconciliation atomically leave the step waiting (or blocked), refresh makes
it Ready, and terminal decision changes require the corresponding terminal step. These
changes require the old dispatch claim to be released in the proposed state. The owner
rechecks the complete bounded child set inside terminal/refresh transactions; counters
alone cannot authorize progress. Existing generic runtime writers and the forthcoming
execution-version handshake still need governed-path integration before activation.

Start requires an existing policy-bearing assignment and its original local budget.
It never retrofits legacy assignments or invents missing historical heads. The initial
capture and every refresh have their own closed accounting allocation. An automatic
refresh consumes exactly one replan and requires the original Replan policy; a manager
refresh consumes no automatic replan, requires a review-capable response and preserves
all actual work. Capture allocation identities cannot be reused by another generation
or child. A recorded overrun stays in the ledger; lifecycle persistence does not make
that allowance spendable or grant publication.

Enrollment requires the matching reservation in the same commit. Child closure requires
the matching settlement and exact closed owner revision in the same commit. Closure of
one child cannot release another allocation. Retain the accounting result separately
from the generation receipt. Quiescence, review and terminal phase changes cannot smuggle
an unrelated accounting command into their receipt. Source/manager/child-owner evidence
will be verified by Application before calling these internal contracts; HTTP and model
output never directly supply them.

No new child may enroll once the current generation is Reconciling. A bounded read of
its complete child set compares the head before/after and verifies count, distinct
identities and open count; stale observations are explicit. Actual journal/receipt reads
happen after this read and outside SQL. Each closure then compares the exact head, child
and budget revisions inside the UOW. Only the final all-closed generation can advance.
The later effect adapters must hold the same root fence through their business commit.

## Replay, compatibility and performance

New optional commit/result fields serialize absent when null, preserving legacy behavior.
Retain a bounded strict canonical versioned receipt with the original typed command,
original result, prior terminal generation when advancing, and affected child. Duplicate
command lookup occurs before current-head CAS; changed, removed or added decision intent
under a command ID fails. Replay returns that original receipt plus a separately observed
current runtime state. It never reconstructs the old generation from the current head.

Use indexed exact head, generation, allocation and child reads; no complete event-history
or catalog scan. Enrollment/closure update constant rows. Only reconciliation enumerates
children and stops at the supported bound plus one to detect corruption. The 32-generation,
64-child and 256-lineage-allocation caps remain. Reject unsupported admission before work;
do not truncate. Migration adds empty tables/nullable receipt storage, with no historical
backfill. Downgrade locks affected tables and refuses retained generation evidence.

## Required proof

Build Runtime, Persistence, Web/migration and Integration with standard Release outputs.
Confirm focused discovery before execution. Exercise actual UOW initial assignments and
accounting, not direct inserts that bypass the producer under test: original replay after
later closure/refresh and restart, altered commands, all-child quiescence, foreign/missing
bindings, allocation reuse, unsupported policies, automatic replan accounting, rollback
after SQL flush, lost acknowledgement, competing mutations and unrelated-root progress.
Inspect exact query shapes and test real migration up/down. Keep pure lifecycle and
existing accounting regression cases in the selected proof. Run full portability-static,
EF pending-model, architecture and documentation gates before closure.

Actual Agent/Workflow reservations, source occurrence keys, material inheritance, manager
continuation and business effect fences remain a following integration gate. No runtime
settings activation or complete R5 closure is implied by storage tests.
