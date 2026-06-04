# AgentFramework Usage In Processes Inventory Task

Codex must generate this inventory in SB02 before implementing movement.

Required grep/source analysis:

- `using CanDoItAll.AgentFramework.Core`
- `using CanDoItAll.AgentFramework.Models`
- `IAgentFrameworkWorkspaceService`
- `IAiTechnicalAgentBridge`
- `ExecutionRunRequest`
- `ExecutionRunResult`
- `ExecutionRunDetail`
- `ExecutionInvocationContext`
- `ExecutionInvocationPolicy`
- `AgentChatRunFailedException`
- `AgentRunFailedException`
- `ToolReceipt`
- `StructuredOutput`
- `Finalizer`
- direct calls to `ExecuteRunAsync`
- direct calls to `GetExecutionRunDetailAsync`
- direct calls to `ListExecutionRunsAsync`

Output must include file path, line count, usage kind, whether in dispatcher, and proposed owner after this bundle.
