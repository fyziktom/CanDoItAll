# SB012 Proof Manifest

## Status
- Completed.

## Scope
- Gate D closure for retry/provider/no-progress diagnostic descriptor parity.
- Confirms Core descriptors, adapter ownership, retry/provider behavior preservation, public API snapshot, and no driver/UI/stub drift.

## Semantic Invariants
- `bundle://proof/SB012/semantic-invariants.md`.
- Invariant IDs: SB012-DIAGNOSTIC-001, SB012-BOUNDARY-001, SB012-DRIVER-001, SB012-UI-001, SB012-STUB-001.

## Failing-First Evidence
- `bundle://proof/SB012/transcripts/failing-first-diagnostic-descriptor-gap.txt` records the pre-SB011/SB012 gap with `ExitCode: 1`.

## Passing Evidence
- Build: `bundle://proof/SB012/transcripts/diagnostic-descriptor-build-before-snapshot.txt`.
- Architecture tests: `bundle://proof/SB012/transcripts/diagnostic-descriptor-architecture-tests.txt`.
- Focused integration tests: `bundle://proof/SB012/transcripts/diagnostic-descriptor-focused-integration-tests.txt`.
- API generation: `bundle://proof/SB012/transcripts/api-surface-generation-after-diagnostics.txt`.
- Source assertions: `bundle://proof/SB012/transcripts/source-assertions.txt`.
- Semantic closure: `bundle://proof/SB012/transcripts/semantic-closure.txt`.

## Scan Evidence
- Core forbidden dependency scan: `bundle://proof/SB012/transcripts/core-diagnostics-forbidden-token-scan.txt`.
- Adapter confinement scan: `bundle://proof/SB012/transcripts/adapter-confinement-scan.txt`.
- Production process-driver token scan: `bundle://proof/SB012/transcripts/production-process-driver-token-scan.txt`.
- UI/media drift scan: `bundle://proof/SB012/transcripts/ui-media-drift-scan.txt`.
- Anti-stub audit: `bundle://proof/SB012/transcripts/anti-stub-audit.txt`.

## Hashes
- Hash index: `bundle://proof/SB012/transcripts/changed-file-hashes.txt`.
- SHA-256 `AC4BD2B655B55D2AE1A0B52B2008F6E29AC3C510BBDCFE252742C0271C246471` for `repo://src/CanDoItAll.Processes.Core/Diagnostics/ProcessRetryDiagnosticDescriptors.cs`.
- SHA-256 `202BF62BC97A9C95CCCD4A8E05D97331DFF65C1D7B50C363272D808E90F81956` for `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRetryDiagnosticDescriptorAdapter.cs`.
- SHA-256 `5AEDE72F85E0B903D26EF41A431F38AAC849C3C6CB8FD2C4D0571EA3AE6C2028` for `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.

## Result
- SB012 passed. Diagnostic descriptors are Core-pure, adapter-owned, and behavior-covered for retry, no-progress, critical failure, and provider failure paths.
