# CanDoItAll.AgentFramework.Workflows.MafAdapter

Compiles validated CanDoItAll workflow definitions into Microsoft Agent Framework
execution, invokes LLM components, normalizes events, and maps backend diagnostics.

This project is an execution adapter. It does not own the stored workflow definition,
catalog, or application-facing runtime contracts.

```powershell
dotnet build .\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.MafAdapter\CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj
```
