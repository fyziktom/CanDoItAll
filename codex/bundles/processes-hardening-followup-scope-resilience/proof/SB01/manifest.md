# SB01 Proof Manifest

## Status

- Completed

## Source Assertions

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs line 10 defines ProcessStepExecutionBoundary.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs line 37 resolves the boundary before mutable target aliases.
- repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs line 18 publishes agentProcessStepExecutionBoundary.
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs line 4037 covers read-only boundary rejection.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| ProcessStepExecutionBoundary metadata | BuildProcessInvocationMetadataJson; source: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs and repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs | ExecutionInvocationMetadata and workspace tool profile policy; proof: bundle://proof/SB01/transcripts/source-assertions.txt | Computed for each DispatchCandidate before execution metadata is built; passing command: bundle://proof/SB01/transcripts/passing.txt | ToolPolicy_rejects_product_mutation_against_read_only_process_boundary rejects a read-only product write.; negative transcript: bundle://proof/SB01/transcripts/failing-first.txt |

## Failing-First Proof

- Transcript: bundle://proof/SB01/transcripts/failing-first.txt
- Summary: Focused metadata tests initially failed with ExitCode: 1 because analysis/design artifact destinations did not receive the explicit external artifact allowlist.

## Passing Proof

- Transcript: bundle://proof/SB01/transcripts/passing.txt
- Tests: ToolPolicy_rejects_product_mutation_against_read_only_process_boundary; BuildProcessInvocationMetadataJson_allows_external_artifact_destination_writes

## Semantic Invariants

- Contract: bundle://proof/SB01/semantic-invariants.md
- Invariant: SB01-INV-001

## Anti-Stub Audit

- Transcript: bundle://proof/SB01/transcripts/anti-stub-audit.txt
- Result: No production stubs, no NotImplementedException placeholders, and no fake artifact satisfiers were introduced.

## Changed-File Hashes

- Transcript: bundle://proof/SB01/transcripts/changed-file-hashes.txt
- 4E89C06DB8B7446D63B4262350FD46EF76D6561BD717E3F7109B204DFFD725A8  repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs
- EB39B389D48C43D1DA14638767533D41CB50AC4388AEA1F25CD30402A83D4656  repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs
- 835DDDCE07F9354B30C081B5B0484FE283A5D9303A56D0E45B82BE9E37576EDA  repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs
- DDE0D4670E819160395A994D3A89A853021544520931DE246582ABB32385FAEE  repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Validation

- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests" exited 0 with 409 passed tests.
- dotnet build CanDoItAll.slnx --no-restore exited 0 with existing EF Core assembly-version warnings and zero errors.

## Blockers

- None.

