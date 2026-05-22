# SB01 Semantic Invariants

## Status

- `Completed`

## Raw Note Owned

- `N002`: "there are not screenshots evidences"
- `N005`: "this should not happen when I run complicated process like this"

## Shallow-Pass Trap

A test that only verifies an evidence pack mentions `screenshot` or contains a `.playwright-mcp` path would pass the current broken behavior. That is not acceptable.

## Adversarial Negative Proof Required

Given a browser-proof-gated step with a screenshot requirement, browser invocation logs, and `.playwright-mcp` files, but no scoped process image artifact record, the system must reject quality acceptance or record a repair/conformance outcome.

## Semantic Positive Proof Required

Given provider-native browser MCP output files and an empty chat-history message array, the production ingestion/projection path must create scoped process artifact records for browser screenshot and console evidence.

## Anti-Stub Audit Required

Audit production code for `TODO`, `NotImplemented`, fixture-only branches, markdown-only acceptance, and hardcoded run ids or `.playwright-mcp` filenames.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Browser screenshot artifact record | Provider-native MCP evidence projection | Process validation and evidence UI | Created after browser tool execution and before step acceptance | Detached screenshot path cannot satisfy required screenshot |
| Browser console artifact record | Provider-native MCP evidence projection | Console diagnostic classifier | Created after console capture and before step acceptance | Missing console artifact cannot be summarized as clean |

## Raw-Note Literal Closure

`N002` is closed at the generic runtime-code level: raw provider-native files and result-summary references now resolve into managed process-run browser artifacts before they can satisfy required evidence. Live row proof is left for the clean-DB user retest.
