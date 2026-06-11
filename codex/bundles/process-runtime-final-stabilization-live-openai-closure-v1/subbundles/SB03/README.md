# SB03: Deterministic representative runtime matrix rerun

## Status
Prepared.

## Objective
Re-run the deterministic automation matrix for Blazor, software-delivery/multi-team, business-plan PostgreSQL, runtime-host readback, and scheduler/workflow read-only jobs.

## Exact Source References
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

## Implementation Steps
- Run the focused matrix from the previous bundle.
- Ensure automation proof methods do not use `SuppressAutomationDispatch=true`.
- Ensure old manual contract tests remain named/classified as manual_contract or state/contract tests.
- Verify run completion, outbox completion, finalizer summaries, artifacts and execution runs.

## Do Not Do
- Do not extract dispatcher/process runtime core into a new package.
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, or driver self-registration.
- Do not weaken Process Core genericity.
- Do not create proof-heavy churn.

## Acceptance Checklist
- Blazor automation passes.
- Software-delivery/multi-team automation passes.
- Business-plan PostgreSQL automation passes.
- Runtime-host readback on real run/step passes.
- Scheduler/workflow jobs pass.

## Proof Required
- Focused integration transcript.
- SuppressAutomationDispatch scan for automation proof methods.
- PostgreSQL availability / skip/fail classification.

## Browser Validation Logging
N/A.

## Progression Gate
SB04 may start only after deterministic runtime matrix is green or a functional blocker is recorded.
