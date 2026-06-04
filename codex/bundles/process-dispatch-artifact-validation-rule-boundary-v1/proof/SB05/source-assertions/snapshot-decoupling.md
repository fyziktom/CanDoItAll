# SB05 Snapshot Decoupling Assertions

## Result

Passed.

## Assertions

- Dispatcher-facing `MatchExpectedArtifactId` overloads still accept `DispatchArtifactExpectation` for existing callers and tests.
- The matcher core now operates on `IReadOnlyList<ProcessArtifactValidationExpectation>`.
- Pure path/narrative/content/matcher helpers now have validation-expectation overloads, with dispatcher conversion happening at the edge.
- Projection conversion now flows through `ProcessArtifactValidationSnapshotBuilder.ToProjectionExpectation`; the old dispatcher-local `private static ProcessArtifactProjectionExpectation ToProjectionExpectation(...)` helper no longer exists.
- `ProcessRunAutomationDispatchService.ArtifactProjection.cs` callers use the snapshot builder conversion instead of a private partial-class converter.
- Focused matcher parity tests passed: 13 `MatchExpectedArtifactId` integration tests.
- Focused architecture tests passed: 3 `Artifact_validation*` unit tests.

## Proof

- Failing-first compile transcript: `bundle://proof/SB05/transcripts/focused-unit-architecture-tests.txt`
- Passing architecture transcript: `bundle://proof/SB05/transcripts/focused-unit-architecture-tests-rerun.txt`
- Passing matcher transcript: `bundle://proof/SB05/transcripts/focused-matcher-integration-tests.txt`
- Hashes and source scans: `bundle://proof/SB05/transcripts/changed-file-hashes.txt`, `bundle://proof/SB05/transcripts/snapshot-decoupling-source-scans.txt`
