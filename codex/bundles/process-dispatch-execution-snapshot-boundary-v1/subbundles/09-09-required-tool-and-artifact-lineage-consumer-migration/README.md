# SB09 - Required-tool and artifact-lineage consumer migration

## Status

- Status: Completed

Completed.

## Objective

Move selected required-tool and artifact-lineage consumers to the receipt observation helper while preserving behavior.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-large-screen-only-proof-policy.md`

## Prerequisites

- SB08 complete

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionClient.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `repo://src/CanDoItAll.Processes.Contracts/Automation/ProcessAutomationExecutionContracts.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessAutomationExecutionClientTests.cs`

## Deliverables

- Targeted dispatcher partials only; no broad artifact subsystem rewrite.

## Dependency Impact

- This subbundle is part of the staged execution-boundary reduction. If this subbundle fails, downstream subbundles must not continue because they may build on an unproven boundary.

## Validation Depth

- Required-tool parity tests, artifact-lineage smoke, receipt provider metadata tests.

## Implementation Steps

1. Re-read the exact source references before editing.
2. Make the smallest change that closes this subbundle.
3. Add or update focused tests before broad validation.
4. Run source scans that prove the intended coupling has moved or stayed fixed.
5. Record transcripts under `proof/SB09/transcripts/`.
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

- [x] Scope remained within this subbundle.
- [x] Tests/source scans are recorded.
- [x] No prohibited viewport proof artifacts exist.
- [x] No hidden MAF/Tooling product dependency is introduced.
- [x] No Process Core or driver-pack project is introduced.

## Proof Required

- Create proof manifest at `proof/SB09/manifest.md`, semantic invariants at `proof/SB09/semantic-invariants.md`, command transcripts under `proof/SB09/transcripts/`, and source assertions under `proof/SB09/source-assertions/`.

## Browser Validation Logging

- N/A unless UI is unexpectedly touched. If UI is touched, use large-screen desktop proof only and record the route, viewport, assertions, and screenshots. Do not create small/medium/mobile proof.

## Progression Gate

- Do not continue to the next subbundle until all acceptance checklist items and proof requirements are complete.

## Suggested Agent Prompt

Execute this subbundle only. Preserve the execution-boundary refactor scope, avoid Process Core and driver-pack work, record proof, and stop at the progression gate.
