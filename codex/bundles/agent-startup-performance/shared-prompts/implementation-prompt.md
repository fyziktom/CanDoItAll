# Future Execution Prompt

Use only after the user explicitly asks to execute this bundle. Prepare-only authorization does not activate this prompt.

Goal: implement recommendations1-3 in `codex/bundles/agent-startup-performance`, improve measured startup on5032and5214, preserve working agent conversations/tools and all documented security/durability/error semantics.

Read README, current subbundle, phase/test/performance/host/UI plans and execution report. Re-anchor current source/host state, collect baseline before changes, then execute dependency-ready units using the bundle-execution skill. Use architecture/CodeAnalytics gates and scoped impacted-test selection based on actual diff. Make the smallest typed local changes; no progress batching, public schema/contract changes, global factory cache or token-only provider shortcut.

Run all required positive/negative/platform cases and real Playwright MCP UI matrix. Do not substitute stubs/API-only actions, lose persisted stages, fabricate failure evidence, or use Stop as cancellation. Keep source/test/proof hashes and transcripts portable and secrets masked. Verify actual test discovery. Update status/traceability honestly. Reopen when assumptions or dependent proof fail; do not expand into recommendation4 to meet timing targets.
