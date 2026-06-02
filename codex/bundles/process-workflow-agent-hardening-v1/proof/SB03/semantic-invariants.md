# SB03 Semantic Invariants

1. Provider usage is represented as durable observations, not inferred from prompt estimates. Each observation has a status, source phase, provider/model identity, token fields, optional cost fields, and execution/process/workflow correlation.

2. Known actual cost is only aggregated from `Observed` or `ObservedFromMetric` usage. `MissingAfterProviderActivity`, `UsageUnavailable`, and `EstimatedFromMetric` remain explicit unknown or estimated evidence and do not become known actual cost by carrying a numeric token or cost field.

3. Execution success is not required for usage persistence. A runtime failure after provider activity can carry `AgentRuntimeUsageException.UsageObservations`, and the execution service persists those observations with the failed run detail.

4. Required finalizer short-circuit paths cannot silently zero out provider usage. They either preserve observed provider usage or emit `MissingAfterProviderActivity` with provider/session diagnostics when the provider had activity but no usage payload.

5. Structured-output repair usage is linked to the parent execution run instead of disappearing inside validation. Repair observations carry the `structured-output-repair` source phase and are appended to the runtime response before run detail persistence.

6. Process actual cost is ledger-first. When execution details have provider usage observations, process cost aggregation uses the ledger and does not also count legacy `AgentRunMetric` rows for the same detail.

7. Workflow LLM usage is summarized separately from process cost. Workflow usage summaries consume runtime usage observations when present and surface known/unknown counts without implicitly charging upstream workflow calls to a process run.

8. Usage-null and missing-usage provider states are first-class states. They stay visible through `ProviderUsageObservationStatus`, `ProviderUsageSummary`, and `WorkflowUsageMetrics.HasUnknownUsage`.

9. File-backed workspace persistence keeps usage observations scoped with execution details. Run-scoped observations are stored under the run usage root, orphan observations are separated, and invariant validation rejects observations that reference missing runs, agents, or sessions.

10. Deduplication is deterministic. Process cost aggregation uses provider response/source-phase identity when available and observation id otherwise, preventing repeated persistence or retry paths from double-counting the same provider response.
