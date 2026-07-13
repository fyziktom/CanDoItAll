# SB01 Proof Manifest

## Status

- Completed.

## Evidence

- Runtime responsibility inventory refreshed during implementation.
- Portable source proof: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB01/transcripts/validation.txt`.
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/validation.txt`.
- Failing-first transcript: N/A - process/no production behavior was added in this inventory boundary; proof is source scan plus downstream passing build/test transcripts.
- Changed file hash: `549adb1a646896cf4df5b999db5392255aad0ca1234b1beaccde50d741ec4e7a`.
- Line counts after extraction:
  - `MafAgentRuntime.cs`: 2397 lines.
  - `MafAgentRuntime.Session.cs`: 247 lines.
  - `MafRuntimeSessionBuilder.cs`: 332 lines.
  - `MafModelParametersBuilder.cs`: 177 lines.
  - `MafContextManifestBuilder.cs`: 103 lines.
  - `MafFinalizerDriver.cs`: 927 lines.
  - `MafToolInvocationArgumentFormatter.cs`: 166 lines.
- Threshold decision: keep `MafAgentRuntime.cs` as orchestration and below the prepared 3436-line baseline; keep new collaborators focused by responsibility. `MafFinalizerDriver` remains under 1000 lines and is explicitly called out for possible later JSON-repair/prompt splitting.
- Symbol scan confirms `ComputeStableHash` responsibility is no longer in runtime and formatting responsibility moved to `MafToolInvocationArgumentFormatter`.

## Commands

- `git diff --check` passed with line-ending warnings only.

## Anti-Stub Audit

- No placeholder extraction was added. Every extracted collaborator is called from production runtime or capability/provider-health code.
