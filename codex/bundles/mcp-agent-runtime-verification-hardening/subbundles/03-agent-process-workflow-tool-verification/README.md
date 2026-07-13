# 03 Agent Process Workflow Tool Verification

## Status

- `Completed`

## Objective

Prove project-structure, workflow, and process agent tooling still works through internal runtime providers and typed access metadata.

## Success Criteria

- Integrated agents do not attach legacy project/process MCP capabilities.
- Delivery agents have project-structure and process access metadata.
- MAF runtime-provider filtering keeps tools within process operation contracts.
- Project-structure process launch integration paths pass.
- `/projects`, `/agents/workflows`, and `/processes` load at large-screen size.

## Covered Inputs

- R004 Agent Project/Process/Workflow Tool Access
- R005 Large-Screen UI Verification

## Prerequisites

- `01-mcp-setup-runtime-repair`
- `02-database-catalog-compatibility`

## Exact Source References

- `repo://src/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Agents/Access/AgentProjectStructureAccessModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Agents/Access/AgentProcessAccessModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.RuntimeToolProviders.cs`
- `repo://tests/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/CanDoItAll.Tests.Components/ProjectStructureProcessAssignmentDialogTests.cs`
- `repo://tests/CanDoItAll.Tests.Components/ContextualAgentAccessResolverTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs`

## Deliverables

- Verified seeded agents use typed internal project/process access metadata.
- Verified runtime-provider filtering against process contracts.
- Verified process launch integration from project structure.
- Verified related pages by Playwright MCP at `1920x1080`.

## Dependency Impact

- This subbundle validates that MCP setup hardening did not regress the separate internal tool path used by project-structure, workflows, and processes.

## Validation Depth

- Process-critical closure

## Implementation Steps

1. Inspect internal runtime-provider and access metadata code paths.
2. Run focused unit tests for runtime-provider composition.
3. Run component tests for contextual filtering and assignment UI.
4. Run integration tests for seed access and process launch behavior.
5. Run large-screen browser smoke checks for projects, workflows, and processes.

## Scope Exceptions

- No small or medium viewport validation by user request.

## Do Not Do

- Do not add broad process tools to all agents.
- Do not bypass process operation-contract filtering.
- Do not reintroduce old project/process MCP capabilities.

## Acceptance Checklist

- `MafAgentRuntimeToolProviderCompositionTests` passed.
- `ProjectStructureProcessAssignmentDialogTests` passed.
- `ContextualAgentAccessResolverTests` passed.
- Project-structure process launch integration tests passed.
- Large-screen Playwright MCP smoke passed for `/projects`, `/agents/workflows`, and `/processes`.

## Proof Required

- Unit, component, and integration test output.
- `projects-large-screen.png`
- `workflows-large-screen.png`
- `processes-large-screen.png`

## Browser Validation Logging

- Routes: `/projects`, `/agents/workflows`, `/processes`
- Viewport: `1920x1080`
- Required actions: navigate, assert meaningful page text, assert no Blazor error banner, screenshot.
- Screenshots: `projects-large-screen.png`, `workflows-large-screen.png`, `processes-large-screen.png`

## Progression Gate

- Downstream closure may continue only after backend access tests and large-screen UI smoke both pass.

## Suggested Agent Prompt

```text
Implement this subbundle only. Prove that internal project-structure, workflow, and process tools are available through typed access metadata and runtime providers, then capture large-screen browser proof.
```
