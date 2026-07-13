# SB07 Proof Manifest

## Status

- Completed.

## Production Changes

- Portable source proof: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`.
- Portable source proof: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafFinalizerDriver.cs`.
- Semantic invariant contract: `bundle://proof/SB07/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB07/transcripts/validation.txt`.
- Anti-stub audit transcript: `bundle://proof/SB07/transcripts/validation.txt`.
- Failing-first transcript: N/A - process/no production behavior was added; slimming is verified by source delegation, line count, build, and unit tests.
- Changed file hash: `549adb1a646896cf4df5b999db5392255aad0ca1234b1beaccde50d741ec4e7a`.
- `MafAgentRuntime.cs` now delegates helper, builder, context-manifest, and finalizer responsibilities to focused collaborators.
- Deleted responsibility partials:
  - `MafAgentRuntime.ModelParameters.cs`.
  - `MafAgentRuntime.ContextManifest.cs`.
- `MafAgentRuntime.Session.cs` now contains only runtime/session operations that still need runtime state.

## Static Proof

- `MafAgentRuntime.cs`: 2397 lines.
- `MafFinalizerDriver.cs`: 927 lines.
- Formatter/model/context/session collaborators are all below 350 lines.

## Tests

- MAF direct build passed.
- Focused unit command passed: 63 passed, 0 failed.

## Anti-Stub Audit

- Old helper methods were removed from the runtime rather than wrapping dead duplicates.
- Runtime call sites point to the extracted collaborators.
