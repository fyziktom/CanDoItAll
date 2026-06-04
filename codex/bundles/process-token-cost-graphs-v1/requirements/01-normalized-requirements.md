# Normalized Requirements

| Requirement | Source | Acceptance |
| --- | --- | --- |
| R001 | N001, N002 | Persisted execution metrics use provider-reported input and output token counts without adding local prompt estimates to successful provider runs. |
| R002 | N003, N004 | OpenAI/Azure OpenAI cached input tokens flow from provider usage into `AgentRunMetric.CachedInputTokens`; providers without cached usage persist zero cached tokens. |
| R003 | N003, N004 | Provider pricing calculates uncached input, cached input, and output costs separately and process actual cost sync uses those totals. |
| R004 | N005 | Live Processes one-day history after refresh includes non-empty cost graph series when completed runs in the window have priced metrics. |
| R005 | N006, N008, N009 | Selected process details expose an all-runs graph tab with explicit `Show graphs of all runs of process` loading, default one-month range, and range options `1 day`, `1 week`, `1 month`, `3 months`, `1 year`, `all`. |
| R006 | N007, N008 | Selected process-run details expose a run-scoped graph tab that loads only when selected and only includes that process run. |
| R007 | N001, N005, N006, N007 | Graphs reuse the live-process chart semantics for context usage, time/tool calls, and process cost where applicable. |
| R008 | N008, N009 | Historical graph queries are bounded by selected range and explicit process or run scope. |
