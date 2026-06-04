# SB05 - Dispatcher result/detail migration

## Status

Prepared. Implementation not started.

## Objective

Migrate dispatcher execution-path consumers from AgentFramework result/detail records to process-owned snapshots.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-large-screen-only-proof-policy.md`

## Prerequisites

SB04 complete

## Exact Source References

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionClient.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `src/CanDoItAll.Processes.Contracts/Automation/ProcessAutomationExecutionContracts.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessAutomationExecutionClientTests.cs`

## Deliverables

Execution.cs, Concurrency.cs, Costing.cs, CompletionArtifactRecovery.cs only where needed.

## Dependency Impact

This subbundle is part of the staged execution-boundary reduction. If this subbundle fails, downstream subbundles must not continue because they may build on an unproven boundary.

## Validation Depth

Source scan: no ExecutionRunResult/ExecutionRunDetail/ExecutionRunRecord in dispatcher partials. Runtime parity tests.

## Implementation Steps

1. Re-read the exact source references before editing.
2. Make the smallest change that closes this subbundle.
3. Add or update focused tests before broad validation.
4. Run source scans that prove the intended coupling has moved or stayed fixed.
5. Record transcripts under `proof/SB05/transcripts/`.
6. Update the execution report and progression gate before continuing.

## Scope Exceptions

- Full Process Core extraction is out of scope.
- Driver packs are out of scope.
- UI optimization is out of scope.

## Do Not Do

- Do not move EF entities, Razor components, or UI view models.
- Do not reintroduce direct MAF product-module dependencies.
- Do not rename process runtime tools.
- Do not run small, medium, mobile, tablet, Android, or iPhone viewport proof.

## Acceptance Checklist

- [ ] Scope remained within this subbundle.
- [ ] Tests/source scans are recorded.
- [ ] No prohibited viewport proof artifacts exist.
- [ ] No hidden MAF/Tooling product dependency is introduced.
- [ ] No Process Core or driver-pack project is introduced.

## Proof Required

Create proof manifest at `proof/SB05/manifest.md`, semantic invariants at `proof/SB05/semantic-invariants.md`, command transcripts under `proof/SB05/transcripts/`, and source assertions under `proof/SB05/source-assertions/`.

## Browser Validation Logging

N/A unless UI is unexpectedly touched. If UI is touched, use large-screen desktop proof only and record the route, viewport, assertions, and screenshots. Do not create small/medium/mobile proof.

## Progression Gate

Do not continue to the next subbundle until all acceptance checklist items and proof requirements are complete.

## Suggested Agent Prompt

Execute this subbundle only. Preserve the execution-boundary refactor scope, avoid Process Core and driver-pack work, record proof, and stop at the progression gate.
