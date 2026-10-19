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
or verify business receipts. Their durable owner must compare revisions and update the
balance/allocation atomically under the existing root fence before runtime activation.

```powershell
dotnet build .\src\Processes\CanDoItAll.Processes.Runtime\CanDoItAll.Processes.Runtime.csproj
```
