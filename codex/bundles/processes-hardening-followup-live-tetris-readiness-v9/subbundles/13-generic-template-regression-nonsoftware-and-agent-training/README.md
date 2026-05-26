# SB13: Generic Template Regression Nonsoftware And Agent Training

## Status

- Status: `Completed`

## Objective

Prove Blazor WASM PWA hardening does not break non-software templates, agent-improvement patterns, training processes, or generic process runtime behavior.

## Covered Inputs

- RQ08 generic runtime breadth.
- RQ09 regression red-team checks.

## Prerequisites

- SB12 runtime health diagnostics are complete.

## Exact Source References

- `repo://Templates/Processes/processes/business-plan-development/definition.json`
- `repo://Templates/Processes/processes/ai-assisted-change-delivery/definition.json`
- `repo://Templates/Processes/processes/architecture-decision-governance/definition.json`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`

## Deliverables

- Regression proof that generic process templates still load and enforce typed contracts.
- Tests/source assertions proving Blazor-specific requirements do not leak into non-Blazor templates.

## Dependency Impact

- SB14 can prepare a live Blazor WASM PWA run only after generic runtime breadth is preserved.

## Validation Depth

- Integration regression across process template pack and seed catalog.

## Implementation Steps

1. Audit non-Blazor template definitions and seed scenarios.
2. Add tests/source assertions that Blazor-specific text stays scoped to Blazor app delivery assets.
3. Validate typed contracts across all manifest templates.

## Do Not Do

- Do not add Blazor/browser proof requirements to non-browser templates.
- Do not reduce the process runtime to a software-only path.

## Acceptance Checklist

- Non-software templates still load.
- Typed operation contract tests cover all manifest templates.
- Blazor-specific proof requirements are scoped.

## Proof Required

- `proof/SB13/manifest.md`
- `proof/SB13/semantic-invariants.md`
- `proof/SB13/transcripts/passing.txt`
- `proof/SB13/transcripts/source-assertions.txt`

## Browser Validation Logging

- N/A unless non-software template UI changes.

## Progression Gate

- SB14 may start after generic regression proof passes.

## Suggested Agent Prompt

Run generic template regression to prove Blazor WASM PWA hardening remains scoped and non-software process patterns still work.
