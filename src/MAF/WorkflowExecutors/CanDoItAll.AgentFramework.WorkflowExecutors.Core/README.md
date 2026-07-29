# CanDoItAll.AgentFramework.WorkflowExecutors.Core

Owns executor descriptors, catalog registration, policy limits, invocation, JSON mapping,
failure diagnostics, contributions, and observability.

Concrete behavior belongs in a focused executor project. The core invokes only validated
descriptors through typed contracts.

```powershell
dotnet build .\src\MAF\WorkflowExecutors\CanDoItAll.AgentFramework.WorkflowExecutors.Core\CanDoItAll.AgentFramework.WorkflowExecutors.Core.csproj
```
