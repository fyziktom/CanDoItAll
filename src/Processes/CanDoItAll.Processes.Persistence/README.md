# CanDoItAll.Processes.Persistence

Implements EF Core stores for process plans, runtime events, runs, artifacts, assignments,
outbox records, projections, history, and recovery lineage.

Transactions and concurrency rules follow the process application/runtime contracts and
the canonical PostgreSQL database. Initial project-scoped admission uses the owner's
existing transaction and the shared infrastructure coordinator. Its data-only policy
acquires the Projects mutation gate and rechecks the saved profile/project/lifetime
before the plan, assignments and runtime state are committed. The advisory gate remains
held through commit; provider and artifact work remains outside this transaction.

Direct standalone construction still accepts only the context and optional clock. It
does not require application profile services for unscoped or historical state. New
explicit project admissions require a coordinator and the project admission policy;
missing configuration fails before writes. The optional InMemory test path requires
an explicitly shared test store and does not provide PostgreSQL atomicity.

Policy-bearing assignments are retained historical evidence, but current Agent and
mapped Workflow authority remains closed until the governed decision runtime is supported.
The direct readers report them as non-current; reservation/mutation guards recheck this
inside the existing root transaction fence. Deferred Workflow continuation checks both
the observed and current policy explicitly, preserving valid released legacy claims.

New policy-bearing assignments receive a durable budget in the same initial transaction.
An explicit accounting command stages one indexed allocation and its balance beside the
runtime mutation, under the existing root fence and state CAS. Exact command receipts
retain the original accounting result across later state changes and acknowledgement
loss. Settlements retain actual overruns and may finish after cancellation. Legacy
assignments have no budget; missing historical accounting is never reconstructed from
current settings. Child-owner settlement verification, generation fencing and enclosing
allocation enrollment remain required before governed execution can be enabled.

Decision heads, generation history and enrolled children use separate indexed tables.
The runtime UOW stages their changes with state, accounting and a strict original-command
receipt. Enrollment requires its reservation; closure requires settlement of the same
child allocation and owner revision. Capture allocations cannot be reused by children or
successor generations. Review/quiescence/terminal transitions must update the runtime step
and release its dispatch claim in that transaction. Terminal transitions verify the full
bounded child set. Actual Agent/Workflow journal authority, source occurrence integration
and effect publication guards remain required before governed execution activation.

The three nullable admission columns retain existing rows without inventing an actor,
permission or lifetime. A complete tuple is immutable, including on replay and child
run creation. Subsequent state transitions can still cancel or observe a run after its
project is retired; every new external effect needs its own current authority check.
The owner model also retains prepared launch payloads, their original
caller intent and accepted run, continuation claims, and Structure link receipts.
Initial run state and accepted admission commit together. Source catalog leases are
acquired before SQL and held through the actual commit; postcommit artifact, provider
and notification work runs after their release. Workbench link delivery and the Process
receipt share one explicit coordinated transaction. Lost acknowledgement preserves
accepted identity, and a removed link is not recreated during reconciliation.

The UI and HTTP producers capture server authority and retain the original preparation.
Historical receipt reads remain available after retirement or revocation. Native effects,
background producer identity and all other callers still require their own current
source/lifetime checks; this admission boundary does not authorize later effects.

```powershell
dotnet build .\src\Processes\CanDoItAll.Processes.Persistence\CanDoItAll.Processes.Persistence.csproj
```

Mapped Workflow admission uses a separate Process-owned direct-dispatch reader and
transaction guard. It reads only the selected run, step, assignment, claim, plan and
accepted preparation; no Agent execution run is fabricated for a Workflow executor.
The guard uses the existing root advisory key and locks the exact runtime, assignment,
step and claim rows inside the caller's coordinated transaction. It rechecks current
claim ownership and the immutable assignment fingerprint before and after the
Workflow flush. Any original source catalog and Project lifetime checks remain with
their owning policies. Normal reader factories stay independent, and this guard
explicitly requires PostgreSQL. Dispatch-only Workflow admission does not grant the
project mutation authority required by later native effects.

## Canonical decision child lookup and publication

