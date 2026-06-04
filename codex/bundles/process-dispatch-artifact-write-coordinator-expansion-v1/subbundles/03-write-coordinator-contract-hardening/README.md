# Harden write coordinator contract and outcome model

## Status

- Status: Completed

Completed.

- Entry gate: Passed on 2026-06-04. SB02 inventory classified every remaining direct storage/record path.
- Closure gate: Passed on 2026-06-04. Critical proof manifest and semantic invariant contract exist under `proof/SB03`, focused coordinator tests passed, architecture guardrails passed, and the coordinator source scan confirms no source matching semantics moved into the coordinator.

## Objective

Harden ProcessArtifactProjectionWriteCoordinator with a structured outcome and explicit failure semantics before migrating more production paths.

## Covered Inputs

- User request to keep dispatcher isolation incremental and avoid Process Core.
- `inputs/01-source-artifacts.md`.
- Current source review in `analysis/01-current-state.md`.

## Prerequisites

- SB02 complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Structured write outcome type.
- Tests proving execution-artifact path still works.
- Coordinator source scan proving it does not own source semantics.

## Dependency Impact

- All migration subbundles depend on the new coordinator contract.

## Validation Depth

- Unit tests for outcome mapping.
- Existing execution artifact projection tests.

## Implementation Steps

- Add outcome record.
- Update WriteAsync return type if needed.
- Keep caller-controlled failure semantics.
- Update execution-artifact caller and tests.

## Scope Exceptions

- No Process Core.
- No driver packs.
- No UI proof unless an unexpected UI file change occurs, and then large desktop/PC only.

## Do Not Do

- Do not rename public process tools.
- Do not change external-reference key formats without explicit parity proof.
- Do not move source matching semantics into the write coordinator.
- Do not run small/medium/mobile proof.

## Acceptance Checklist

- Coordinator returns managed path and projection identity data.
- Coordinator does not reference DispatchCandidate.
- Execution artifact path parity passes.

## Proof Required

- proof/SB03/manifest.md
- proof/SB03/semantic-invariants.md
- proof/SB03/transcripts/coordinator-tests.txt
- proof/SB03/transcripts/failing-first-coordinator-outcome-tests.txt
- proof/SB03/transcripts/architecture-tests.txt
- proof/SB03/source-assertions/coordinator-source-scan.txt
- proof/SB03/source-assertions/outcome-contract-source-scan.txt
- proof/SB03/source-assertions/anti-stub-audit.txt
- proof/SB03/source-assertions/changed-file-hashes.txt

## Browser Validation Logging

- N/A expected. Runtime/service refactor only. If unexpectedly needed, record only large desktop/PC proof and explain why service tests were insufficient.

## Progression Gate

- SB04 architecture gate must pass before production path migrations.

## Suggested Agent Prompt

Implement this subbundle only. Record source assertions, tests, and anti-stub audit before proceeding. Preserve all prior behavior and update `reviews/01-execution-report.md`.

