# Architecture Direction

## Target Boundary For This Bundle

```text
ProcessRunAutomationDispatchService.Execution.cs
  -> ProcessExecutionAttemptContext
  -> ProcessExecutionAttemptLauncher / ResultNormalizer
  -> ProcessExecutionPostAttemptFacts
  -> ProcessExecutionRetryDecisionRules
  -> ProcessNoProgressRetrySignalRules
  -> ProcessProviderRecoveryCoordinator
  -> ProcessExecutionLoopFacade (thin, module-local)
```

```text
ProcessRunAutomationDispatchService.Concurrency.cs
  -> ProcessExecutionResponseTextResolver
  -> ProcessConcurrentExecutionAdoptionCoordinator
  -> ProcessRecoverableProviderFailureRules
  -> ProcessNoProgressRetrySignalBuilder
  -> ProcessNoProgressRetryJournalCoordinator
```

The dispatcher remains the owner of route order, claim/heartbeat safety, final transition, and orchestration. Helpers may compute facts, build requests, or coordinate explicit side effects. They must not become public Core contracts.

## Future Driver Readiness

This bundle should document evidence families that future drivers may produce or verify, but only as documentation:

- `ExecutionAttemptEvidence`
- `RecoveredExecutionEvidence`
- `ConcurrentExecutionAdoptionEvidence`
- `RetryDecisionEvidence`
- `NoProgressRetryEvidence`
- `ProviderFallbackEvidence`
- `ProviderHealthProbeEvidence`
- `RecoveryDirectiveEvidence`

No production driver API is allowed.
