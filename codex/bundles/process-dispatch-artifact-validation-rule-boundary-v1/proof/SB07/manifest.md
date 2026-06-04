# SB07 Proof Manifest

Status: Completed.

## Objective

Move title/slug/text-content signal matching into helper rules with parity tests.

## Evidence Recorded

- Source assertion: `bundle://proof/SB07/source-assertions/title-text-rule-extraction.md`
- Failing-first compile transcript: `bundle://proof/SB07/transcripts/focused-unit-architecture-tests.txt`
- Passing architecture test transcript: `bundle://proof/SB07/transcripts/focused-unit-architecture-tests-rerun.txt`
- Passing title/text integration test transcript: `bundle://proof/SB07/transcripts/focused-title-text-integration-tests.txt`
- Changed-file hashes: `bundle://proof/SB07/transcripts/changed-file-hashes.txt`
- Source scans and anti-stub audit: `bundle://proof/SB07/transcripts/text-rule-source-scans.txt`

## Changed File Hashes

- `C139AB340F98B3640E70718C8EE212E18C59793E9A12545903A727AA8ABACAE9` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactTextMatchRules.cs`
- `1FA8AD80026FF3B2450726B97F8ECE9273BE0586CB8DEEA6A1E341D8C99620A0` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`
- `385C865BD6A1DA24249E8448F919DB689B009685982E41B9E2AF1EA82C98A244` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `49EF16B8A24CCAF387C9CF78950B4F5A765B1584C66CBD9DFCD52AED4992C2A8` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Failing-First Proof

- `bundle://proof/SB07/transcripts/focused-unit-architecture-tests.txt` captured the first compile failure after extracting text rules without importing `CanDoItAll.SharedKernel` for `FileSafeSlugBuilder`.

## Passing Proof

- `bundle://proof/SB07/transcripts/focused-unit-architecture-tests-rerun.txt`
- `bundle://proof/SB07/transcripts/focused-title-text-integration-tests.txt`

## Source Assertions

- `bundle://proof/SB07/source-assertions/title-text-rule-extraction.md`

## Anti-Stub Audit

- `bundle://proof/SB07/transcripts/text-rule-source-scans.txt`

## Browser And Host Proof

N/A expected. Large desktop/PC only if unexpectedly needed.
