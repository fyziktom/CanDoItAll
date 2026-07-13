# SB02 Proof Manifest

## Status

- Completed.

## Production Changes

- Portable source proof: `repo://src/Foundation/CanDoItAll.SharedKernel/Common/StableContentHash.cs`.
- Portable source proof: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafToolInvocationArgumentFormatter.cs`.
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB02/transcripts/validation.txt`.
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/validation.txt`.
- Failing-first transcript: N/A - process/no production behavior was added; characterization tests were added for helper extraction semantics.
- Changed file hash: `a78ab26f6579dddd0ca10ea28a0f4a358df671484096cc4bae175c81a2eebc7e`.
- Changed file hash: `7b728c48d2273b83c72a5c1b5741ffa4dfd53df49dbcda13436e81a27803d6fd`.
- Added `CanDoItAll.SharedKernel.StableContentHash`.
- Added `MafToolInvocationArgumentFormatter`.
- Added a SharedKernel project reference to `CanDoItAll.AgentFramework.Maf`.
- Updated MAF runtime/capability call sites to use the formatter and JSON argument conversion helpers.

## Tests

- `StableContentHashTests` validates the known SHA-256 short hash for `hello` and invalid byte counts.
- `MafToolInvocationArgumentFormatterTests` validates deterministic truncation/hash behavior and invalid JSON handling.
- Included in the focused unit command that passed: 63 passed, 0 failed.

## Anti-Stub Audit

- Hash helper is used by runtime argument formatting.
- Formatter is used by tool invocation descriptions, approval summaries, call keys, and runtime-tool-provider JSON conversion.
