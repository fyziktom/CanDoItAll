# SB11 - Pre-execution route planner

## Status

Prepared.

## Objective

Pre-execution route planner.

## Covered Inputs

- User request to continue small dispatcher isolation.
- Current `maf-processes-refactor` branch.
- Prior step-completion finalizer boundary output.

## Prerequisites

Previous gate must pass before this subbundle starts.

## Exact Source References

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

Return route decisions for DB requirement, upstream materialization, stranded recovery, subprocess, workflow, and agent execution without side effects.

## Dependency Impact

Downstream subbundles depend on this source shape and must re-run targeted scans.

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

- [ ] Scope remains limited to this subbundle.
- [ ] Existing behavior is preserved.
- [ ] Required tests and scans are recorded.
- [ ] No Process Core or production driver API exists.
- [ ] No prohibited viewport proof artifacts exist.
- [ ] Downstream dependency impact is updated.

## Proof Required

- Transcript under `proof/SB11/transcripts/`.
- Source assertions under `proof/SB11/source-assertions/`.
- Semantic invariants under `proof/SB11/semantic-invariants.md`.
- Manifest under `proof/SB11/manifest.md`.

## Browser Validation Logging

N/A expected. Runtime/service refactor only. If UI changes appear, stop and escalate. If proof becomes unavoidable, use large desktop/PC only.

## Progression Gate

Do not start SB12 until this subbundle's closure gate passes.

## Suggested Agent Prompt

Implement SB11 of `process-dispatch-claim-route-boundary-v1`. Preserve behavior, record proof, and do not broaden scope.
