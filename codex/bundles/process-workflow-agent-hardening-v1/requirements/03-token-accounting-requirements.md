# Token Accounting Requirements

## Functional Requirements

1. Persist usage at provider response granularity when available.
2. Preserve usage independently of execution success/failure.
3. Track usage-null states explicitly.
4. Support input, cached input, output, reasoning, and total tokens.
5. Support provider-reported cost and internally calculated cost.
6. Link each usage record to process/workflow/execution/correlation context.
7. Surface both known and unknown usage in API/UI.
8. Keep old `AgentRunMetric` compatible as a summary view or migration bridge.
9. Include private/local provider pricing categories without pretending they are OpenAI costs.
10. Make provider fallback/substitution explicit in usage records.

## Non-Functional Requirements

- Idempotent persistence: retrying a provider response persistence must not double count.
- Deterministic aggregation: repeated process detail calls must return the same cost until new usage observations arrive.
- Auditability: cost can be traced from process run -> execution run -> provider response.
- Reconciliation: old metric sum, new ledger sum, and provider response usage can be compared.
- Extensibility: schema supports OpenAI Responses, chat completions, Azure OpenAI, Ollama/private, image generation, and future providers.

## Migration Requirements

- Existing `AgentRunMetric` rows must be backfilled into `ProviderUsageObservation` with source phase `legacy-agent-run-metric` and usage status `estimated-from-metric` or `observed-from-metric` depending available evidence.
- Existing process actual cost must not be overwritten blindly until the backfill summary has been recorded.
- UI must show a migration/reconciliation note for historical runs where usage is incomplete.
