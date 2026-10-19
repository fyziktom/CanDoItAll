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
