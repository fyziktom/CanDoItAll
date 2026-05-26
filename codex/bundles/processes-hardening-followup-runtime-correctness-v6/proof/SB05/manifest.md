# SB05 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs`: adds typed `ProcessStepBlockCause` with own-output, upstream-input, runtime-evidence, and policy-denied causes.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs`: `ProcessStepTransitionRequest` carries optional typed `BlockCause`.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunBlockState.cs`: maps typed causes to persisted `ProcessStepBlockReasonCode` and cause-specific recovery options; legacy text inference no longer maps generic missing required artifacts to missing upstream artifacts.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs`: blocked/failed transitions apply block state using `request.BlockCause`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`: artifact-validation failure ownership resolves to a typed block cause before transition.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`: upstream materialization blocks pass `ProcessStepBlockCause.UpstreamInput`.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB05 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs | bundle://proof/SB05/manifest.md | bundle://proof/SB05/transcripts/passing.txt | bundle://proof/SB05/transcripts/failing-first.txt |
## Semantic Invariant Contract

- `bundle://proof/SB05/semantic-invariants.md`

## Failing-First or Red-Team Proof

- Transcript: `bundle://proof/SB05/transcripts/failing-first.txt`

## Passing Proof

- Transcript: `bundle://proof/SB05/transcripts/passing.txt`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessStepRunBlockState_SB05_INV_001_maps_own_missing_required_artifact_to_artifact_contract_recovery`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessStepRunBlockState_SB05_INV_002_maps_upstream_missing_artifact_to_materialization_recovery`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessStepRunBlockState_SB05_INV_003_does_not_infer_own_required_artifact_as_upstream`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ResolveArtifactContractBlockCause_SB05_INV_001_prefers_upstream_ownership_when_present`
- Test name: `CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.TransitionStepAsync_SB05_INV_001_persists_own_output_artifact_contract_block_cause`
- Test name: `CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.TransitionStepAsync_SB05_INV_002_persists_upstream_input_materialization_block_cause`
- Test name: `CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.TransitionStepAsync_SB09_INV_001_persists_typed_policy_denial_block_state`

## Anti-Stub Audit

- Transcript: `bundle://proof/SB05/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

- Transcript: `bundle://proof/SB05/transcripts/changed-file-hashes.txt`
- `665EDD1369199AA58819BC89F26AAC45028ADED0E3E6A8622A26605E80FF9DE1` `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs`
- `71C1E3D49B2406DAABF749C983F81A5C9C1F77DBD32476590978C906A891A605` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs`
- `AFBA9DC0BE73523FD38B0CF4DE41EA85A431D02648FD8AFF2521E4605CA47A59` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunBlockState.cs`
- `7617BF90730FF5AC22849D02985606C0B5160FF9AF62456E610B5E107344E7A4` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs`
- `4F56E75CB0A6BBB6DE0439A3C893AEF530766B87C57828A2818D3C30D3F17A0C` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `586CD10DE06CC37501793D8B26B8E4AC16AF0E067D7D8DBF218B5C2D91079BAD` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `6160AB4E8AAA20AC00B838AF899B6947DA559BF3E1F026FC2177164D4E55C476` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `FD71701A6D33F262EB874F7DDC71B3CF99F7705476FAD797DDB3697D09BC52F8` `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

## Validation

- Focused integration tests passed: `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessStepRunBlockState_SB05_INV|FullyQualifiedName~ResolveArtifactContractBlockCause_SB05_INV|FullyQualifiedName~TransitionStepAsync_SB05_INV|FullyQualifiedName~TransitionStepAsync_SB09_INV_001_persists_typed_policy_denial_block_state"`.

## Blockers

None.


