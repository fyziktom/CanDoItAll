# SB049 - Start transition handler host narrowing

## Status

Prepared.

## Objective

Reduce host surface used by the start transition handler.

## Covered Inputs

- Continue smaller dispatcher isolation.
- Preserve original functionality.
- Do not rush Process Core.
- Prepare future drivers safely as documentation only.
- Plan enough phases and enforce refactor gates.
- Keep UI/mobile proof out of scope.

## Prerequisites

Previous subbundle completed; all earlier critical gates passed.

## Exact Source References

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePipeline.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchClaimLease.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExceptionClosure.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables / Scope

Named handlers/coordinators for stranded recovery, subprocess, and start transition.

## Dependency Impact

Phase: `P3 - Recovery, subprocess, and start-transition handlers`

Critical foundation: No.

If this subbundle is wrong, reopen it and all downstream subbundles that depend on its route order, claim lifecycle, side-effect ownership, handler context or proof artifacts.

## Validation Depth

Build, focused route/subprocess tests, line-count scan, no-core/no-driver scan.

## Implementation Steps

1. Re-read the exact source references.
2. Apply the smallest behavior-preserving source movement required for this subbundle.
3. Keep route order and side-effect ownership explicit.
4. Run the specified focused proof.
5. Update `reviews/01-execution-report.md` with this exact subbundle row.
6. If this is a critical gate, create `proof/SB049/manifest.md` and `proof/SB049/semantic-invariants.md`.

## Scope Exceptions

- Do not create Process Core.
- Do not create production driver APIs.
- Do not touch UI or proof screenshots.
- Do not move EF entities.

## Do Not Do

Do not introduce CanDoItAll.Processes.Core, production driver APIs, UI changes, or route-order changes.

## Acceptance Checklist

- [ ] Behavior preserved.
- [ ] Route order preserved.
- [ ] No hidden side effects in pure helpers.
- [ ] No Process Core.
- [ ] No production driver API.
- [ ] No UI/mobile proof drift.
- [ ] Tests/source scans recorded.
- [ ] Execution report row updated individually.

## Proof Required

- Source diff summary
- Focused test or source scan appropriate to this subbundle
- Anti-stub scan for changed production dispatch files
- Critical manifest/invariants if this is a critical gate

## Browser Validation Logging

N/A - runtime/service refactor only. Do not create small/medium/mobile/browser proof.

## Progression Gate

Downstream work may continue only after the closure checklist and proof are complete. Critical gate subbundles require manifest + semantic invariant proof.

## Suggested Agent Prompt

Implement `SB049 - Start transition handler host narrowing` from `process-dispatch-route-handler-pipeline-boundary-v1`. Preserve behavior, keep all work module-local, do not create Process Core or driver APIs, and update proof before continuing.
