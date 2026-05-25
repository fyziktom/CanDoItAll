# SB09 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs:16` defines the durable `no-progress-retry-observed` journal event alongside the existing compressed diagnostic event.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs:26` defines `NoProgressRetrySignal` with execution run id, tool signature, artifact validation fingerprint, mutation delta, proof delta, and the combined fingerprint.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs:64` filters active execution run adoption by the current step attempt window instead of accepting stale active runs from earlier attempts.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs:438` consumes prior no-progress journal events and rejects repeated fingerprints from a different execution run after restart.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:384` creates the no-progress signal before retry decision finalization and `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:393` stops retry when the same durable signal already exists.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs:175` persists observed no-progress retry signals to the process journal before retry recovery is queued.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs:216` covers stale active-run rejection against the attempt window, and `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs:235` covers durable no-progress detection after JSON reload.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `no-progress-retry-observed` journal entry | `PersistNoProgressRetryObservedAsync` before retry recovery journal creation | `HasPriorNoProgressRetrySignalAsync` during next retry decision | Durable process journal state keyed by process run, step run, event type, and correlation fingerprint | SB09 restart test reloads journal JSON and proves a repeated fingerprint from a different execution run is detected |
| `NoProgressRetrySignal` fingerprint parts | `TryCreateNoProgressRetrySignal` from execution details, required tools, validation failures, mutation receipts, proof receipts, and retry reasons | Retry compression and journal producer/consumer | Recomputed from current evidence for each candidate; changes when new tool/artifact/mutation/proof evidence appears | Test proves the same fingerprint is blocked only when it came from another execution run, not duplicate processing of the same run |
| Current-attempt active execution filter | `ResolveBlockingAutomationExecutionRunId(ProcessStepRun, ...)` and competing-run lookup | Concurrent execution adoption and blocking checks | Runtime-only reconciliation based on the step run's current `StartedAtUtc` claim/window | SB09 active-run test proves an active execution from a previous attempt is ignored even if updated recently |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB09/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB09/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB09/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB09/transcripts/changed-file-hashes.txt`

## Validation

Passed:

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~SB09_INV_001" --no-restore -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~AutomationExecutionRunId|FullyQualifiedName~NoProgressRetry|FullyQualifiedName~SB09_INV_001" --no-restore --no-build -v minimal`

Known unrelated warning noise: MSB3277 reports existing EntityFrameworkCore.Relational 10.0.0/10.0.4 conflicts during build.

## Blockers

None.
