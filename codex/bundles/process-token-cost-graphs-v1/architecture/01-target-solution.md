# Target Solution

## Accounting Boundary

- Extend the runtime response path to carry cached input tokens as a strongly typed integer value.
- Map provider usage from Microsoft Agent Framework into input, cached input, and output token counters with predictable clamping for invalid or oversized counts.
- Treat provider usage as the source of truth for successful runs. Do not add local prompt estimates to provider-reported input tokens.
- Continue using local estimates only where no provider usage exists, such as pre-provider failure metrics.
- Persist `AgentRunMetric.CachedInputTokens` and let `ProviderPricingCalculator` calculate uncached input, cached input, and output costs from the metric.

## Analytics Boundary

- Build graph points from persisted execution and process-run data so completed runs still appear after refresh.
- Add typed process/run graph query scope and range handling rather than passing ad hoc strings through components.
- Keep query ranges bounded by the selected period: one day, one week, one month, three months, one year, or all.
- Preserve existing live dashboard semantics for context usage, duration/tool calls, money, and tool usage.

## UI Boundary

- Extend `ProcessWorkspace` and nearby process components; do not introduce a separate graph page.
- Add a selected-process graph tab that renders a lightweight empty state and controls on activation, then loads all-runs graph data only after the explicit button click.
- Add a selected-run graph tab that loads only when the run graph tab is selected and scopes data to that run.
- Reuse existing chart wrapper components and project styling.

## Explicit Non-Goals

- Do not call external provider billing APIs.
- Do not add silent fallback prices for unknown provider/model combinations.
- Do not introduce a new charting library.
- Do not refactor unrelated process workspace tabs.
