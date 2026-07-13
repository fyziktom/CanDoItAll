# SB06 Proof Manifest

## Status

- Completed with integration fixture caveat.

## Production Changes

- Portable source proof: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafFinalizerDriver.cs`.
- Semantic invariant contract: `bundle://proof/SB06/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB06/transcripts/validation.txt`.
- Anti-stub audit transcript: `bundle://proof/SB06/transcripts/validation.txt`.
- Failing-first transcript: N/A - process/no production behavior was added; finalizer negative behavior remains covered by existing policy tests.
- Changed file hash: `f45955b9128963cb3ccd28d8469cc6fb6d65b8b921ef2a0bfd0a96659375f548`.
- Added `MafFinalizerDriver`.
- Moved finalizer repair decision logic, finalizer tool resolution, repair chat option construction, repair prompts, JSON repair normalization, effective finalizer invocation selection, effective tool trace selection, and streamed finalizer capture into the driver.
- Kept runtime-specific `ToolInvocationTraceRecorder` adaptation in `MafAgentRuntime`, because that recorder still uses runtime instrumentation state.

## Tests

- `AgentFinalizerPolicyTests` now directly covers driver methods for repair options, JSON normalization, streamed finalizer recording, effective finalizer selection, bounded prompts, and reasoning transport diagnostics.
- Focused unit command passed: 63 passed, 0 failed.
- Focused integration command ran and reached 20 tests: 17 passed, 3 failed with provider-profile fixture/data mismatch before refactored runtime behavior was reached.

## Anti-Stub Audit

- `MafAgentRuntime` delegates required-finalizer repair, JSON repair, streamed capture, and effective invocation selection to `MafFinalizerDriver`.
- No fallback was introduced to silently accept invalid finalizer output.
