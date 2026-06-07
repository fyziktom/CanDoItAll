# SB015 Proof Manifest

## Status
- Completed.

## Scope
- Gate E closure for projection/validation descriptor parity.
- Confirms Core descriptors, adapter ownership, source order preservation, lineage parity, provider-native browser satisfaction behavior, public API snapshot, and no driver/UI/stub drift.

## Semantic Invariants
- `bundle://proof/SB015/semantic-invariants.md`.
- Invariant IDs: SB015-PROJECTION-001, SB015-LINEAGE-001, SB015-PROVIDER-BROWSER-001, SB015-BOUNDARY-001, SB015-DRIVER-001, SB015-UI-001, SB015-STUB-001.

## Failing-First Evidence
- `bundle://proof/SB015/transcripts/failing-first-projection-validation-descriptor-gap.txt` records the pre-SB014/SB015 gap with `ExitCode: 1`.

## Passing Evidence
- Build: `bundle://proof/SB015/transcripts/projection-validation-descriptor-build.txt`.
- Architecture tests: `bundle://proof/SB015/transcripts/projection-validation-architecture-tests.txt`.
- Focused integration tests: `bundle://proof/SB015/transcripts/projection-validation-focused-integration-tests.txt`.
- API generation: `bundle://proof/SB015/transcripts/api-surface-generation-after-projection-evidence.txt`.
- Source assertions: `bundle://proof/SB015/transcripts/source-assertions.txt`.
- Semantic closure: `bundle://proof/SB015/transcripts/semantic-closure.txt`.

## Scan Evidence
- Core forbidden dependency scan: `bundle://proof/SB015/transcripts/forbidden-core-source-scan.txt`.
- Adapter confinement scan: `bundle://proof/SB015/transcripts/dispatch-core-reference-scan.txt`.
- UI/media drift scan: `bundle://proof/SB015/transcripts/ui-media-drift-scan.txt`.
- Anti-stub audit: `bundle://proof/SB015/transcripts/anti-stub-audit.txt`.

## Hashes
- Hash index: `bundle://proof/SB015/transcripts/changed-file-hashes.txt`.
- SHA-256 `4308E00E1350BA17087FCE9D9C36BD30478BBF68582AF936912AA1C1EBC3D321` for `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactProjectionEvidenceDescriptors.cs`.
- SHA-256 `C3095BF143885AB46F4CD8F47A36E07BF0D1D3293262F740BFE82F972651CB47` for `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionEvidenceDescriptorAdapter.cs`.
- SHA-256 `496A478F3DB29FF4B6BEBB50762205091A9AEC082A859BAD70D2C79FF0D8B9F6` for `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- SHA-256 `CA1B6122DCB41C7FC2CBD015BF322233D7871C1E9F8EFC8DDC788CACF167FEA7` for `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.

## Result
- SB015 passed. Projection evidence descriptors are Core-pure, adapter-owned, and behavior-covered for source order, lineage, provider-native browser evidence, and validation policy parity.