`EfProcessDecisionChildQuery` addresses one enrolled child and its original allocation,
including retained historical generations, without enumerating execution/event history.
`EfProcessDecisionPublicationFence` enlists in the actual PostgreSQL ReadCommitted effect
transaction and takes the same root advisory lock as runtime commits. It rereads the
complete binding, current generation, runtime phase, closure and budget overrun before
publication. The outer transaction retains the lock through effect commit or rollback.
This condition does not supply source/manager authorization or enable a policy executor.

## Canonical captured material

`EfProcessDecisionMaterialStore` stages one immutable scope and its bounded material
through the existing runtime unit of work. The selected canonical database profile,
original policy namespace, real run/step, executor contract and exact command stamp must
agree. Scope/material, generation, budget settlement, runtime mutation and the small
versioned receipt commit together. Retries compare the retained original, even after
the decision advances. Legacy opaque material IDs remain absent from canonical storage.

Payloads use bounded text, preserving exact UTF-8 content, and captures use UTC ticks
to preserve sub-microsecond identity. Exact reads verify content fingerprints. Unique
scope/generation indexes and restrictive foreign keys prevent ambiguous bindings and
removal of referenced material. Downgrade refuses retained material or newer receipts.
No material is copied into each state/receipt, and effect queries retain small metadata.
Run-wide refresh remains refused by the step owner until the shared parent can fence
and reconcile all affected decisions. Historical material storage alone does not enable
governed Agent/Workflow execution.

Scopes explicitly distinguish executor-bound legacy material from shared business
content. The shared protocol stores its schema ID/version and a null executor;
legacy rows retain their original executor and fingerprint. SQL shape checks and
reader reconstruction reject mixed or unknown formats. Downgrade refuses shared
scopes and their prepared/committed admissions, including before capture. One run
payload can be reused by Agent and Workflow assignments with different execution
contracts when both originally admit the same content schema.

## Root funding and publication

`EfProcessDecisionFundingStore` stages root admission and local funding through the
existing runtime unit of work. A genuine root budget has no step ID and retains the
original admission command with a restrictive receipt reference. Nested launches reuse
that budget after the existing typed parent-step precondition verifies lineage.

Every local reservation and settlement changes its root allocation in the same fenced
transaction. Exact budget/allocation indexes avoid sibling scans on the ordinary path.
Direct root commands cannot take over an allocation already owned by a child. Cancellation
or terminal root state refuses new descendant work; closed owner evidence can still settle
it. Unknown work remains reserved and recorded overruns block new work and publication.

Child publication additionally joins the exact root funding allocation and root runtime
status. The optional parent metadata projection uses nullable fields for legacy steps
without funding. It does not load the root admission payload or scan child journals.
Migration downgrade refuses root admissions, funded allocations and newer receipts,
including prepared launches before execution. Active shared material coordination remains
a separate prerequisite for governed runtime activation.

`EfProcessDecisionRunScopeStore` now owns that active scope boundary inside the same root
transaction. A declared scope exists before capture; its material pointer remains empty
until closed root work and canonical content commit together. Pending capture can only be
settled by its matching completion or explicit abort. Root and local reservations and new
dispatch claims are refused while the scope is frozen.

Bounded membership links each inherited local generation to its original parent material.
Refresh checks the complete membership, actual child rows, cleared claims and all root
allocations. Local inheritance and publication use indexed metadata reads; neither copies
the payload. Old generations remain readable but lose publication authority as soon as the
parent starts reconciliation. The new migration refuses downgrade even for a prepared-only
scope declaration. Executor producers and inherited child journals remain separate work.

## Root cancellation and new-work admission

Persisted root cancellation is monotonic. New descendant work, reservations, claim
renewals, decision enrollment/refresh and shared capture require the root to remain
eligible under the existing root lock. Admission compares persisted claim/step fields,
so a caller cannot disguise lease renewal by modifying its original snapshot.

Finalization checks outstanding runtime, decision, budget and shared-scope work with
one scalar query. Unsettled or uncertain work keeps the root CancelRequested with an
explicit diagnostic. Complete original owner settlement before retrying finalization;
do not combine the last settlement and finalization in one request. Accepted historical
receipts still replay and exact accounting remains legal after stopping. Durable root
recovery and inherited producer activation require their separate integration gates.
