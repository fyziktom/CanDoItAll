# CanDoItAll.Memory.Persistence

Implements EF Core stores for Memory operations, events, feedback, provider profiles,
source requests, retention, and worker leases.

Persistence maps provider-neutral records to the canonical PostgreSQL application model.
It does not execute provider operations.

```powershell
dotnet build .\src\Memory\CanDoItAll.Memory.Persistence\CanDoItAll.Memory.Persistence.csproj
```
