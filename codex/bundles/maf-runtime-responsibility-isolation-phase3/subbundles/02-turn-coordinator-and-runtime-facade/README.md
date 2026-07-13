# SB02 Turn Coordinator And Runtime Facade

## Status

- `Ready after SB01`

## Objective

Make `MafAgentRuntime` a real thin facade for `RunAsync`, `RespondToPendingApprovalsAsync`, hosted-agent creation, and provider diagnostics by extracting the top-level turn orchestration boundary.

## Success Criteria

- `MafAgentRuntime` delegates run and approval continuation orchestration to injected/extracted collaborators.
- `MafRuntimeTurnCoordinator` or equivalent is directly unit-tested without `MafAgentRuntime`.
- Existing provider diagnostics and handoff smoke still pass.

## Covered Inputs

- R03, R04, R10, R11.

## Prerequisites

- SB01 closure.
- Characterization tests for runtime run/approval entry paths.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeServiceCollectionExtensions.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeExecutionOptionsResolver.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Input/InputAttachmentPreparer.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafRuntimeArchitectureServicesTests.cs`

## Deliverables

- `IMafRuntimeTurnCoordinator` and implementation.
- Runtime facade delegation for run entry points.
- DI registration for the coordinator and dependencies.
- Direct unit tests for coordinator orchestration.
- Source assertion that run orchestration no longer lives in `MafAgentRuntime`.

## Dependency Impact

- SB03 depends on this seam so streaming/finalizer/session behavior can move behind driver interfaces without changing public runtime callers.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Introduce strongly typed turn request/result records if needed.
2. Extract option normalization, attachment preparation, runtime build call, prompt/session message creation, and executor call into the coordinator.
3. Change `MafAgentRuntime` to delegate to the coordinator.
4. Register the coordinator in `AddMafRuntimeArchitectureServices`.
5. Add direct coordinator tests with fake build/executor/attachment collaborators.
6. Add architecture source assertions for facade behavior.

## Scope Exceptions

- Do not move the streaming loop or finalizer repair in this subbundle unless it is required to create a clean coordinator interface.

## Do Not Do

- Do not create `MafRuntimeManager`.
- Do not keep duplicate run logic in runtime and coordinator.
- Do not add partial runtime files.

## C# Architecture Impact

Reduces the runtime from behavior owner to compatibility facade for top-level turn entry.

## Boundary Ownership

`MafAgentRuntime` owns public API delegation. `MafRuntimeTurnCoordinator` owns orchestration.

## Dependency Direction

Runtime facade depends inward on coordinator abstraction. The coordinator depends on explicit collaborators, not the old runtime.

## Pattern Decision

Thin Facade plus extracted orchestration class.

## Testability Contract

Coordinator tests must not instantiate `MafAgentRuntime`.

## Partial Class Policy

No new partials. Add a guard if necessary.

## Architecture Proof Required

- Direct coordinator unit tests.
- Source assertion that `MafAgentRuntime.RunAsync` and approval entry points delegate.
- Build and focused MAF tests.

## Acceptance Checklist

- [ ] Runtime facade has no turn orchestration internals.
- [ ] Coordinator has one clear reason to change.
- [ ] Tests prove coordinator behavior with fakes.
- [ ] Existing integration smoke remains green or blocker recorded.

## Proof Required

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- passing focused unit transcript.
- source assertion transcript.

## Browser Validation Logging

- N/A. Backend runtime behavior only.

## Progression Gate

- SB03 may start only when runtime facade delegation is proven and direct coordinator tests pass.

## Suggested Agent Prompt

```text
Execute SB02 only. Extract the top-level turn coordinator and make MafAgentRuntime delegate without moving finalizer/session internals yet. Add direct unit tests and source assertions.
```
