# Execution Boundary Staging

This legacy proof note records the staged cutline for the process-owned execution boundary.

## SB06 Movement Cutline

- `IProcessAutomationExecutionClient` is the process-owned facade used by dispatcher execution code.
- The first stage may still return selected AgentFramework types while callers are migrated.
- This is not a final `Processes.Core` contract.
- Out-of-scope callers remain manager chat, observation services, recovery worker, UI run-detail loaders.
