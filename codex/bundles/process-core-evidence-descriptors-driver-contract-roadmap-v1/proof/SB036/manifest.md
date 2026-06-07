# SB036 Proof Manifest

## Status
- Completed.

## Scope
- Gate L broad smoke closure.

## Semantic Invariants
- `bundle://proof/SB036/semantic-invariants.md`.
- Invariant IDs: SB036-BUILD-001, SB036-TEST-001, SB036-CORE-001, SB036-DRIVER-001, SB036-UI-001, SB036-STUB-001.

## Failing-First Evidence
- `bundle://proof/SB036/transcripts/failing-first-broad-smoke-gap.txt` records the broad-smoke gate requirement with `ExitCode: 1`.

## Passing Evidence
- Build: `bundle://proof/SB034/transcripts/final-solution-build.txt`.
- Full unit tests: `bundle://proof/SB034/transcripts/full-unit-tests.txt`.
- Focused integration: `bundle://proof/SB035/transcripts/focused-integration-matrix.txt`.
- Source assertions: `bundle://proof/SB036/transcripts/source-assertions.txt`.
- Semantic closure: `bundle://proof/SB036/transcripts/semantic-closure.txt`.

## Scan Evidence
- Forbidden Core source scan: `bundle://proof/SB036/transcripts/final-forbidden-core-source-scan.txt`.
- Core project reference scan: `bundle://proof/SB036/transcripts/final-core-project-reference-scan.txt`.
- Production process-driver token scan: `bundle://proof/SB036/transcripts/final-production-driver-token-scan.txt`.
- UI/media drift scan: `bundle://proof/SB036/transcripts/final-ui-media-drift-scan.txt`.
- Anti-stub audit: `bundle://proof/SB036/transcripts/final-anti-stub-audit.txt`.
- Proof index shape scan: `bundle://proof/SB036/transcripts/proof-index-shape-scan.txt`.

## Hashes
- Hash index: `bundle://proof/SB036/transcripts/changed-file-hashes.txt`.
- SHA-256 `89932F34BD10EE1E977A1F046EB02E8844224C0AE7A01F34569B4F0C48C9CDE5` for `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.

## Result
- SB036 passed. Broad smoke proof is complete and clean.
