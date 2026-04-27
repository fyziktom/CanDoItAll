# 03 — Efficient Context Selection and Session Policy


## Problem

The dispatcher usually creates a fresh MAF session after failed attempts. This is safe but can waste useful context. The system needs explicit context strategy per recovery mode.

## Tasks

1. Define `AgentContextStrategy` with options such as:
   - `FreshSessionWithDurableArtifacts`
   - `BoundedContinuation`
   - `ApprovalContinuation`
   - `TranscriptSummaryOnly`
   - `NoTranscript`
2. Implement a recovery decision policy that chooses strategy based on failure type.
3. Never use failed chat transcript as source of truth. Use process state, artifacts, receipts, validated outputs, and rework packets.
4. Limit transcript excerpts by size and redact secrets/tool outputs.
5. For QA rework, require direct artifact inspection rather than trusting old summaries.

## Acceptance criteria

- Fresh vs continuation behavior is explicit in `AgentRecoveryDecision`.
- Provider/tool-loop/finalizer-sequence failures use fresh session.
- Approval continuations can use bounded continuation when needed.
- QA repair uses fresh or bounded session plus typed packet and durable artifacts.
- Tests prove the selected strategy for each failure category.

