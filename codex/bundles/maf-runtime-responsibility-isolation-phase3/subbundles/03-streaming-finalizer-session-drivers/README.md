# SB03 Streaming, Finalizer, Session, And Approval Drivers

## Status

- `Ready after SB02 and SB04 prerequisites`

## Objective

Move the most complex execution behavior out of `MafAgentRuntime`: provider streaming, required-finalizer repair, typed JSON fallback, provider-failure finalizer preservation, session persistence, and pending approval continuation.

## Success Criteria

- Execution drivers are direct unit-test targets.
- `MafAgentRuntime` and the turn coordinator do not own moved driver logic.
- Finalizer/session/approval behavior remains compatible.

## Covered Inputs

- R05, R10, R11, R13.

## Prerequisites

- SB02 runtime facade seam complete.
- SB04 must provide a runtime build result seam stable enough for executor input.
- Characterization tests for finalizer/session/approval behavior are present or explicitly added first.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafFinalizerDriver.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeExecutionContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Input/RequestScopedSessionContentScrubber.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderStreamingRunner.cs`

## Deliverables

- `MafRuntimeTurnExecutor`.
- `MafFinalizerRepairCoordinator`.
- `MafRuntimeSessionPersistenceDriver`.
- `MafApprovalContinuationDriver`.
- Narrow adapter(s) for `AIAgent`/`AgentSession` only if needed for tests.
- Direct positive and negative unit tests.

## Dependency Impact

- SB07 and SB08 depend on this because runtime testability cannot improve while these drivers remain private methods in `MafAgentRuntime`.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add characterization tests for current execution/finalizer/session/approval behavior.
2. Extract session persistence and approval continuation first because they have clear inputs/outputs.
3. Extract finalizer repair coordination with fakes around streaming.
4. Extract provider streaming loop and response assembly.
5. Replace old runtime methods with delegation and delete duplicate logic.
6. Add negative tests for shallow implementations.

## Scope Exceptions

- Do not deeply refactor `MafFinalizerDriver` static policy internals unless necessary for driver extraction.

## Do Not Do

- Do not hide all execution behavior in one `ExecutionManager`.
- Do not rely on live provider credentials.
- Do not leave old private methods as alternate production paths.

## C# Architecture Impact

Separates runtime orchestration from execution drivers and creates fast unit-test seams for the historically hardest behavior.

## Boundary Ownership

Executor owns streaming. Finalizer coordinator owns repair. Session driver owns serialization. Approval driver owns cache/rehydration.

## Dependency Direction

Drivers depend on provider streaming abstractions and runtime records. They must not depend on `MafAgentRuntime`.

## Pattern Decision

Driver and Strategy-style decomposition for state-specific execution behavior.

## Testability Contract

Use fake streaming updates, fake session serialization, and fake progress recorder. No unit test may use a live provider.

## Partial Class Policy

No partials allowed.

## Architecture Proof Required

- Direct unit tests for each driver.
- Negative tests for timeout, missing finalizer, invalid approval state, and request-scoped payload scrubbing.
- Source assertion that moved methods no longer live in `MafAgentRuntime`.

## Acceptance Checklist

- [ ] Driver responsibilities are cohesive.
- [ ] Moved logic is not duplicated.
- [ ] Focused MAF tests pass.
- [ ] Handoff smoke passes or compatibility blocker recorded.

## Proof Required

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- failing-first or characterization transcript.
- passing unit/integration transcript.
- anti-stub/source assertion transcript.

## Browser Validation Logging

- N/A. Backend runtime behavior only.

## Progression Gate

- SB07 cannot start until driver tests pass and source assertions prove runtime ownership moved.

## Suggested Agent Prompt

```text
Execute SB03 only. Move streaming, finalizer repair, session persistence, and approval continuation into cohesive drivers with direct unit tests and negative cases. Do not broaden scope into capability composition.
```
