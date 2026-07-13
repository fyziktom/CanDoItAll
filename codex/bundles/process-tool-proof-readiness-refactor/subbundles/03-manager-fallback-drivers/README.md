# 03-manager-fallback-drivers

## Status

- `Completed`

## Objective

- Route missing proof through manager fallback and process drivers so artifact-only recovery cannot hide absent required tool receipts.

## Success Criteria

- Missing required receipt diagnostics become manager-recoverable events with typed reason codes.
- Manager fallback can choose proof-focused redispatch, reassignment, driver recovery, or explicit NeedsAttention.
- Existing artifact recovery remains available for actual missing artifacts but is not used as a substitute for current-run proof.

## Covered Inputs

- R5 Manager Fallback And Process Drivers.
- R4 Outcome Receipt Gate.
- User question: whether manager fallback could have recovered the QA recheck blocker.

## Prerequisites

- `01-runtime-receipt-contracts` completed with receipt-gate diagnostics.
- `02-hr-capability-readiness` completed if fallback needs reassignment/readiness comparison.

## Exact Source References

- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverPackage.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterStrategyFactory.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessManagerPorts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessManagerRecoveryContracts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessRuntimeStepAssignmentRepairService.cs`
- `bundle://architecture/03-csharp-pattern-selection-records.md`

## Deliverables

- Missing proof diagnostic mapped to process manager recovery inputs.
- Fallback planner service that chooses the next action from typed diagnostics and readiness results.
- Process driver extension point for proof-specific recovery contribution.
- Tests proving artifact-only fallback is rejected when current-run proof is required.
- Updated process events or logs that make the recovery decision auditable.

## Dependency Impact

- `04-template-process-e2e` depends on fallback behavior to avoid repeated artifact-only QA recheck attempts.
- Future domain-specific processes can contribute recovery behavior without editing MAF or the standard process manager.

## Validation Depth

- Process-critical recovery gate.
- Unit and integration tests must cover negative and recovery-routing cases.

## Implementation Steps

1. Model missing proof as a first-class recovery diagnostic from the receipt gate.
2. Add or extend fallback planner interfaces in the process application/runtime boundary.
3. Wire manager recovery so missing proof cannot be converted to artifact-only success.
4. Add a standard proof fallback strategy for redispatch with explicit proof requirements.
5. Add driver extension support for domain-specific proof recovery where needed.
6. Add tests for redispatch, reassignment, driver recovery, NeedsAttention, and artifact-only rejection.
7. Add logs/events with run id, step id, missing receipt ids, selected fallback action, and masked sensitive data.

## Scope Exceptions

- Do not implement template migration here.
- Do not create a separate development-agent-tools project unless implementation proof shows a real project boundary is needed.
- Do not change unrelated recovery paths.

## Do Not Do

- Do not let provider timeout recovery accept success when required receipts are absent.
- Do not use generic prompt repair as the only recovery for missing proof.
- Do not add domain-specific recovery branches inside MAF execution services.
- Do not log raw secrets, credentials, or full prompt contents.

## Acceptance Checklist

- Missing screenshot or image-analysis receipt produces a typed fallback diagnostic.
- Artifact recovery remains possible only for missing artifacts, not missing proof.
- Manager fallback records the selected action and why alternatives were rejected.
- Driver recovery can be added without changing MAF.
- Tests reproduce the run `6f0d229f` artifact-only retry pattern and prove it no longer passes.

## Proof Required

- `dotnet test` for fallback planner and recovery integration tests.
- Negative transcript where artifact-only recovery is rejected for missing required receipts.
- Source proof that missing proof decisions are logged with run/step/action state.
- Source proof that MAF does not own domain-specific fallback choices.

## Browser Validation Logging

- N/A unless manager recovery UI is changed.
- If UI is changed, record process run detail route, desktop viewport, recovery decision display, and screenshot path.

## Progression Gate

- Template migration may start only after missing proof cannot be accepted as success and fallback chooses a deliberate recovery path.

## C# Architecture Impact

- Adds focused fallback planning around existing driver/recovery seams instead of expanding finalizer logic.

## Boundary Ownership

- Process runtime/application owns fallback decisions. Drivers contribute domain-specific strategies. MAF provides generic diagnostics.

## Dependency Direction

- Driver abstractions may define contribution contracts. Standard drivers implement them. MAF must not reference driver packages.

## Pattern Decision

- Use a narrow chain of fallback strategies only for materially different recovery choices.

## Testability Contract

- Fallback planner must be testable with diagnostics and readiness fixtures; no live process run required for unit coverage.

## Partial Class Policy

- Do not add large recovery branches to existing partial classes. Extract services and keep partials as orchestration shells.

## Architecture Proof Required

- Include tests and source proof that artifact-only recovery cannot satisfy required current-run receipts.
- Include dependency proof for driver isolation.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Connect missing required receipt diagnostics to manager fallback and process drivers, reject artifact-only recovery for missing proof, add focused tests and actionable logs, and keep MAF free of domain-specific recovery choices.
```
