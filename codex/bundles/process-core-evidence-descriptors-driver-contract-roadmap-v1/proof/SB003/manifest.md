# SB003 Proof Manifest

## Outcome
- Entry gate: Passed.
- Closure gate: Passed.
- Gate A result: clean baseline proof passed.
- Semantic invariant contract: bundle://proof/SB003/semantic-invariants.md.

## Changed File Hashes
- FC8B3248D34088C19B9027B6649703AF9A2E867BB75473D7943AA9FDA15C4F18 repo://src/CanDoItAll.AgentFramework.Persistence/Validation/SandboxWorkspaceDocumentInvariantValidator.cs.
- E4EC4A6CF113976F7FFEFB1F44D53CA691D147AE723369DD87C57FDEA3FF7D52 repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowEventNormalizer.cs.
- 633A40FF5A1E22EB8C9E7A053572F9BD3D4B693DDD5761356D45C76EEA89D989 repo://src/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs.

## Command Transcripts
- Failing-first transcript: bundle://proof/SB003/transcripts/failing-first-warning-gate.txt.
- Passing build transcript: bundle://proof/SB002/transcripts/post-warning-cleanup-build.txt.
- Passing focused test transcript: bundle://proof/SB002/transcripts/focused-unit-tests.txt.
- Source assertion transcript: bundle://proof/SB003/transcripts/source-assertions.txt.
- Changed-file hash transcript: bundle://proof/SB003/transcripts/changed-file-hashes.txt.
- Core forbidden-token scan transcript: bundle://proof/SB003/transcripts/core-forbidden-token-scan.txt.
- Production process-driver token scan transcript: bundle://proof/SB003/transcripts/production-process-driver-token-scan.txt.
- UI/media drift scan transcript: bundle://proof/SB003/transcripts/ui-media-drift-scan.txt.
- Anti-stub audit transcript: bundle://proof/SB003/transcripts/anti-stub-changed-production-scan.txt.
- Semantic closure transcript: bundle://proof/SB003/transcripts/semantic-closure.txt.

## Source Assertions
- Nullable provider usage observation validation now uses an explicit captured execution run id before run-id membership checks.
- MAF output event normalization avoids compile-time use of the obsolete `SourceId` member while preserving a legacy reflective fallback.
- The provider profile registry no longer declares an unused runtime profile accessor dependency.

## Failing-First And Passing Proof
- Failing-first transcript: bundle://proof/SB003/transcripts/failing-first-warning-gate.txt proves the warning gate failed before cleanup because the baseline build had `CS8629`, `CS0618`, and `CS9113`.
- Passing transcript: bundle://proof/SB002/transcripts/post-warning-cleanup-build.txt proves `dotnet build CanDoItAll.slnx -v minimal` completed with 0 warnings and 0 errors.
- Semantic positive proof: bundle://proof/SB002/transcripts/focused-unit-tests.txt proves 102 targeted unit and architecture tests passed after cleanup.

## Anti-Stub Audit
- Anti-stub audit transcript: bundle://proof/SB003/transcripts/anti-stub-changed-production-scan.txt.

## Downstream Decision
- SB004 may proceed because warning cleanup is explicit, Core remains dependency-clean, production process-driver APIs remain absent, and no UI/media files changed.
