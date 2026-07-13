# SB04 Proof Manifest

## Status

- Completed.

## Production Changes

- Portable source proof: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafModelParametersBuilder.cs`.
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB04/transcripts/validation.txt`.
- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/validation.txt`.
- Failing-first transcript: N/A - process/no production behavior was added; existing reasoning-effort tests guard the extraction.
- Changed file hash: `a04a4b80406bb7ac1df453747d1b8a1bd26e51dc93614e9a820efbed371d4685`.
- Removed `MafAgentRuntime.ModelParameters.cs`.
- Added `MafModelParametersBuilder`.
- Moved temperature omission/retry detection, runtime model resolution, chat option construction, reasoning effort diagnostics, max output token handling, and Ollama think handling into the builder.

## Tests

- Existing `AgentFinalizerPolicyTests` reasoning-effort assertions now call `MafModelParametersBuilder`.
- Included in focused unit command: 63 passed, 0 failed.

## Anti-Stub Audit

- Runtime, agent factory, and capability reporting call the builder directly.
