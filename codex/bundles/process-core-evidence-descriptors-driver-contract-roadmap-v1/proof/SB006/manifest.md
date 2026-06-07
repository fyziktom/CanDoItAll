# SB006 Proof Manifest

## Status
- Completed.

## Scope
- Gate B closure for execution evidence descriptor parity.
- Confirms Core descriptors, adapter ownership, public API snapshot, behavior preservation, and no driver/UI/stub drift.

## Semantic Invariants
- `bundle://proof/SB006/semantic-invariants.md`.
- Invariant IDs: SB006-EXEC-001, SB006-BOUNDARY-001, SB006-DRIVER-001, SB006-UI-001, SB006-STUB-001.

## Failing-First Evidence
- `bundle://proof/SB006/transcripts/failing-first-execution-descriptor-gap.txt` records the pre-SB005/SB006 gap with `ExitCode: 1`.
- `bundle://proof/SB006/transcripts/execution-descriptor-architecture-tests.txt` records an interim guard failure before the source assertion/snapshot rebuild was corrected with `ExitCode: 1`.

## Passing Evidence
- Build: `bundle://proof/SB006/transcripts/execution-descriptor-build-after-snapshot-prep.txt`.
- Architecture tests: `bundle://proof/SB006/transcripts/execution-descriptor-architecture-tests-after-fix.txt`.
- Focused integration tests: `bundle://proof/SB006/transcripts/execution-descriptor-focused-integration-tests.txt`.
- API generation: `bundle://proof/SB006/transcripts/api-surface-generation-transcript.txt`.
- Source assertions: `bundle://proof/SB006/transcripts/source-assertions.txt`.
- Semantic closure: `bundle://proof/SB006/transcripts/semantic-closure.txt`.

## Scan Evidence
- Core forbidden dependency scan: `bundle://proof/SB006/transcripts/core-execution-forbidden-token-scan.txt`.
- Adapter confinement scan: `bundle://proof/SB006/transcripts/adapter-confinement-scan.txt`.
- Production process-driver token scan: `bundle://proof/SB006/transcripts/production-process-driver-token-scan.txt`.
- UI/media drift scan: `bundle://proof/SB006/transcripts/ui-media-drift-scan.txt`.
- Anti-stub audit: `bundle://proof/SB006/transcripts/anti-stub-audit.txt`.

## Hashes
- Hash index: `bundle://proof/SB006/transcripts/changed-file-hashes.txt`.
- SHA-256 `7C87FB9798B3E19F44FE13CA8EBA78A71B817CA2D05DFAF3CB84140A1E05BB24` for `repo://src/CanDoItAll.Processes.Core/Execution/ProcessExecutionEvidenceDescriptors.cs`.
- SHA-256 `F03698FC62199A2558275B10DECA4A3729D5BC59CF3D06351A0AAB2152318134` for `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionEvidenceDescriptorAdapter.cs`.
- SHA-256 `EFC9269496889A633F20608036F83687D50040F08B4A7B43D14F97CDE54EEF94` for `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.

## Result
- SB006 passed. Execution descriptors are Core-pure, adapter-owned, behavior-covered, and guarded by public API and architecture tests.
