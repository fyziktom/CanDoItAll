# SB08 Proof Manifest

## Status

Completed.

## Source Assertions

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB08 runtime governance artifact | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs and bundle://proof/SB08/transcripts/passing.txt | Verified by bundle://proof/SB08/transcripts/source-assertions.txt and dotnet test proof | Rejected by bundle://proof/SB08/transcripts/failing-first.txt |

## Semantic Invariant Contract

- bundle://proof/SB08/semantic-invariants.md

## Failing-First Or Red-Team Proof

- bundle://proof/SB08/transcripts/failing-first.txt

## Passing Proof

- bundle://proof/SB08/transcripts/passing.txt
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.BuildProcessInvocationMetadataJson_SB08_INV_001_uses_persisted_operation_contract_without_text_markers`
- Test name: `CanDoItAll.Tests.Integration.ProcessDefinitionLinterTests.Analyze_SB08_INV_001_warns_when_operation_contract_is_text_inferred`
- Test name: `CanDoItAll.Tests.Integration.ProcessDefinitionLinterTests.Analyze_SB08_INV_001_accepts_typed_operation_contract_without_text_markers`
- Test name: `CanDoItAll.Tests.Integration.ProcessDefinitionLinterTests.Analyze_SB08_INV_001_rejects_partial_typed_operation_contract`

## Anti-Stub Audit

- bundle://proof/SB08/transcripts/anti-stub-audit.txt

## Changed-File Hashes

- bundle://proof/SB08/transcripts/changed-file-hashes.txt
- `566e1d25ab55b1518ba558b9e3335a7acb5605e4f0d1486ef63fbc369407894a`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `ae2bb3c20c14fecd65547fe71bf4b53babefb4b6423a205c6513c9e93c7212f0`  `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs`
- `723a55dcba4d78b63c9ea24f6d820e95ce6694a86bfd30d75f0b18b893bc6af8`  `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Validation

- Focused proof commands passed for SB08; see bundle://proof/SB08/transcripts/passing.txt.
- Source assertions passed for SB08; see bundle://proof/SB08/transcripts/source-assertions.txt.
- Anti-stub audit found no stub-only production implementation; see bundle://proof/SB08/transcripts/anti-stub-audit.txt.

## Blockers

None.
