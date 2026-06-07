# SB019 — Phase 7 / Core project hygiene

## Status

- Status: `Completed`
## Objective

Add README/package notes for Core seed and allowed/future forbidden contents.

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
- Updated proof transcript under this bundle's `proof/SB019/`.
- Execution report row updated individually.

## Dependency Impact

- Downstream subbundles cannot rely on this slice until its proof passes.
## Validation Depth

- Core README complete.
## Implementation Steps

1. Re-open exact source files and confirm current state.
2. Make the smallest behavior-preserving change for this subbundle.
3. Run targeted build/test/source scans.
4. Record proof transcript.
5. Update execution report row.

## Scope Exceptions

No broad Process Core split. No driver runtime/API implementation.

## Do Not Do

Do not publish packages or NuGet metadata unless existing repo convention requires.

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

Implement SB019 only. Preserve behavior. Do not broaden scope. Record proof before proceeding.
