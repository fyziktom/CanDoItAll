# SB09: Process Step Work Briefs For Agent Limitations

## Status

- Status: `Completed`

## Objective

Ensure generated work briefs expose agent limitations, required artifacts, missing prerequisites, and recovery paths for generic Blazor WASM PWA live runs.

## Covered Inputs

- RQ07 step-by-step limitation visibility.
- RQ03 downstream step contract clarity.

## Prerequisites

- SB08 assignment and tool profile validation is complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Work brief text that names required tools, current step boundary, upstream artifacts, output artifacts, and recovery expectations.
- Tests/source assertions proving validation and writeback briefs do not ask agents to mutate product code.

## Dependency Impact

- SB10 artifact proof depends on clear work brief handoffs.

## Validation Depth

- Focused tests for work brief generation and negative source assertions for unsafe instructions.

## Implementation Steps

1. Audit work brief generation for generic Blazor WASM PWA runs.
2. Add missing limitation and artifact language.
3. Add tests for implementation, validation, repair, and writeback brief boundaries.

## Do Not Do

- Do not hide missing tools or missing artifacts behind generic retry language.
- Do not put app-topic-specific acceptance criteria into reusable brief templates.

## Acceptance Checklist

- Work briefs identify current step responsibilities.
- Work briefs identify missing capability and artifact blockers.
- Review and validation briefs remain non-mutating.

## Proof Required

- `proof/SB09/manifest.md`
- `proof/SB09/semantic-invariants.md`
- `proof/SB09/transcripts/passing.txt`
- `proof/SB09/transcripts/source-assertions.txt`

## Browser Validation Logging

- N/A unless work brief UI rendering changes.

## Progression Gate

- SB10 may start after work brief tests/source assertions show clear artifact and recovery handoffs.

## Suggested Agent Prompt

Harden generic Blazor WASM PWA work briefs so limitations, artifacts, tools, and recovery actions are explicit at each step.
