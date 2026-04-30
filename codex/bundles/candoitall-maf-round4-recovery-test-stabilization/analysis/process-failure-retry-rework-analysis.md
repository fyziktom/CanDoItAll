# Process Failure, Retry, and Rework Analysis

## Current behavior

The process automation dispatcher retries the current process step. It does not rerun the entire process, which is the right default. The retry loop:

1. Executes or recovers an AgentFramework execution run.
2. Reads execution detail and response text.
3. Validates missing required tools, critical tool failures, structured outcome, and process completion rules.
4. Carries some successful tool names across attempts.
5. Builds a text recovery directive when the attempt is not accepted.
6. Usually clears the chat session id so the next attempt starts with a fresh MAF session.
7. Tries provider fallback/repair in some assigned-agent-provider failure cases.

## Strengths

- The system avoids rerunning the entire process.
- Fresh sessions reduce contamination from failed tool loops or invalid assistant state.
- Durable files/artifacts remain available, so agents can inspect previous work rather than relying only on old chat history.
- Required proof tools are handled more conservatively than ordinary successful tool names.

## Weaknesses

### Text directive instead of typed decision

`BuildRecoveryDirective(...)` is useful but opaque. It does not produce a durable, queryable, validated object. This makes it hard to distinguish:

- provider failure;
- format-only output failure;
- missing finalizer;
- missing required tool;
- build/test failure;
- QA rejection;
- partial implementation that needs finishing;
- repeated loop that needs escalation.

### No typed rework packet

When QA rejects a result or a build/test/browser proof fails, the next agent should receive a structured packet with findings, impacted artifacts, known-good artifacts, proof requirements, and prohibited actions. Today the next attempt mostly receives a textual directive.

### No proof fingerprint reuse

Successful tool names are too coarse. A green build/test/browser proof is only reusable if the relevant inputs did not change. The system needs proof fingerprints, not only tool-name carry-forward.

### Session strategy is implicit

Fresh session is usually safe, but not always the most efficient. Missing finalizer or approval continuation may be better handled as a constrained continuation. Provider errors or looped tool calls should use a fresh session. This should be explicit and persisted in a recovery decision.

## Target recovery taxonomy

Implement a typed recovery decision with one of these modes:

- `FormatRepair`: No new process-step attempt. Repair/extract structured JSON only, then validate.
- `FreshStepRetry`: Start a new step attempt with a fresh session. Use for provider failure, poisoned context, missing required tool execution, invalid finalizer sequence, or repeated loops.
- `ReworkContinuation`: Create a new attempt focused on completing/repairing existing work. Use a typed rework packet and durable artifacts, not old chat transcript as source of truth.
- `ProviderFallback`: Switch provider/profile and retry with explicit reason and budget.
- `HumanEscalation`: Stop automated retries and ask a human for a decision.

## Target context strategy

- Source of truth: process state, step state, artifacts, tool receipts, validated outputs, recovery ledger.
- MAF session: conversational context only; never the sole source of process truth.
- Failed chat transcript: summarize/redact and include only bounded excerpts when useful.
- Existing artifacts: inspect directly through tools; do not trust summaries.
- Proofs: reuse only through fingerprints.

## QA rework ideal flow

```text
QA agent returns typed findings
  -> process creates AgentReworkPacket
  -> repair agent reads packet and impacted artifacts
  -> repair agent makes minimal changes
  -> invalidated proof tools rerun
  -> repair outcome finalizer submits typed result
  -> QA recheck consumes repair report and proof receipts
```

This turns "try the whole step again" into "finish the exact remaining work".
