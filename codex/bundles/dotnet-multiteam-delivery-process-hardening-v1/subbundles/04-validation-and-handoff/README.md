# Validation and handoff

## Status

- `Ready`

## Objective

Validate the process template changes, close raw notes, and leave the CanDoItAll app running for the user's process-run tests.

## Covered Inputs

- R09, R10 and closure proof for R01 through R08

## Prerequisites

- SB02 and SB03 closure gates passed.

## Exact Source References

- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessSubprocessIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Components/ProcessCanvasRecompositionServiceTests.cs`
- `repo://CanDoItAll.slnx`

## Deliverables

- Targeted test transcript.
- Build transcript or exact blocker.
- Final bundle validation transcript.
- App running URL/process details.
- Raw-note closure table.

## Dependency Impact

- This is closure. If proof is incomplete, the user cannot trust the live process test results.

## Validation Depth

- Process-critical closure

## Implementation Steps

1. Run targeted process template tests.
2. Run build or the narrowest practical test suite.
3. Run completed-stage bundle validation.
4. Start CanDoItAll app without starting a delivery process.
5. Update execution report and raw-note closure.

## Scope Exceptions

- Do not run the software-delivery process. The user will do that with test projects.

## Do Not Do

- Do not seed or launch a live delivery run.
- Do not hide failed tests as residual risk.

## Acceptance Checklist

- Targeted tests pass.
- Bundle completed-stage validation passes or exact residual blocker is recorded.
- App is running and URL is reported.
- Final response notes that the process was not run.

## Proof Required

- Test/build transcripts.
- Source assertion artifacts.
- App startup command and URL.

## Browser Validation Logging

- N/A for template changes. App startup URL is reported for user-led manual/process testing.

## Progression Gate

- Final response only after validation is complete or a concrete blocker is recorded.

## Suggested Agent Prompt

```text
Validate the completed process-template change. Do not run the delivery process. Start the app for the user's test run and report the URL.
```
