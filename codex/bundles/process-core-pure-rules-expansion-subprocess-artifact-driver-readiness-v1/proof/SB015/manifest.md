# SB015 Proof Manifest
## Summary
- Subbundle: SB015 - Gate E artifact matcher and satisfaction parity.
- Status: Completed.
- Invariant ID: SB015-INV-001
- Hash reference: repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactExpectationMatcher.cs SHA-256 61e7c8cc1ca3b900121e2cb5d7b4540abcdd6bf4d06313bd90134e83196c7e42
- Semantic invariant contract: bundle://proof/SB015/semantic-invariants.md
- Changed file: repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactExpectationMatcher.cs
- Changed file: repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactRecordedSatisfactionRules.cs
## Evidence
- Source assertion transcript: bundle://proof/shared/transcripts/source-assertions.txt
- Source proof artifact: repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactExpectationMatcher.cs
- Passing transcript: bundle://proof/shared/transcripts/focused-integration.txt
- Failing-first transcript: bundle://proof/SB015/transcripts/failing-first.txt
- Anti-stub audit transcript: bundle://proof/shared/transcripts/anti-stub-scan.txt
- Dependency scan transcript: bundle://proof/shared/transcripts/core-forbidden-scan.txt
- Build transcript: bundle://proof/shared/transcripts/build.txt
- Driver token scan transcript: bundle://proof/shared/transcripts/driver-token-scan.txt
- No UI/media transcript: bundle://proof/shared/transcripts/no-ui-media-drift-scan.txt
## Closure
- Expected behavior: Core matching disambiguates expected artifacts by kind and recorded-satisfaction identifiers.
- Disallowed shallow implementation: Matching only by display name, ignoring kind, or treating any recorded artifact as satisfaction.
- Downstream dependency check: bundle://proof/shared/transcripts/build.txt
