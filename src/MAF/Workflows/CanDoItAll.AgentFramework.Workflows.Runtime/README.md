# CanDoItAll.AgentFramework.Workflows.Runtime

Owns workflow run state, checkpoints, artifact content, external requests, progress
observation, usage observations, event payloads, and active-run coordination.

Durable stores and adapters implement runtime contracts; UI progress is a projection and
not a replacement for persisted run state.

```powershell
dotnet build .\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Runtime\CanDoItAll.AgentFramework.Workflows.Runtime.csproj
```
