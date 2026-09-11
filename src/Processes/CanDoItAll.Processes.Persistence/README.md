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

The three nullable admission columns retain existing rows without inventing an actor,
permission or lifetime. A complete tuple is immutable, including on replay and child
run creation. Subsequent state transitions can still cancel or observe a run after its
project is retired; every new external effect needs its own current authority check.
The nineteen-entity owner model also retains prepared launch payloads, their original
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
