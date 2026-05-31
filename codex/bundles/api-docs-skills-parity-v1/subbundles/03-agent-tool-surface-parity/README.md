# Agent Tool Surface Parity

## Status

- `Completed`

## Objective

- Resolve the mismatch between HTTP API capabilities and internal agent runtime tools for processes and project structure.

## Success Criteria

- Missing process/project-structure tool areas are implemented with strongly typed tool requests, service calls, policy constants, approval behavior, and tests, or are explicitly documented as HTTP-only operations.
- Skills and docs no longer imply direct runtime tool support where none exists.

## Covered Inputs

- RQ-003 agent runtime tool parity.
- GAP-010 process tools: 23 runtime tools versus 58 HTTP routes.
- GAP-011 project-structure tools: 28 runtime tools versus 51 HTTP routes.

## Prerequisites

- SB01 workbook regenerated and reviewed.
- SB02 route contract decisions reviewed if a tool depends on an API route that might be hidden or changed.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProjectStructureTools.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`
- `repo://src/CanDoItAll.Web/ProjectStructureAgentApi.cs`
- `repo://tests`

## Deliverables

- Implemented missing tools for selected parity gaps or a written HTTP-only exception map.
- Updated policy constants and approval requirements for new tools.
- Focused tests for descriptors, request execution, and approval policy behavior.
- Workbook and execution report updated with final tool decisions.

## Dependency Impact

- SB04 docs and SB05 skills depend on this decision because agent-facing guidance must not overclaim runtime capabilities.
- Security and approval behavior depend on policy updates if tools are added.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Compare Tool Parity workbook rows to current MAF tool source.
2. Prioritize operations that agents need for process orchestration and project updates.
3. For each operation, choose implementation or explicit HTTP-only documentation.
4. For implemented tools, add typed request records, descriptors, service calls, policy constants, approval behavior, and tests.
5. Regenerate the workbook if tool counts or decisions change.
6. Record final parity decisions and test proof.

## Scope Exceptions

- Do not implement a broad generic HTTP caller as a substitute for typed tools.
- Do not add tools for plugin/project APIs unless the phase explicitly expands scope and records why.

## Do Not Do

- Do not silently route missing tools through hidden fallback logic.
- Do not add a descriptor without policy and approval review.
- Do not claim parity before tests cover the new path.

## Acceptance Checklist

- Every process/project-structure tool gap is marked implemented, deferred, or HTTP-only with a reason.
- New tools have policy constants and approval decisions.
- Focused tests pass or blockers are recorded.
- Docs/skills downstream have a clear capability map.

## Proof Required

- Focused `dotnet test` command covering `MafAgentRuntime` and `AgentToolInvocationPolicy`.
- Updated workbook Tool Parity sheet if counts or decisions changed.
- Execution report entries for each implemented or intentionally HTTP-only operation group.

## Browser Validation Logging

- `N/A` unless tool work changes visible Blazor configuration pages. If UI changes occur, add Playwright route, viewport, actions, screenshot, and result.

## Progression Gate

- SB04 and SB05 may not claim direct agent-tool capability until this subbundle records implemented tools or explicit HTTP-only exceptions.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Resolve process and project-structure runtime tool parity with typed tools or explicit HTTP-only decisions. Update policy and tests with every implemented tool, record proof, and stop if a security or approval boundary is unclear.
```
