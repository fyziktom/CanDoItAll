# Refactor Gate B source adapter parity

## Status

Prepared.

## Objective

Prove first adapter migrations preserve external keys, duplicate skip, lineage and required-artifact behavior.

## Covered Inputs

- User request to continue small dispatcher isolation steps and avoid Process Core.
- Large-screen-only proof policy.
- Requirements: RQ-007, RQ-011.

## Prerequisites

SB06 closure gate must pass.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionLineageBuilder.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Source changes or proof artifacts matching this subbundle objective.
- Updated proof manifest under `proof/SB07/manifest.md`.
- Updated semantic invariants under `proof/SB07/semantic-invariants.md`.
- Source assertions and command transcripts for all acceptance checks.

## Dependency Impact

This subbundle affects downstream migration safety. If it fails, later subbundles must not start because artifact behavior parity would be untrustworthy.

## Validation Depth

- Source scan before and after movement.
- Focused tests for the changed helper/adapters.
- No Process Core / no driver-pack scan.
- No prohibited viewport proof artifact scan.

This is a hard refactor gate. Do not continue to downstream production movement until all listed proof is recorded and reviewed.


## Implementation Steps

1. Re-read the exact source references.
2. Update or create the smallest necessary code/proof files.
3. Preserve all public behavior and external reference key formats unless this subbundle explicitly proves a migration.
4. Run focused tests first.
5. Run required source scans.
6. Record transcripts and source assertions in this subbundle proof folder.

## Scope Exceptions

No Process Core, no driver packs, no UI work, no mobile/small/medium proof.

## Do Not Do

- Do not move EF entities or DbContext code.
- Do not rename process tools.
- Do not weaken artifact validation.
- Do not create hidden fallbacks that mask missing projection sources.
- Do not create mobile/tablet/small/medium screenshots.

## Acceptance Checklist

- [ ] Objective implemented or proof-only scope completed.
- [ ] Behavior parity proven with focused tests.
- [ ] Source scans show no forbidden architecture movement.
- [ ] No prohibited viewport proof artifacts exist.
- [ ] Execution report row updated.

## Proof Required

- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`
- `proof/SB07/transcripts/*.txt`
- `proof/SB07/source-assertions/*.txt` or `.md`

## Browser Validation Logging

N/A expected. If any UI proof is unexpectedly needed, use PC/large-screen only and record why.

## Progression Gate

SB08 may start only after this closure gate passes.

## Suggested Agent Prompt

Execute subbundle `07-refactor-gate-b-source-adapter-parity` exactly. Keep the change small, preserve behavior, record proof, and stop if any guardrail fails.
