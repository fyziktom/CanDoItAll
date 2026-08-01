# CanDoItAll.AgentFramework.WorkflowExecutors.Standard

Provides the dependency-injection entry point that registers the complete built-in
workflow executor set.

Concrete executors remain in focused projects so optional document, media, network, and
workspace dependencies do not leak into the core executor boundary.

```powershell
dotnet build .\src\MAF\WorkflowExecutors\Standard\CanDoItAll.AgentFramework.WorkflowExecutors.Standard\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.csproj
```
