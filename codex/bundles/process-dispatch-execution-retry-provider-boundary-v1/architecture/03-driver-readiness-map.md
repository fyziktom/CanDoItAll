# Documentation-Only Driver Readiness Map

This bundle must not introduce production driver APIs. The following names are only vocabulary for future design.

| Future evidence family | Current runtime meaning | Current source | Do now |
| --- | --- | --- | --- |
| `ExecutionAttemptEvidence` | An automation execution attempt was launched, recovered, adopted, normalized, or observed active. | `ProcessExecutionInvocationRequestBuilder`, `ProcessExecutionAttemptResultNormalizer`, `ProcessRecoveredExecutionAdoptionCoordinator`, `ProcessConcurrentExecutionAdoptionCoordinator`, `ProcessObservedExecutionOutcomeBuilder` | Document only |
| `PostAttemptFactEvidence` | Missing tools, critical failures, completion status/reason, branch outcome, and carried proof were captured after an attempt. | `ProcessExecutionPostAttemptFacts`, `ProcessExecutionPostAttemptFactsBuilder` | Document only |
| `RetryDecisionEvidence` | Dispatcher chose retry/stop based on missing tools, critical failures, proof gaps, finalizer/provider/interruption reasons, and no-progress signals. | `ProcessIncompleteSuccessfulRunRetryRules`, `ProcessRecoverableFailedRunRetryRules`, `ProcessExecutionRetryReasonAggregator` | Document only |
| `NoProgressRetryEvidence` | Current attempt repeated the same failure signature and may be compressed. | `ProcessNoProgressRetrySignal`, `ProcessNoProgressRetrySignalBuilder`, `ProcessNoProgressEvidenceDeltaRules`, `ProcessNoProgressRetryJournalQueryCoordinator`, `ProcessNoProgressRetryJournalWriter` | Document only |
| `ProviderFallbackEvidence` | Provider health/fallback was used to repair assigned technical agents. | `ProcessRecoverableProviderFailureRules`, `ProcessProviderFallbackSelectionRules`, `ProcessProviderRepairCoordinator`, `ProcessAssignedAgentProviderRepairCoordinator` | Document only |
| `ProviderHealthProbeEvidence` | Fallback provider health probe succeeded or failed under the configured timeout. | `ProcessProviderHealthProbeCoordinator` | Document only |
| `HistoricalCarryForwardEvidence` | Prior terminal execution details were queried for carried implementation proof. | `ProcessHistoricalCarriedProofQueryCoordinator` | Document only |
| `ExecutionLoopOrchestrationEvidence` | The execution partial coordinates launch, post-attempt facts, provider recovery, no-progress retry, and recovery journaling without owning the helper internals. | `ProcessRunAutomationDispatchService.Execution`, `ProcessExecutionAttemptLoopFacade` | Document only |
| `RecoveryDirectiveEvidence` | Typed recovery directive and optional rework packet were produced. | `ProcessProviderRecoveryDirectiveBuilder`, `RecoveryPackets.cs` | Document only |
