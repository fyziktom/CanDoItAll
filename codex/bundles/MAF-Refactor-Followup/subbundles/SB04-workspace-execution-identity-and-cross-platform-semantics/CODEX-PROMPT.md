# Codex execution prompt — SB04

You are the senior C# architecture implementer for `fyziktom/CanDoItAll`. Execute only `SB04` at xHigh/deep reasoning. Read the root review, findings, execution order, this README, SharedInfo skills, current source/project files, and CodeAnalytics evidence. Add failing-first tests, implement the smallest cohesive fix at the owning boundary, run focused build/tests and architecture guards, update proof/SESSION-HANDOFF.md, and return the required closure report. Never widen UI authority, recapture current UI context for continuation, mix workspace scopes, silently replay state, reintroduce process semantics in MAF, use full agents for lightweight LLM, add partial architecture, or expand failure allow-lists. Do not commit/push without explicit instruction.

## Mission

Make every workspace bundle identifiable by profile, generation, authority, run, root, and logical scope, with correct Windows/Linux path equality.

## Owned tasks

1. Create WorkspaceExecutionScope from the admitted execution run and governance snapshot rather than from root + WorkspaceScopeDescriptor only.
2. Populate DatabaseProfileId, DatabaseProfileGeneration, AuthorityId, AuthorityFingerprint, and ExecutionRunId on every run-owned bundle.
3. Define which fields participate in identity, reuse, and diagnostic labels; project GUID alone must not identify a scope across profiles.
4. Canonicalize roots with Path.GetFullPath and use an OS-aware comparer: case-insensitive on Windows, case-sensitive on Linux unless the filesystem is explicitly known otherwise.
5. Reject mismatched profile/generation/authority/run bundles before capability composition.
6. Add deterministic tests for Windows-style and Linux-style roots without requiring the tests to run on both operating systems.
