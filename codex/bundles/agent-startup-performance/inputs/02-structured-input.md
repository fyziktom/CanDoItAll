# Structured Input

## Requested Outcome

Reduce the slow preparation before agents begin useful work on both existing instances, without breaking their currently working execution.

## Scope Decision

- N001: recommendations 1-3 are authorized for planning: operation-local filesystem facts, validated provider revision projections, validated immediate-commit reuse.
- N002: recommendation 4 is explicitly excluded. The accumulation/exception rationale does not override “only those three first parts”; no progress batching or removed per-stage durability.
- N003: future proof must use Playwright MCP through the UI on 5032 and 5214 for real conversations and tool execution.
- N004: preserve successful work and explicit failure propagation/history; “faster” alone is insufficient.
- N005: preparation only now. Execution, tests that launch agents, deployment and live changes wait for a later execute request.

## Ownership And Environment

Only `CanDoItAll` owns planned changes. Native 5032 and Docker client 5214 are acceptance targets. Central publisher 5210 and all sibling repositories remain unchanged. Desktop 1920×1080. Source state and instance versions must be revalidated at execution; historical IDs are discovery hints.

## Completion Bar

Three focused implementation gates plus a combined real-UI/host/performance gate. Every negative/security/recovery invariant must pass. If performance remains inconclusive or an integrity check cannot be preserved, stop/reopen the affected unit; do not declare success or expand into batching.
