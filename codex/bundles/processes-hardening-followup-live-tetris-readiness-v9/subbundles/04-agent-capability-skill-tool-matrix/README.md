# SB04: Agent Capability Skill Tool Matrix

## Status

- Status: `Completed`

## Objective

Define and validate the role-to-skill-to-tool matrix required for generic Blazor WASM PWA delivery without unsafe extra mutation rights.

## Covered Inputs

- RQ04 role skill/tool readiness.
- RQ07 visible agent limitations.

## Prerequisites

- SB03 template operation contracts are complete.

## Exact Source References

- `repo://Templates/Processes/processes/blazor-app-delivery/definition.json`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.GovernedRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Source or template updates that make required Blazor/browser/project-structure/process tools explicit for assigned roles.
- Tests proving missing required tools result in not-ready or typed blocked state instead of improvisation.

## Dependency Impact

- SB08 depends on this matrix to validate assignments and launch readiness.

## Validation Depth

- Focused integration/unit proof for required tool resolution and mutation-boundary alignment.

## Implementation Steps

1. Audit required tools inferred from Blazor WASM PWA implementation, validation, and writeback contracts.
2. Add missing role/tool/skill guidance using generic wording.
3. Add or update tests for positive and missing-tool paths.

## Do Not Do

- Do not grant write tools to review-only steps.
- Do not rely on free-text role names when a typed contract or constant is available.

## Acceptance Checklist

- Implementation role has build-capable workspace tools.
- Validation role has runtime/browser proof tools without product mutation.
- Writeback role has controlled project-structure external action tools.
- Missing required tools produce actionable readiness diagnostics.

## Proof Required

- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`
- `proof/SB04/transcripts/passing.txt`
- `proof/SB04/transcripts/source-assertions.txt`

## Browser Validation Logging

- N/A. This subbundle validates tool readiness, not rendered UI.

## Progression Gate

- SB05 may start after role/tool readiness tests or source assertions pass.

## Suggested Agent Prompt

Harden the generic Blazor WASM PWA role, skill, and tool matrix, then prove missing tools block predictably.
