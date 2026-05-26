# SB07: UI Template Import And Run Start Preflight

## Status

- Status: `Completed`

## Objective

Prepare a UI/API preflight path for importing/selecting the generic Blazor WASM PWA template and starting a fresh run from the generic live-run profile.

## Covered Inputs

- RQ06 UI/API preflight.
- RQ09 fake-completed baseline confusion.

## Prerequisites

- SB06 checkpoint A is complete.

## Exact Source References

- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.RuntimeOperations.cs`
- `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackModels.cs`
- `repo://Templates/Processes/manifest.json`
- `repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs`

## Deliverables

- API route, UI sequence, or runbook-ready preflight for generic profile selection and fresh run start.
- Tests or source assertions proving profile metadata is available and fresh starts do not seed completed artifacts.

## Dependency Impact

- SB14 browser harness preparation depends on this preflight path.

## Validation Depth

- API/integration proof plus browser proof if UI is changed.

## Implementation Steps

1. Expose or document generic live-run profile metadata.
2. Ensure run start uses user-supplied topic and project-structure context.
3. Prove seeded regression scenarios are not used as live execution proof.

## Do Not Do

- Do not add a button or route that starts from pre-completed seed data.
- Do not hardcode the future demonstration topic into UI labels or API examples.

## Acceptance Checklist

- Generic profile is discoverable.
- Fresh run start path is clear.
- Profile has no pre-seeded transitions or artifacts.
- UI/API proof is recorded when browser-visible surfaces change.

## Proof Required

- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`
- `proof/SB07/transcripts/passing.txt`
- `proof/SB07/transcripts/source-assertions.txt`

## Browser Validation Logging

- Record route, viewport, actions, assertions, screenshots, and pass/fail result in `bundle://reviews/01-execution-report.md` if UI changes.

## Progression Gate

- SB08 may start after generic preflight is available and validated or explicitly documented as API-only.

## Suggested Agent Prompt

Implement the generic Blazor WASM PWA template import/start preflight and prove it starts fresh live runs rather than seeded completed examples.
