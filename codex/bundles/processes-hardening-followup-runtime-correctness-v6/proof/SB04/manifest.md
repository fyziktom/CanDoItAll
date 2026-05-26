# SB04 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.InvocationMetadataBuilder.cs`: `ProcessInvocationMetadataBuilder` owns production metadata assembly and routes through the extracted operation-contract resolver and target-grounding ledger builder.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OperationContractResolver.cs`: `ProcessStepOperationContractResolver` exposes operation-contract and execution-boundary resolution as a named checkpoint boundary.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.TargetGroundingLedgerBuilder.cs`: `ProcessTargetGroundingLedgerBuilder` exposes grounding resolution, alias pruning, mutable/read-only alias selection, and ledger construction.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs`: `BuildProcessInvocationMetadataJson` now delegates to the extracted metadata builder.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`: SB04 tests call the extracted builders/resolver directly without private-method reflection.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB04 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.InvocationMetadataBuilder.cs | bundle://proof/SB04/manifest.md | bundle://proof/SB04/transcripts/passing.txt | bundle://proof/SB04/transcripts/failing-first.txt |
## Semantic Invariant Contract

- `bundle://proof/SB04/semantic-invariants.md`

## Failing-First or Red-Team Proof

- Transcript: `bundle://proof/SB04/transcripts/failing-first.txt`

## Passing Proof

- Transcript: `bundle://proof/SB04/transcripts/passing.txt`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.BuildProcessInvocationMetadataJson_SB04_INV_001_keeps_stale_upstream_product_alias_read_only_for_mutating_step`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessInvocationMetadataBuilder_SB04_INV_001_builds_external_artifact_destination_metadata_without_reflection`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessStepOperationContractResolver_SB04_INV_001_resolves_persisted_contract_without_reflection`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessTargetGroundingLedgerBuilder_SB04_INV_001_resolves_current_run_grounding_without_reflection`

## Anti-Stub Audit

- Transcript: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

- Transcript: `bundle://proof/SB04/transcripts/changed-file-hashes.txt`
- `1A9581FD5016E510BF531301309044C50012BB72182369DD96FDADBCC464509D` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.InvocationMetadataBuilder.cs`
- `4F65810A6199FD9B2926A081C7A642645B79388EEFA48C00A019E3B8D88742DC` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OperationContractResolver.cs`
- `00A42D756A89867D755DAD0BD5D46B0CA50DD93C6013E9AC68B4B28275136F2C` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.TargetGroundingLedgerBuilder.cs`
- `54798445F86084C3633E34B9531D4351804F440F7F3C30BD57375B2C488B50D7` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs`
- `D29B56AA4809BA87A6F8FCF3BB52A5DE58A3C074B7698847A1072DC8DBDD214D` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs`
- `C07370CA77A868233D245017E98435024EE3DA6054AD56934D45CB4EAFB8B1FB` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `C7A8DA245654DB8816908B7A9406CEA61BCF2E707812765E72F73BCC18531F19` `repo://codex/bundles/processes-hardening-followup-runtime-correctness-v6/architecture/02-refactoring-checkpoints.md`

## Validation

- Focused integration tests passed: `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~BuildProcessInvocationMetadataJson_SB04_INV_001|FullyQualifiedName~ProcessInvocationMetadataBuilder_SB04_INV_001|FullyQualifiedName~ProcessStepOperationContractResolver_SB04_INV_001|FullyQualifiedName~ProcessTargetGroundingLedgerBuilder_SB04_INV_001"`.

## Blockers

None.


