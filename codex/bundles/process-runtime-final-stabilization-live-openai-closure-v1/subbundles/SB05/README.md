# SB05: Boundary and regression scans

## Status
Prepared.

## Objective
Ensure stabilization did not reintroduce architectural drift while fixing functional/runtime issues.

## Exact Source References
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Modules.Processes`
- `repo://src/CanDoItAll.Processes.Contracts`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs`

## Implementation Steps
- Scan Process Core for template/domain/runtime/driver/EF/UI/AgentFramework/OpenAI leakage.
- Scan driver/runtime-host paths for effectful APIs.
- Scan scheduler/workflow paths for direct driver hooks.
- Scan source/tests for unintended concrete bundle-path coupling.
- Run relevant boundary unit tests.

## Do Not Do
- Do not extract dispatcher/process runtime core into a new package.
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, or driver self-registration.
- Do not weaken Process Core genericity.
- Do not create proof-heavy churn.

## Acceptance Checklist
- Process Core remains generic.
- No execution-capable host or driver registry added.
- No fallback selector/reflection discovery/self-registration.
- No hidden process mutation through drivers.

## Proof Required
- Source scans.
- Boundary unit tests.
- Anti-stub scan.

## Browser Validation Logging
N/A.

## Progression Gate
SB06 may start only after boundary scans pass.
