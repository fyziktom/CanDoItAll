# SB19 - Declared missing-tool-without-receipt rule

## Status

Prepared.

## Objective

Extract blocked-outcome missing required tool without receipt detection.

## Covered Inputs

- Original request: continue smaller dispatcher isolation.
- Preserve original behavior.
- Do not rush Process Core.
- Prepare future driver readiness without production driver APIs.

## Prerequisites

- Previous subbundle closure gate passed.
- Current branch is `maf-processes-refactor`.
- Prepared-stage bundle validation has passed before SB01 production movement.

## Exact Source References


- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`


## Deliverables

- Implement only the scoped work for this subbundle.
- Preserve wrapper entry points where existing tests or other partials call them.
- Record source assertions and command transcripts under `proof/SB19/`.

## Dependency Impact

This subbundle feeds later observation/outcome/completion boundaries. If it is wrong, downstream completion/retry proof is untrustworthy.

## Validation Depth

Focused validation: build where relevant, targeted tests or source scans, anti-stub proof.

## Implementation Steps

1. Re-read this README and exact source references.
2. Make the smallest source movement that satisfies the objective.
3. Keep new helpers internal and module-local.
4. Preserve behavior and wrapper signatures.
5. Run the listed proof.
6. Update proof manifest and semantic invariants.

## Scope Exceptions

- No Process Core.
- No production driver API.
- No UI proof unless unexpected UI changes are made; unexpected UI changes should be reverted.

## Do Not Do

- Do not create `CanDoItAll.Processes.Core`.
- Do not add `IProcessDriverPack`, driver registries, driver packages, or driver DI.
- Do not move EF/storage/execution-client/provider-save/workflow/subprocess/finalizer side effects into pure helpers.
- Do not delete existing tests.
- Do not create small/medium/mobile/browser screenshots.

## Acceptance Checklist

- [ ] Source change is scoped.
- [ ] Existing behavior is preserved.
- [ ] New helper is internal/module-local.
- [ ] No forbidden API or project introduced.
- [ ] Proof transcripts recorded.
- [ ] Downstream continuation decision recorded.

## Proof Required

- `dotnet build CanDoItAll.slnx --no-restore` at critical gates.
- Focused tests named in this subbundle or gate.
- Source scan for forbidden Process Core/driver API at critical gates.
- Anti-stub scan.
- No UI/prohibited viewport proof path scan.

## Browser Validation Logging

N/A expected. Runtime/service refactor only. If any UI file changes, revert it unless explicitly justified; large desktop/PC proof only if unavoidable.

## Progression Gate

Local closure gate. Downstream subbundle may continue only if focused proof passes.

## Suggested Agent Prompt

Implement SB19: Declared missing-tool-without-receipt rule. Keep the change module-local, behavior-preserving, and proof-backed. Do not start the next subbundle until this progression gate is closed.
