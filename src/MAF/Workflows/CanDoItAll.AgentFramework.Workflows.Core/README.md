# CanDoItAll.AgentFramework.Workflows.Core

Owns workflow definition validation, catalogs, launch idempotency, analytics, activity
queries, diagnostics, and core service registration.

Stored CanDoItAll workflow definitions are canonical. Execution backends consume the
validated definition through abstractions.

```powershell
dotnet build .\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Core\CanDoItAll.AgentFramework.Workflows.Core.csproj
```
