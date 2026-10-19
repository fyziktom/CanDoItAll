# CanDoItAll.Processes.Runtime

Owns process-manager decisions, step execution, completion gates, subprocess control,
recovery classification, runtime incidents, and durable execution coordination.

All state transitions use typed process contracts and emit records required for
persistence, recovery, and projection. An optional saved project admission binds a run
to one database profile, project and project lifetime. It is an immutable scope fact,
not a permission grant. Legacy runs retain a null admission; observation and existing
unscoped transitions remain supported without deriving authority from launch variables.

SharedKernel's decision budget values and pure transitions reserve bounded child allowances and settle
exact closed-owner usage without resetting consumed work. Historical retries are read-only;
recorded overruns prevent further reservations. These values do not authorize execution
or verify business receipts. Typed accounting commands now let the persistence owner
compare revisions and update the balance/allocation atomically with runtime state under
the existing root fence. Receipts preserve the original accounting result separately
from current state. Actual child-owner settlement verification, generation fencing and
inherited allocations remain required before runtime activation.

The pure decision-generation lifecycle separates active dispatch, manager review and
quiescence. It compares exact generation/revision expectations, retains immutable child
bindings and closed-owner receipt references, and requires every child to close before
supersession or cancellation. Heads and children are separate bounded values; refreshing
material preserves the decision and budget identity. These calculations neither verify
owner evidence nor replace the required durable transaction and effect fences.

```powershell
dotnet build .\src\Processes\CanDoItAll.Processes.Runtime\CanDoItAll.Processes.Runtime.csproj
```

## Decision child occurrences

`ProcessDecisionChildIdentity` derives a versioned child identity from the original
decision, generation, owner and admitted logical ordinal. Retries preserve that ordinal;
claim IDs and mutable inputs do not create new occurrences. `ProcessDecisionChildAdmission`
binds the exact runtime address, material and allocated work. These values do not grant
source permission or prove that an executor supports governed decisions.
