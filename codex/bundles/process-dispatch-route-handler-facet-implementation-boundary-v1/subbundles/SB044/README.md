# SB044 - Critical Gate I: pre-execution parity

## Status

- Status: `Completed`
- Closure proof: `bundle://reviews/01-execution-report.md`; `bundle://proof/transcripts/build-no-restore.txt`; `bundle://proof/transcripts/unit-focused-route-boundary-tests.txt`; `bundle://proof/transcripts/integration-focused-route-tests.txt`; `bundle://proof/transcripts/source-assertions.txt`.
## Objective

Critical Gate I: pre-execution parity

## Covered Inputs

- Raw request: continue smaller dispatcher isolation; do not rush Process Core; preserve all functionality; plan enough phases for longer Codex work.
- Current-state review: route handlers are currently nested and many route handlers call `ProcessRunAutomationDispatchService` directly.

## Prerequisites

- Previous subbundles in phase `P4` must be complete.
- Critical gates before SB044 must be passed.
- Branch must remain `maf-processes-refactor`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePipeline.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchClaimLease.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExceptionClosure.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration`

## Deliverables

- Critical manifest, semantic invariants and source/test proof transcripts.
- Focused tests that prove behavior did not drift.

## Dependency Impact

- This subbundle is part of phase `P4: Pre-execution route handler split`. Downstream route-handler extraction depends on this subbundle preserving route order and side-effect ownership.

## Validation Depth

- Critical gate: source scans, focused unit tests, focused integration tests, semantic invariant proof, anti-stub scan and no-core/no-driver/no-UI scan are required.

## Implementation Steps

1. Re-open the exact source references.
2. Apply only the smallest source changes needed for the objective.
3. Preserve route order and return semantics.
4. Update or add focused tests/source assertions.
5. Record proof in the execution report row for `SB044`.
6. If any dependent proof fails, stop and repair this subbundle before moving on.

## Scope Exceptions

- Process Core extraction is explicitly out of scope.
- Production driver API extraction is explicitly out of scope.
- UI proof is explicitly out of scope.

## Do Not Do

- Do not create Process Core.
- Do not create production driver APIs.
- Do not touch UI/Razor/CSS/JS/TS files.
- Do not collapse report rows.
- Do not remove existing behavior.
- Do not hide side effects in vague helpers.

## Acceptance Checklist

- [ ] Objective is implemented.
- [ ] Route order is unchanged.
- [ ] Behavior is preserved.
- [ ] No Core/driver/UI drift.
- [ ] No new stubs or TODO placeholders.
- [ ] Execution report has a distinct `SB044` row.
- [ ] Downstream dependency impact checked.

## Proof Required

- Source assertion for this subbundle.
- Focused route-handler/unit/integration proof if production code changed.
- Full build at phase-level gates.
- For critical gates: manifest and semantic invariants.

## Browser Validation Logging

- `N/A` - runtime/service refactor only. Do not create browser screenshots. Do not run small, medium, mobile, phone or tablet proof.

## Progression Gate

- Do not start downstream subbundles until this critical gate passes.

## Suggested Agent Prompt

Implement `SB044 - Critical Gate I: pre-execution parity` exactly. Preserve route behavior and route order. Do not create Process Core or production driver APIs. Record proof in the execution report under a distinct `SB044` row.
