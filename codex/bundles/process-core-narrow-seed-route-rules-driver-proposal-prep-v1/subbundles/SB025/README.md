# SB025 — Phase 9 / Broad smoke matrix

## Status

Prepared.

## Objective

Run full solution build, full unit tests, focused dispatch/route/subprocess/projection integration tests.

## Covered Inputs

- User request to move toward Process Core safely.
- Latest branch proof that only a narrow pure-rule Core seed is ready.
- Constraint to avoid production driver APIs.

## Prerequisites

- Previous subbundle gates complete.
- Branch is `maf-processes-refactor`.
- Active architecture guards are updated for this bundle.

## Exact Source References

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/`
- `src/CanDoItAll.Processes.Contracts/`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/`

## Deliverables

- Production or documentation/test-only changes as specified by this subbundle.
- Updated proof transcript under this bundle's `proof/SB025/`.
- Execution report row updated individually.

## Dependency Impact

Downstream subbundles cannot rely on this slice until its proof passes.

## Validation Depth

Smoke proof generated.

## Implementation Steps

1. Re-open exact source files and confirm current state.
2. Make the smallest behavior-preserving change for this subbundle.
3. Run targeted build/test/source scans.
4. Record proof transcript.
5. Update execution report row.

## Scope Exceptions

No broad Process Core split. No driver runtime/API implementation.

## Do Not Do

Document any unrelated failures.

## Acceptance Checklist

- [ ] Behavior preserved.
- [ ] No forbidden Core dependencies.
- [ ] No production driver API.
- [ ] No UI/media drift.
- [ ] Tests/scans recorded.
- [ ] Execution report row updated.

## Proof Required

- Build/test/source scan proof appropriate to subbundle scope.
- Critical gates must include semantic invariants and red-team checks.

## Browser Validation Logging

N/A — runtime/service/refactor bundle. If UI files change unexpectedly, fail this subbundle.

## Progression Gate

Do not continue to dependent work until acceptance checklist is complete.

## Suggested Agent Prompt

Implement SB025 only. Preserve behavior. Do not broaden scope. Record proof before proceeding.
