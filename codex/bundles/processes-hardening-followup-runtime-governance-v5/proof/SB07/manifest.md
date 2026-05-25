# SB07 Proof Manifest

## Status

Completed.

## Source Assertions

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB07 runtime governance artifact | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs and bundle://proof/SB07/transcripts/passing.txt | Verified by bundle://proof/SB07/transcripts/source-assertions.txt and dotnet test proof | Rejected by bundle://proof/SB07/transcripts/failing-first.txt |

## Semantic Invariant Contract

- bundle://proof/SB07/semantic-invariants.md

## Failing-First Or Red-Team Proof

- bundle://proof/SB07/transcripts/failing-first.txt

## Passing Proof

- bundle://proof/SB07/transcripts/passing.txt
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ArtifactContractValidation_SB07_INV_001_classifies_missing_required_artifact_as_own_output`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ArtifactContractValidation_SB02_INV_001_accepts_manager_recovery_with_compact_key_and_typed_lineage`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ArtifactDispositionRouter_SB07_INV_001_blocks_missing_own_required_artifact_even_with_negative_branch`

## Anti-Stub Audit

- bundle://proof/SB07/transcripts/anti-stub-audit.txt

## Changed-File Hashes

- bundle://proof/SB07/transcripts/changed-file-hashes.txt
- `566e1d25ab55b1518ba558b9e3335a7acb5605e4f0d1486ef63fbc369407894a`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `fddc21fa79ddafced92b0e5d65bbab64c6aa77ac04d5da9d0c3f7994423056b1`  `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs`
- `723a55dcba4d78b63c9ea24f6d820e95ce6694a86bfd30d75f0b18b893bc6af8`  `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Validation

- Focused proof commands passed for SB07; see bundle://proof/SB07/transcripts/passing.txt.
- Source assertions passed for SB07; see bundle://proof/SB07/transcripts/source-assertions.txt.
- Anti-stub audit found no stub-only production implementation; see bundle://proof/SB07/transcripts/anti-stub-audit.txt.

## Blockers

None.
