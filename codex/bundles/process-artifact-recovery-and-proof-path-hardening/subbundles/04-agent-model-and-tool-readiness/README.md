# SB04: Agent Model And Tool Readiness

## Status

- Status: `Completed`
- Critical foundation: `Yes`

## Scope

- Verify HR-selected agents are capable of doing the work the process assigns.
- Verify CanDoItAll agents use `gpt-5.4-mini`.
- Verify missing permissions are corrected through generic configuration or process instructions.

## Objective

Prevent a correct process definition from failing because HR assigned agents without model, tool, or permission readiness.

## Covered Inputs

- Follow-up request `03-live-blazor-delivery-request`
- `R009`
- `R011`

## Prerequisites

- SB03 template work complete.
- Runtime API host available.

## Exact Source References

- `repo://src/CanDoItAll.Web/Api/AgentsApi.cs`
- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`
- `repo://src/CanDoItAll.Modules.Processes/Launch`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools`

## Dependency Impact

- Agent/provider/process configuration only unless a generic capability exposure defect is found.
- No generated demo app edits.

## Validation Depth

- API transcript of providers, agents, launch plan, and assignments.
- Capability verdict table.

## Contract

- CanDoItAll agent provider/model selection for the run uses `gpt-5.4-mini`.
- Cognitive memory stays disabled.
- Assigned implementation agents have workspace file/command, dotnet, process artifact, and project-structure read/write tools.
- Assigned QA agents have browser/Playwright evidence tools, screenshot capture, console capture, image inspection, process artifact, and project-structure asset writeback tools.
- Manager/release roles can record decisions, approvals, run summaries, and UX/process observations.

## Implementation Steps

- Read provider and agent data through API.
- Ensure selected provider/model is `gpt-5.4-mini`.
- Create or update generic capability/agent configuration only if a real gap is found.
- Capture HR launch-plan assignment and capability audit before execution.

## Do Not Do

- Do not bypass HR assignment by manually choosing agents unless the launch-plan workflow supports the selection.
- Do not use a non-PostgreSQL runtime.
- Do not re-enable cognitive memory.

## Acceptance Checklist

- [x] HR launch-plan assignments are exported before run execution.
- [x] Every selected agent has the required tool/capability set for its role.
- [x] Every selected agent uses `gpt-5.4-mini`.
- [x] Cognitive memory is disabled.

## Proof Required

- `bundle://proof/SB04/manifest.md`
- `bundle://proof/SB04/transcripts/agent-readiness.json`
- `bundle://proof/SB04/transcripts/agent-seed-tests.txt`

## Browser Validation Logging

- Not applicable. This subbundle validates agent/tool readiness, not rendered UI.

## Progression Gate

- SB04 passes when all selected agents are ready or the run is blocked with a concrete generic configuration gap.

## Suggested Agent Prompt

Use `bundle://shared-prompts/implementation-prompt.md`.
