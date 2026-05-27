# SB11 Proof Manifest

## Status

Completed.

## Goal

Add explicit content policy for strict required narrative artifacts.

## Semantic Invariant Contract

- `bundle://proof/SB11/semantic-invariants.md`

## Failing-first or adversarial proof

- `bundle://proof/SB11/transcripts/failing-first.txt`
- Invariant ID: `SB11-INV-001`
- Test name: `ArtifactContractValidation_SB11_INV_001_reports_missing_required_brief_content`

## Passing proof

- `bundle://proof/SB11/transcripts/passing.txt`
- Invariant ID: `SB11-INV-001`
- Test name: `ArtifactContractValidation_SB11_INV_001_reports_missing_required_brief_content`

## Source assertions

- `bundle://proof/SB11/transcripts/source-assertions.txt`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Anti-stub audit

- `bundle://proof/SB11/transcripts/anti-stub-audit.txt`

## Changed-file hashes

- `bundle://proof/SB11/transcripts/changed-file-hashes.txt`
- `B5C93B4C73D111202419E786679875E9179D81A84C2ACCF8FA5A084C3B805F05` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs`
- `15FDE8B9366DA2026E98C379D02C4ED791338D38CDD748030D60FD9A62E2B9AC` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `1705CC8271AD148E235D93DBD89CA0A8C81FE5A8A1B468D8C6E16645F35253C0` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs`
- `6971115B787893E9CE5775B872A7D0B5F91B5E73975AB1299B0BDD6CD7771C0F` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
