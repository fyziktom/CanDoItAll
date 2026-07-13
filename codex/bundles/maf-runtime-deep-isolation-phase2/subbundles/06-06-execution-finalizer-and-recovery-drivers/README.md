# SB06 Execution Finalizer And Recovery Drivers

## Status

- `Ready`

## Objective

Extract runtime execution support behavior from `MafAgentRuntime.cs` and `MafAgentRuntime.AgentFactory.cs`: finalizer recovery, process-artifact parsing, provider failure diagnostics, session serialization decisions, approval rehydration, and repeated-tool invocation guard.

## Covered Inputs

- N003, N005, N006
- MAF2-R007, MAF2-R008, MAF2-R009, MAF2-R010

## Prerequisites

- SB03 closure proof.
- SB04 closure proof for builder seams.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafFinalizerDriver.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeExecutionContracts.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`

## Deliverables

- `MafRuntimeExecutionCoordinator` or equivalent for public run orchestration.
- `RuntimeSessionPersistenceService` for serialization/rehydration decisions.
- `ProcessArtifactRecoveryService` for process artifact status/outcome recovery.
- `ProviderFailureDiagnosticBuilder` or equivalent.
- `ToolInvocationGuard` top-level service.
- `MafAgentRuntime` delegates to these collaborators and no longer owns large recovery/helper blocks.

## Dependency Impact

- SB08 cannot claim a thin runtime until execution/recovery helpers leave `MafAgentRuntime`.
- SB07 test migration depends on direct collaborator tests.

## Validation Depth

- Critical behavior phase.
- Requires Semantic Adequacy Gate proof for recovery behavior and guard behavior.

## Implementation Steps

1. Extract pure process-artifact parsing/recovery helpers and migrate existing finalizer tests.
2. Extract provider failure diagnostic and finalizer response recovery.
3. Extract session serialization and request-scoped attachment filtering decisions.
4. Extract approval mapping/rehydration if it can be separated cleanly.
5. Extract repeated tool invocation guard.
6. Introduce an execution coordinator only after helper services are named; avoid creating a new god class.

## Scope Exceptions

- Full public runtime adapter cleanup is reserved for SB08 after tests and guards pass.

## Do Not Do

- Do not use exceptions as control flow where a typed result is clearer unless preserving existing behavior requires it.
- Do not hide execution dependencies behind `IServiceProvider`.
- Do not broaden finalizer recovery semantics without failing-first tests.

## Acceptance Checklist

- Finalizer/process recovery tests target extracted services.
- Tool invocation guard tests target extracted guard.
- Session serialization decision tests target extracted service.
- `MafAgentRuntime.cs` no longer contains large private static process-artifact parsing blocks.

## Proof Required

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- Build transcript.
- Focused finalizer/recovery/session/guard unit tests.
- Source scan for moved helper names in `MafAgentRuntime.cs`.

## Browser Validation Logging

- N/A: backend execution refactor.

## Progression Gate

- SB07 may start only after runtime-level tests are no longer the only proof for finalizer/recovery/session behavior.

## Suggested Agent Prompt

```text
Implement SB06 only. Extract finalizer, recovery, provider failure, session persistence, approval mapping, and repeated-tool guard behavior into named services. Keep behavior parity and add direct tests.
```
