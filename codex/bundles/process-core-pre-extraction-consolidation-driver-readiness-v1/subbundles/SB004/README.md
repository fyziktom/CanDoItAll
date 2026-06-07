# SB004 — Split pure route DTOs from dispatcher source payloads

## Status

- Completed

## Objective

Move source payloads into explicit envelope/adapter models so route DTOs are pure read models.

## Covered Inputs

- Continue smaller dispatcher/process isolation without rushing Process Core.
- Preserve existing behavior.
- Prepare future Core and driver boundaries safely.

## Prerequisites

- Branch: `maf-processes-refactor`.
- Previous subbundle(s) in phase completed.
- If this is a gate subbundle, all earlier subbundles in this phase must have source proof.

## Exact Source References

Start by inspecting the relevant subset:

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://codex/bundles/process-core-contract-candidate-driver-readiness-prep-v1/reviews/01-execution-report.md`
- `repo://codex/bundles/process-core-contract-candidate-driver-readiness-prep-v1/architecture/07-core-extraction-readiness-scorecard.md`

## Deliverables

- Behavior-preserving source changes only if this subbundle is an implementation subbundle.
- Updated or added tests when behavior is moved or API boundaries are tightened.
- Proof transcript under `bundle://proof/SB004/transcripts/`.
- Semantic invariants under `bundle://proof/SB004/semantic-invariants.md`.
- Manifest under `bundle://proof/SB004/manifest.md`.

## Dependency Impact


- This subbundle gates the dependent phase work named in the phase plan.
This subbundle feeds later Core-readiness and driver-readiness decisions. If its proof is weak, downstream gates must be reopened.

## Validation Depth


- Use the focused validation described below and escalate at the phase gate.
Focused validation: compile relevant project or run targeted tests if source moved; otherwise source assertions and build where appropriate.

## Implementation Steps

1. Re-read the current source before editing.
2. Make the smallest behavior-preserving change that completes the objective.
3. Keep side-effectful application/infrastructure behavior out of pure-rule candidates.
4. Update tests before moving to the next subbundle.
5. Record proof in the execution report.

## Scope Exceptions

- Do not create Process Core in this subbundle.
- Do not create production driver APIs.
- Do not touch UI/browser/mobile surfaces.

## Do Not Do

- Do not remove existing behavior.
- Do not silently weaken route/finalizer/subprocess/projection semantics.
- Do not hide EF, filesystem, storage, claim, transition, AgentFramework, or finalizer side effects behind pure-rule names.
- Do not collapse execution report rows.

## Acceptance Checklist

- [x] Objective completed.
- [x] No Process Core project created.
- [x] No production driver API added.
- [x] Existing behavior preserved.
- [x] Tests/source scans updated.
- [x] Proof files written.
- [x] Execution report row completed.

## Proof Required

- Source assertions.
- Build/test transcript as appropriate.
- No-Core/no-driver/no-UI scan.
- Shallow-pass trap statement.
- Reopen triggers.

## Browser Validation Logging


- N/A - runtime/service refactor only. Stop if UI or media files change.
N/A — runtime/service refactor only. Confirm no UI/media files changed.

## Progression Gate


- Do not continue until this subbundle row and proof artifacts are updated.
Do not proceed to the phase gate until this subbundle proof is recorded.

## Closure Notes

- Entry gate: `Passed`; Gate A is completed.
- Closure gate: `Passed`; route DTOs no longer carry source interfaces and adapter source recovery remains covered.
- Proof: `bundle://proof/SB004/manifest.md` and `bundle://proof/SB004/semantic-invariants.md`.
- Downstream dependencies checked: `Passed`; `SB005` must verify adapter confinement at route services/handlers.

## Suggested Agent Prompt

Implement `SB004 — Split pure route DTOs from dispatcher source payloads` from `process-core-pre-extraction-consolidation-driver-readiness-v1`. Preserve behavior, update tests, record proof, and do not create Process Core or production driver APIs.


