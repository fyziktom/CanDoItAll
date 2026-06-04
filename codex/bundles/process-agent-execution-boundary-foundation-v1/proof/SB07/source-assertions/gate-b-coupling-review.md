# SB07 Gate B Coupling Review Source Assertions

- Dispatcher direct `workspaceService.*` calls fell from 26 in the SB05 baseline to 0 after SB06.
- Dispatcher execution coupling now appears as 26 `executionClient.*` call sites, preserving the same operation count behind `IProcessAutomationExecutionClient`.
- Remaining `IAgentFrameworkWorkspaceService` usage in the Processes module is intentionally outside the dispatcher movement scope: `ProcessAutomationExecutionClient`, `ProcessRunRecoveryWorker`, `ProcessesService`, observation/manager chat services, and UI run-detail loader paths.
- `ProcessRunAutomationDispatchService` remains large. Gate B does not justify a broad extraction yet; it records the next pressure points as artifact validation/projection, step finalization, tooling validation, and concurrency responsibilities.
- Process runtime tool names and purpose/access behavior stayed stable through `ProcessAgentRuntimeToolProviderTests`, `MafAgentRuntimeToolProviderCompositionTests`, and `ProcessRuntimeToolProviderCompositionIntegrationTests`.
- Receipt projection still passes through `AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests`, so the execution-client movement did not weaken tool receipt visibility.
- No Process Core or driver-pack project was introduced.
- Browser validation is N/A because SB07 changed no rendered UI route and produced no screenshots.
