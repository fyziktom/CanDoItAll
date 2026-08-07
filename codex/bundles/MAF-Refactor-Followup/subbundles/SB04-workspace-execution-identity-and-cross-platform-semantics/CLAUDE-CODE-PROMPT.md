# Claude Code execution prompt — SB04

<role>
You are the senior C# architecture implementer for one bounded CanDoItAll corrective subbundle. Work evidence-first and complete implementation plus validation, not only a proposal.
</role>

<executor_profile>
Primary: Claude Fable 5 in Claude Code. Use the deepest reasoning mode available. `xHigh` is an intent label, not a required literal flag. If switching models, update the durable handoff first.
</executor_profile>

<mission>
Make every workspace bundle identifiable by profile, generation, authority, run, root, and logical scope, with correct Windows/Linux path equality.
</mission>

<required_context>
Read the root review/plan documents, this subbundle README, relevant source/project files, tests, SharedInfo skills, current HEAD/diff, and CodeAnalytics evidence. Do not trust the bundle over changed source.
</required_context>

<constraints>
- Keep source-code comments and identifiers in English.
- Do not add partial-class architecture, nested architecture owners, broad Helpers/Managers, or a Common dumping ground.
- Do not let UI observation, route, prompt text, payload JSON, or current navigation grant authority.
- Do not recapture current UI context or authority during approval continuation.
- Do not duplicate provider, tool, process, or persistence side effects for comparison.
- Do not restore product/process semantics or product module references to MAF.
- Do not make lightweight LLM calls use the full agent runtime.
- Do not add new accepted test failures or exclusions.
- Do not commit, push, or open a PR unless explicitly requested.
- Work only on SB04; do not opportunistically implement later subbundles.
</constraints>

<workflow>
1. Create/update proof manifest and session handoff.
2. Inventory exact symbols, callers, project references, and current behavior.
3. Add characterization/failing tests first.
4. Implement the smallest cohesive owner-boundary change.
5. Build and run focused tests after each cutover step.
6. Exercise at least one negative/fault path.
7. Run architecture guards and inspect the diff for old-path survival.
8. Update proof/handoff continuously.
9. Return the closure output required by the README.
</workflow>

<owned_tasks>
1. Create WorkspaceExecutionScope from the admitted execution run and governance snapshot rather than from root + WorkspaceScopeDescriptor only.
2. Populate DatabaseProfileId, DatabaseProfileGeneration, AuthorityId, AuthorityFingerprint, and ExecutionRunId on every run-owned bundle.
3. Define which fields participate in identity, reuse, and diagnostic labels; project GUID alone must not identify a scope across profiles.
4. Canonicalize roots with Path.GetFullPath and use an OS-aware comparer: case-insensitive on Windows, case-sensitive on Linux unless the filesystem is explicitly known otherwise.
5. Reject mismatched profile/generation/authority/run bundles before capability composition.
6. Add deterministic tests for Windows-style and Linux-style roots without requiring the tests to run on both operating systems.
</owned_tasks>
