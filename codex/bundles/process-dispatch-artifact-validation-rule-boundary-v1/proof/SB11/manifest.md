# SB11 Proof Manifest

Status: Completed.

## Objective

Move project-structure downgrade/defer/drop preservation checks into helper rules.

## Evidence Recorded

- Source assertion: `bundle://proof/SB11/source-assertions/project-structure-preservation-rule-extraction.md`
- Passing architecture test transcript: `bundle://proof/SB11/transcripts/focused-unit-architecture-tests.txt`
- Passing project-structure preservation integration test transcript: `bundle://proof/SB11/transcripts/focused-project-structure-preservation-integration-tests.txt`
- Changed-file hashes: `bundle://proof/SB11/transcripts/changed-file-hashes.txt`
- Source scans and anti-stub audit: `bundle://proof/SB11/transcripts/project-structure-rule-source-scans.txt`
- No-core/no-driver scan: `bundle://proof/SB11/transcripts/no-core-no-driver-scan.txt`
- Line-count transcript: `bundle://proof/SB11/transcripts/line-count.txt`

## Changed File Hashes

- `17C271A8F46F0BD7CC73478B826D62C324DC347C3E4430210484139675AEE799` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectStructureRequirementValidationRules.cs`
- `74D8C7B64E719AB14CAF0FE96D1439FC0D41E3FF98248FE9E9581CF54E098DA0` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`
- `403A11C89B0F06F1E37A65C4708D8FD62AA7A60FE401D0F0670AB618EFACD063` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `59A3A2B776BAA6D3D9EAC9F473A9D2FFF2D54AA98BF1D5B3FAF5BDFB2C235E1C` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Passing Proof

- `bundle://proof/SB11/transcripts/focused-unit-architecture-tests.txt`
- `bundle://proof/SB11/transcripts/focused-project-structure-preservation-integration-tests.txt`

## Source Assertions

- `bundle://proof/SB11/source-assertions/project-structure-preservation-rule-extraction.md`

## Anti-Stub Audit

- `bundle://proof/SB11/transcripts/project-structure-rule-source-scans.txt`

## Browser And Host Proof

N/A expected. Large desktop/PC only if unexpectedly needed.
