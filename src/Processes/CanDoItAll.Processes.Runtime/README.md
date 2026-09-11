# CanDoItAll.Processes.Runtime

Owns process-manager decisions, step execution, completion gates, subprocess control,
recovery classification, runtime incidents, and durable execution coordination.

All state transitions use typed process contracts and emit records required for
persistence, recovery, and projection. An optional saved project admission binds a run
to one database profile, project and project lifetime. It is an immutable scope fact,
not a permission grant. Legacy runs retain a null admission; observation and existing
unscoped transitions remain supported without deriving authority from launch variables.

```powershell
dotnet build .\src\Processes\CanDoItAll.Processes.Runtime\CanDoItAll.Processes.Runtime.csproj
```
