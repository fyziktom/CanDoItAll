# SB08 Semantic Invariants

- Invariant ID: `SB08_INV_001`
- Source raw note: Final release matrix and fake-proof resistance.
- Expected behavior: Build passes, code-first guard passes, representative SB03-SB07 integration matrix passes, Core boundary remains clean, added lines contain no stub markers or secrets, bundle-path coupling is limited to the guard fixture, and large runtime-file growth is split into focused partials.
- Disallowed shallow implementation: closing the bundle with only prose, manual transitions, or focused happy-path tests.
- Passing tests: `bundle://proof/SB08/transcripts/focused-test.txt` shows the build, code-first guard, and representative integration matrix passed after the runtime split.
- Changed source files: process-mock runtime split across `ProcessMockAgentRuntime.cs`, `ProcessMockAgentRuntime.PromptArtifacts.cs`, `ProcessMockAgentRuntime.SessionState.cs`, and `ProcessMockAgentRuntime.BranchOutcomes.cs`.
- Production assertions: process-mock durable artifact/tool-output behavior remains source-backed while runtime file line count is reduced to 630 lines.
- Red-team negative case: added-line scans found no stubs or secrets; Process Core dependency drift scan stayed clean.
- Downstream dependency check: No downstream blocker remains for this bundle.
