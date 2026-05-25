# SB06 Proof Manifest

## Status

Completed.

## Source Assertions

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB06 runtime governance artifact | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs | repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs and bundle://proof/SB06/transcripts/passing.txt | Verified by bundle://proof/SB06/transcripts/source-assertions.txt and dotnet test proof | Rejected by bundle://proof/SB06/transcripts/failing-first.txt |

## Semantic Invariant Contract

- bundle://proof/SB06/semantic-invariants.md

## Failing-First Or Red-Team Proof

- bundle://proof/SB06/transcripts/failing-first.txt

## Passing Proof

- bundle://proof/SB06/transcripts/passing.txt
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.WorkflowArtifactProjectionMapping_SB06_INV_001_uses_explicit_output_id_when_same_kind_names_conflict`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.WorkflowArtifactProjectionMapping_SB06_INV_001_blocks_same_kind_heuristic_without_explicit_output_id`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.SubprocessArtifactProjectionMapping_SB06_INV_001_uses_child_expectation_id_when_same_kind_titles_conflict`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.SubprocessArtifactProjectionMapping_SB06_INV_001_blocks_same_kind_heuristic_without_child_mapping`

## Anti-Stub Audit

- bundle://proof/SB06/transcripts/anti-stub-audit.txt

## Changed-File Hashes

- bundle://proof/SB06/transcripts/changed-file-hashes.txt
- `566e1d25ab55b1518ba558b9e3335a7acb5605e4f0d1486ef63fbc369407894a`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `723a55dcba4d78b63c9ea24f6d820e95ce6694a86bfd30d75f0b18b893bc6af8`  `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Validation

- Focused proof commands passed for SB06; see bundle://proof/SB06/transcripts/passing.txt.
- Source assertions passed for SB06; see bundle://proof/SB06/transcripts/source-assertions.txt.
- Anti-stub audit found no stub-only production implementation; see bundle://proof/SB06/transcripts/anti-stub-audit.txt.

## Blockers

None.
