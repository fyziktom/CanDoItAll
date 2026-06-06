# AgentFramework Usage In Processes

Legacy inventory restored for architecture guard coverage.

| Source | Direct calls | Disposition |
| --- | --- | --- |
| `ProcessRunAutomationDispatchService.Execution.cs` | `ExecuteRunAsync`, `GetExecutionRunDetailAsync`, `ListExecutionRunsAsync` | Process automation execution client facade where execution-path related |

Out of dispatcher-boundary scope for this bundle: manager chat, observation services, recovery worker, UI run-detail loaders.
