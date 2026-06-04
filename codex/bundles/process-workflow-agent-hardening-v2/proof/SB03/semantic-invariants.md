# SB03 Semantic Invariants

## Positive Invariants

- OpenAI and Azure OpenAI usage observations read provider-native `input_tokens`, `input_tokens_details.cached_tokens`, `output_tokens`, `output_tokens_details.reasoning_tokens`, and `total_tokens` from raw Responses-style usage JSON.
- `usage: null` is preserved as `UsageUnavailable` and does not invent cost-bearing token counts from fallback runtime counters.
- MAF runtime usage observation creation routes through `DefaultProviderUsageNormalizer` for normal runtime, finalizer short-circuit, finalizer recovery, and provider-failure usage paths.
- Process and workflow cost paths continue to consume usage observations before legacy metric counters.
- Reconciliation reports expose internal response id, request id, source phase, internal/external token totals, token delta, cost delta, and known/unknown status.

## Negative Invariants

- Unknown or missing provider usage is not represented as precise zero cost.
- Reasoning tokens are stored for audit/reconciliation but are not priced as a separate fourth billing dimension; output-token pricing remains the chargeable output bucket.
- The reconciliation report does not record provider secrets or raw OpenAI API keys.
- Legacy `AgentRunMetric` rows remain fallback-only when usage observations exist.

## Downstream Impact

SB04 must prove real provider runs create non-empty usage observations or explicitly block provider E2E. SB08 must render known, estimated, missing, usage-null, and unknown usage states without presenting unknown usage as exact zero actual cost.
