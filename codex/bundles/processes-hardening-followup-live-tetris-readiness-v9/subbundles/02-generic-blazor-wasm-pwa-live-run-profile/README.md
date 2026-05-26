# SB02: Generic Blazor WASM PWA Live-Run Profile

## Status

- Status: `Completed`

## Objective

Create a generic Blazor WASM PWA live-run profile that is separate from seeded regression scenarios and contains no pre-completed transitions or artifacts.

## Covered Inputs

- RQ02 live-run profile separation.
- RQ09 fake-completed baseline confusion.

## Prerequisites

- SB01 build/source gate is complete or explicitly blocked with a safe follow-up.

## Exact Source References

- `repo://Templates/Processes/manifest.json`
- `repo://Templates/Processes/seed-catalog/baseline-scenarios.json`
- `repo://Templates/Processes/README.md`
- `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`

## Deliverables

- Generic live-run profile metadata for Blazor WASM PWA delivery.
- Tests proving live profiles have assignments/guidance but no seeded completed transitions or artifacts.
- Source assertion proving no reusable template or process API skill contains app-topic-specific instructions.

## Dependency Impact

- SB03 through SB16 depend on SB02 because template and proof hardening must target a generic live profile, not seeded regression data.

## Validation Depth

- Critical foundation. Require Semantic Adequacy Gate proof: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and literal raw-note closure.

## Implementation Steps

1. Introduce or update typed run-profile data using generic Blazor WASM PWA language.
2. Keep seeded regression scenarios distinct from live-run profiles.
3. Ensure no live profile contains completed transitions or artifacts.
4. Update tests and process documentation.

## Do Not Do

- Do not encode a demonstration topic into reusable profile names, template steps, role names, skill instructions, or API examples.
- Do not use seeded transition/artifact data as live-run proof.

## Acceptance Checklist

- Generic Blazor WASM PWA live profile exists.
- Live profile has zero seeded transitions and zero seeded artifacts.
- Reusable templates and process API skill are topic-neutral.
- Regression scenario tests still prove typed operation contracts.

## Proof Required

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- `proof/SB02/transcripts/passing.txt`
- `proof/SB02/transcripts/source-assertions.txt`

## Browser Validation Logging

- N/A unless UI profile selection is added in this subbundle; UI route proof moves to SB07/SB14.

## Progression Gate

- SB03 may start only after generic profile separation and anti-topic-specific source assertions pass.

## Suggested Agent Prompt

Implement generic Blazor WASM PWA live-run profile separation, prove it cannot be confused with seeded completed regression data, and record SB02 semantic proof.
