# SB03 Proof Manifest

## Status

Completed.

## Source Assertions

- repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentWorkspaceToolAccessMetadataTests.cs

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB03 runtime governance artifact | repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs and bundle://proof/SB03/transcripts/passing.txt | Verified by bundle://proof/SB03/transcripts/source-assertions.txt and dotnet test proof | Rejected by bundle://proof/SB03/transcripts/failing-first.txt |

## Semantic Invariant Contract

- bundle://proof/SB03/semantic-invariants.md

## Failing-First Or Red-Team Proof

- bundle://proof/SB03/transcripts/failing-first.txt

## Passing Proof

- bundle://proof/SB03/transcripts/passing.txt
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.BuildProcessInvocationMetadataJson_allows_external_artifact_destination_without_product_mutation`
- Test name: `CanDoItAll.Tests.Unit.AgentWorkspaceToolAccessMetadataTests.GroundPromptExternalTargetAliases_SB04_INV_001_keeps_process_free_text_alias_read_only_even_when_product_mutation_is_allowed`

## Anti-Stub Audit

- bundle://proof/SB03/transcripts/anti-stub-audit.txt

## Changed-File Hashes

- bundle://proof/SB03/transcripts/changed-file-hashes.txt
- `2ab3f2d6e98713abfc4968c15b5633839d5f49fe4788731295476f78a0218f93`  `repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`
- `a4a1e05e97e01ea81b3f7a4b4ea9dc1355b158e02bb4791dad58131597a869ad`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs`
- `723a55dcba4d78b63c9ea24f6d820e95ce6694a86bfd30d75f0b18b893bc6af8`  `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Validation

- Focused proof commands passed for SB03; see bundle://proof/SB03/transcripts/passing.txt.
- Source assertions passed for SB03; see bundle://proof/SB03/transcripts/source-assertions.txt.
- Anti-stub audit found no stub-only production implementation; see bundle://proof/SB03/transcripts/anti-stub-audit.txt.

## Blockers

None.
