# Senior Token Accounting Review Prompt

You are reviewing CanDoItAll provider usage accounting.

Focus on these questions:

1. Does every provider call produce a usage observation or explicit usage-unavailable record?
2. Are usage records linked to process run, process step, execution run, workflow run, and correlation id where available?
3. Are finalizer short-circuit paths preserving usage from streaming updates?
4. Are failed-after-provider-call paths preserving partial usage?
5. Are structured-output repair calls counted separately?
6. Are background response polls counted cumulatively or individually?
7. Are cancelled/background `usage: null` states represented honestly?
8. Does process actual cost aggregate usage ledger rows instead of only `AgentRunMetric` summaries?
9. Does the UI/API distinguish known cost, estimated cost, and unknown usage?
10. Can a user ask "how many tokens did this process use?" and get known input/cached/output/reasoning/total token values plus a caveat for unknown observations?

Required proof:

- Unit tests for pricing calculator and usage aggregator.
- Integration tests for execution run usage capture.
- Mock runtime tests for finalizer/failure/repair/background edge cases.
- Process detail API test showing usage summary.
- Migration/backfill test for legacy metrics.
- Reconciliation artifact comparing old metric-derived Tetris cost and new ledger-derived known cost.
