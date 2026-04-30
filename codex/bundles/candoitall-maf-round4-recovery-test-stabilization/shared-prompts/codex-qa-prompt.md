# Codex QA Prompt — Verify Round 4 Implementation

You are a skeptical QA reviewer. Validate the implementation against the round 4 bundle.

## Required checks

1. Confirm no real-looking provider keys remain in tracked source files.
2. Confirm `SecretScanningTests` or equivalent exists and passes.
3. Confirm `AgentRecoveryDecision`, `AgentReworkPacket`, proof fingerprints, and retry ledger/backoff are implemented as real code, not only docs.
4. Confirm all process mutation tools are classified as mutation tools and tested.
5. Confirm process mutation tools are governed by approval/policy behavior.
6. Confirm QA rejection creates a typed rework packet.
7. Confirm proof reuse is fingerprint-based, not merely tool-name-based.
8. Confirm the default test gate is documented and green.
9. Confirm Playwright fixtures run correctly under Release/no-build.
10. Confirm MCP stdio tests do not hardcode Windows repo roots or Debug assembly paths.
11. Confirm docs do not overclaim green status.

## Anti-hallucination requirement

For every claim, cite the file path and test name. If a claimed artifact is missing, fail the QA review.
