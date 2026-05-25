# SB01 Proof Manifest

## Status

Completed.

## Source Assertions

- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs
- repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessDefinitionEntityConfigurations.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Components/ProcessStepEditorFormTests.cs

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB01 runtime governance artifact | repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs | repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessDefinitionEntityConfigurations.cs and bundle://proof/SB01/transcripts/passing.txt | Verified by bundle://proof/SB01/transcripts/source-assertions.txt and dotnet test proof | Rejected by bundle://proof/SB01/transcripts/failing-first.txt |

## Semantic Invariant Contract

- bundle://proof/SB01/semantic-invariants.md

## Failing-First Or Red-Team Proof

- bundle://proof/SB01/transcripts/failing-first.txt

## Passing Proof

- bundle://proof/SB01/transcripts/passing.txt
- Test name: `CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.Save_export_import_and_publish_SB08_INV_001_preserve_step_operation_contract`
- Test name: `CanDoItAll.Tests.Components.ProcessStepEditorFormTests.Render_SB08_INV_001_operation_contract_controls_update_model`

## Anti-Stub Audit

- bundle://proof/SB01/transcripts/anti-stub-audit.txt

## Changed-File Hashes

- bundle://proof/SB01/transcripts/changed-file-hashes.txt
- `a4a1e05e97e01ea81b3f7a4b4ea9dc1355b158e02bb4791dad58131597a869ad`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs`
- `3083277fccf897fb6a73f49bef706d3584fd0250164f245b831a0309f01fccc6`  `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

## Validation

- Focused proof commands passed for SB01; see bundle://proof/SB01/transcripts/passing.txt.
- Source assertions passed for SB01; see bundle://proof/SB01/transcripts/source-assertions.txt.
- Anti-stub audit found no stub-only production implementation; see bundle://proof/SB01/transcripts/anti-stub-audit.txt.

## Blockers

None.
