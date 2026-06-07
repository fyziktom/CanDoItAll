# SB002 — Phase 1 / Active architecture guard setup

## Status

- Status: `Completed`
## Objective

Add or update tests that fail if the active bundle collapses report rows, creates driver APIs, or leaks forbidden dependencies.

## Covered Inputs

- User request to move toward Process Core safely.
- Latest branch proof that only a narrow pure-rule Core seed is ready.
- Constraint to avoid production driver APIs.

## Prerequisites

- Previous subbundle gates complete.
- Branch is `maf-processes-refactor`.
- Active architecture guards are updated for this bundle.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `repo://src/CanDoItAll.Processes.Contracts`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration`

## Deliverables

- Production or documentation/test-only changes as specified by this subbundle.
- Updated proof transcript under this bundle's `proof/SB002/`.
- Execution report row updated individually.

## Dependency Impact

- Downstream subbundles cannot rely on this slice until its proof passes.
## Validation Depth

- Architecture tests must fail before implementation if forbidden tokens are intentionally seeded in a local scratch check.
## Implementation Steps

1. Re-open exact source files and confirm current state.
2. Make the smallest behavior-preserving change for this subbundle.
3. Run targeted build/test/source scans.
4. Record proof transcript.
5. Update execution report row.

## Scope Exceptions

No broad Process Core split. No driver runtime/API implementation.

## Do Not Do

Do not move code yet.

## Acceptance Checklist

- [x] Behavior preserved.
- [x] No forbidden Core dependencies.
- [x] No production driver API.
- [x] No UI/media drift.
- [x] Tests/scans recorded.
- [x] Execution report row updated.

## Proof Required

- Build/test/source scan proof appropriate to subbundle scope.
- Critical gates must include semantic invariants and red-team checks.

## Browser Validation Logging

- N/A — runtime/service/refactor bundle. If UI files change unexpectedly, fail this subbundle.
## Progression Gate

- Do not continue to dependent work until acceptance checklist is complete.
## Suggested Agent Prompt

Implement SB002 only. Preserve behavior. Do not broaden scope. Record proof before proceeding.
