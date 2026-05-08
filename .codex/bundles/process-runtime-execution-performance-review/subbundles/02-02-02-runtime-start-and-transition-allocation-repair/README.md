# 02-02 Runtime Start And Transition Allocation Repair

## Status

- `Completed`

## Objective

Reduce repeated in-memory scans and allocations during process run start without changing process lifecycle behavior.

## Covered Inputs

- N002, N004, N007

## Prerequisites

- Subbundle 01 progression gate passed.
- Hot path is limited to generic runtime-start indexing and assignment selection.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.RunStart.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessLaunchPlanningIntegrationTests.cs`

## Deliverables

- Pre-index step role requirements by step id once per run start.
- Pre-index artifact expectation titles by step id once per run start.
- Build effective role assignments once per step and reuse the lookup.
- Replace per-call assignment `GroupBy` / `OrderBy` with a single-pass dictionary preserving existing precedence.

## Dependency Impact

- This subbundle is a critical foundation.
- Dispatch proof and mock-agent proof are not trustworthy until runtime start behavior tests pass.

## Validation Depth

- Targeted integration tests cover run start, assignment binding, step readiness, artifact gates, branch progression, and direct-assisted launch behavior.

## Implementation Steps

1. Add local indexes in `StartRunAsync` before the step loop.
2. Update executor and capability-gap helpers to consume a precomputed effective assignment lookup.
3. Preserve assignment precedence with explicit comparison logic.
4. Run targeted process integration tests.

## Scope Exceptions

- Do not optimize persistence queries or dispatch file I/O unless a test failure or new measurement makes them part of this hot path.

## Do Not Do

- Do not change process status semantics.
- Do not change public request or view model contracts.
- Do not encode .NET build instructions in process runtime.

## Acceptance Checklist

- [x] Code compiles.
- [x] Targeted process runtime tests pass.
- [x] Diff contains only generic runtime logic.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~ProcessesServiceIntegrationTests.StartRunAsync_prefills_project_role_binding_and_persists_runtime_signals -v:minimal`
- Additional targeted launch/runtime tests if the first pass exposes a behavior risk.

## Browser Validation Logging

- N/A unless runtime changes break a browser-visible test path.

## Progression Gate

- `Passed`: targeted runtime tests passed and the diff preserves assignment precedence explicitly.

## Suggested Agent Prompt

Implement the runtime-start allocation repair in `ProcessesService.Runtime.RunStart.cs`. Keep process logic generic, preserve assignment precedence, and validate with targeted integration tests.
