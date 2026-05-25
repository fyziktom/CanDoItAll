# SB06 Semantic Invariants

## Invariants

### SB06-I1 Process step execution ownership is durable

Raw note: "A process step may have at most one active canonical automation execution at a time."

Expected behavior: process step automation dispatch stores claim token, claimant id, claimed timestamp, lease expiry, and attempt count on `ProcessStepRun` before long-running automation starts.

Shallow-pass trap: keep the static per-step semaphore around long work and call it a concurrency fix, which fails across processes and serializes too much within one process.

Adversarial negative proof: focused integration tests cover process dispatch and workflow execution; source assertions show durable claim fields and renewal/finalization paths.

Semantic positive proof: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` and `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs`.

Production assertions: `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt`.

Changed source files: see `bundle://proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv`.

Downstream dependency check: SB08 EF drift proof confirms durable claim schema is represented in the PostgreSQL migration snapshot.

### SB06-I2 Long work does not hold the process-local guard as canonical protection

Raw note: "Release in-memory semaphore before long external execution if a durable lease safely owns the work."

Expected behavior: `StepDispatchGuards` protects short claim/finalization windows only. Durable PostgreSQL claim ownership controls long execution.

Shallow-pass trap: add columns but continue to hold the process-local semaphore across the long execution path.

Adversarial negative proof: source assertions show claim/renew/release methods and focused integration tests exercise dispatch after the change.

Semantic positive proof: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`.

Production assertions: `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ProcessStepRun` automation dispatch claim | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Dispatcher renewal/finalization paths | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-ef-has-pending-model-changes.txt` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` |
