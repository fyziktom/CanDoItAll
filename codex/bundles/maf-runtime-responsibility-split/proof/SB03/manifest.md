# SB03 Proof Manifest

## Status

- Completed.

## Production Changes

- Portable source proof: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`.
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB03/transcripts/validation.txt`.
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/validation.txt`.
- Failing-first transcript: N/A - process/no production behavior was added; existing attachment/session characterization tests guard the extraction.
- Changed file hash: `006a0f68e4a140c1e8ac1c487217093f573fa23e4e454da83a4961b47ddee5e9`.
- Added `MafRuntimeSessionBuilder`.
- Moved session restoration/creation, prompt message construction, user input attachment handling, run option construction, response-format application, role mapping, and background-response support checks out of `MafAgentRuntime`.
- Reduced `MafAgentRuntime.Session.cs` to provider streaming/snapshot operations that still need runtime instance state.

## Tests

- `MafAgentRuntimeAttachmentTests` now calls `MafRuntimeSessionBuilder` directly for prompt input characterization.
- Included in focused unit command: 63 passed, 0 failed.

## Anti-Stub Audit

- Runtime uses `MafRuntimeSessionBuilder` in normal execution, approval continuation, provider health mapping, and agent factory construction.
