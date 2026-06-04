# SB06 Proof Manifest

Status: Completed.

## Objective

Move path normalization, managed path matching, and governed path comparison into helper rules.

## Evidence Recorded

- Source assertion: `bundle://proof/SB06/source-assertions/path-rule-extraction.md`
- Failing-first compile transcript: `bundle://proof/SB06/transcripts/focused-unit-architecture-tests.txt`
- Passing architecture test transcript: `bundle://proof/SB06/transcripts/focused-unit-architecture-tests-rerun.txt`
- Passing path integration test transcript: `bundle://proof/SB06/transcripts/focused-path-integration-tests.txt`
- Changed-file hashes: `bundle://proof/SB06/transcripts/changed-file-hashes.txt`
- Source scans and anti-stub audit: `bundle://proof/SB06/transcripts/path-rule-source-scans.txt`

## Changed File Hashes

- `64BBF7A1005B394B65C95A1F0302A183A30AC001A94BF43C1C9FE7787AE7B867` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactPathValidationRules.cs`
- `EC78AFC0096B1CC6D5E6609F185D7D23342DC2609C5EC62148DD352F2A43E52E` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `1497F9505DCC5AA2BA151187B1713706554911013C14233459A6563F5A26E2FD` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Failing-First Proof

- `bundle://proof/SB06/transcripts/focused-unit-architecture-tests.txt` captured the first compile failure when the extracted helper missed the `CanDoItAll.AgentFramework.Models` import for `WorkspaceScopeDescriptor`.

## Passing Proof

- `bundle://proof/SB06/transcripts/focused-unit-architecture-tests-rerun.txt`
- `bundle://proof/SB06/transcripts/focused-path-integration-tests.txt`

## Source Assertions

- `bundle://proof/SB06/source-assertions/path-rule-extraction.md`

## Anti-Stub Audit

- `bundle://proof/SB06/transcripts/path-rule-source-scans.txt`

## Browser And Host Proof

N/A expected. Large desktop/PC only if unexpectedly needed.
