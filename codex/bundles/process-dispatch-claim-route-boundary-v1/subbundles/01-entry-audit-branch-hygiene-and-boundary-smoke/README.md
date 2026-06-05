# SB01 - Entry audit, branch hygiene, existing boundary smoke

## Status

Prepared.

## Objective

Entry audit, branch hygiene, existing boundary smoke.

## Covered Inputs

- User request to continue small dispatcher isolation.
- Current `maf-processes-refactor` branch.
- Prior step-completion finalizer boundary output.

## Prerequisites

Latest branch must be clean enough for source inventory and build smoke.

## Exact Source References

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

Record line counts, branch status, previous helper presence, no-core/no-driver/no-ui scans.

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

- Transcript under `proof/SB01/transcripts/`.
- Source assertions under `proof/SB01/source-assertions/`.
- Semantic invariants under `proof/SB01/semantic-invariants.md`.
- Manifest under `proof/SB01/manifest.md`.

## Browser Validation Logging

N/A expected. Runtime/service refactor only. If UI changes appear, stop and escalate. If proof becomes unavoidable, use large desktop/PC only.

## Progression Gate

Do not start SB02 until this subbundle's closure gate passes.

## Suggested Agent Prompt

Implement SB01 of `process-dispatch-claim-route-boundary-v1`. Preserve behavior, record proof, and do not broaden scope.
