# Current manager review actions — design review

Status: reviewed for implementation after the 331-case waiting-result prerequisite.
The guarded aggregate write is qualified by 56 passing tests, and the durable Agent
review owner by 147 passing cases. Implement the workspace and Process connection next.
Executor, Workflow and SPA activation remain gated.

## Ownership and dependency direction

AgentFramework Core owns the one saved proposal, its exact displayed expectation,
approval choice, dispatch lease and measured work. Workbench owns binding that
operation to a Process child and original Project task plan. Process Application
and Persistence own the waiting receipt, result completion and atomic accounting.
The Web composition layer can call the Workbench owner directly; Processes must
not acquire a reference to Workbench. Core must not reference either module.

Reuse the existing workspace facade with an inherited review operation that only
binds activity and delegates to a cohesive review coordinator. Extract the shared
publication acknowledgement/classification/settlement from the interactive
coordinator. Extract the existing pure cancellation transition out of the large
journal partial, preserving interactive behavior and null background chat.
Do not add a partial, general pipeline or new persistence table.

Local before evidence is retained under
`.artifacts/concurrent-mutations/validation/c4-process-review-completion/`
in `manager-action-architecture-before.json` and `manager-persistence-before.json`:
Core snap-20261021103047-8e94da1d and module
snap-20261021121850-9cce2675. Existing collector errors and cycles are limitations,
not clean analysis. Compare added dependencies/findings and owner sizes afterward.

## Durable intent and replay

For the one pending proposal, use ExecutionApprovalRecord.DecisionSourceKind and
DecisionSourceId. They already persist with the journal approval in one aggregate
write. Use a dedicated versioned kind and a SHA-256 digest over:

- Original manager action ID, approve/reject choice and trusted current actor.
- Exact original Process run, step, child, waiting receipt, decision and budget.
- Exact displayed Agent expectation, approval ID and proposal binding.
- Original parent binding and owner contract.

The Web entry derives the actor from authenticated context; it never trusts a
requested-by field. Exclude temporary access-token expiry from durable identity
so renewed credentials can recover the same action. Always validate the renewed
current authority separately.

Read the original aggregate and apply current disclosure checks before returning
any result. A matching retained approval intent plus matching proposal permits
replay before exact current-head validation. A terminal child by itself does not.
An already accepted intent never enters publication again. Closed evidence can
complete the original Process receipt; open work/effects remain explicitly
pending. No fabricated duration, zero-work closure or refund.

Fresh approval requires the exact displayed heads, original pending proposal,
current parent review, source lifetime and current manager authority before and
after waiting for the execution lease. Persist intent in the same update that
approves the proposal and opens work.

Reject applies the existing closed cancellation transition and records its intent
atomically. ChangeClosedPlanningAsync must support an explicit inherited parent
and recheck current read/manager authority after the dispatch-lease wait. Retain
exact revision checks inside the writer. Reject must not dispatch a tool/provider.

## Publication authority and locking

Carry a separate current-manager publication callback on the bound inherited
continuation. Invoke it through live work at the enlisted Project publication
boundary, including the existing after-flush check. The callback checks current
operator permission/profile/token using the existing authority lease plus the
existing bounded Web credential observer. The launch lease alone checks expiry
and configuration but does not reread managed-token revocation or user revision.

For a human manager, capture operator authority without a new Project admission:
the original saved effect already enlists its original Project lifetime. This
avoids acquiring Project locks in reverse order or another Agent catalog lease.
Do not allow an Agent principal through this manager entry.

Web constructs a server-issued capability from the authenticated principal; the
request cannot supply its actor or a permission boolean. Admission requires
Process read/write/execute, Agent read/write/execute, and Project read/write grants.
The current Project write observer already validates the complete original
credential scope list, identity, expiry, revocation, user authentication revision
and session authority under a one-second cancellation budget. Reuse that observer
behind a typed Workbench capability callback and the existing configuration/profile
lease. Do not introduce a Web reference into Workbench or Processes, or copy token
registry logic into a business owner. All authority observations must finish before
authorizing a mutation; unavailable observation fails explicitly.

The stable actor identity includes authenticated issuer, subject and original
profile; local unauthenticated API authority uses its explicit local-operator kind.
Access-token expiry and renewed credential ID are current authority, not durable
action identity. A newly authorized credential for the same actor can recover an
accepted action; a different actor cannot adopt its receipt.

The Process publication fence still validates original material, selected child,
waiting phase and root lifecycle in the same SQL transaction. The new callback
adds current manager authority; it does not replace the original-source fence.

### Aggregate approval publication

Source review found a second wait after the tool-dispatch lease: the file aggregate
writer acquires its selected publication keys. A current-manager check before that
wait is insufficient for a revocation that occurs while the run key is blocked.
The publication-context tests exercise the existing owner; they do not qualify this
new manager boundary.

Add one explicitly supported guarded overload to
ISandboxWorkspaceExecutionRunMutationStore. Owners without that capability must
throw NotSupportedException before calling their legacy mutation path. The existing
file implementation shares its current mutation preparation, exact source witness,
selected keys and durable commit. It must not call the preparation delegate twice.
The guard is a bounded, read-only current-manager observation, with no workspace
or Process mutation and no model/provider invocation.

