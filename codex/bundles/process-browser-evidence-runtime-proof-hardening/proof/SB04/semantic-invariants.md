# SB04 Semantic Invariants

## Status

- `Partially completed`

## Raw Notes Owned

- `N001`: "final app was not properly tested"
- `N002`: "there are not screenshots evidences"
- `N003`: "Playwright MCP ... would discover..."
- `N004`: "js trouble in console output"
- `N005`: "this should not happen when I run complicated process like this"

## Shallow-Pass Trap

A clean process run that completes and cites `.playwright-mcp` files in markdown, but has no scoped browser artifact records and no screenshot review, would repeat the current failure. Final closure must reject that.

## Adversarial Negative Proof Required

Create or reuse a test fixture where the process attempts to accept detached browser evidence only. It must fail, route to repair, or record conformance observations.

## Semantic Positive Proof Required

A fresh clean-development-DB process run must produce scoped process artifact records for screenshot, console, and DOM/snapshot/evaluate evidence and must record a representative interaction assertion.

## Anti-Stub Audit Required

Audit final proof for manual DB inserts, manually copied artifacts that bypass production projection, hardcoded run ids, and fake browser analytics rows.

## Production Behavior Artifact Matrix

| Artifact or signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Fresh process browser evidence records | Live process execution | Final closure and user demo validation | Created during real QA browser-proof step | Detached raw MCP files alone cannot pass |
| Browser validation analytics | Execution report from live proof | Bundle closure review | Filled after artifact query and screenshot review | Missing screenshot review fails final closure |

## Raw-Note Literal Closure

Every raw note is closed for code-level hardening and remains open for live-process closure until a clean development DB run proves screenshot, console, snapshot or evaluate state, and interaction proof are process-visible artifacts for the fresh run. The database is clean and migrated for that retest; the live multi-agent run was not executed so the clean state remains available to the user.
