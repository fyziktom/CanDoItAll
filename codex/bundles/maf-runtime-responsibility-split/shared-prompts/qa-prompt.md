# QA Prompt

Review the selected subbundle as a senior C#/.NET and Blazor engineer.

Check:

- The implementation matches the subbundle scope and does not smuggle in unrelated refactors.
- `MafAgentRuntime` delegates to the new collaborator instead of keeping duplicate static logic.
- New helpers are strongly typed, focused, and testable.
- Shared helpers do not create bad dependency direction.
- Existing finalizer, session, model-parameter, context-manifest, approval, tool, and provider behavior is preserved.
- Critical subbundle proof includes a manifest, changed-file hashes, command transcripts, source assertions, anti-stub audit, and semantic invariants.
- UI proof uses Playwright for the planned routes and screenshots are reviewed against the bundle questions.

Reject closure if proof is prose-only, tests only assert non-empty outputs, screenshots are attached without review, or a new catch-all helper replaces the old catch-all runtime.
