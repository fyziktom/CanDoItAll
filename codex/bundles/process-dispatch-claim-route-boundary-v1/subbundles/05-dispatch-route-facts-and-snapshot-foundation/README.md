# SB05 - Dispatch route facts and snapshot foundation

## Status

- Status: `Completed`

## Objective

Dispatch route facts and snapshot foundation.

## Covered Inputs

- User request to continue small dispatcher isolation.
- Current `maf-processes-refactor` branch.
- Prior step-completion finalizer boundary output.

## Prerequisites

- Previous gate must pass before this subbundle starts.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Add module-local facts/snapshots for trigger, step/run status, route eligibility, and current attempt context.

## Dependency Impact

- Downstream subbundles depend on this source shape and must re-run targeted scans.

## Validation Depth

- Source scan.
- Focused test or proof appropriate to the subbundle.
- Anti-stub check.
- No-core/no-driver scan.
- No prohibited viewport proof scan.
- Build or scoped build when production code changes.

## Implementation Steps

1. Re-read the source files listed above.
2. Make the smallest coherent change for this subbundle only.
3. Preserve wrapper methods unless the subbundle explicitly proves all callers.
4. Record source assertions and changed-file hashes.
5. Run the required proof before downstream work.

## Scope Exceptions

- Do not create Process Core.
- Do not create production process driver APIs.
- Do not move EF entities or UI components.

## Do Not Do

- Do not broaden MAF/Tooling product dependencies.
- Do not change public process tool names.
- Do not run or store small/medium/mobile proof artifacts.
- Do not hide side effects inside pure route helpers.

## Acceptance Checklist

- [x] Scope remains limited to this subbundle.
- [x] Existing behavior is preserved.
- [x] Required tests and scans are recorded.
- [x] No Process Core or production driver API exists.
- [x] No prohibited viewport proof artifacts exist.
- [x] Downstream dependency impact is updated.

## Proof Required

- Transcript under `proof/SB05/transcripts/`.
- Source assertions under `proof/SB05/source-assertions/`.
- Semantic invariants under `proof/SB05/semantic-invariants.md`.
- Manifest under `proof/SB05/manifest.md`.

## Browser Validation Logging

- N/A expected. Runtime/service refactor only. If UI changes appear, stop and escalate. If proof becomes unavoidable, use large desktop/PC only.

## Progression Gate

- Do not start SB06 until this subbundle's closure gate passes.

## Suggested Agent Prompt

Implement SB05 of `process-dispatch-claim-route-boundary-v1`. Preserve behavior, record proof, and do not broaden scope.

