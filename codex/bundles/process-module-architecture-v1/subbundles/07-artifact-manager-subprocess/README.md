# SB07 Artifacts, Manager, Subprocess

## Status

Planned.

## Objective

Implement artifact ledger/recovery, process manager behavior, subprocess lifecycle, parent/child manager communication, error preprocessing, recovery strategies, and loop protection.

## Covered Inputs

- REQ-015 through REQ-025
- REQ-042 through REQ-045

## Prerequisites

- SB04 complete.
- SB05 complete.
- SB06 complete.

## Exact Source References

- `bundle://architecture/01-target-solution.md`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/AgentRecoveryModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
- `repo://src/CanDoItAll.Modules.Processes/Canvas/ProcessCanvasBranching.cs`

## Deliverables

- Artifact ledger.
- Artifact resolver.
- Recovery/resupply workflow.
- Generic process manager runtime.
- Error preprocessing strategy pipeline.
- Escalation and budget enforcement.
- Parent/child manager communication.
- Branch/switch runtime with backward route loop protection.

## Dependency Impact

- Monitoring and UI cannot be complete until manager incidents, artifact state, subprocess state, and branch decisions emit events.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Implement artifact slots, instances, references, and availability transitions.
2. Implement missing artifact incident flow.
3. Implement manager decision pipeline.
4. Implement error preprocessing and user-facing incidents.
5. Implement automatic recovery budget enforcement.
6. Implement subprocess manager communication.
7. Implement branch decision records and backward routing.
8. Implement loop fingerprint and escalation.

## Scope Exceptions

Domain-specific recovery strategies may be representative, but the generic pipeline must be complete.

## Do Not Do

- Do not discard completed step records.
- Do not recover artifacts by hidden dispatcher prompt text.
- Do not allow backward routes without budgets.
- Do not expose raw domain diagnostics directly to the UI.

## Acceptance Checklist

- Later steps can consume artifacts from any earlier permitted step.
- Missing artifact recovery can request producer resupply.
- Subprocess artifact references can flow parent to child and child to parent.
- Manager incidents are user-actionable.
- Loop budget escalation stops repeated routes.

## Proof Required

- Unit and integration tests for artifact lifecycle.
- Recovery/resupply tests.
- Parent/subprocess manager communication tests.
- Branch backward route and loop budget tests.
- Semantic Adequacy Gate.
- `proof/SB07/manifest.md`.
- Production Behavior Artifact Matrix for artifact ledger, manager incident, subprocess message, branch decision, and loop budget events.

## Browser Validation Logging

- N/A until UI is wired.
- If incident UI is introduced early, capture component or browser proof.

## Progression Gate

- SB08 and SB09 cannot close without these events and read models.

## Suggested Agent Prompt

Implement the reliability layer: artifacts, manager, subprocesses, recovery, branches, and loops as explicit runtime concepts.
