# SB07 - Exact Runtime Tool Preflight

## Status

- `Completed`
- Critical foundation: yes

## Objective

Block mandatory missing, denied, or uncomposed runtime tools before agent execution, using the exact governed process context instead of agent metadata alone.

## Covered Inputs

- F08, F10.
- R10, R15.
- GPTPro B04.

## Prerequisites

- SB02 blocked packet categories complete.
- SB01 inventory of required tool surfaces complete.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Agents/AgentProcessReadinessEvaluator.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`

## Deliverables

- `IProcessRuntimeToolPreflightService` and typed result model.
- Required tool source model: capability scope, required receipts, launch context, subprocess contract, validation/mutation requirement, runtime-owned tool.
- Preflight categories: `required_tool_not_composed`, `required_tool_denied`, `required_tool_missing_provider`, `required_tool_missing_agent_capability`, `required_tool_missing_process_scope`.
- Dispatch integration that prevents `ExecuteRunAsync` when mandatory preflight fails.
- Diagnostics consumed by blocked packet/operator action.

## Dependency Impact

- SB08 template hardening can require exact tools only after preflight exists. SB09 must prove missing tools do not waste LLM runs.

## Validation Depth

- Critical foundation with semantic adequacy gate.

## Implementation Steps

1. Inventory all current required runtime tool sources.
2. Define typed required-tool record with tool name, source, required/optional/runtime-owned flag, process run id, step id, agent id, and remediation hint.
3. Implement preflight abstraction and production implementation using composed provider/tool data available at dispatch time.
4. Integrate before claim/agent execution where mandatory tools are known.
5. Return deterministic `NeedsManager`/blocked diagnostics with blocked packet category.
6. Add tests for missing provider, not composed, denied scope, missing capability, missing process scope, and available tool.

## Scope Exceptions

- Do not replace all existing readiness evaluator behavior. This is execution-time preflight, not broad agent selection redesign.
- Do not require preflight for runtime-owned tools that are not exposed to agents unless the runtime itself depends on a provider and can detect absence.

## Do Not Do

- Do not launch the agent to discover a mandatory missing tool.
- Do not rely only on `AllowedOperations`.
- Do not use magic strings where `ToolContractCatalog` or constants exist.
- Do not treat denied and uncomposed as the same failure.

## Acceptance Checklist

- [ ] Missing `project_structure_process_subprocess_launch` blocks before agent execution when agent-owned fallback is mandatory.
- [ ] Denied process scope reports run id, step id, allowed operations, and required operation.
- [ ] Missing workspace build/test tools are categorized before LLM execution.
- [ ] Available tools allow normal execution.
- [ ] Operator packet includes tool preflight category and remediation.

## Proof Required

- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`
- Failing-first transcript for missing tool launching agent today.
- Passing preflight tests for each category.
- Source assertion that dispatch checks preflight before `ExecuteRunAsync`.
- Changed-file hashes.
- Anti-stub audit.
- Production Behavior Artifact Matrix for preflight result and diagnostic categories.

## Browser Validation Logging

- `N/A`.

## Progression Gate

- SB08 may mark template tools as required only after preflight blocks before agent execution and diagnostics reach operator packet.

## C# Architecture Impact

Adds execution-time tool availability boundary.

## Boundary Ownership

Dispatch/application owns preflight decision; provider/tool composition details stay in module/MAF implementation.

## Dependency Direction

Do not make MAF tool policy depend on process templates. Use tool catalog constants and process context metadata.

## Pattern Decision

Strategy with typed denial categories.

## Testability Contract

Tests fake composed provider/tool catalog and assert `ExecuteRunAsync` is not invoked on failure.

## Partial Class Policy

Do not grow `AgentToolInvocationPolicy` or dispatch service with broad inline preflight logic.

## Architecture Proof Required

- Source assertion for focused preflight service.
- Direct tests and dependency check.

## Suggested Agent Prompt

```text
Execute SB07 only. Add exact runtime tool preflight before agent execution. Prove missing/denied/uncomposed tools produce deterministic diagnostics and no LLM run.
```
