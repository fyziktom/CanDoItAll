# SB07 Runtime, Scheduler, Dispatcher Claims, And Event Ports Proof Manifest

## Status

Completed on 2026-06-15.

## Implementation Summary

SB07 implements the process runtime state machine foundation without reviving the archived dispatcher. Runtime state mutation is centralized in `ProcessRuntimeEngine`, ready-work calculation is isolated in `ProcessRuntimeScheduler`, strategy invocation is limited to `ProcessStrategyDispatcher`, and persistence/event/outbox/artifact writes are represented by explicit runtime ports.

## Source Assertions

| Assertion | Source |
| --- | --- |
| Runtime owns transition entry points for activation, scheduling, claims, result submission, and cancellation. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs:13` |
| Applied runtime mutations flow through a unit-of-work commit boundary. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Commit.cs:9` |
| Runtime state, step state, claim state, result receipt, mutation, and commit result are typed records. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs:54` |
| Runtime ports are explicit and persistence-implementation-free. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimePorts.cs:6` |
| Scheduler calculates ready steps and dispatchable work from immutable runtime state and immutable plan data. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeScheduler.cs:8` |
| Dispatcher only invokes the strategy factory selected by the immutable plan binding. | `repo://src/CanDoItAll.Processes.Runtime/ProcessStrategyDispatcher.cs:9` |
| Result application emits claim completion, step terminal, run terminal, outbox, and artifact ledger records. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs:22` |
| Runtime event names are centralized as strongly typed `ProcessEventType` constants. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEventTypes.cs:5` |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Proof |
| --- | --- | --- | --- | --- |
| `ProcessRuntimeStateSnapshot` | Builder output plus runtime transitions | Scheduler, runtime engine, persistence port implementers | Created from compiled plan, mutated only through runtime transitions, persisted through unit of work | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs:54`, `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:35` |
| `DispatchClaimState` / `DispatchClaimToken` | Runtime claim methods | Dispatcher lease owners and persistence adapters | Claimed, renewed, expired, reclaimed, completed, or cancelled | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs:33`, `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:94` |
| `StrategyResultReceipt` / idempotency key | Runtime result submission | Runtime duplicate detection and persistence adapters | Recorded once per step, strategy, and result key; duplicate submissions return duplicate without committing | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs:88`, `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:172` |
| `ProcessRuntimeEventEnvelope` | Runtime transitions | Runtime event store, projections, outbox subscribers | Emitted for accepted transitions and validated before commit | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Commit.cs:75`, `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:131` |
| `ProcessOutboxMessage` | Runtime mutation builder | Outbox writer and downstream subscribers | One outbox message per runtime event for projection/notification work | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimePorts.cs:48`, `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:131` |
| `ProcessArtifactLedgerEvent` | Runtime result application | Artifact ledger store and future projections | Emitted only for produced artifacts and tied to the causal runtime event id | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs:62`, `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:131` |
| `DispatchWorkItem` | Runtime scheduler | Dispatcher/adapters | Emitted only for ready executable steps with plan strategy binding and no active unexpired claim | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeScheduler.cs:34`, `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:68` |
| `StrategyResultEnvelope` | Strategy implementations | Runtime result submission | Accepted only when the claim is current and not expired; duplicate result keys are idempotent | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs:89`, `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:203` |

## Tests And Command Proof

| Proof | Result |
| --- | --- |
| `bundle://proof/SB07/build-unit-sb07.txt` | Unit test project build passed with 0 warnings and 0 errors. |
| `bundle://proof/SB07/test-unit-sb07.txt` | Focused SB03-SB07 tests passed: 51/51. |
| `bundle://proof/SB07/build-solution-sb07.txt` | Full solution build passed with 0 warnings and 0 errors. |
| `bundle://proof/SB07/runtime-forbidden-dependency-scan.txt` | Runtime has no EF, SQL, UI, Git, module, agent, workflow, or automation dependencies. |
| `bundle://proof/SB07/dispatcher-domain-decision-scan.txt` | Dispatcher contains no branch, recovery, manager, artifact validation, EF, or module decision logic. |
| `bundle://proof/SB07/old-symbol-scan.txt` | Runtime and SB07 tests do not reference old dispatcher/step-run symbols. |
| `bundle://proof/SB07/performance-scan-summary.json` | No sync waits, per-call HTTP/JSON/regex allocation, string casing allocation, `ContainsKey`, or production LINQ materialization matches; `.Result` matches are the typed command property, not `Task.Result`. |
| `bundle://proof/SB07/anti-stub-audit.txt` | No TODO, placeholder, fake, or `NotImplementedException` markers. |
| `bundle://proof/SB07/codeanalytics-snapshot-summary.txt` | CodeAnalytics snapshot `snap-20260615194513-574a5aa6` reports 0 diagnostics, 0 cycles, and no runtime persistence entities. |
| `bundle://proof/SB07/bundle-validator-prepared-sb07.txt` | Prepared-stage bundle validator passed after SB07 status/proof synchronization. |
| `bundle://proof/SB07/changed-file-hashes.txt` | Portable hash proof for changed SB07 files. |

## Test Coverage Anchors

| Behavior | Test |
| --- | --- |
| Activation emits runtime event and outbox message. | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:35` |
| Terminal run rejects later activation without commit. | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:53` |
| Scheduler marks ready steps and calculates ready work. | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:68` |
| Claim lifecycle renews, expires, and reclaims work. | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:94` |
| Strategy result completes step/run and emits event/outbox/artifact ledger records. | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:131` |
| Duplicate result returns duplicate without a second commit. | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:172` |
| Expired or lost claim rejects strategy result. | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:203` |
| Cancellation without open claims terminally cancels run and steps. | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:237` |
| Cancellation with open claim only requests cancellation until claim drains. | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:255` |
| Failed strategy result terminally fails run and emits failure event. | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:287` |
| Dispatcher invokes factory with plan binding only. | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:326` |

## Failing-First / Red-Team Evidence

Before closure, CodeAnalytics snapshot `snap-20260615194029-83b0e1c5` flagged `ProcessRuntimeEngine.Helpers.cs` as an oversized source file. The runtime helper code was split into focused partials, then snapshot `snap-20260615194513-574a5aa6` passed with no warning-severity findings. Lost-lease, duplicate-result, terminal-state, and cancellation tests are negative-path coverage for the runtime transition model.

## Browser Validation

Not required. SB07 changes non-UI runtime contracts and tests only.

## Downstream Handoff

SB08 should implement the persistence ports introduced here without moving EF or SQL dependencies into `CanDoItAll.Processes.Runtime`. It should persist runtime state, event store entries, outbox rows, artifact ledger rows, and idempotency data with the same commit semantics captured by `ProcessRuntimeCommitRequest`.
