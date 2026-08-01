# CanDoItAll.Processes.Runtime

Owns process-manager decisions, step execution, completion gates, subprocess control,
recovery classification, runtime incidents, and durable execution coordination.

All state transitions use typed process contracts and emit records required for
persistence, recovery, and projection.

```powershell
dotnet build .\src\Processes\CanDoItAll.Processes.Runtime\CanDoItAll.Processes.Runtime.csproj
```
