# Documentation-Only Driver Readiness Map

This bundle must not introduce production driver APIs. The following names are only vocabulary for future design.

| Future evidence family | Current runtime meaning | Current source | Do now |
| --- | --- | --- | --- |
| `ExecutionAttemptEvidence` | An automation execution attempt was launched, recovered, adopted, or observed active. | `Execution.cs` | Document only |
| `RetryDecisionEvidence` | Dispatcher chose retry/stop based on missing tools, critical failures, proof gaps, or no-progress signals. | `Concurrency.cs` | Document only |
| `NoProgressRetryEvidence` | Current attempt repeated the same failure signature and may be compressed. | `Concurrency.cs` | Document only |
| `ProviderFallbackEvidence` | Provider health/fallback was used to repair assigned technical agents. | `Execution.cs` | Document only |
| `ProviderHealthProbeEvidence` | Fallback provider health probe succeeded or failed. | `Execution.cs` | Document only |
| `RecoveryDirectiveEvidence` | Typed recovery directive and optional rework packet were produced. | `Execution.cs` / `RecoveryPackets.cs` | Document only |