The guard runs after selected run keys are acquired and before reacquiring the
short workspace publication lock. This is the approval/rejection authorization
point. Permission changes observed while waiting for run keys therefore refuse the
intent. A later concurrent revocation may follow that authorization point; actual
Project effects still require a fresh guard at the enlisted SQL boundary. The
source witness is revalidated after workspace reacquisition, so changes to the
selected aggregate cannot be silently adopted while checking authority. Do not
hold the global workspace lock across token/user I/O.

Guarded no-op writes still validate authority without changing aggregate/index
revisions. The guarded path must release selected keys on refusal, cancellation
or authority observation failure. Existing unguarded callers retain their behavior;
the new manager coordinator explicitly requires the guarded capability.

Core/Persistence baseline: snap-20261021130755-803d411d, 920 types, 7,275 members,
277 findings, four existing scoped cycles and 189 collector diagnostics.
AgentPlanningCoordinator has 17 members, AgentToolAdmissionJournal 117,
FileSandboxWorkspaceStore 155 and FileSandboxWorkspacePublicationScope 21.
No new project reference or partial file is needed. Sharing publication completion
and cancellation removes behavior from the current coordinator/journal instead of
growing their responsibilities.

Prove the guard independently with the existing file publication scenario:
revocation during selected-key wait; unrelated Agent publication while the guard
is paused; no-op validation; cancellation and failure releasing keys; exact stale
source refusal; and unsupported owners refusing without entering legacy writes.
Then prove approved/rejected intent persistence through the actual inherited owner.

## Failure and concurrency

Source tracing resolved the suspected shared-budget retry problem without a
production change. EfProcessDecisionChildQuery returns the original step's local
balance, not the root balance. EfProcessDecisionResultQuery verifies that local
budget against the original child. EfProcessDecisionFundingStore.StageAsync loads
the current parent balance inside the existing root transaction and derives its
funding expectation there. Sibling steps can advance the root budget without
invalidating this step's displayed local budget expectation. OtherWorkPending
detects competing allocations in this local budget.

Keep the exact displayed local budget revision and stable completion identity.
Do not weaken the receipt's budget comparison or substitute new local expectations.
The manager owner should load the current runtime state for completion, retaining
the original waiting receipt/decision/child and action. A runtime CAS race permits
an exact later retry; it does not permit publication again. Add a sibling root-budget
advance and lost-acknowledgement case to the manager integration to prove this
existing separation. A changed local budget or decision still requires refusal.

Two choices for the same displayed proposal cannot both apply because the
dispatch lease and aggregate revision check serialize the original choice.
Concurrent identical actions may return a revision conflict while the first is
active; an exact retry recovers its original receipt. Never retry dispatch from
an approval receipt.

Failure between Project publication and Process completion recovers through the
saved Agent approval/result and original Process completion key. A crash while
measured work is open remains pending until truthful recovery is available.
Do not claim crash-complete accounting from receipt presence alone.

After a durable choice and an observed effect, closing actual work and settling
the Process are accounting for that accepted action. Do not add a second manager
authorization requirement that could abandon already committed work when the HTTP
request is cancelled or its token expires. Use the existing bounded settlement
window and the original intent/actor. The Process still enforces its current root,
decision, child and budget fences. Check current disclosure before returning or
replaying data to a caller. A rejected final disclosure can coexist with a durable
completed receipt; an authorized exact retry reads that receipt without dispatch.

Approval/rejection intent only covers a pending proposal. A later cancel after
BasisChanged cannot overwrite the earlier approval as a second intent. Parent
refresh/cancel instead needs a Process reconciliation command receipt, phase
fencing, complete child retirement/settlement and then the parent transition.
RootSnapshot refresh must rebind the shared material atomically; StepSnapshot
refresh must not alter unrelated decisions. This is a separate owner operation,
not an extra interpretation of an approval row.

## Public transport

Use a separate Process planning review API mapper under the existing Process group.
Keep the existing large ProcessesApi as a registration facade. Preserve explicit
authorization policies, strict unique JSON fields, finite request size and no-store
responses. Encode all Process and Agent revisions as lossless decimal strings at
the HTTP boundary; do not add an Agent-Core converter dependency to Process Runtime.

The endpoint returns the retained action/result state with the original identities.
A committed Agent effect followed by pending Process completion remains pending,
with the same action available for exact recovery. It cannot be represented as a
failed publication that a client may dispatch again. Missing approval intent,
changed choice or actor, and unavailable original source produce explicit refusals.
Read review data only after current disclosure checks, including on replay.

Keep the original completion receipt separate from the latest runtime view. A
duplicate Process command retains its historical state by design; it must not
replace a UI's newer sibling-step progress. The endpoint should return the original
action outcome and reload current runtime state for reconciliation, or explicitly
require that reload in the client contract. Test replay after unrelated sibling
progress as well as after parent-budget advancement.

## Validation

Independent Core tests cover intent identity, mismatched actor/action/expectation,
retained approval/proposal validation, null-chat rejection, competing decisions,
current-authority rechecks after lease waits and no second dispatch on replay.
Reuse interactive cancellation/publication tests for the extracted behavior.

Actual workspace/SQL tests cover both snapshot modes; approve/reject; revoked
manager permission while waiting and after SQL flush; changed original Project,
Process decision and budget; lost acknowledgements; full host restart; and one
Project receipt plus one Process settlement without another provider call.
Use the existing PostgreSQL fixture and standard Release outputs.

After these pass, wire the actual governed Process executor and public review
entry with endpoint authorization tests. Workflow/settings/SPA and sustained
capacity remain separate required work. SPA remains codex/concurrent-mutations.
