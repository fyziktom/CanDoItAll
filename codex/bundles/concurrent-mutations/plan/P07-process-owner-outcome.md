# Original Process child outcome evidence

Status: entry design accepted after signed atomic-completion checkpoint 187ef2df5.
This slice supplies the registered owner evidence reader. The following result-commit
and producer slices consume it; it does not activate execution or claim bundle closure.

## Boundary and failure model

Application owns a provider-neutral reader keyed by the exact execution contract. Its
input is the original child/material/allocation binding, whose child ID is the actual
Agent execution ID. Its output is either closed owner work and effect evidence, or a
typed pending reason. No prompt, task content, current assignment or diagnostic string
is used as a control protocol. Closed evidence distinguishes success, failure and
cancellation independently from whether an effect was committed.

Workbench owns the task-plan interpretation and reads the exact retained Agent run.
It already references Agent Core and Process Application. The reader verifies schema 7,
original session/profile, inherited origin, original accepted source/policy and canonical
parent binding before interpreting accounting. It does not acquire a current Agent
catalog, publish an effect, run a provider, list all runs or recapture material. No new
project reference, service locator, runtime partial or persistence migration is needed.

Missing journals, open metered work, interrupted provider calls, unknown effects
and unacknowledged calls retain the reservation. An approval or changed-basis wait is
explicit and cannot supply terminal settlement. Closed work uses the exact saved work
revision and ticks, including overruns, and zero child replans. Completed/failed/cancelled
run and planning states must agree; contradictory terminal state is corrupt evidence.
The saved dispatch lease ID is retained after lease disposal and is not a liveness
indicator. Closed work and resolved effect/provider state determine accounting readiness;
the terminal persistence rules prevent new work on that completed inherited generation.

One committed task-plan operation is supported by this execution contract. The validated
journal binds it to the original prepared proposal, which the existing Workbench codec
resolves once. Owner-confirmed and cancellation receipts must also match its exact
effect kind, operation identity and typed owner result. Multiple committed operations
are rejected, never collapsed into one receipt. Normal adapter checkpoints stay opaque;
Workbench must not reference MAF to parse them. Explicit known non-commit is required
for a no-effect closure; absence of a receipt is not evidence of no effect.

## Accounting and access

Accounting is an internal control-plane operation, not a disclosure or publication
permission. Current read/write revocation must not erase already incurred work or an
accepted effect. Validate the original accepted source through the existing indexed
original-receipt authority reader, without current catalog grants. Match the current
database identity/fingerprint and prevent a profile switch during the read; a later
generation of the same profile may account for the original run. User-visible history
and all publication continue through their current authorization gates.

## Cost and concurrency

Read one bounded retained run and the existing exact indexed original-source projection;
do not load the launch document or SQL material payload. Interpret only the one committed
proposal, avoiding repeated material decoding for every journal entry. Journal byte and
entry limits remain enforced by the existing serializer/model. No SQL transaction, root
fence or catalog lease spans file/provider work. Closed evidence is read-only; pending
evidence is never cached as terminal. The next slice must atomically compare original
child/allocation and work identity when committing its runtime result.

The ordinary execution-run reader takes the workspace publication lock and can recover
unfinished writes. Accounting therefore uses the existing historical read-store contract,
with its five-second read budget, bounded documents and publication witnesses. The owning
AgentFramework module registers its existing canonical profile adapter; Workbench depends
only on the Core contract. Pending recovery, changed snapshots, unsupported layouts and
exceeded limits fail explicitly without attempting recovery or releasing reserved work.
A held-publication-lock test and file observations prove this boundary.

The architecture baseline snap-20261020171043-6f5b6f80 covers Workbench, Application,
Persistence and Runtime: 1886 types, 16717 members, 609 findings and 205 existing
diagnostics. Six existing Workbench module/type cycles are recorded; no project cycle
is introduced. Direct builds and a before/after comparison distinguish existing tool
diagnostics from new compilation errors.

## Proof and following integration

Pure tests exercise closed accepted/no-effect outcomes, exact work and receipt identity,
approval/basis waits, open work, unresolved dispatch/effects, corrupt binding and terminal
state, and receipt mismatches. Registered integration tests execute the existing real
MAF-controlled provider/saved tool and real SQL source; verify original accounting after
permission revocation, profile-generation change, assignment repair, claim expiry and
lost acknowledgement. The lost-acknowledgement case must retain open work even after its
effect receipt is recovered. Assert read-only workspace behavior, bounded SQL selection
and no extra provider calls.

Build affected Application, Workbench and AgentFramework module projects directly, then the owning test
assemblies in standard Release with local libraries. Confirm exact discovery before
bounded execution. Reuse the existing isolated PostgreSQL fixture and declared file
families; record source/runtime identity, architecture comparison, full portability
enforcement without baseline writes, documentation and resource reconciliation.

Next, connect the normalized runtime result to the original outcome using stable command
identity and the existing atomic completion. A new terminal strategy result must not
bypass unsettled policy-owned work, including pending capture. That invariant applies
to new result acceptance: ordinary cancellation still stops dispatch before later work
reconciliation. Closed-child manager waits and exact lost-acknowledgement replay need
explicit result design before producer activation. Durable capture, selected producer,
manager retirement/approval, Workflow, settings/SPA and sustained capacity remain open.
