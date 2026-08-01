# CanDoItAll.Memory.Application

Owns Memory provider registration, operation admission and handling, asynchronous workers,
manual source ingestion, source safety policy, retention coordination, and diagnostics.

Drivers implement provider calls and persistence projects implement stores. Application
semantics remain transport-neutral.

```powershell
dotnet build .\src\Memory\CanDoItAll.Memory.Application\CanDoItAll.Memory.Application.csproj
```
