# CanDoItAll.Processes.Persistence

Implements EF Core stores for process plans, runtime events, runs, artifacts, assignments,
outbox records, projections, history, and recovery lineage.

Transactions and concurrency rules follow the process application/runtime contracts and
the canonical PostgreSQL database.

```powershell
dotnet build .\src\Processes\CanDoItAll.Processes.Persistence\CanDoItAll.Processes.Persistence.csproj
```
