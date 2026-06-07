# Driver Alpha Diagnostic Taxonomy

## .NET Transcript Diagnostics
- BuildWarning
- BuildError
- TestFailure
- UnsupportedTargetFramework
- MissingArtifact
- RuntimeProofGap
- NullableWarning
- PlatformCompatibilityWarning
- AnalyzerWarning

## Rust Transcript Diagnostics
- CargoTestFailure
- CompileError
- ClippyWarning
- MissingCargoArtifact
- UnsupportedToolchain
- PanicDetected

## Shared Diagnostics
- TranscriptMissing
- TranscriptUntrusted
- EvidenceHashMismatch
- InsufficientProof
- NoIssueDetected
- UnsupportedTranscriptFormat

## Diagnostic Rules
- Diagnostics must be deterministic.
- Diagnostics must cite evidence reference ids or transcript references.
- Diagnostics must not include secrets or unredacted emails/tokens.
- Diagnostics must not suggest executing commands directly in verification-only mode.
