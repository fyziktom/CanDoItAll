# SB04 Proof Manifest

## Status

Completed.

## Source Assertions

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB04 runtime governance artifact | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs and bundle://proof/SB04/transcripts/passing.txt | Verified by bundle://proof/SB04/transcripts/source-assertions.txt and dotnet test proof | Rejected by bundle://proof/SB04/transcripts/failing-first.txt |

## Semantic Invariant Contract

- bundle://proof/SB04/semantic-invariants.md

## Failing-First Or Red-Team Proof

- bundle://proof/SB04/transcripts/failing-first.txt

## Passing Proof

- bundle://proof/SB04/transcripts/passing.txt
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ArtifactContractValidation_SB04_INV_001_reads_catalog_backed_storage_reference`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ArtifactContractValidation_SB05_INV_001_rejects_malformed_json_from_relative_managed_storage_path`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ArtifactContractValidation_SB05_INV_001_reports_missing_relative_managed_storage_content`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ArtifactContractValidation_SB05_INV_001_rejects_relative_managed_storage_content_over_validation_limit`

## Anti-Stub Audit

- bundle://proof/SB04/transcripts/anti-stub-audit.txt

## Changed-File Hashes

- bundle://proof/SB04/transcripts/changed-file-hashes.txt
- `f21cdf047934c3e7467912db584c05b42d677bb2d522753e98af06330ed1bd33`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`
- `566e1d25ab55b1518ba558b9e3335a7acb5605e4f0d1486ef63fbc369407894a`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `723a55dcba4d78b63c9ea24f6d820e95ce6694a86bfd30d75f0b18b893bc6af8`  `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Validation

- Focused proof commands passed for SB04; see bundle://proof/SB04/transcripts/passing.txt.
- Source assertions passed for SB04; see bundle://proof/SB04/transcripts/source-assertions.txt.
- Anti-stub audit found no stub-only production implementation; see bundle://proof/SB04/transcripts/anti-stub-audit.txt.

## Blockers

None.
