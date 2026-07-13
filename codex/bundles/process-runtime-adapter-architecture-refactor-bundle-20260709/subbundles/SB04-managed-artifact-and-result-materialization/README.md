# SB04 - Managed Artifact And Result Materialization

## Status

- Status: `Completed`

## Objective

Extract managed artifact materialization, evidence validation, acceptance, and result conversion from the adapter.

## Covered Inputs

- User requirement for split responsibilities.
- GPTPro artifact ledger and finalizer semantic gap findings.

## Prerequisites

- SB01 characterization complete.
- SB02 contract boundaries complete.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifactEvidence.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ParentSubprocessArtifactBridge.cs`

## Dependency Impact

- Expected to remain mostly in `Modules.Processes` unless stable artifact contracts are needed.

## Validation Depth

- Direct unit tests with fake workspace file service.
- Adapter delegation proof.

## Do Not Do

- Do not move MAF-specific models into generic runtime.
- Do not accept physical file existence as runtime-accepted evidence.
- Do not create a broad artifact manager.

## Acceptance Checklist

- [ ] Materializer direct tests pass.
- [ ] Result converter direct tests pass.
- [ ] Adapter delegates.
- [ ] Moved methods removed from adapter.

## Proof Required

- Proof manifest with direct tests.
- Source assertions.
- Adapter shrink proof.
- No-new-partial proof.

## Browser Validation Logging

- Not applicable.

## Progression Gate

- SB05 may rely on artifact contracts only after ledger/readback behavior is directly tested.

## Suggested Agent Prompt

Implement SB04 only. Extract managed artifact behavior and result conversion without changing gate semantics.

## Goal

Extract managed outcome artifact materialization, artifact evidence validation, artifact acceptance append behavior, produced artifact content hashing, and final result conversion from the adapter into testable top-level services.

## Scope

- `AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`
- `AgentFrameworkProcessExecutionAdapter.ManagedArtifactEvidence.cs`
- artifact acceptance/readback helpers
- produced artifact hashes
- result conversion that does not belong to branch routing or recovery

## Implementation Steps

1. Use SB01 characterization tests as baseline.
2. Create `IManagedOutcomeArtifactMaterializer` with explicit input/output records.
3. Create `IManagedArtifactEvidenceValidator` if validation is separable.
4. Create `IProcessOutcomeResultConverter` for accepted outcome to adapter result conversion.
5. Move artifact write/append/readback operations behind `IWorkspaceFileService` abstractions or existing file services.
6. Keep MAF-specific model mapping in `Modules.Processes`.
7. Update adapter to delegate materialization and conversion.
8. Delete moved methods from adapter partial files.
9. Add direct unit tests with fake workspace file service.
10. Run targeted tests and build.

## C# Architecture Impact

This subbundle reduces adapter state and makes artifact behavior testable without MAF execution runs or the full runtime adapter.

## Boundary Ownership

MAF-specific artifact materialization remains in `Modules.Processes` unless a generic artifact contract is already available. Stable produced/requested artifact records remain in process contracts/abstractions.

## Dependency Direction

Do not move MAF model dependencies into `Processes.Runtime`. If runtime needs a generic materialization result, define a small contract and map in module integration.

## Pattern Decision

Use Adapter around workspace file operations only where needed. Use a simple extracted service for materialization; do not introduce a broad artifact manager.

## Testability Contract

Required direct tests:

- Materializer writes missing primary artifact.
- Materializer appends runtime gate findings.
- Ungrounded artifact reference is rejected.
- Readback failure produces explicit issue.
- Final result conversion uses accepted artifact refs and content hashes.
- Test fakes file reads/writes without full adapter.

## Partial Class Policy

Delete or shrink:

- `AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`
- `AgentFrameworkProcessExecutionAdapter.ManagedArtifactEvidence.cs`
- result conversion methods that only build final adapter result.

No new partials.

## Architecture Proof Required

- Source assertion that artifact behavior moved out of adapter.
- Direct unit tests for materializer/converter.
- No-new-partial proof.
- Adapter line-count reduction proof.
