# SB08: Agent Assignment And Tool Profile Validation

## Status

- Status: `Completed`

## Objective

Validate that agent assignment and workspace tool profiles are sufficient for generic Blazor WASM PWA live runs and block predictably when required capabilities are absent.

## Covered Inputs

- RQ04 role/tool readiness.
- RQ07 visible limitations.

## Prerequisites

- SB07 preflight is complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Cooperation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.GovernedRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.Staffing.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Tests proving missing Blazor/PWA/browser/project-structure/process tools result in typed not-ready or blocked state.
- Source assertions proving review-only assignments do not get product mutation rights.

## Dependency Impact

- SB09 and SB10 depend on trustworthy assignment/tool diagnostics.

## Validation Depth

- Critical foundation. Require semantic positive and adversarial missing-tool proof.

## Implementation Steps

1. Audit assignment and tool profile resolution for generic Blazor WASM PWA runs.
2. Add tests for complete and incomplete capability sets.
3. Ensure diagnostics name missing tools or skills explicitly.

## Do Not Do

- Do not silently fall back to broad workspace access.
- Do not infer tool safety from topic-specific role names.

## Acceptance Checklist

- Implementation assignment can build and mutate only in allowed steps.
- Validation assignment can run/browser-proof without product mutation.
- Missing required tools are visible before or during dispatch.

## Proof Required

- `proof/SB08/manifest.md`
- `proof/SB08/semantic-invariants.md`
- `proof/SB08/transcripts/failing-first.txt`
- `proof/SB08/transcripts/passing.txt`

## Browser Validation Logging

- N/A unless assignment readiness UI changes; otherwise record API/test proof.

## Progression Gate

- SB09 may start only after assignment/tool validation proves both positive and missing-capability paths.

## Suggested Agent Prompt

Harden generic Blazor WASM PWA assignment and tool profile validation, then prove missing capabilities block with actionable typed diagnostics.
