# SB14 Proof Manifest

## Scope

SB14 proves materialization fingerprint, journal, rerun request, and coordinator parity.

## Changed File Hashes

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs` SHA-256 b51da451d02dbfe83e753155be58c83b0eec6f4c1e9ec0a3037b7d3fd2244a56
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` SHA-256 e202b81b6e7f9317b80dc682940eb5839c1aabd12ff5c863ece182f9d22d7d8c
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` SHA-256 76fa61a52587deafba8f3f7cf9d8368c8d2cf29e43cd7cb6fb5b4072861a8499

## Semantic Contract

- `bundle://proof/SB14/semantic-invariants.md`

## Evidence

- Passing transcript: `bundle://proof/SB14/transcripts/sb14-focused-helper-tests-rerun.txt`
- Build transcript: `bundle://proof/SB14/transcripts/sb14-build-after-test-adjustment.txt`
- Source assertion transcript: `bundle://proof/SB14/transcripts/sb14-source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB19/transcripts/sb19-final-source-scans.txt`
- Failing-first proof: N/A - process refactor parity gate; helper tests cover preserved behavior and no production behavior change was intended.

