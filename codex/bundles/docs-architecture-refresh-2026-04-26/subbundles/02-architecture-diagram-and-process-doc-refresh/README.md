# architecture-diagram-and-process-doc-refresh

## Status

- `Completed`

## Objective

- Add a source-grounded architecture-beta document with architecture overview, C4 diagrams, and sequence diagrams, with detailed coverage of process execution by AI agents.

## Covered Inputs

- `N002`: repair docs to match actual architecture.
- `N003`: add architecture-beta, C4, and sequence diagrams.
- `N004`: explain running processes with AI agents.

## Prerequisites

- `01-architecture-inventory-and-doc-audit` completed and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\DependencyInjection\InfrastructureServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.RunStart.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.StepTransitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkAiTechnicalAgentBridge.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\MafAgentRuntime.Capabilities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\MafAgentRuntime.Capabilities.Mcp.cs`

## Deliverables

- `docs/architecture-beta.md`.
- Mermaid `architecture-beta` overview diagram.
- Mermaid C4 context, container, and process/agent component diagrams.
- Mermaid sequence diagrams for startup, process AI-agent execution, and AgentFramework tool/artifact execution.

## Dependency Impact

- `03` depends on this page for root README links and project README architecture pointers.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Create `docs/architecture-beta.md`.
2. Document current architecture boundaries from source references.
3. Add requested diagram families.
4. Add detailed process AI-agent execution explanation.
5. Keep claims aligned with actual services and files.

## Scope Exceptions

- No rendered Mermaid screenshot is required because no docs-rendering tool is configured in this repo.

## Do Not Do

- Do not invent APIs, deployment targets, or future-state behavior.
- Do not change product code.

## Acceptance Checklist

- Architecture doc includes `architecture-beta`.
- Architecture doc includes C4 diagrams.
- Architecture doc includes sequence diagrams.
- Process AI-agent runtime is explained from run start through artifact projection and dependency progression.

## Proof Required

- Text checks for `architecture-beta`, `C4Context`, `C4Container`, `C4Component`, and `sequenceDiagram`.
- Manual review against source references.

## Browser Validation Logging

- N/A. This subbundle changes Markdown documentation only.

## Progression Gate

- Passed. `docs/architecture-beta.md` exists and contains `architecture-beta`, C4, and sequence diagrams.

## Suggested Agent Prompt

```text
Execute subbundle 02 only. Author the architecture-beta document from the source references, include architecture-beta/C4/sequence diagrams, and explain process execution with AI agents in detail.
```
