# SB03 Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `ProviderUsageObservation` | `MafAgentRuntime`, `AgentFrameworkWorkspaceExecutionService.Usage`, structured-output repair results, and legacy metric bridge | Workspace execution state, file-backed slice store, execution run detail DTOs, pricing summary, process cost sync, workflow usage metrics | Created at provider-response or metric-bridge boundary, enriched with run/process/workflow context, normalized, persisted under `runs/{id}/usage` or `orphans/usage`, pruned with removed agents/runs | `ExecuteRunAsync_preserves_usage_when_runtime_fails_after_provider_call`; `ExecuteRunAsync_records_finalizer_short_circuit_usage_when_metrics_are_zero`; failing-first mutation transcript |
| `ProviderUsageSummary` | `ProviderPricing.SummarizeUsage` | API/DTO callers that need known/unknown token and cost totals | Built from durable observations at read/aggregation time; known totals only include known statuses; unknown count remains visible | `Usage_summary_counts_only_observed_usage_as_known_actual_cost` |
| `WorkflowUsageMetrics` | `MafWorkflowLlmComponentInvoker.CreateWorkflowUsageMetrics` | Workflow progress/result events and workflow run DTOs | Built from runtime usage observations when present, otherwise legacy token fields; carries known/unknown observation counts and `HasUnknownUsage` | `MafWorkflowLlmComponentInvokerUsesProviderUsageObservationsForWorkflowUsage`; `MafWorkflowLlmComponentInvokerMarksUnavailableWorkflowUsageAsUnknown` |
| Pricing profile records | Existing provider pricing catalog and `ProviderPricing.TryResolveObservationCost` | Usage observation enrichment and process/workflow cost aggregation | Resolved from provider/model identity; calculated cost is stamped only when pricing is available and usage status is known | `ProviderPricingTests` targeted slice |
| Process actual cost synchronization | `ProcessRunAutomationDispatchService.ResolveProcessRunActualCost` | Process run cost refresh and process detail reporting | Aggregates known usage ledger observations with deterministic dedupe; falls back to legacy metrics only when detail has no usage observations | `ResolveProcessRunActualCost_prefers_usage_ledger_over_legacy_metrics` |

## Dependency Smoke Proof

- SB07 can consume known/unknown usage through `ProviderUsageSummary`, execution run `UsageObservations`, and `WorkflowUsageMetrics` without depending on legacy prompt-estimate semantics.
- SB08/SB09 can replay process detail and workflow execution scenarios against explicit usage statuses instead of treating zero or missing usage as a valid actual total.
