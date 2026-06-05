# SB08 Proof Manifest

## Scope

SB08 proves database guard and upstream gap facts parity after helper extraction.

## Changed File Hashes

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDatabaseRequirementBlocker.cs` SHA-256 a6a7a71feb20f1ef5fffdd3463b175d7682db1233d724e4a53e9a6043ee62f17
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs` SHA-256 b51da451d02dbfe83e753155be58c83b0eec6f4c1e9ec0a3037b7d3fd2244a56
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` SHA-256 76fa61a52587deafba8f3f7cf9d8368c8d2cf29e43cd7cb6fb5b4072861a8499

## Semantic Contract

- `bundle://proof/SB08/semantic-invariants.md`

## Evidence

- Passing transcript: `bundle://proof/SB08/transcripts/sb08-focused-guard-tests-rerun.txt`
- Source assertion transcript: `bundle://proof/SB08/transcripts/sb08-source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB19/transcripts/sb19-final-source-scans.txt`
- Failing-first proof: N/A - process refactor parity gate; no behavior-changing production path was intended.

