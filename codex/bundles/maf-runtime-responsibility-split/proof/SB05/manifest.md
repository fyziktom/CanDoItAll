# SB05 Proof Manifest

## Status

- Completed.

## Production Changes

- Portable source proof: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafContextManifestBuilder.cs`.
- Semantic invariant contract: `bundle://proof/SB05/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB05/transcripts/validation.txt`.
- Anti-stub audit transcript: `bundle://proof/SB05/transcripts/validation.txt`.
- Failing-first transcript: N/A - process/no production behavior was added; manifest creation was moved without changing the manifest contract.
- Changed file hash: `c6ed48ca0210986197deb99fea597b351ed6877e5bf2dcbf1f5ada7d79e004fb`.
- Removed `MafAgentRuntime.ContextManifest.cs`.
- Added `MafContextManifestBuilder`.
- Moved runtime context manifest creation and tool schema/token estimation out of `MafAgentRuntime`.
- Updated capability reporting to use `MafContextManifestBuilder.EstimateToolSchemaChars`.

## Tests

- Covered by focused unit command through existing runtime/capability compilation and MAF build.

## Anti-Stub Audit

- Runtime creates manifests through the builder in normal execution and approval continuation.
- Capability projection uses the builder for schema estimates.
